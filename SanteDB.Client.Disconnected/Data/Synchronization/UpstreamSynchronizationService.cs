using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class UpstreamSynchronizationService : ISynchronizationService, IDaemonService
    {
        readonly Tracer _Tracer;

        readonly SynchronizationConfigurationSection _Configuration;

        readonly IUpstreamIntegrationService _UpstreamIntegrationService;
        readonly IUpstreamAvailabilityProvider _UpstreamAvailabilityProvider;
        readonly IThreadPoolService _ThreadPool;
        readonly ISynchronizationLogService _SynchronizationLogService;
        readonly ISynchronizationQueueManager _QueueManager;
        readonly ISubscriptionRepository _SubscriptionRepository;
        readonly IJobManagerService _JobManager;
        readonly IServiceManager _ServiceManager;
        readonly IRestClientFactory _RestClientFactory;

        readonly LocalRepositoryFactory _LocalRepositoryFactory;

        readonly SynchronizationMessagePump _MessagePump;

        public bool IsRunning { get; private set; }

        public string ServiceName => "Remote Data Synchronization Service";

        public bool IsSynchronizing { get; private set; }

        public event EventHandler Starting;
        public event EventHandler Started;
        public event EventHandler Stopping;
        public event EventHandler Stopped;
        public event EventHandler PullCompleted;
        public event EventHandler PushCompleted;

        private object _PushLock;
        private object _PullLock;
        private TimeSpan _LockTimeout;

        public UpstreamSynchronizationService(
            IConfigurationManager configurationManager,
            IUpstreamIntegrationService upstreamIntegrationService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IThreadPoolService threadPool,
            ISynchronizationLogService synchronizationLogService,
            ISynchronizationQueueManager queueManager,
            ISubscriptionRepository subscriptionRepository,
            IJobManagerService jobManagerService,
            IServiceManager serviceManager,
            IRestClientFactory restClientFactory)
        {
            _Tracer = new Tracer(nameof(UpstreamSynchronizationService));
            _Configuration = configurationManager.GetSection<SynchronizationConfigurationSection>();
            _UpstreamIntegrationService = upstreamIntegrationService;
            _UpstreamAvailabilityProvider = upstreamAvailabilityProvider;
            _ThreadPool = threadPool;
            _SynchronizationLogService = synchronizationLogService;
            _QueueManager = queueManager;
            _JobManager = jobManagerService;
            _ServiceManager = serviceManager;
            _RestClientFactory = restClientFactory;

            _PushLock = new object();
            _PullLock = new object();
            _LockTimeout = TimeSpan.FromMilliseconds(100);

            _MessagePump = new SynchronizationMessagePump(_QueueManager, _ThreadPool);

            _LocalRepositoryFactory = _ServiceManager.CreateInjected<LocalRepositoryFactory>();
        }

        protected virtual List<SubscriptionDefinition> GetSubscriptionDefinitions()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                using (var restclient = _RestClientFactory?.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {
                    using (var client = new AmiServiceClient(restclient))
                    {
                        var coll = client.GetSubscriptionDefinitions();

                        return coll?.CollectionItem?.OfType<SubscriptionDefinition>()?.Where(s => _Configuration.Subscriptions.Contains(s.Uuid))?.ToList();
                    }
                }
            }
        }


        protected virtual TimeSpan GetUpstreamDrift(ServiceEndpointType endpointType)
        {
            var result = _UpstreamAvailabilityProvider.GetTimeDrift(endpointType) ?? TimeSpan.FromMilliseconds(_UpstreamAvailabilityProvider.GetUpstreamLatency(endpointType));

            if (result < TimeSpan.Zero) // GetUpstreamLatency returns -1 when the service is unavailable.
            {
                throw new TimeoutException("Service Unavailable.");
            }

            return result;
        }

        private void PullInternal(object state = null)
        {
            if (Monitor.TryEnter(_PullLock, _LockTimeout))
            {
                try
                {
                    foreach (var inboundqueue in _QueueManager.GetAll(SynchronizationPattern.UpstreamToLocal))
                    {
                        _MessagePump.RunDefault(inboundqueue, entry =>
                        {
                            //TODO: Do we still need this? RemoteSynchronizationService.cs:413, sdk - SanteDB.DisconnectedClient.Synchronization.RemoteSynchronizationService
                            if (entry is SecurityUser || entry is SecurityRole || entry is SecurityPolicy)
                            {
                                return SynchronizationMessagePump.Continue;
                            }

                            var repotype = typeof(IRepositoryService<>).MakeGenericType(entry.Data.GetType());

                            if (_LocalRepositoryFactory.TryCreateService(repotype, out var localrepoobj))
                            {
                                if (localrepoobj is IRepositoryService localrepo)
                                {
                                    if (entry.Data is IVersionedData versioned)
                                    {
                                        var existing = localrepo.Get(versioned.Key.Value) as IVersionedData;

                                        if (existing?.VersionKey == versioned.VersionKey)
                                        {
                                            //Skip
                                            return SynchronizationMessagePump.Continue;
                                        }
                                    }

                                    localrepo.Save(entry.Data); //Save does an upsert for us.
                                    return SynchronizationMessagePump.Continue;
                                }
                            }

                            _Tracer.TraceError("Error getting repository.");
                            throw new InvalidOperationException("Error getting repository."); //Will throw onto deadletter queue
                        });
                    }
                }
                finally
                {
                    PullCompleted?.Invoke(this, EventArgs.Empty);
                    Monitor.Exit(_PullLock);
                }
            }
        }

        private void SendOutboundEntryInternal(ISynchronizationQueueEntry entry)
        {
            if (null == entry)
            {
                return;
            }

            MapUpstreamTemplateKey(entry?.Data);

            switch (entry.Operation)
            {
                case SynchronizationQueueEntryOperation.Insert:
                    _UpstreamIntegrationService.Insert(entry?.Data);
                    break;
                case SynchronizationQueueEntryOperation.Update:
                    _UpstreamIntegrationService.Update(entry?.Data, forceUpdate: entry.IsRetry);
                    break;
                case SynchronizationQueueEntryOperation.Obsolete:
                    _UpstreamIntegrationService.Obsolete(entry?.Data, forceObsolete: entry.IsRetry);
                    break;
                default:
                    break;
            }
        }

        private void PushInternal(object state = null)
        {
            if (Monitor.TryEnter(_PushLock, _LockTimeout))
            {
                try
                {

                    //TODO: Should we make this parallel?
                    foreach (var outboundqueue in _QueueManager.GetAll(SynchronizationPattern.LocalToUpstream))
                    {
                        _MessagePump.RunDefault(outboundqueue, entry =>
                        {
                            SendOutboundEntryInternal(entry);

                            return SynchronizationMessagePump.Continue;
                        });
                    }
                }
                finally
                {
                    PushCompleted?.Invoke(this, EventArgs.Empty);
                    Monitor.Exit(_PushLock);
                }
            }
        }


        private Guid GetUpstreamTemplateKey(TemplateDefinition template)
        {
            var servertemplate = _UpstreamIntegrationService.Find<TemplateDefinition>(e => e.Mnemonic == template.Mnemonic).FirstOrDefault();

            if (null == servertemplate)
            {
                servertemplate = template;
                _UpstreamIntegrationService.Insert(servertemplate);
            }

            return servertemplate.Key.Value;

        }

        private void MapUpstreamTemplateKey(IdentifiedData data)
        {
            if (data is TemplateDefinition td)
            {
                GetUpstreamTemplateKey(td); //Will force the insert to upstream.
            }
            else if (data is Entity entity && entity.TemplateKey.HasValue)
            {
                entity.TemplateKey = GetUpstreamTemplateKey(entity.LoadProperty(e => e.Template));
            }
            else if (data is Act act)
            {
                act.TemplateKey = GetUpstreamTemplateKey(act.LoadProperty(a => a.Template));
            }
        }

        private int ScaleTakeCount(int takecount)
        {
            var scalefactor = 1;

            /* if (this.m_configuration.BigBundles && (count == 5000 && perfTimer.ElapsedMilliseconds < 40000 ||
                                count < 5000 && result.TotalResults > 20000 && perfTimer.ElapsedMilliseconds < 40000))
                                count = 5000;
                            else if (this.m_configuration.BigBundles && (count == 2500 && perfTimer.ElapsedMilliseconds < 30000 ||
                                count < 2500 && result.TotalResults > 10000 && perfTimer.ElapsedMilliseconds < 30000))
                                count = 2500;
                            else if (count == 1000 && perfTimer.ElapsedMilliseconds < 20000 ||
                                count < 1000 && result.TotalResults > 5000 && perfTimer.ElapsedMilliseconds < 20000)
                                count = 1000;
                            else if (count == 500 && perfTimer.ElapsedMilliseconds < 10000 ||
                                count < 200 && result.TotalResults > 1000 && perfTimer.ElapsedMilliseconds < 10000)
                                count = 500;
                            else
                                count = 100;
            */

            return takecount * scalefactor;
        }


        public bool Fetch(Type modelType)
        {
            //TODO: This was never implemented. Should do a head in constrained bandwidth scenarios and if modified, will trigger sync.
            throw new NotImplementedException();
        }

        public void Pull(SubscriptionTriggerType trigger)
        {
            var subscriptions = GetSubscriptionDefinitions();

            foreach (var subscription in subscriptions)
            {
                foreach (var def in subscription.ClientDefinitions.Where(cd => (cd.Trigger & trigger) == trigger))
                {
                    //TODO: Execute.
                }
            }

            throw new NotImplementedException();
        }

        public int Pull(Type modelType)
            => Pull(modelType, null, false);

        public int Pull(Type modelType, NameValueCollection filter)
            => Pull(modelType, filter, false);

        public int Pull(Type modelType, NameValueCollection filter, bool always)
        {
            //always ignores If-Modified-Since
            var filterstring = filter?.ToString();

            //TODO: Fetch items from upstream
            var lastmodificationdate = !always ? _SynchronizationLogService.GetLastTime(modelType, filterstring) : (DateTime?)null;

            if (null != lastmodificationdate)
            {
                var drift = GetUpstreamDrift(ServiceEndpointType.HealthDataService);

                lastmodificationdate = lastmodificationdate.Value.Add(drift);
            }
            else
            {
                if (!_UpstreamAvailabilityProvider.IsAvailable(ServiceEndpointType.HealthDataService))
                {
                    return 0;
                }
            }

            var offset = 0;
            var etag = string.Empty;
            var entitycount = 0;
            var queryid = Guid.NewGuid();
            var takecount = _Configuration.BigBundles ? 1_000 : 100;

            var existingquery = _SynchronizationLogService.FindQueryData(modelType, filterstring);

            if (IsExistingQueryLogValid(existingquery))
            {
                queryid = existingquery.QueryId;
                offset = existingquery.QueryOffset;
            }
            else
            {
                _SynchronizationLogService.CompleteQuery(existingquery);

                _SynchronizationLogService.SaveQuery(modelType, filterstring, queryid, 0);
            }

            var inboundqueue = _QueueManager.GetInboundQueue();

            var result = _UpstreamIntegrationService.Find(modelType, filter, new UpstreamIntegrationOptions { IfModifiedSince = lastmodificationdate, Timeout = 120_000 }).AsStateful(queryid);

            if (null == result)
            {
                //TODO: Log this scenario.
                return 0;
            }

            var currenttotal = result.Count();

            for (; offset < currenttotal; offset += takecount)
            {
                takecount = ScaleTakeCount(takecount);

                var entities = result.Skip(offset).Take(takecount).ToList();

                etag = entities.GetFirstEtag();

                inboundqueue.Enqueue(entities, SynchronizationQueueEntryOperation.Sync);

                _SynchronizationLogService.SaveQuery(modelType, filterstring, queryid, offset);

                entitycount += entities.Count;

                if (entities.Count == 0)
                {
                    //TODO: check if this is an error.
                    var newtotal = result.Count();
                    if (newtotal > 0)
                    {
                        currenttotal = newtotal;
                    }
                    break;
                }
            }

            _SynchronizationLogService.Save(modelType, filterstring, etag, lastmodificationdate);

            _ThreadPool.QueueUserWorkItem(PullInternal);

            PullCompleted?.Invoke(this, EventArgs.Empty);

            return entitycount;
        }

        private bool IsExistingQueryLogValid(ISynchronizationLogQuery query)
        {
            if (null == query)
            {
                return false;
            }
            return DateTime.UtcNow.Subtract(query.QueryStartTime).TotalHours <= 1;
        }

        public void Push()
        {
            _ThreadPool.QueueUserWorkItem(PushInternal);
        }

        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (_Configuration.PollInterval != default(TimeSpan))
            {
                ApplicationServiceContext.Current.Started += (s, e) =>
                {
                    try
                    {
                        this.Pull(SubscriptionTriggerType.OnStart);

                        var job = _ServiceManager.CreateInjected<UpstreamSynchronizationJob>();
                        _JobManager.AddJob(job, JobStartType.DelayStart);
                        _JobManager.SetJobSchedule(job, _Configuration.PollInterval);

                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        _Tracer.TraceError("Error Adding Upstream Sync Job: {0}", ex);
                    }

                };
            }

            IsRunning = true;

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            IsRunning = false;
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Bundle dependent objects for resubmit
        /// </summary>
        private IdentifiedData BundleDependentObjects(IdentifiedData data, Bundle currentBundle = null)
        {
            // Bundle establishment
            currentBundle = currentBundle ?? new Bundle();
            if (data is Bundle dataBundle)
                currentBundle.Item.AddRange(dataBundle.Item);

            if (data is Person entity) // Fix entity key
            {
                foreach (var rel in entity.Relationships)
                    if (!currentBundle.Item.Any(i => i.Key == rel.TargetEntityKey))// && !integrationService.Exists<Entity>(rel.TargetEntityKey.Value))
                    {
                        var loaded = rel.LoadProperty<Entity>(nameof(EntityRelationship.TargetEntity));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, currentBundle); // cascade load
                    }

            }
            else if (data is Act act)
            {
                foreach (var rel in act.Relationships)
                    if (!currentBundle.Item.Any(i => i.Key == rel.TargetActKey))// && !integrationService.Exists<Act>(rel.TargetActKey.Value))
                    {
                        var loaded = rel.LoadProperty<Act>(nameof(ActRelationship.TargetAct));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, currentBundle);
                    }
                foreach (var rel in act.Participations)
                    if (!currentBundle.Item.Any(i => i.Key == rel.PlayerEntityKey))// && !integrationService.Exists<Entity>(rel.PlayerEntityKey.Value))
                    {
                        var loaded = rel.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, currentBundle);
                    }
            }
            else if (data is Bundle bundle)
            {
                foreach (var itm in bundle.Item.ToArray())
                {
                    this.BundleDependentObjects(itm, currentBundle);
                }
            }

            return currentBundle;
        }
    }
}

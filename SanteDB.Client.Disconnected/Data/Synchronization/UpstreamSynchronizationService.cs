using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
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


        private void PullInternal(object state = null)
        {
            if (Monitor.TryEnter(_PullLock, _LockTimeout))
            {
                try
                {
                    foreach (var inboundqueue in _QueueManager.GetAll(SynchronizationPattern.UpstreamToLocal))
                    {
                        _MessagePump.Run(inboundqueue, entry =>
                        {
                            var repotype = typeof(IRepositoryService<>).MakeGenericType(entry.Data.GetType());

                            if (_LocalRepositoryFactory.TryCreateService(repotype, out var localrepoobj))
                            {
                                if (localrepoobj is IRepositoryService localrepo)
                                {
                                    localrepo.Save(entry.Data);
                                    return true;
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

        private void PushInternal(object state = null)
        {
            if (Monitor.TryEnter(_PushLock, _LockTimeout))
            {
                try
                {
                    var amirestclient = _RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService);
                    var hdsirestclient = _RestClientFactory.GetRestClientFor(Core.Interop.ServiceEndpointType.HealthDataService);

                    foreach (var outboundqueue in _QueueManager.GetAll(SynchronizationPattern.LocalToUpstream))
                    {
                        _MessagePump.Run(outboundqueue, entry =>
                        {
                            switch (entry?.EndpointType)
                            {
                                case Core.Interop.ServiceEndpointType.AdministrationIntegrationService:
                                    amirestclient.Put<IdentifiedData, IdentifiedData>(entry?.Type, entry?.Data);
                                    break;
                                case Core.Interop.ServiceEndpointType.HealthDataService:
                                    hdsirestclient.Put<IdentifiedData, IdentifiedData>(entry?.Type, entry?.Data);
                                    break;
                                case null: break;
                                default:
                                    throw new NotSupportedException($"ServiceEndpointType \"{entry?.EndpointType}\" not supported");
                            }

                            return true;
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


        public bool Fetch(Type modelType)
        {
            //TODO: This was never implemented. Should do a head in constrained bandwidth scenarios and if modified, will trigger sync.
            throw new NotImplementedException();
        }

        public void Pull(SubscriptionTriggerType trigger)
        {
            var subscriptions = GetSubscriptionDefinitions();

            foreach(var subscription in subscriptions)
            {
                foreach(var def in subscription.ClientDefinitions.Where(cd=>(cd.Trigger & trigger) == trigger))
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

            //TODO: Fetch items from upstream



            _ThreadPool.QueueUserWorkItem(PullInternal);

            return 0;
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

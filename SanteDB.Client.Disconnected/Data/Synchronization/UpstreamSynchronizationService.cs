/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 * User: fyfej
 * Date: 2023-3-10
 */
using Polly;
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Client.Exceptions;
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
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using SanteDB.Messaging.AMI.Client;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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

        readonly ISyncPolicy _PushExceptionPolicy;

        readonly Dictionary<string, Expression<Func<object, bool>>> _GuardExpressionCache;

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
            _GuardExpressionCache = new Dictionary<string, Expression<Func<object, bool>>>();

            _MessagePump = new SynchronizationMessagePump(_QueueManager, _ThreadPool);

            _LocalRepositoryFactory = _ServiceManager.CreateInjected<LocalRepositoryFactory>();

            //Exception policies
            _PushExceptionPolicy = Policy.Handle<UpstreamIntegrationException>(ex => ex.InnerException != null)
                .Retry(2);

        }

        private List<SubscriptionDefinition> GetSubscriptionDefinitions()
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


        private TimeSpan GetUpstreamDrift(ServiceEndpointType endpointType)
        {
            var result = _UpstreamAvailabilityProvider.GetTimeDrift(endpointType) ?? TimeSpan.FromMilliseconds(_UpstreamAvailabilityProvider.GetUpstreamLatency(endpointType));

            if (result < TimeSpan.Zero) // GetUpstreamLatency returns -1 when the service is unavailable.
            {
                throw new TimeoutException("Service Unavailable.");
            }

            return result;
        }

        private IRepositoryService GetLocalRepositoryForData(Type entityType)
        {
            if (null == entityType)
            {
                return null;
            }

            var repotype = typeof(IRepositoryService<>).MakeGenericType(entityType);

            if (_LocalRepositoryFactory.TryCreateService(repotype, out var localrepoobj))
            {
                if (localrepoobj is IRepositoryService localrepo)
                {
                    return localrepo;
                }
                else
                {
                    _Tracer.TraceError("Error: Repository {0} does not implement IRepositoryService.", repotype.Name);
                }
            }

            return null;
        }
        private IRepositoryService GetLocalRepositoryForData(IdentifiedData data)
            => GetLocalRepositoryForData(data.GetType());

        private void RunOutboundMessagePump(object state = null)
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
        private void RunInboundMessagePump(object state = null)
        {
            if (Monitor.TryEnter(_PullLock, _LockTimeout))
            {
                try
                {
                    foreach (var inboundqueue in _QueueManager.GetAll(SynchronizationPattern.UpstreamToLocal))
                    {
                        _MessagePump.RunDefault(inboundqueue, entry =>
                        {
                            if (null == entry?.Data)
                            {
                                return SynchronizationMessagePump.Continue;
                            }

                            //TODO: Do we still need this? RemoteSynchronizationService.cs:413, sdk - SanteDB.DisconnectedClient.Synchronization.RemoteSynchronizationService
                            if (entry.Data is SecurityUser || entry.Data is SecurityRole || entry.Data is SecurityPolicy)
                            {
                                _Tracer.TraceVerbose("Skipping {0} because data is SecurityUser, SecurityRole, or SecurityPolicy", entry.Id);
                                return SynchronizationMessagePump.Continue;
                            }

                            var localrepo = GetLocalRepositoryForData(entry.Data);

                            if (null == localrepo)
                            {
                                _Tracer.TraceError("Error getting repository.");
                                throw new InvalidOperationException("Error getting repository."); //Will throw onto deadletter queue
                            }

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
                    _PushExceptionPolicy.Execute(()=>_UpstreamIntegrationService.Insert(entry?.Data));
                    break;
                case SynchronizationQueueEntryOperation.Update:
                    _PushExceptionPolicy.Execute(() => _UpstreamIntegrationService.Update(entry?.Data, forceUpdate: entry.IsRetry));
                    break;
                case SynchronizationQueueEntryOperation.Obsolete:
                    _PushExceptionPolicy.Execute(() => _UpstreamIntegrationService.Obsolete(entry?.Data, forceObsolete: entry.IsRetry));
                    break;
                default:
                    break;
            }
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
            else if (data is IHasTemplate iht)
            {
                iht.TemplateKey = GetUpstreamTemplateKey(data.LoadProperty<TemplateDefinition>(nameof(IHasTemplate.Template)));
            }
        }

        private int ScaleTakeCount(int takecount, long lastoperation, long? estimatedLatency = null)
        {
            if (lastoperation > 0) //Don't scale with a negative value. This is for init values.
            {
                var peritemcost = (lastoperation - (estimatedLatency ?? 0)) / takecount; //y=mx+b, m = (y - b) / x



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
            }

            return takecount;
        }



        private List<object> GetSubscribedObjects()
        {
            var subscribedrepository = GetLocalRepositoryForData(_Configuration.SubscribeToResource.Type);

            if (null == subscribedrepository)
            {
                _Tracer.TraceError("No local repository exists for {0}", _Configuration.SubscribeToResource.Type);
                throw new InvalidOperationException($"No local repostiory exists for type {_Configuration.SubscribeToResource.Type}");
            }

            Expression<Func<object, bool>> expr = obj => null != ((IdentifiedData)obj).Key && _Configuration.SubscribedObjects.Contains(((IdentifiedData)obj).Key.Value);

            return subscribedrepository.Find(expr).OfType<object>().ToList();
        }

        

        private bool IsExistingQueryLogValid(ISynchronizationLogQuery query)
        {
            if (null == query)
            {
                return false;
            }
            return DateTime.UtcNow.Subtract(query.QueryStartTime).TotalHours <= 1;
        }

        /// <summary>
        /// Gets a subset of the <paramref name="subscribedObjects"/> that match any guard conditions present in the <paramref name="definition"/>.
        /// </summary>
        /// <param name="definition">The definition to look for guards with.</param>
        /// <param name="subscribedObjects">A list of objects that are part of the subscription.</param>
        /// <returns>A list of the objects from <paramref name="subscribedObjects"/> that meet any guard conditions present in the definition.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        private List<object> GetSubscribedObjectsForDefinition(SubscriptionClientDefinition definition, List<object> subscribedObjects)
        {
            if (null == definition)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition?.Guards?.Count < 1) //No guards
            {
                return subscribedObjects;
            }

            if (subscribedObjects?.Count < 1) //No subscription objects
            {
                return new List<object>();
            }

            var query = subscribedObjects.AsQueryable();

            foreach (var guard in definition.Guards)
            {
                if (!_GuardExpressionCache.TryGetValue(guard, out var expr))
                {
                    expr = QueryExpressionParser.BuildLinqExpression(_Configuration.SubscribeToResource.Type, NameValueCollectionExtensions.ParseQueryString(guard)).ConvertToObjectLambda(_Configuration.SubscribeToResource.Type);
                    _GuardExpressionCache.Add(guard, expr);
                }

                query = query.Where(expr);
            }

            return query.ToList();
        }

        private int ProcessSubscription(SubscriptionDefinition subscription, SubscriptionClientDefinition clientDefinition, List<object> subscribedObjects)
        {
            var totalresults = 0;

            if (clientDefinition?.Filters?.Count > 0)
            {
                foreach (var filter in clientDefinition.Filters)
                {
                    if (filter.IndexOf("$subscribed") > -1)
                    {
                        object subscribed = null;
                        var expr = QueryExpressionParser.BuildLinqExpression(subscription.ResourceType, NameValueCollectionExtensions.ParseQueryString(filter), "o", new Dictionary<string, Func<object>>
                        {
                            { "subscribed", () => subscribed }
                        });

                        foreach (var subscribedobject in subscribedObjects)
                        {
                            subscribed = subscribedobject;
                            var newfilter = QueryExpressionBuilder.BuildQuery(subscription.ResourceType, expr).ToHttpString();
                            totalresults += PullInternal(subscription.ResourceType, NameValueCollectionExtensions.ParseQueryString(newfilter), clientDefinition.IgnoreModifiedOn);
                        }
                    }
                    else
                    {
                        totalresults += PullInternal(subscription.ResourceType, NameValueCollectionExtensions.ParseQueryString(filter), clientDefinition.IgnoreModifiedOn);
                    }
                }
            }
            else
            {
                totalresults += PullInternal(subscription.ResourceType, null, false);
            }

            return totalresults;
        }

        private int PullInternal(Type modelType, NameValueCollection filter, bool ignoreModifiedOn)
        {
            //always ignores If-Modified-Since
            var filterstring = filter?.ToString();

            //TODO: Fetch items from upstream
            var lastmodificationdate = !ignoreModifiedOn ? _SynchronizationLogService.GetLastTime(modelType, filterstring) : (DateTime?)null;

            var estimatedlatency = _UpstreamAvailabilityProvider.GetUpstreamLatency(ServiceEndpointType.HealthDataService);

            if (estimatedlatency < 0) //Unavailable
            {
                _Tracer.TraceInfo("Upstream is unavialable. Exiting Pull.");
                return 0;
            }

            _Tracer.TraceVerbose("Estimated upstream latency: {0}", estimatedlatency);

            //If we are using last modified, check what the time drift is. 
            if (null != lastmodificationdate)
            {
                var drift = GetUpstreamDrift(ServiceEndpointType.HealthDataService);

                _Tracer.TraceVerbose("Upstream time difference: {0}", drift);

                lastmodificationdate = lastmodificationdate.Value.Add(drift);
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

            if (null == inboundqueue)
            {
                _Tracer.TraceError("No inbound queue is available.");
                throw new NotSupportedException("No inbound queue available.");
            }

            var result = _UpstreamIntegrationService.Find(modelType, filter, new UpstreamIntegrationOptions { IfModifiedSince = lastmodificationdate, Timeout = 120_000 }).AsStateful(queryid);

            if (null == result)
            {
                //TODO: Log this scenario.
                return 0;
            }

            var currenttotal = result.Count();
            var lastoperationtime = -1L;
            var sw = new Stopwatch();

            for (; offset < currenttotal; offset += takecount)
            {
                takecount = ScaleTakeCount(takecount, lastoperationtime, estimatedlatency);

                sw.Restart();
                var entities = result.Skip(offset).Take(takecount).ToList();
                sw.Stop();
                lastoperationtime = sw.ElapsedMilliseconds;

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
                    else
                    {
                        break;
                    }
                }
            }

            _SynchronizationLogService.Save(modelType, filterstring, etag, lastmodificationdate);

            return entitycount;
        }

        /// <inheritdoc />
        public bool Fetch(Type modelType)
        {
            //TODO: This was never implemented. Should do a head in constrained bandwidth scenarios and if modified, will trigger sync.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Pull(SubscriptionTriggerType trigger)
        {
            var subscriptions = GetSubscriptionDefinitions();
            var subscribedobjects = GetSubscribedObjects();

            var totalresults = 0;

            foreach (var subscription in subscriptions)
            {
                foreach (var def in subscription.ClientDefinitions.Where(cd => (cd.Trigger & trigger) == trigger))
                {
                    if ((_Configuration.Mode == SynchronizationMode.Full && ((def.Mode & SubscriptionModeType.Full) == SubscriptionModeType.Full)) ||
                        (_Configuration.Mode == SynchronizationMode.Partial && ((def.Mode & SubscriptionModeType.Partial) == SubscriptionModeType.Partial)))
                    {
                        _Tracer.TraceInfo("Processing definition {0}, subscription {1}.", def.Name, subscription.Uuid);

                        var objectstoevaluate = GetSubscribedObjectsForDefinition(def, subscribedobjects);

                        totalresults += ProcessSubscription(subscription, def, objectstoevaluate);
                    }
                    else
                    {
                        _Tracer.TraceVerbose("Skipping {0} because the mode does not match the CDR mode.", def.Name);
                    }
                }
            }

            _ThreadPool.QueueUserWorkItem(RunInboundMessagePump);
        }

        /// <inheritdoc />
        public int Pull(Type modelType)
            => Pull(modelType, null, false);

        /// <inheritdoc />
        public int Pull(Type modelType, NameValueCollection filter)
            => Pull(modelType, filter, false);

        /// <inheritdoc />
        public int Pull(Type modelType, NameValueCollection filter, bool ignoreModifiedOn)
        {
            var entitycount = PullInternal(modelType, filter, ignoreModifiedOn);

            _ThreadPool.QueueUserWorkItem(RunInboundMessagePump);

            return entitycount;
        }

        /// <inheritdoc />
        public void Push()
        {
            _ThreadPool.QueueUserWorkItem(RunOutboundMessagePump);
        }
        /// <inheritdoc />
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (_Configuration.PollInterval != default(TimeSpan))
            {
                ApplicationServiceContext.Current.Started += (s, e) =>
                {

                    try
                    {
                        var job = _ServiceManager.CreateInjected<UpstreamSynchronizationJob>();
                        _JobManager.AddJob(job, JobStartType.DelayStart);
                        _JobManager.SetJobSchedule(job, _Configuration.PollInterval);

                        this.Pull(SubscriptionTriggerType.OnStart);

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
        /// <inheritdoc />
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            IsRunning = false;
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        
    }
}

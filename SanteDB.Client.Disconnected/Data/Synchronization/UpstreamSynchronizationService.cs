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
 * Date: 2023-5-19
 */
using Polly;
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl.Repository;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// An implementation of the <see cref="ISynchronizationService"/> which pulls HDSI and AMI data from the remote
    /// </summary>
    /// // TODO: Add auditing to the entire class
    public class UpstreamSynchronizationService : UpstreamServiceBase, ISynchronizationService, IDaemonService, IReportProgressChanged
    {
        readonly SynchronizationConfigurationSection _Configuration;

        readonly IThreadPoolService _ThreadPool;
        readonly ISynchronizationLogService _SynchronizationLogService;
        readonly ISynchronizationQueueManager _QueueManager;
        readonly IJobManagerService _JobManager;
        private readonly ISubscriptionRepository _SubscriptionRepository;
        readonly IServiceManager _ServiceManager;
        private readonly IServiceProvider _ServiceProvider;
        private readonly ILocalizationService _LocalizationService;
        readonly LocalRepositoryFactory _LocalRepositoryFactory;

        readonly SynchronizationMessagePump _MessagePump;

        readonly ISyncPolicy _PushExceptionPolicy;

        readonly Dictionary<string, Expression<Func<object, bool>>> _GuardExpressionCache;

        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public string ServiceName => "Remote Data Synchronization Service";

        /// <inheritdoc/>
        public bool IsSynchronizing { get; private set; }

        /// <inheritdoc/>
        public event EventHandler Starting;
        /// <inheritdoc/>
        public event EventHandler Started;
        /// <inheritdoc/>
        public event EventHandler Stopping;
        /// <inheritdoc/>
        public event EventHandler Stopped;
        /// <inheritdoc/>
        public event EventHandler PullCompleted;
        /// <inheritdoc/>
        public event EventHandler PushCompleted;
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        private object _OutboundQueueLock;
        private object _InboundQueueLock;
        private readonly IAdhocCacheService _AdhocCache;
        private TimeSpan _LockTimeout;

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamSynchronizationService(
            IConfigurationManager configurationManager,
            IUpstreamIntegrationService upstreamIntegrationService,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IThreadPoolService threadPool,
            ISynchronizationLogService synchronizationLogService,
            ISynchronizationQueueManager queueManager,
            ISubscriptionRepository subscriptionRepository,
            IJobManagerService jobManagerService,
            IServiceManager serviceManager,
            IServiceProvider serviceProvider,
            ILocalizationService localizationService,
            IAdhocCacheService adhocCacheService,
            IRestClientFactory restClientFactory) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this._Configuration = configurationManager.GetSection<SynchronizationConfigurationSection>();
            this._ThreadPool = threadPool;
            this._SynchronizationLogService = synchronizationLogService;
            this._QueueManager = queueManager;
            this._JobManager = jobManagerService;
            this._SubscriptionRepository = subscriptionRepository;
            this._ServiceManager = serviceManager;
            this._ServiceProvider = serviceProvider;
            this._LocalizationService = localizationService;
            this._OutboundQueueLock = new object();
            this._InboundQueueLock = new object();
            this._AdhocCache = adhocCacheService;
            this._LockTimeout = TimeSpan.FromMilliseconds(100);
            this._GuardExpressionCache = new Dictionary<string, Expression<Func<object, bool>>>();

            this._MessagePump = new SynchronizationMessagePump(_QueueManager, _ThreadPool);

            this._LocalRepositoryFactory = _ServiceManager.CreateInjected<LocalRepositoryFactory>();

            //Exception policies
            this._PushExceptionPolicy = Policy.Handle<UpstreamIntegrationException>(ex => ex.InnerException != null)
                .Retry(2);

        }

        /// <summary>
        /// Load a list of all subscription definitions which are applicable to this configuration
        /// </summary>
        /// <remarks>The dCDR may select which subscriptions are being "subscribed" to - this function loads the data from the configured <see cref="ISubscriptionRepository"/> based 
        /// on the configuration provided</remarks>
        private List<SubscriptionDefinition> GetSubscriptionDefinitions()
        {
            return this._SubscriptionRepository.Find(o => this._Configuration.Subscriptions.Contains(o.Key.Value)).OrderBy(o => o.Order).ToList();
        }


        /// <summary>
        /// Get the overall drift in time between the upstream and this machine
        /// </summary>
        /// <param name="endpointType">The endpoint from which the drift should be calculated</param>
        private TimeSpan GetUpstreamDrift(ServiceEndpointType endpointType)
        {
            var result = this.UpstreamAvailabilityProvider.GetTimeDrift(endpointType)?.TotalMilliseconds ?? this.UpstreamAvailabilityProvider.GetUpstreamLatency(endpointType);

            if (!result.HasValue) // GetUpstreamLatency returns -1 when the service is unavailable.
            {
                throw new TimeoutException("Service Unavailable.");
            }
            return TimeSpan.FromMilliseconds((double)result);
        }

        /// <summary>
        /// Exhause the outbound queue by sending the data to the remote server
        /// </summary>
        private void RunOutboundMessagePump(object state = null)
        {
            if (Monitor.TryEnter(this._OutboundQueueLock, this._LockTimeout))
            {
                try
                {
                    foreach (var outboundqueue in this._QueueManager.GetAll(SynchronizationPattern.LocalToUpstream))
                    {
                        this._MessagePump.RunDefault(outboundqueue, entry =>
                        {
                            this.SendOutboundEntryInternal(entry);
                            return SynchronizationMessagePump.Continue;
                        });
                    }
                }
                finally
                {
                    this.PushCompleted?.Invoke(this, EventArgs.Empty);
                    Monitor.Exit(_OutboundQueueLock);
                }
            }
        }

        /// <summary>
        /// Exuast the inbound queue and attempt to insert the data into the local persistence service
        /// </summary>
        private void RunInboundMessagePump(object state = null)
        {
            if (Monitor.TryEnter(_InboundQueueLock, _LockTimeout))
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

                            if (entry.Data is SecurityUser || entry.Data is SecurityRole || entry.Data is SecurityPolicy)
                            {
                                _Tracer.TraceVerbose("Skipping {0} because data is SecurityUser, SecurityRole, or SecurityPolicy", entry.Id);
                                return SynchronizationMessagePump.Continue;
                            }

                            using (AuthenticationContext.EnterSystemContext())
                            {
                                var localPersistence = this.GetPersistenceService(entry.Data.GetType());

                                if (entry.Data is Bundle bdl)
                                {
                                    localPersistence.Insert(entry.Data);
                                }
                                else
                                {
                                    var existing = localPersistence.Get(entry.Data.Key.Value);
                                    if (existing is IVersionedData existingVersioned && entry.Data is IVersionedData newVersioned)
                                    {
                                        if (newVersioned?.VersionKey == existingVersioned.VersionKey)
                                        {
                                            //Skip
                                            return SynchronizationMessagePump.Continue;
                                        }
                                    }

                                    // If there is an existing - update otherwise insert
                                    if (existing == null)
                                    {
                                        localPersistence.Insert(entry.Data);
                                    }
                                    else
                                    {
                                        localPersistence.Update(entry.Data);
                                    }
                                }
                            }
                            return SynchronizationMessagePump.Continue;


                        });
                    }
                }
                finally
                {
                    PullCompleted?.Invoke(this, EventArgs.Empty);
                    Monitor.Exit(_InboundQueueLock);
                }
            }
        }

        /// <summary>
        /// Get the data persistence service <paramref name="forType"/> 
        /// </summary>
        /// <param name="forType">The type for which the <see cref="IDataPersistenceService"/> should be retrieved</param>
        /// <returns>The configured <see cref="IDataPersistenceService"/> for <paramref name="forType"/></returns>
        /// <remarks>A <see cref="IDataPersistenceService"/> is used here since the <see cref="IRepositoryService"/> may 
        /// include logic for enqueuing the data received from the local REST APIs</remarks>
        private IDataPersistenceService GetPersistenceService(Type forType)
        {
            var persistenceType = typeof(IDataPersistenceService<>).MakeGenericType(forType);
            var retVal = this._ServiceProvider.GetService(persistenceType) as IDataPersistenceService;
            if (retVal == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.SERVICE_NOT_FOUND, persistenceType));
            }
            return retVal;
        }

        /// <summary>
        /// Sends a single queue entry to the upstream
        /// </summary>
        /// <param name="entry">The queue entry to send</param>
        private void SendOutboundEntryInternal(ISynchronizationQueueEntry entry)
        {
            if (null == entry)
            {
                return;
            }
            else if (this._Configuration.ForbidSending.Any(t => t.Type == entry.Data.GetType())) // Don't send forbidden entries to the server
            {
                return;
            }

            var dataToSubmit = entry.Data;
            if (entry.IsRetry)
            {
                dataToSubmit = this.BundleDependentObjects(dataToSubmit);
            }

            switch (entry.Operation)
            {
                case SynchronizationQueueEntryOperation.Insert:
                    this._PushExceptionPolicy.Execute(() => this.UpstreamIntegrationService.Insert(dataToSubmit));
                    break;
                case SynchronizationQueueEntryOperation.Update:
                    this._PushExceptionPolicy.Execute(() => this.UpstreamIntegrationService.Update(dataToSubmit, forceUpdate: this._Configuration.OverwriteServer)); // TODO: Add option for a re-queued dead letter queue entry to force retry || entry.Force));
                    break;
                case SynchronizationQueueEntryOperation.Obsolete:
                    this._PushExceptionPolicy.Execute(() => this.UpstreamIntegrationService.Obsolete(dataToSubmit, forceObsolete: this._Configuration.OverwriteServer)); // TODO: Add option for a re-queued dead letter queue entry to force retry || entry.Force));
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// Bundle dependent objects for resubmit
        /// </summary>
        /// <remarks>This is important when re-sending a dead-letter object since there may have been missing
        /// data in the original submission which is now available</remarks>
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

        private int ScaleTakeCount(int takecount, long lastoperation, long? estimatedLatency = null)
        {
            if (lastoperation > 0) //Don't scale with a negative value. This is for init values.
            {
                var peritemcost = (lastoperation - (estimatedLatency ?? 0)) / (float)takecount; //y=mx+b, m = (y - b) / x
                if(peritemcost < 0)
                {
                    peritemcost = 1;
                }

                // Our timeout is 120 seconds but let's try to ensure we can fulfill the request in 30 seconds
                var objsInThirty = 30000 / (peritemcost + 1);

                if (!this._Configuration.BigBundles && objsInThirty > 5_000)
                {
                    return 2_500;
                }
                else
                {
                    return (int)objsInThirty;
                }

            }
            return takecount;
        }

        /// <summary>
        /// Existing query log file entry for query continuation is valid
        /// </summary>
        /// <param name="query">The query which is to be validated</param>
        /// <returns>True if the query in the cache is still valid and should be continued</returns>
        private bool IsExistingQueryLogValid(ISynchronizationLogQuery query)
        {
            if (null == query)
            {
                return false;
            }
            return DateTime.UtcNow.Subtract(query.QueryStartTime).TotalHours <= 1;
        }

        /// <summary>
        /// Get the subscribed objects in this dCDR which have been "subscribed" to
        /// </summary>
        /// <returns>The subscribed objects</returns>
        private IEnumerable GetSubscribedObjects()
        {
            // We actually have subscribed objects 
            if (this._Configuration.Mode == SynchronizationMode.Partial)
            {
                // Attempt to load the definition from the local database first (note: there will be no local if we haven't sync'd yet)
                var subscribedToType = this._Configuration.SubscribeToResource.Type;
                var upstreamEndpoint = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(subscribedToType);
                var persistenceService = this.GetPersistenceService(subscribedToType);
                var queryToExecute = new NameValueCollection();
                queryToExecute.Add("id", this._Configuration.SubscribedObjects.Select(o => o.ToString()).ToArray());
                var expression = QueryExpressionParser.BuildLinqExpression(subscribedToType, queryToExecute);
                var results = persistenceService.Query(expression);

                // Any local results?
                if (!results.Any() && base.IsUpstreamAvailable(upstreamEndpoint))
                {
                    try
                    {
                        using (var client = base.CreateRestClient(upstreamEndpoint, AuthenticationContext.SystemPrincipal))
                        {
                            return client.Get<Bundle>($"/{subscribedToType.GetSerializationName()}", queryToExecute).Item;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new UpstreamIntegrationException(this._LocalizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(SubscriptionDefinition) }), e);
                    }
                }
                else
                {
                    return results;
                }
            }
            else
            {
                return null; // There are no subscribed objects - we are a full synchronization
            }
        }

        /// <summary>
        /// Gets a subset of the <paramref name="subscribedObjects"/> that match any guard conditions present in the <paramref name="definition"/>.
        /// </summary>
        /// <param name="definition">The definition to look for guards with.</param>
        /// <param name="subscribedObjects">A list of objects that are part of the subscription.</param>
        /// <returns>A list of the objects from <paramref name="subscribedObjects"/> that meet any guard conditions present in the definition.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        private List<object> GetSubscribedtObjectsApplyingGuards(SubscriptionClientDefinition definition, List<object> subscribedObjects)
        {
            if (null == definition)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            else if (subscribedObjects == null)
            {
                return null;
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


        /// <summary>
        /// Process the subscription by filling in the subscription parameters in
        /// </summary>
        private void ProcessSubscriptionClientDefinition(SubscriptionClientDefinition clientDefinition, List<object> subscribedObjects, float progressIndicator)
        {
            if (clientDefinition?.Filters?.Count > 0)
            {
                foreach (var filter in clientDefinition.Filters)
                {
                    if (filter.IndexOf("$subscribed") > -1)
                    {
                        foreach (var subscribedobject in subscribedObjects)
                        {
                            var expr = QueryExpressionParser.BuildLinqExpression(clientDefinition.ResourceType, NameValueCollectionExtensions.ParseQueryString(filter), "o", new Dictionary<string, Func<object>>
                            {
                                { "subscribed", () => subscribedobject }
                            }, relayControlVariables: true, lazyExpandVariables: true);

                            var newfilter = QueryExpressionBuilder.BuildQuery(clientDefinition.ResourceType, expr).ToHttpString();
                            this.PullInternal(clientDefinition.ResourceType, newfilter.ParseQueryString(), clientDefinition.IgnoreModifiedOn, progressIndicator);
                        }
                    }
                    else
                    {
                        this.PullInternal(clientDefinition.ResourceType, filter.ParseQueryString(), clientDefinition.IgnoreModifiedOn, progressIndicator);
                    }
                }
            }
            else
            {
                this.PullInternal(clientDefinition.ResourceType, null, false, progressIndicator);
            }
        }

        /// <summary>
        /// Perform the pulling of data 
        /// </summary>
        private void PullInternal(Type modelType, NameValueCollection filter, bool ignoreModifiedOn, float progressIndicator)
        {
            //always ignores If-Modified-Since
            var filterstring = filter?.ToHttpString();

            //TODO: Fetch items from upstream
            var lastmodificationdate = !ignoreModifiedOn ? _SynchronizationLogService.GetLastTime(modelType, filterstring) : (DateTime?)null;

            var estimatedlatency = this.UpstreamAvailabilityProvider.GetUpstreamLatency(ServiceEndpointType.HealthDataService);

            if (!estimatedlatency.HasValue) //Unavailable
            {
                _Tracer.TraceInfo("Upstream is unavialable. Exiting Pull.");
                return;
            }

            _Tracer.TraceVerbose("Estimated upstream latency: {0}", estimatedlatency);

            //If we are using last modified, check what the time drift is. 
            if (null != lastmodificationdate)
            {
                var drift = GetUpstreamDrift(ServiceEndpointType.HealthDataService);

                _Tracer.TraceVerbose("Upstream time difference: {0}", drift);

                lastmodificationdate = lastmodificationdate.Value.Add(drift);
            }

            // Query control options which impact how the pulling is done from the upstream
            var lastEtag = String.Empty;
            var queryControlOptions = new UpstreamIntegrationQueryControlOptions()
            {
                IfModifiedSince = lastmodificationdate,
                Count = _Configuration.BigBundles ? 1000 : 100,
                Offset = 0,
                QueryId = Guid.NewGuid(),
                Timeout = 120_000
            };

            // Attempt to find an existing log of sync in the local database (for continuing queries)
            var existingquery = _SynchronizationLogService.FindQueryData(modelType, filterstring);
            if (IsExistingQueryLogValid(existingquery))
            {
                queryControlOptions.QueryId = existingquery.QueryId;
                queryControlOptions.Offset = existingquery.QueryOffset;
            }
            else
            {
                _SynchronizationLogService.CompleteQuery(existingquery);
                // Mark the start of our querying
                _SynchronizationLogService.SaveQuery(modelType, filterstring, queryControlOptions.QueryId, 0);
            }

            var inboundqueue = _QueueManager.GetInboundQueue();
            if (null == inboundqueue)
            {
                _Tracer.TraceError("No inbound queue is available.");
                throw new NotSupportedException("No inbound queue available.");
            }

            var filterExpression = QueryExpressionParser.BuildLinqExpression(modelType, filter, "o", relayControlVariables: true);

            try
            {
                // Continue to query until there are no further results 
                IResourceCollection result = null;
                var sw = new Stopwatch();
                do
                {

                    sw.Restart();
                    result = this.UpstreamIntegrationService.Query(modelType, filterExpression, queryControlOptions);
                    sw.Stop();

                    this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(UpstreamSynchronizationService), progressIndicator, this._LocalizationService.GetString(UserMessageStrings.SYNC_PULL_STATE, new { resource = modelType.GetSerializationName(), count = queryControlOptions.Offset })));

                    if (result == null)
                    {
                        break; // no result indicating a 304
                    }
                    if (!(result is Bundle bdl))
                    {
                        bdl = new Bundle(result.Item.OfType<IdentifiedData>());
                    }

                    if (bdl.Item.Any())
                    {
                        // Provenance objects from the upstream don't apply here since they're used for tracking only
                        bdl = this.StripUpstreamMetadata(bdl);

                        // We want to set the last e-tag not of any included objects but only of the focal object (the original thing we queried for and not any of the supporting objects)
                        queryControlOptions.Count = ScaleTakeCount(bdl.Count, sw.ElapsedMilliseconds, estimatedlatency);
                        queryControlOptions.Offset += bdl.Count;

                        lastEtag = bdl.GetFocalItems().FirstOrDefault()?.Tag ?? lastEtag;
                        inboundqueue.Enqueue(bdl, SynchronizationQueueEntryOperation.Sync);
                        _SynchronizationLogService.SaveQuery(modelType, filterstring, queryControlOptions.QueryId, queryControlOptions.Offset);
                    }
                } while (result.Item.Any());

                this._SynchronizationLogService.Save(modelType, filterstring, lastEtag, DateTime.Now);

            }
            catch (Exception e)
            {
                this._SynchronizationLogService.SaveError(modelType, filterstring, e);
                this._Tracer.TraceError("Error Synchronizing {0}?{1} - {2}", modelType, filterstring, e.ToHumanReadableString());
            }


        }

        /// <summary>
        /// Strips upstream metadata objects which may appear in the bundle and ensures that the data in the bundle is 
        /// suitable for moving to this machine
        /// </summary>
        private Bundle StripUpstreamMetadata(Bundle bdl)
        {
            bdl.Item.RemoveAll(itm => itm is SecurityProvenance || itm is SecurityUser || itm is SecurityRole || itm is SecurityDevice || itm is SecurityApplication);
            bdl.Item.ForEach(itm =>
            {
                if (itm is IVersionedData ver) // Versioned data - we don't download old versions of data we only replicate current - so we need to set appropriate parameters
                {
                    ver.IsHeadVersion = true;
                    ver.PreviousVersionKey = null;
                    ver.VersionSequence = null;
                }
                itm.AddAnnotation(SystemTagNames.UpstreamDataTag);
                if (itm is ITaggable taggable)
                {
                    taggable.AddTag(SystemTagNames.UpstreamDataTag, "true");
                }
            });
            return bdl;
        }

        /// <inheritdoc />
        public void Pull(SubscriptionTriggerType trigger)
        {
            var subscriptions = GetSubscriptionDefinitions().ToArray();
            var subscribedobjects = GetSubscribedObjects()?.OfType<Object>().ToList();

            var s = 0;
            float progressPerSubscription = 1.0f / subscriptions.Length;
            foreach (var subscription in subscriptions)
            {
                var progress = (float)s++ / subscriptions.Length;
                var applicableClientDefinitions = subscription.ClientDefinitions.Where(cd => (cd.Trigger & trigger) == trigger && ((int)cd.Mode & (int)this._Configuration.Mode) == (int)this._Configuration.Mode).ToArray();
                this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(UpstreamSynchronizationService), progress, this._LocalizationService.GetString(UserMessageStrings.SYNC_PULL, new { resource = this._LocalizationService.GetString(subscription.Name) })));
                var d = 0;
                foreach (var def in applicableClientDefinitions)
                {
                    try
                    {
                        // We keep the reported progress for firing the progress indicator and because we want to inform the user of the progress of data as the pull is happening
                        progress = (float)d++ / applicableClientDefinitions.Length * progressPerSubscription + progressPerSubscription * (s - 1);
                        this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(UpstreamSynchronizationService), progress, this._LocalizationService.GetString(UserMessageStrings.SYNC_PULL, new { resource = this._LocalizationService.GetString(def.Name) })));
                        _Tracer.TraceInfo("Processing definition {0}, subscription {1}.", def.Name, subscription.Uuid);
                        var objectstoevaluate = GetSubscribedtObjectsApplyingGuards(def, subscribedobjects);
                        this.ProcessSubscriptionClientDefinition(def, objectstoevaluate, progress);
                    }
                    catch (Exception e)
                    {
                        this._Tracer.TraceError("Error executing subscription definition for {0} - {1}", def.Resource, e.ToHumanReadableString());
                    }
                }
            }

        }

        /// <inheritdoc />
        public void Pull(Type modelType)
            => Pull(modelType, null, false);

        /// <inheritdoc />
        public void Pull(Type modelType, NameValueCollection filter)
            => Pull(modelType, filter, false);

        /// <inheritdoc />
        public void Pull(Type modelType, NameValueCollection filter, bool ignoreModifiedOn)
        {
            this.PullInternal(modelType, filter, ignoreModifiedOn, 0.0f);
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

                try
                {
                    var job = _ServiceManager.CreateInjected<UpstreamSynchronizationJob>();
                    
                    _JobManager.AddJob(job, JobStartType.DelayStart);
                    _JobManager.SetJobSchedule(job, _Configuration.PollInterval);
                    // Background the initial pull so we don't block startup
                    _ThreadPool.QueueUserWorkItem(_ => this.RunInboundMessagePump()); // Process anything in the inbox
                    _ThreadPool.QueueUserWorkItem(_ => this.Pull(SubscriptionTriggerType.OnStart));

                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    _Tracer.TraceError("Error Adding Upstream Sync Job: {0}", ex);
                }

            }

            // Subscribe to the inbound queues and run the inbound message pump whenever there is data enqueued
            foreach (var itm in _QueueManager.GetAll(SynchronizationPattern.UpstreamToLocal))
            {
                itm.Enqueued += (o, e) => this._ThreadPool.QueueUserWorkItem(_ => this.RunInboundMessagePump());
            }
            foreach (var itm in _QueueManager.GetAll(SynchronizationPattern.LocalToUpstream))
            {
                itm.Enqueued += (o, e) => this._ThreadPool.QueueUserWorkItem(_ => this.RunOutboundMessagePump());
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

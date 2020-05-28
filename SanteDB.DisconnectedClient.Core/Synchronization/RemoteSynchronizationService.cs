/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace SanteDB.DisconnectedClient.Synchronization
{
    /// <summary>
    /// Represents a synchronization service which can query the HDSI and place 
    /// entries onto the inbound queue
    /// </summary>
    public class RemoteSynchronizationService : ISynchronizationService, IDaemonService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Remote Data Synchronization Service";

        // Error tickle has been rai
        private bool m_errorTickle = false;
        // Lock
        private object m_lock = new object();
        // Get the tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteSynchronizationService));
        // The configuration for synchronization
        private SynchronizationConfigurationSection m_configuration;
        // Thread pool
        private IThreadPoolService m_threadPool;
        // Network service
        private IClinicalIntegrationService m_integrationService;
        // Network information service
        private INetworkInformationService m_networkInfoService;

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the service has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when the service ahs stopped
        /// </summary>
        public event EventHandler Stopped;
        /// <summary>
        /// Pull has completed
        /// </summary>
        public event EventHandler<SynchronizationEventArgs> PullCompleted;

        /// <summary>
        /// Returns true if the service is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets whether the object is synchronizing
        /// </summary>
        public bool IsSynchronizing
        {
            get; private set;
        }

        /// <summary>
        /// Get the log entries
        /// </summary>
        public List<ISynchronizationLogEntry> Log => ApplicationContext.Current.GetService<ISynchronizationLogService>().GetAll();

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            // Get configuration
            this.m_configuration = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>();
            this.m_threadPool = ApplicationContext.Current.GetService<IThreadPoolService>();
            this.m_integrationService = ApplicationContext.Current.GetService<IClinicalIntegrationService>();
            this.m_networkInfoService = ApplicationContext.Current.GetService<INetworkInformationService>();

            this.m_networkInfoService.NetworkStatusChanged += (o, e) => this.Pull(SynchronizationPullTriggerType.OnNetworkChange);

            this.m_tracer.TraceInfo("Performing OnStart trigger pull...");
            this.Pull(SynchronizationPullTriggerType.OnStart);

            // Polling
            if (this.m_configuration.SynchronizationResources.Any(o => (o.Triggers & SynchronizationPullTriggerType.PeriodicPoll) != 0) &&
                this.m_configuration.PollInterval != default(TimeSpan))
            {
                Action<Object> pollFn = null;
                pollFn = _ =>
                {
                    this.Pull(SynchronizationPullTriggerType.PeriodicPoll);
                    ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(this.m_configuration.PollInterval, pollFn, null);

                };
                ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(this.m_configuration.PollInterval, pollFn, null);
            }
            this.Started?.Invoke(this, EventArgs.Empty);

            return true;

        }

        /// <summary>
        /// Forces a push and blocks until all data is pushed
        /// </summary>
        public void Push()
        {
            var qmService = ApplicationContext.Current.GetService<IQueueManagerService>();
            if (!qmService.IsBusy && !this.IsSynchronizing)
            {
                ManualResetEventSlim waitHandle = new ManualResetEventSlim(false);

                // Wait for outbound queue to finish
                EventHandler<QueueExhaustedEventArgs> exhaustCallback = (o, e) =>
                {
                    if (e.Queue == "outbound")
                        waitHandle.Set();
                };

                qmService.QueueExhausted += exhaustCallback;
                qmService.ExhaustOutboundQueues();
                waitHandle.Wait();
                waitHandle.Reset();
                qmService.QueueExhausted -= exhaustCallback;
            }
        }

        /// <summary>
        /// Pull from remote
        /// </summary>
        private void Pull(SynchronizationPullTriggerType trigger)
        {
            // Pool startup sync if configured..
            this.m_threadPool.QueueUserWorkItem((state) =>
            {

                var logSvc = ApplicationContext.Current.GetService<ISynchronizationLogService>();
                bool initialSync = !logSvc.GetAll().Any();

                if (Monitor.TryEnter(this.m_lock, 100)) // Do we have a lock?
                {
                    try
                    {
                        this.IsSynchronizing = true;

                        DateTime lastSync = DateTime.MinValue;
                        if (logSvc.GetAll().Count() > 0)
                            lastSync = logSvc.GetAll().Min(o => o.LastSync);

                        // Trigger
                        if (!this.m_integrationService.IsAvailable())
                        {
                            if (trigger == SynchronizationPullTriggerType.OnStart)
                                ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(new TimeSpan(0, 5, 0), o => this.Pull(SynchronizationPullTriggerType.OnStart), null);
                            return;
                        }

                        int totalResults = 0;
                        var syncTargets = this.m_configuration.SynchronizationResources.Where(o => (o.Triggers & trigger) != 0).ToList();
                        for (var i = 0; i < syncTargets.Count; i++)
                        {

                            var syncResource = syncTargets[i];

                            ApplicationContext.Current.SetProgress(String.Format(Strings.locale_startingPoll, syncResource.Name), (float)i / syncTargets.Count);
                            foreach (var fltr in syncResource.Filters)
                                totalResults += this.Pull(syncResource.ResourceType, NameValueCollection.ParseQueryString(fltr), syncResource.Always, syncResource.Name);
                            if (syncResource.Filters.Count == 0)
                                totalResults += this.Pull(syncResource.ResourceType, new NameValueCollection(), false, syncResource.Name);

                        }
                        ApplicationContext.Current.SetProgress(Strings.locale_startingPoll, 1.0f);

                        // Pull complete?
                        this.IsSynchronizing = false;

                        if (totalResults > 0 && initialSync)
                        {
                            var tickleService = ApplicationContext.Current.GetService<ITickleService>();
                            tickleService.SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Information, Strings.locale_importDoneBody, new DateTime().AddMinutes(5)));
                            this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(true, totalResults, lastSync));
                        }
                        else if (totalResults > 0)
                            this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(totalResults, lastSync));
                        else
                            this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(0, lastSync));

                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Cannot process startup command: {0}", e);
                    }
                    finally
                    {
                        this.IsSynchronizing = false;

                        Monitor.Exit(this.m_lock);
                    }
                }
                else
                    this.m_tracer.TraceWarning("Will not execute {0} due to - already pulling", trigger);
            });

        }

        /// <summary>
        /// Perform a fetch operation which performs a head
        /// </summary>
        public bool Fetch(Type modelType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Perform a pull on the root resource
        /// </summary>
        public int Pull(Type modelType)
        {
            return this.Pull(modelType, new NameValueCollection(), false);

        }

        /// <summary>
        /// Pull the model according to a filter
        /// </summary>
        public int Pull(Type modelType, NameValueCollection filter)
        {
            return this.Pull(modelType, filter, false);
        }

        /// <summary>
        /// Pull with always filter
        /// </summary>
        public int Pull(Type modelType, NameValueCollection filter, bool always)
        {
            return this.Pull(modelType, filter, always, null);
        }

        /// <summary>
        /// Internal pull function
        /// </summary>
        public int Pull(Type modelType, NameValueCollection filter, bool always, String name)
        {
            lock (this.m_lock)
            {
                var logSvc = ApplicationContext.Current.GetService<ISynchronizationLogService>();

                var lastModificationDate = logSvc.GetLastTime(modelType, filter.ToString());
                if (always)
                    lastModificationDate = null;
                if (lastModificationDate != null)
                    lastModificationDate = lastModificationDate.Value;

                // Performance timer for more intelligent query control
                Stopwatch perfTimer = new Stopwatch();
                EventHandler<RestResponseEventArgs> respondingHandler = (o, e) => perfTimer.Stop();
                this.m_integrationService.Responding += respondingHandler;
                try
                {

                    this.m_tracer.TraceInfo("Start synchronization on {0} (filter:{1})...", modelType, filter);

                    // Get last modified date
                    this.m_tracer.TraceVerbose("Synchronize all on {0} since {1}", modelType, lastModificationDate);

                    var result = new Bundle() { TotalResults = 1 };
                    var eTag = String.Empty;
                    var retVal = 0;
                    int count = 100;
                    var qid = Guid.NewGuid();

                    // Attempt to find an existing query
                    var existingQuery = logSvc.FindQueryData(modelType, filter.ToString());
                    if (existingQuery != null && DateTime.Now.ToUniversalTime().Subtract(existingQuery.StartTime).TotalHours <= 1)
                    {
                        qid = existingQuery.Uuid;
                        result.Count = existingQuery.LastSuccess;
                        result.TotalResults = result.Count + 1;
                    }
                    else
                    {
                        if (existingQuery != null) logSvc.CompleteQuery(existingQuery.Uuid);
                        logSvc.SaveQuery(modelType, filter.ToString(), qid, name, 0);
                    }

                    DateTime startTime = DateTime.Now;

                    // Enqueue
                    for (int i = result.Count; i < result.TotalResults; i += result.Count)
                    {
                        float perc = i / (float)result.TotalResults;

                        if (result.TotalResults > result.Offset + result.Count + 1)
                            ApplicationContext.Current.SetProgress(String.Format(Strings.locale_sync, modelType.Name, i, result.TotalResults), perc);
                        NameValueCollection infopt = null;
                        if (filter.Any(o => o.Key.StartsWith("_")))
                        {
                            infopt = new NameValueCollection();
                            foreach (var itm in filter.Where(o => o.Key.StartsWith("_")))
                                infopt.Add(itm.Key, itm.Value);
                        }

                        perfTimer.Reset();
                        perfTimer.Start();

                        result = this.m_integrationService.Find(modelType, filter, i, count, new IntegrationQueryOptions() { IfModifiedSince = lastModificationDate, Timeout = 120000, Lean = true, InfrastructureOptions = infopt, QueryId = qid });

                        // Queue the act of queueing
                        if (result != null)
                        {
                            
                            if (count == 5000 && perfTimer.ElapsedMilliseconds < 40000 ||
                                count < 5000 && result.TotalResults > 20000 && perfTimer.ElapsedMilliseconds < 40000)
                                count = 5000;
                            else if (count == 2500 && perfTimer.ElapsedMilliseconds < 30000 ||
                                count < 2500 && result.TotalResults > 10000 && perfTimer.ElapsedMilliseconds < 30000)
                                count = 2500;
                            else if (count == 1000 && perfTimer.ElapsedMilliseconds < 20000 ||
                                count < 1000 && result.TotalResults > 5000 && perfTimer.ElapsedMilliseconds < 20000)
                                count = 1000;
                            else if (count == 200 && perfTimer.ElapsedMilliseconds < 10000 ||
                                count < 500 && result.TotalResults > 1000 && perfTimer.ElapsedMilliseconds < 10000)
                                count = 500;
                            else
                                count = 100;

                            this.m_tracer.TraceVerbose("Download {0} ({1}..{2}/{3})", modelType.FullName, i, i + result.Count, result.TotalResults);

                            result.Item.RemoveAll(o => o is SecurityUser || o is SecurityRole || o is SecurityPolicy);
                            ApplicationContext.Current.GetService<IQueueManagerService>().Inbound.Enqueue(result, SynchronizationOperationType.Sync);
                            logSvc.SaveQuery(modelType, filter.ToString(), qid, name, result.Offset + result.Count);

                            retVal = result.TotalResults;
                        }
                        else
                            break;

                        if (String.IsNullOrEmpty(eTag))
                            eTag = result?.Item.FirstOrDefault()?.Tag;

                        if (result.Count == 0) break;
                    }

                    if (result?.TotalResults > result?.Count)
                        ApplicationContext.Current.SetProgress(String.Format(Strings.locale_sync, modelType.Name, result.TotalResults, result.TotalResults), 1.0f);

                    // Log that we synchronized successfully
                    logSvc.Save(modelType, filter.ToString(), eTag, name, startTime);

                    // Clear the query
                    logSvc.CompleteQuery(qid);

                    // Fire the pull event
                    this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(modelType, filter, lastModificationDate.GetValueOrDefault(), retVal));
                    this.m_errorTickle = false;

                    return retVal;
                }
                catch (TargetInvocationException ex)
                {
                    var e = ex.InnerException;
                    this.m_tracer.TraceError("Error synchronizing {0} : {1} ", modelType, e);
                    if (!this.m_errorTickle)
                    {
                        var tickleService = ApplicationContext.Current.GetService<ITickleService>();
                        tickleService.SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format($"{Strings.locale_downloadError}: {Strings.locale_downloadErrorBody}", modelType.Name)));
                    }
                    this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(modelType, filter, lastModificationDate.GetValueOrDefault(), 0));

                    return 0;

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error synchronizing {0} : {1} ", modelType, e);

                    if (!this.m_errorTickle)
                    {
                        var tickleService = ApplicationContext.Current.GetService<ITickleService>();
                        tickleService.SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format($"{Strings.locale_downloadError}: {Strings.locale_downloadErrorBody}", modelType.Name)));
                        this.m_errorTickle = true;
                    }
                    this.PullCompleted?.Invoke(this, new SynchronizationEventArgs(modelType, filter, lastModificationDate.GetValueOrDefault(), 0));

                    return 0;
                }
                finally
                {
                    this.m_integrationService.Responding -= respondingHandler;
                }
            }
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

/*
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
 * Date: 2020-5-1
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Synchronization;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Synchronization.Model;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SanteDB.Core;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Auditing;

namespace SanteDB.DisconnectedClient.SQLite.Synchronization
{

    /// <summary>
    /// Queue manager daemon
    /// </summary>
    public class SQLiteQueueManagerService : IQueueManagerService, IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SQLite Queue Manager";

        // Error tickle has been rai
        private bool m_errorTickle = false;
        // Template correction keys
        private Dictionary<String, Guid> m_templateCorrection = new Dictionary<string, Guid>();
        private Object m_inboundLock = new object();
        private Object m_outboundLock = new object();
        private Object m_adminLock = new object();

        private IThreadPoolService m_threadPool = null;

        /// <summary>
        /// Queue has been exhuasted
        /// </summary>
        public event EventHandler<QueueExhaustedEventArgs> QueueExhausted;

        // Queue manager 
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteQueueManagerService));
        public event EventHandler Started;

        /// <summary>
        /// Events surrounding the daemon
        /// </summary>
        public event EventHandler Starting;
        public event EventHandler Stopped;

        public event EventHandler Stopping;

        /// <summary>
        /// Returns true if the service is running
        /// </summary>
        public bool IsRunning => true;

        /// <summary>
        /// True if synchronization is occurring
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return Monitor.IsEntered(this.m_inboundLock) || Monitor.IsEntered(this.m_outboundLock);
            }
        }

        /// <summary>
        /// Administrative queue
        /// </summary>
        public ISynchronizationQueue Admin => SynchronizationQueue.Admin;

        /// <summary>
        /// Gets the outbound queue
        /// </summary>
        public ISynchronizationQueue Outbound => SynchronizationQueue.Outbound;

        /// <summary>
        /// Gets the inbound queue
        /// </summary>
        public ISynchronizationQueue Inbound => SynchronizationQueue.Inbound;

        /// <summary>
        /// Gets the deadletter queue
        /// </summary>
        public ISynchronizationQueue DeadLetter => SynchronizationQueue.DeadLetter;

        /// <summary>
        /// Exhausts the inbound queue
        /// </summary>
        public void ExhaustInboundQueue()
        {

            bool locked = false;
            try
            {
                AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.SystemPrincipal);
                locked = Monitor.TryEnter(this.m_inboundLock, 100);
                if (!locked) return;

                // Exhaust the queue
                int remain = SynchronizationQueue.Inbound.Count();
                int maxTotal = 0;
                var remote = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>()?.Domain;

                InboundQueueEntry nextPeek = null;
                IdentifiedData nextDpe = null;

                while (remain > 0)
                {
                    InboundQueueEntry queueEntry = null;

                    try
                    {
                        if (remain > maxTotal)
                            maxTotal = remain;

                        if (maxTotal > 5)
                            ApplicationContext.Current.SetProgress(String.Format("{0} - [{1}]", Strings.locale_import, remain), (maxTotal - remain) / (float)maxTotal);

#if PERFMON
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
#endif
                        IdentifiedData dpe = null;
                        if (nextPeek != null) // Was this loaded before? {
                        {
                            queueEntry = nextPeek;
                            dpe = nextDpe;
                            nextPeek = SynchronizationQueue.Inbound.PeekRaw(1);
                        }
                        else
                        {
                            queueEntry = SynchronizationQueue.Inbound.PeekRaw();
                            dpe = SynchronizationQueue.Inbound.DeserializeObject(queueEntry);
                            nextPeek = SynchronizationQueue.Inbound.PeekRaw(1);
                        }

                        // Try to peek off the next queue item while we're doing something else
                        Task<IdentifiedData> nextPeekTask = null;
                        if (nextPeek != null)
                            nextPeekTask = Task<IdentifiedData>.Run(() => SynchronizationQueue.Inbound.DeserializeObject(nextPeek));

#if PERFMON
                        sw.Stop();
                        ApplicationContext.Current.PerformanceLog(nameof(QueueManagerService), nameof(ExhaustInboundQueue), "DeQueue", sw.Elapsed);
                        sw.Reset();
                        sw.Start();
#endif


                        //(dpe as SanteDB.Core.Model.Collection.Bundle)?.Reconstitute();
                        var bundle = dpe as Bundle;
                        dpe = bundle?.Entry ?? dpe;

                        try
                        {
                            //if (bundle?.Item.Count > 1000)
                            //{
                            //    var ofs = 0;
                            //    while (ofs < bundle.Item.Count)
                            //    {
                            //        this.ImportElement(new Bundle()
                            //        {
                            //            Item = bundle.Item.Skip(ofs).Take(500).ToList()
                            //        });
                            //        ofs += 500;
                            //    }
                            //}
                            //else
                            this.ImportElement(dpe);

                            AuditUtil.AuditSynchronization(AuditableObjectLifecycle.Import, remote, OutcomeIndicator.Success, dpe);
                        }
                        catch (Exception e)
                        {
                            try
                            {
                                this.m_tracer.TraceError("Error processing inbound queue entry: {0}", e);
                                //this.CreateUserAlert(Strings.locale_importErrorSubject, Strings.locale_importErrorBody, )
                                SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(queueEntry, Encoding.UTF8.GetBytes(e.ToString())) { OriginalQueue = "inbound" });
                                AuditUtil.AuditSynchronization(AuditableObjectLifecycle.Import, remote, OutcomeIndicator.MinorFail, dpe);

                            }
                            catch (Exception e2)
                            {
                                this.m_tracer.TraceEvent(System.Diagnostics.Tracing.EventLevel.Critical, "Error putting dead item on deadletter queue: {0}", e);
                                throw;
                            }
                        }
                        finally
                        {
                            SynchronizationQueue.Inbound.Delete(queueEntry.Id);
                        }

                        this.QueueExhausted?.Invoke(this, new QueueExhaustedEventArgs("inbound", bundle?.Item.AsParallel().Select(o => o.Key.Value).ToArray() ?? new Guid[] { dpe.Key.Value }));

#if PERFMON
                        sw.Stop();
                        ApplicationContext.Current.PerformanceLog(nameof(QueueManagerService), nameof(ExhaustInboundQueue), "ImportComplete", sw.Elapsed);
                        sw.Reset();
#endif
                        nextPeekTask?.Wait();
                        nextDpe = nextPeekTask?.Result;

                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceError("Error processing inbound queue entry: {0}", e);
                    }
                    remain = SynchronizationQueue.Inbound.Count();

                }

                if (maxTotal > 5)
                    ApplicationContext.Current.SetProgress(String.Format(Strings.locale_import, String.Empty, String.Empty), 1.0f);

            }
            finally
            {
                if (locked) Monitor.Exit(this.m_inboundLock);
            }
        }

        /// <summary>
        /// Exhaust the administrative queue
        /// </summary>
        public void ExhaustAdminQueue()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(this.m_adminLock, 100);
                if (!locked) return;
                // TODO: Sleep thread here
                var amiService = SanteDB.DisconnectedClient.ApplicationContext.Current.GetService<IAdministrationIntegrationService>();
                if (!amiService.IsAvailable())
                {
                    return;
                }

                // Exhaust the queue
                while (SynchronizationQueue.Admin.Count() > 0)
                {


                    // Exhaust the outbound queue
                    // Is there more than one item on the queue?
                    var syncItm = SynchronizationQueue.Admin.PeekRaw();
                    var dpe = SynchronizationQueue.Admin.DeserializeObject(syncItm);

                    // try to send
                    try
                    {
                        // Send the object to the remote host
                        switch (syncItm.Operation)
                        {
                            case SynchronizationOperationType.Insert:
                                amiService.Insert(dpe);
                                break;
                            case SynchronizationOperationType.Obsolete:
                                amiService.Obsolete(dpe, syncItm.IsRetry);
                                break;
                            case SynchronizationOperationType.Update:
                                amiService.Update(dpe, syncItm.IsRetry);
                                break;
                        }


                        SynchronizationQueue.Admin.Delete(syncItm.Id); // Get rid of object from queue
                    }
                    catch (WebException ex)
                    {
                        this.m_tracer.TraceError("Remote server rejected object: {0}", ex);

                        Exception ie = ex;
                        while (ie != null && (ie as WebException)?.Response == null)
                            ie = ie.InnerException;

                        // Get status
                        var we = ie as WebException;
                        if (we.Status == WebExceptionStatus.ConnectFailure)
                            continue;
                        else if (we?.Response == null )
                        {
                            SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                            SynchronizationQueue.Admin.DequeueRaw(); // Get rid of the last item
                        }
                        else
                        {
                            var resp = we.Response as HttpWebResponse;
                            switch (resp.StatusCode)
                            {
                                case HttpStatusCode.NotFound:
                                    if (resp.Method == "DELETE") // Can't find the thing we're deleting? that is fine :)
                                        SynchronizationQueue.Admin.DequeueRaw(); // Get rid of the last item
                                    else
                                    {
                                        SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                                        SynchronizationQueue.Admin.DequeueRaw(); // Get rid of the last item
                                    }
                                    break;
                                default:
                                    SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                                    SynchronizationQueue.Admin.DequeueRaw(); // Get rid of the last item
                                    break;
                            }
                        }

                        // Construct an alert
                        //this.CreateUserAlert(Strings.locale_rejectionSubject, Strings.locale_rejectionBody, String.Format(Strings.ResourceManager.GetString((ex.Response as HttpWebResponse)?.StatusDescription ?? "locale_syncErrorBody"), ex, dpe), dpe);
                    }
                    catch (TimeoutException ex) // Timeout due to lack of connectivity
                    {

                        this.m_tracer.TraceError("Error sending object {0}: {1}", dpe, ex);

                        syncItm.IsRetry = false;
                        syncItm.RetryCount++;
                        // Re-queue
                        if (syncItm.RetryCount > 90) // TODO: Make this configurable
                        {
                            SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                            SynchronizationQueue.Admin.DequeueRaw(); // Get rid of the last item
                            //this.CreateUserAlert(Strings.locale_syncErrorSubject, Strings.locale_syncErrorBody, ex, dpe);
                        }
                        else
                        {
                            SynchronizationQueue.Admin.UpdateRaw(syncItm);
                        }
                    }
                    catch (SecurityException) { }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error sending object to AMI: {0}", ex);
                        //this.CreateUserAlert(Strings.locale_syncErrorSubject, Strings.locale_syncErrorBody, ex, dpe);
                        SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                        SynchronizationQueue.Admin.DequeueRaw();

                        throw;
                    }
                }
                this.QueueExhausted?.Invoke(this, new QueueExhaustedEventArgs("admin"));

            }
            finally
            {
                if (locked) Monitor.Exit(this.m_adminLock);
            }
        }

        /// <summary>
        /// Map local template to server template key
        /// </summary>
        private Guid MapServerTemplateKey(IIntegrationService integrationService, TemplateDefinition localDefinition)
        {
            // Attempt to correct template definition
            if (!m_templateCorrection.TryGetValue(localDefinition.Mnemonic, out Guid updated))
            {
                var remoteTpl = integrationService.Find<TemplateDefinition>(o => o.Mnemonic == localDefinition.Mnemonic, 0, 1);
                if (!remoteTpl.Item.Any())
                {
                    integrationService.Insert(localDefinition);
                    return localDefinition.Key.Value;
                }
                else
                {
                    updated = remoteTpl.Item.First().Key.Value;
                    m_templateCorrection.Add(localDefinition.Mnemonic, updated);
                    return updated;
                }
            }
            else
                return updated;
        }

        /// <summary>
        /// Exhaust the outbound queue
        /// </summary>
        public void ExhaustOutboundQueue()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(this.m_outboundLock, 100);
                if (!locked) return;
                List<Guid> notifyExportKeys = new List<Guid>();
                // Exhaust the queue
                while (SynchronizationQueue.Outbound.Count() > 0)
                {
                    // Exhaust the outbound queue
                    var integrationService = SanteDB.DisconnectedClient.ApplicationContext.Current.GetService<IClinicalIntegrationService>();
                    var syncItm = SynchronizationQueue.Outbound.PeekRaw();
                    var dpe = SynchronizationQueue.Outbound.DeserializeObject(syncItm);

                    if (!integrationService.IsAvailable())
                    {
                        // Come back in 30 seconds...
                        return;
                    }

                    OutcomeIndicator? outcome = null;
                    // try to send
                    try
                    {

                        // Sync item is a retry, so we want to bundle the dependent objects
                        if (syncItm.IsRetry)
                            dpe = this.BundleDependentObjects(dpe, integrationService);

                        IEnumerable<Guid> objectKeys = null;
                        // Reconstitute bundle
                        if (dpe is Bundle bundle)
                        {
                            objectKeys = bundle.Item.Select(o => o.Key.Value).ToArray();

                            // Now we want to correct any template keys we may have 
                            foreach (var itm in bundle.Item.ToArray())
                            {
                                if (itm is TemplateDefinition)
                                    this.MapServerTemplateKey(integrationService, itm as TemplateDefinition);
                                else if (itm is Entity bEntity && bEntity.TemplateKey.HasValue)
                                    bEntity.TemplateKey = this.MapServerTemplateKey(integrationService, bEntity.LoadProperty<TemplateDefinition>(nameof(Entity.Template)));
                                else if (itm is Act bAct && bAct.TemplateKey.HasValue)
                                    bAct.TemplateKey = this.MapServerTemplateKey(integrationService, bAct.LoadProperty<TemplateDefinition>(nameof(Act.Template)));
                            }
                        }
                        else
                        {
                            objectKeys = new Guid[] { dpe.Key.Value };
                            if (dpe is Entity entity && entity.TemplateKey.HasValue)
                                entity.TemplateKey = this.MapServerTemplateKey(integrationService, entity.LoadProperty<TemplateDefinition>(nameof(entity.Template)));
                            else if (dpe is Act act && act.TemplateKey.HasValue)
                                act.TemplateKey = this.MapServerTemplateKey(integrationService, act.LoadProperty<TemplateDefinition>(nameof(act.Template)));
                        }

                        // Send the object to the remote host
                        switch (syncItm.Operation)
                        {
                            case SynchronizationOperationType.Insert:
                                integrationService.Insert(dpe);
                                break;
                            case SynchronizationOperationType.Obsolete:
                                integrationService.Obsolete(dpe, syncItm.IsRetry);
                                break;
                            case SynchronizationOperationType.Update:
                                integrationService.Update(dpe, syncItm.IsRetry);
                                break;
                        }


                        // operation was successful
                        notifyExportKeys.AddRange(objectKeys);

                        outcome = OutcomeIndicator.Success;
                        SynchronizationQueue.Outbound.Delete(syncItm.Id); // Get rid of object from queue
                    }
                    catch (WebException ex)
                    {
                        Exception ie = ex;
                        while (ie != null && (ie as WebException)?.Response == null)
                            ie = ie.InnerException;

                        // Get status
                        var we = ie as WebException;
                        if (we?.Response == null)
                        {
                            outcome = OutcomeIndicator.MinorFail;
                            SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                            SynchronizationQueue.Outbound.DequeueRaw(); // Get rid of the last item
                        }
                        else
                        {
                            var resp = we.Response as HttpWebResponse;
                            switch (resp.StatusCode)
                            {
                                case HttpStatusCode.NotFound:
                                    if (resp.Method == "DELETE") // Can't find the thing we're deleting? that is fine :)
                                        SynchronizationQueue.Outbound.DequeueRaw(); // Get rid of the last item
                                    else
                                    {
                                        outcome = OutcomeIndicator.MinorFail;
                                        SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                                        SynchronizationQueue.Outbound.DequeueRaw(); // Get rid of the last item
                                    }
                                    break;
                                default:
                                    outcome = OutcomeIndicator.MinorFail;
                                    SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                                    SynchronizationQueue.Outbound.DequeueRaw(); // Get rid of the last item
                                    break;
                            }
                        }

                        // Construct an alert
                        //this.CreateUserAlert(Strings.locale_rejectionSubject, Strings.locale_rejectionBody, String.Format(Strings.ResourceManager.GetString((ex.Response as HttpWebResponse)?.StatusCode.ToString()) ?? Strings.locale_syncErrorBody, ex, dpe), dpe);
                    }
                    catch (TimeoutException ex) // Timeout due to lack of connectivity
                    {

                        this.m_tracer.TraceError("Error sending object {0}: {1}", dpe, ex);

                        syncItm.RetryCount++;

                        // Re-queue
                        if (syncItm.RetryCount > 3) // TODO: Make this configurable
                        {
                            outcome = OutcomeIndicator.MinorFail;
                            SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                            SynchronizationQueue.Outbound.DequeueRaw(); // Get rid of the last item
                            //this.CreateUserAlert(Strings.locale_syncErrorSubject, Strings.locale_syncErrorBody, ex, dpe);
                        }
                        else
                        {
                            SynchronizationQueue.Outbound.UpdateRaw(syncItm);
                        }
                    }
                    catch (SecurityException e)
                    {
                        if (!this.m_errorTickle)
                        {
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.locale_syncUploadError, e.GetType().Name)));
                            this.m_errorTickle = true;
                        }
                        this.m_tracer.TraceError("Error upload data to central server: {0}", e);
                    }
                    catch (ZlibException e)
                    {
                        if (!this.m_errorTickle)
                        {
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.locale_syncUploadError, e.GetType().Name)));
                            this.m_errorTickle = true;
                        }
                        this.m_tracer.TraceError("Error uploading data to central server: {0}", e);
                    }
                    catch (XmlException e)
                    {
                        if (!this.m_errorTickle)
                        {
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.locale_syncUploadError, e.GetType().Name)));
                            this.m_errorTickle = true;
                        }
                        this.m_tracer.TraceError("Error uploading data to central server: {0}", e);
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error sending object to IMS: {0}", ex);
                        //this.CreateUserAlert(Strings.locale_syncErrorSubject, Strings.locale_syncErrorBody, ex, dpe);
                        outcome = OutcomeIndicator.MinorFail;
                        SynchronizationQueue.DeadLetter.EnqueueRaw(new DeadLetterQueueEntry(syncItm, Encoding.UTF8.GetBytes(ex.ToString())));
                        SynchronizationQueue.Outbound.DequeueRaw();
                        if (!this.m_errorTickle)
                        {
                            ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.locale_syncUploadError, ex.GetType().Name)));
                            this.m_errorTickle = true;
                        }

                        throw;
                    }
                    finally
                    {
                        if (outcome.HasValue)
                        {
                            var remote = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>()?.Domain;
                            AuditUtil.AuditSynchronization(AuditableObjectLifecycle.Export, remote, outcome.Value, dpe);
                        }
                    }
                }
                this.QueueExhausted?.Invoke(this, new QueueExhaustedEventArgs("outbound", notifyExportKeys.ToArray()));

            }
            finally
            {
                if (locked) Monitor.Exit(this.m_outboundLock);
            }
        }

        /// <summary>
        /// Bundle dependent objects for resubmit
        /// </summary>
        private IdentifiedData BundleDependentObjects(IdentifiedData data, IClinicalIntegrationService integrationService, Bundle currentBundle = null)
        {
            // Bundle establishment
            currentBundle = currentBundle ?? new Bundle();
            if (data is Bundle dataBundle)
                currentBundle.Item.AddRange(dataBundle.Item);

            if (data is Person entity) // Fix entity key
            {
                foreach (var rel in entity.Relationships)
                    if (!currentBundle.Item.Any(i => i.Key == rel.TargetEntityKey) && !integrationService.Exists<Entity>(rel.TargetEntityKey.Value))
                    {
                        var loaded = rel.LoadProperty<Entity>(nameof(EntityRelationship.TargetEntity));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, integrationService, currentBundle); // cascade load
                    }

            }
            else if (data is Act act)
            {
                foreach (var rel in act.Relationships)
                    if (!currentBundle.Item.Any(i => i.Key == rel.TargetActKey) && !integrationService.Exists<Act>(rel.TargetActKey.Value))
                    {
                        var loaded = rel.LoadProperty<Act>(nameof(ActRelationship.TargetAct));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, integrationService, currentBundle);
                    }
                foreach (var rel in act.Participations)
                    if (!currentBundle.Item.Any(i => i.Key == rel.PlayerEntityKey) && !integrationService.Exists<Entity>(rel.PlayerEntityKey.Value))
                    {
                        var loaded = rel.LoadProperty<Entity>(nameof(ActParticipation.PlayerEntity));
                        currentBundle.Item.Insert(0, loaded);
                        this.BundleDependentObjects(loaded, integrationService, currentBundle);
                    }
            }
            else if (data is Bundle bundle)
            {
                foreach (var itm in bundle.Item.ToArray())
                {
                    this.BundleDependentObjects(itm, integrationService, currentBundle);
                }
            }

            return currentBundle;
        }

        /// <summary>
        /// Import element
        /// </summary>
        private void ImportElement(IdentifiedData data)
        {
            var idpType = typeof(IDataPersistenceService<>).MakeGenericType(data.GetType());
            var svc = SanteDB.DisconnectedClient.ApplicationContext.Current.GetService(idpType) as IDataPersistenceService;
            try
            {
                IdentifiedData existing = null;
                if (!(data is Bundle))
                    existing = svc.Get(data.Key.Value) as IdentifiedData;

                this.m_tracer.TraceVerbose("Inserting object from inbound queue: {0}", data);
                if (existing == null)
                    svc.Insert(data);
                else
                {
                    IVersionedEntity ver = data as IVersionedEntity;
                    if (ver?.VersionKey == (existing as IVersionedEntity)?.VersionKey) // no need to update
                        this.m_tracer.TraceVerbose("Object {0} is already up to date", existing);
                    else
                    {
                        svc.Update(data);
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error inserting object data: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Starts the queue manager service.
        /// </summary>
        /// <returns>Returns true if the service started successfully.</returns>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_threadPool = ApplicationContext.Current.GetService<IThreadPoolService>();

            // Bind to the inbound queue
            SynchronizationQueue.Inbound.Enqueued += (o, e) =>
            {
                // Someone already got this!
                if (Monitor.IsEntered(this.m_inboundLock)) return;
                Action<Object> async = (itm) =>
                {
                    this.ExhaustInboundQueue();
                };
                this.m_threadPool.QueueUserWorkItem(async);
            };

            // Bind to outbound queue
            SynchronizationQueue.Outbound.Enqueued += (o, e) =>
            {
                // Trigger sync?
                if (e.Data.Type.StartsWith(typeof(Patch).FullName) ||
                            e.Data.Type.StartsWith(typeof(Bundle).FullName) ||
                            ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>().SynchronizationResources.
                    Exists(r => r.ResourceType == Type.GetType(e.Data.Type) &&
                            (r.Triggers & SynchronizationPullTriggerType.OnCommit) != 0))
                {
                    Action<Object> async = (itm) =>
                    {
                        this.ExhaustOutboundQueue();
                    };
                    this.m_threadPool.QueueUserWorkItem(async);
                }
            };

            // Bind to administration queue
            SynchronizationQueue.Admin.Enqueued += (o, e) =>
            {
                // Admin is always pushed
                this.m_threadPool.QueueUserWorkItem(a => this.ExhaustAdminQueue());
            };

            // Application started 
            ApplicationContext.Current.Started += (o, e) =>
            {
                // startup
                ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((iar) =>
                {
                    try
                    {
                        this.ExhaustOutboundQueue();
                        this.ExhaustInboundQueue();
                        this.ExhaustAdminQueue();
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error executing initial queues: {0}", ex);
                    }
                }, null);
            };


            // Does the outbound queue have data?
            int dlc = SynchronizationQueue.DeadLetter.Count();
            if (dlc > 0 &&
                ApplicationContext.Current.Confirm(Strings.locale_startupRequeueConfirm))
            {
                int i = 0;
                while (SynchronizationQueue.DeadLetter.Count() > 0)
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_requeueing, ((float)i++) / (float)dlc);

                    var itm = SynchronizationQueue.DeadLetter.PeekRaw();
                    switch (itm.OriginalQueue)
                    {
                        case "inbound":
                        case "inbound_queue":
                            SynchronizationQueue.Inbound.EnqueueRaw(new InboundQueueEntry(itm));
                            break;
                        case "outbound":
                        case "outbound_queue":
                            SynchronizationQueue.Outbound.EnqueueRaw(new OutboundQueueEntry(itm));
                            break;
                        case "admin":
                        case "admin_queue":
                            SynchronizationQueue.Admin.EnqueueRaw(new OutboundAdminQueueEntry(itm));
                            break;
                    }
                    SynchronizationQueue.DeadLetter.Delete(itm.Id);
                }
                this.ExhaustOutboundQueues();
            }

            this.Started?.Invoke(this, EventArgs.Empty);

            return true;
        }


        /// <summary>
        /// Stopping the services
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.Stopped?.Invoke(this, EventArgs.Empty);

            return true;

        }

        /// <summary>
        /// Exhaust the outbound queues
        /// </summary>
        public void ExhaustOutboundQueues()
        {
            this.ExhaustOutboundQueue();
            this.ExhaustAdminQueue();
        }
    }

}

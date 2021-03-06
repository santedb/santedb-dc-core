﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.Warehouse;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace SanteDB.DisconnectedClient.Data.Warehouse
{
    /// <summary>
    /// The protocol watch service is used to watch patient regsitrations and ensure the clinical
    /// protocol is complete
    /// </summary>
    public class LocalCarePlanManagerService : IDaemonService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Care Plan Manager Service";

        // Parameters
        private readonly Dictionary<String, Object> m_parameters = new Dictionary<string, object>()
        {
            { "isBackground", true }
        };

        /// <summary>
        /// Truncate the existing care plan database
        /// </summary>
        public void Truncate()
        {
            try
            {
                if (this.m_actCarePlanPromise.Count == 0)
                {
                    // Deploy schema?
                    this.m_dataMart = this.m_warehouseService.GetDatamart("oizcp");
                    if (this.m_dataMart != null)
                        this.m_warehouseService.DeleteDatamart(this.m_dataMart.Id);

                    this.m_tracer.TraceInfo("Datamart for care plan service doesn't exist, will have to create it...");
                    this.m_dataMart = this.m_warehouseService.CreateDatamart("oizcp", DatamartSchema.Load(typeof(LocalCarePlanManagerService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Data.Warehouse.CarePlan.CarePlanWarehouseSchema.xml")));
                    this.m_tracer.TraceVerbose("Datamart {0} created", this.m_dataMart.Id);
                }
                else
                    throw new InvalidOperationException(Strings.err_already_computing_careplan);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error truncating care plan: {0}", e);
                this.m_resetEvent.Set();
                throw;
            }
        }

        // True when the object is subscribed
        private bool m_isSubscribed = false;

        // Warehouse service
        private IAdHocDatawarehouseService m_warehouseService;

        // m_reset event
        private ManualResetEventSlim m_resetEvent = new ManualResetEventSlim(false);

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalCarePlanManagerService));

        // Represents a promise to perform a care plan
        private readonly List<IdentifiedData> m_actCarePlanPromise = new List<IdentifiedData>();

        // Data mart
        private DatamartDefinition m_dataMart = null;

        // Lock
        private object m_lock = new object();

        // Running state
        private bool m_running = false;

        /// <summary>
        /// True when running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return m_running;
            }
        }

        /// <summary>
        /// Fired when the watch is starting
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the watch is Starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the watch has stopped
        /// </summary>
        public event EventHandler Stopped;
        /// <summary>
        /// Fired when the watch is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Start the daemon service
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_tracer.TraceInfo("Starting care plan manager / warehousing service...");

            // Application context has started
            ApplicationContext.Current.Started += (ao, ae) =>
            ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(xo =>
            {
                try
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_start_careplan, 0);

                    // Warehouse service
                    this.m_warehouseService = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();
                    foreach (var cp in ApplicationContext.Current.GetService<ICarePlanService>().Protocols)
                    {
                        ApplicationContext.Current.SetProgress(String.Format(Strings.locale_starting, cp.Name), 0);
                        this.m_tracer.TraceInfo("Loaded {0}...", cp.Name);
                    }

                    // Deploy schema?
                    this.m_dataMart = this.m_warehouseService.GetDatamart("oizcp");
                    if (this.m_dataMart == null)
                    {
                        this.m_tracer.TraceInfo("Datamart for care plan service doesn't exist, will have to create it...");
                        this.m_dataMart = this.m_warehouseService.CreateDatamart("oizcp", DatamartSchema.Load(typeof(LocalCarePlanManagerService).GetTypeInfo().Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Data.Warehouse.CarePlan.CarePlanWarehouseSchema.xml")));
                        this.m_tracer.TraceVerbose("Datamart {0} created", this.m_dataMart.Id);
                    }
                    //else // prune datamart
                    //{
                    //    this.m_warehouseService.Delete(this.m_dataMart.Id, new
                    //    {
                    //        max_date = String.Format("<=", DateTime.Now.AddDays(-365))
                    //    });
                    //}

                    // Stage 2. Ensure consistency with existing patient dataset
                    var patientPersistence = ApplicationContext.Current.GetService<IStoredQueryDataPersistenceService<Patient>>();
                    var remoteSyncService = ApplicationContext.Current.GetService<ISynchronizationService>();
                    var queueService = ApplicationContext.Current.GetService<IQueueManagerService>();
                    var lastRefresh = DateTime.Parse(ApplicationContext.Current.ConfigurationManager.GetAppSetting("santedb.mobile.core.protocol.plan.lastRun") ?? "0001-01-01");

                    // Should we ?
                    var patientSync = ApplicationContext.Current.GetService<ISynchronizationService>().Log.FirstOrDefault(o => o.ResourceType == "Person");

                    this.RefreshCarePlan(false);
                    remoteSyncService.PullCompleted += (o, e) => { if (!remoteSyncService.IsSynchronizing) this.m_resetEvent.Set(); };
                    // Subscribe to events
                    this.SubscribeEvents();

                    // We're not connected to the in
                    queueService.QueueExhausted += (o, e) =>
                            {
                                int inboundQueueCount = queueService.Inbound.Count();
                                if (e.Queue == "inbound" && !remoteSyncService.IsSynchronizing &&
                                inboundQueueCount == 0)
                                {
                                    Guid queryId = Guid.NewGuid();
                                    int tr = 1, ofs = 0;
                                    queryId = Guid.NewGuid();
                                    tr = 1;
                                    ofs = 0;
                                    this.m_actCarePlanPromise.RemoveAll(i => !(i is Patient));
                                    //this.m_actCarePlanPromise.Clear();
                                    var syncFilter = patientSync?.LastSync ?? new DateTime(1900, 01, 01);
                                    while (ofs < tr)
                                    {
                                        if (tr > 1)
                                            ApplicationContext.Current.SetProgress(Strings.locale_calculateImportedCareplan, ofs / (float)tr);

                                        var prodPatients = patientPersistence.Query(p => p.ObsoletionTime == null && p.StatusConcept.Mnemonic != "OBSOLETE" && p.CreationTime >= syncFilter, queryId, ofs, 15, out tr, AuthenticationContext.SystemPrincipal);
                                        this.QueueWorkItem(prodPatients);
                                        ofs += 15;
                                    }

                                    this.m_isSubscribed = false;
                                    this.SubscribeEvents();
                                }
                                else if (e.Queue == "inbound")
                                {
                                    this.m_tracer.TraceWarning("Cannot execute CP yet because inbound={0} & isSync={1}", inboundQueueCount, queueService.IsBusy);
                                }
                            };


                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not bind clinical protocols: {0}", e);
                }
            });

            this.m_running = true;

            // Polling for the doing of work
            ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((o) =>
            {
                try
                {
                    ISynchronizationService syncService = ApplicationContext.Current.GetService<ISynchronizationService>();

                    while (this.m_running)
                    {
                        this.m_resetEvent.Wait();
                        // de-queue
                        int promiseCount = this.m_actCarePlanPromise.Count;
                        while (!syncService.IsSynchronizing &&
                            ApplicationContext.Current.GetService<IQueueManagerService>().Inbound.Count() == 0 &&
                            this.m_actCarePlanPromise.Count > 0)
                        {
                            if (this.m_actCarePlanPromise.Count > promiseCount)
                                promiseCount = this.m_actCarePlanPromise.Count;

                            ApplicationContext.Current.SetProgress(String.Format(Strings.locale_calculatingCarePlan, this.m_actCarePlanPromise.Count), (promiseCount - this.m_actCarePlanPromise.Count) / (float)promiseCount);
                            IdentifiedData qitm = null;
                            qitm = this.m_actCarePlanPromise.First();
                            if (qitm is Patient)
                            {
                                Patient[] patients = null;
                                // Get all patients
                                lock (this.m_lock)
                                {
                                    patients = this.m_actCarePlanPromise.OfType<Patient>().Take(5).ToArray();
                                    this.m_actCarePlanPromise.RemoveAll(d => patients.Contains(d));
                                }
                                this.UpdateCarePlan(patients);

                                // We can also remove all acts that are for a patient
                                //lock (this.m_lock)
                                //    this.m_actCarePlanPromise.RemoveAll(i => i is Act && (i as Act).Participations.Any(p => p.PlayerEntityKey == qitm.Key));
                            }
                            else if (qitm is Act)
                            {
                                // Get all patients
                                var act = this.m_actCarePlanPromise.OfType<Act>().First();
                                this.UpdateCarePlan(act);

                                //// Remove all acts which are same protocol and same patient
                                lock (this.m_lock)
                                {
                                    this.m_actCarePlanPromise.Remove(act);
                                    this.m_actCarePlanPromise.RemoveAll(i => i is Act && (i as Act).Protocols.Any(p => (qitm as Act).Protocols.Any(q => q.ProtocolKey == p.ProtocolKey)) && (i as Act).Participations.Any(p => p.PlayerEntityKey == (qitm as Act).Participations.FirstOrDefault(c => c.ParticipationRoleKey == ActParticipationKey.RecordTarget || c.ParticipationRole?.Mnemonic == "RecordTarget").PlayerEntityKey));
                                }
                            }

                            // Drop everything else in the queue
                            lock (this.m_lock)
                                this.m_actCarePlanPromise.RemoveAll(i => i.Key == qitm.Key);
                        }

                        if (promiseCount > 0 && this.m_actCarePlanPromise.Count == 0)
                        {
                            ApplicationContext.Current.SetProgress(String.Format(Strings.locale_calculatingCarePlan, 0), 1.0f);
                            ApplicationContext.Current.ConfigurationManager.SetAppSetting("santedb.mobile.core.protocol.plan.lastRun", DateTime.Now.ToString("o"));
                        }

                        this.m_resetEvent.Reset();

                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error polling warehouse service: {0}", e);
                }
            }, null);

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Refresh care plan
        /// </summary>
        public void RefreshCarePlan(bool force)
        {
            if (m_actCarePlanPromise.Count > 0)
            {
                this.m_resetEvent.Set();
                this.m_tracer.TraceWarning("Care planner is already busy aborting...");
                return;
            }
            var patientPersistence = ApplicationContext.Current.GetService<IStoredQueryDataPersistenceService<Patient>>();
            var remoteSyncService = ApplicationContext.Current.GetService<ISynchronizationService>();
            var queueService = ApplicationContext.Current.GetService<IQueueManagerService>();
            var lastRefresh = DateTime.Parse(ApplicationContext.Current.ConfigurationManager.GetAppSetting("santedb.mobile.core.protocol.plan.lastRun") ?? "0001-01-01");

            // Should we ?
            var patientSync = remoteSyncService.Log.FirstOrDefault(o => o.ResourceType == "Person");
            var inboundQueueCount = queueService.Inbound.Count();
            if (patientSync != null &&
                inboundQueueCount == 0 &&
                    (force ||
                        lastRefresh == DateTime.MinValue ||
                        DateTime.Now.Subtract(lastRefresh).TotalDays > 7 && ApplicationContext.Current.Confirm(Strings.locale_refreshCarePlanPrompt)
                    )
                )
            {
                int tr = 1, ofs = 0;
                var warehousePatients = this.m_warehouseService.StoredQuery(this.m_dataMart.Id, "consistency", new { }, 0, -1, out tr);
                Guid queryId = Guid.NewGuid();
                tr = 1;
                while (ofs < tr)
                {
                    ApplicationContext.Current.SetProgress(Strings.locale_refreshCarePlan, ofs / (float)tr);
                    var prodPatients = patientPersistence.Query(o => o.StatusConcept.Mnemonic != "OBSOLETE", queryId, ofs, 15, out tr, AuthenticationContext.SystemPrincipal);
                    ofs += 15;

                    //if (queueService.IsBusy ||
                    //    SynchronizationQueue.Inbound.Count() > 0) break; // bail out , we can do this later

                    foreach (var p in prodPatients.Where(o => !warehousePatients.Any(w => w.patient_id == o.Key)))
                        this.QueueWorkItem(p);
                }
            }
            else
                this.m_tracer.TraceWarning("Care planner does not need to run because iq={0}, lr={1}, f={2}...", inboundQueueCount, lastRefresh, force);

        }

        /// <summary>
        /// Queue work item data
        /// </summary>
        private void QueueWorkItem(object importedData)
        {
            if (importedData is Bundle)
                this.QueueWorkItem((importedData as Bundle)?.Item);
            else if (importedData is Patient || importedData is Act)
                this.QueueWorkItem(importedData as IdentifiedData);
            else if (importedData is IEnumerable)
                this.QueueWorkItem((importedData as IEnumerable).OfType<IdentifiedData>().ToArray());
        }

        /// <summary>
        /// Subscribes to all events
        /// </summary>
        private void SubscribeEvents()
        {
            if (this.m_isSubscribed) return;

            var patientPersistence = ApplicationContext.Current.GetService<IDataPersistenceService<Patient>>();
            if (patientPersistence != null)
            {
                patientPersistence.Inserted += (o, e) => this.QueueWorkItem(e.Data);
                patientPersistence.Updated += (o, e) => this.QueueWorkItem(e.Data);
                patientPersistence.Obsoleted += (o, e) =>
                {
                    var warehouseService = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();
                    //var dataMart = warehouseService.GetDatamart("oizcp");
                    warehouseService.Delete(this.m_dataMart.Id, new { patient_id = e.Data.Key.Value });
                };
            }

            // Subscribe to acts
            var bundlePersistence = ApplicationContext.Current.GetService<IDataPersistenceService<Bundle>>();
            if (bundlePersistence != null)
            {
                bundlePersistence.Inserted += (o, e) =>
                {
                    if (e.Data.EntryKey.HasValue)
                    {
                        if (e.Data.Entry is Patient)
                            this.QueueWorkItem(e.Data.Entry as Patient);
                        else if (e.Data.Entry is Act)
                            this.QueueWorkItem(e.Data.Entry as Act);
                    }
                    else
                    {
                        this.QueueWorkItem(e.Data.Item.Where(c => c is Patient || c is Act).ToArray());
                    }
                };
                bundlePersistence.Updated += (o, e) =>
                {
                    if (e.Data.EntryKey.HasValue)
                    {
                        this.QueueWorkItem(e.Data.Entry);
                    }
                    else
                    {
                        this.QueueWorkItem(e.Data.Item.Where(i => i is Act || i is Patient).ToArray());
                    }
                };

            }

            // Act persistence services
            foreach (var t in typeof(Act).GetTypeInfo().Assembly.ExportedTypes.Where(o => o == typeof(Act) || typeof(Act).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo()) && !o.GetTypeInfo().IsAbstract))
            {
                var pType = typeof(IDataPersistenceService<>).MakeGenericType(t);
                var pInstance = ApplicationContext.Current.GetService(pType) as IDataPersistenceService;

                if (pInstance == null) continue;

                // Create a delegate which calls UpdateCarePlan
                // Construct the delegate
                var ppeArgType = typeof(DataPersistedEventArgs<>).MakeGenericType(t);
                var evtHdlrType = typeof(EventHandler<>).MakeGenericType(ppeArgType);
                var senderParm = Expression.Parameter(typeof(Object), "o");
                var eventParm = Expression.Parameter(ppeArgType, "e");
                var eventData = Expression.Convert(Expression.MakeMemberAccess(eventParm, ppeArgType.GetRuntimeProperty("Data")), typeof(Act));
                var insertInstanceDelegate = Expression.Lambda(evtHdlrType, Expression.Call(Expression.Constant(this), typeof(LocalCarePlanManagerService).GetRuntimeMethod(nameof(QueueWorkItem), new Type[] { typeof(IdentifiedData) }), eventData), senderParm, eventParm).Compile();
                var updateInstanceDelegate = Expression.Lambda(evtHdlrType, Expression.Call(Expression.Constant(this), typeof(LocalCarePlanManagerService).GetRuntimeMethod(nameof(QueueWorkItem), new Type[] { typeof(IdentifiedData) }), eventData), senderParm, eventParm).Compile();
                var obsoleteInstanceDelegate = Expression.Lambda(evtHdlrType, Expression.Call(Expression.Constant(this), typeof(LocalCarePlanManagerService).GetRuntimeMethod(nameof(QueueWorkItem), new Type[] { typeof(IdentifiedData) }), eventData), senderParm, eventParm).Compile();

                // Bind to events
                pType.GetRuntimeEvent("Inserted").AddEventHandler(pInstance, insertInstanceDelegate);
                pType.GetRuntimeEvent("Updated").AddEventHandler(pInstance, updateInstanceDelegate);
                pType.GetRuntimeEvent("Obsoleted").AddEventHandler(pInstance, obsoleteInstanceDelegate);

            }
        }

        /// <summary>
        /// Queue the work item
        /// </summary>
        public void QueueWorkItem(params IdentifiedData[] data)
        {
            lock (this.m_lock)
                this.m_actCarePlanPromise.AddRange(data);

            this.m_resetEvent.Set();
        }

        /// <summary>
        /// Queue the work item
        /// </summary>
        public void QueueWorkItem(IdentifiedData data)
        {
            lock (this.m_lock)
                this.m_actCarePlanPromise.Add(data);

            this.m_resetEvent.Set();
        }


        /// <summary>
        /// Update the care plan given that a new act exists
        /// </summary>
        public void UpdateCarePlan(Act act)
        {
            try
            {
                List<Object> warehousePlan = new List<object>();
                var warehouseService = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();
                var careplanService = ApplicationContext.Current.GetService<ICarePlanService>();

                CarePlan carePlan = null;

                // First step, we delete all acts in the warehouse for the specified patient in the protocol
                var patientId = act.Participations.FirstOrDefault(o => o.ParticipationRoleKey == ActParticipationKey.RecordTarget)?.PlayerEntityKey;
                if (patientId == null)
                {
                    this.m_tracer.TraceWarning("Cannot update care plan for act as it seems to have no RecordTarget");
                    return;
                }

                var patient = ApplicationContext.Current.GetService<IDataPersistenceService<Patient>>().Get(patientId.Value, null, true, AuthenticationContext.SystemPrincipal);

                // Is there a protocol for this act?
                if (act.Protocols.Count() == 0)
                {
                    // Need to re-calculate the entire care plan
                    this.m_tracer.TraceWarning("Will need to calculate the entire care plan for patient {0}", patientId);

                    this.m_tracer.TraceVerbose("Calculating care plan for {0}", patient.Key);

                    // First, we clear the warehouse
                    warehouseService.Delete(this.m_dataMart.Id, new { patient_id = patient.Key.Value });

                    // Now calculate
                    carePlan = careplanService.CreateCarePlan(patient, false, this.m_parameters);

                    // Remove for all on this patient
                    lock (this.m_lock)
                        this.m_actCarePlanPromise.RemoveAll(i => i is Act && (i as Act).Participations.Any(p => p.PlayerEntityKey == patient.Key.Value || i is Patient && (i as Patient).Key == patient.Key.Value));

                }
                else
                {
                    warehouseService.Delete(this.m_dataMart.Id, new
                    {
                        patient_id = patientId.Value,
                        protocol_id = act.Protocols.FirstOrDefault()?.ProtocolKey
                    });

                    carePlan = careplanService.CreateCarePlan(patient, false, this.m_parameters, act.Protocols.FirstOrDefault().ProtocolKey);

                    // Remove for all on this patient
                    lock (this.m_lock)
                        this.m_actCarePlanPromise.RemoveAll(i => i is Act && (i as Act).Participations.Any(p => p.PlayerEntityKey == patient.Key.Value || i is Patient && (i as Patient).Key == patient.Key.Value) && (i as Act).Protocols.FirstOrDefault()?.ProtocolKey == act.Protocols.FirstOrDefault()?.ProtocolKey);

                }

                /// Create a plan for the warehouse
                warehousePlan.AddRange(carePlan.Action.Select(o => new
                {
                    creation_date = DateTime.Now,
                    patient_id = patient.Key.Value,
                    location_id = patient.Relationships.FirstOrDefault(r => r.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation || r.RelationshipType?.Mnemonic == "DedicatedServiceDeliveryLocation")?.TargetEntityKey.Value,
                    act_id = o.Key.Value,
                    protocol_id = o.Protocols.FirstOrDefault().ProtocolKey,
                    class_id = o.ClassConceptKey.Value,
                    type_id = o.TypeConceptKey.Value,
                    min_date = o.StartTime?.Date,
                    max_date = o.StopTime?.Date,
                    act_date = o.ActTime.Date,
                    product_id = o.Participations?.FirstOrDefault(r => r.ParticipationRoleKey == ActParticipationKey.Product || r.ParticipationRole?.Mnemonic == "Product")?.PlayerEntityKey.Value,
                    sequence_id = o.Protocols.FirstOrDefault()?.Sequence,
                    dose_seq = (o as SubstanceAdministration)?.SequenceId
                }));


                // Insert plans
                warehouseService.Add(this.m_dataMart.Id, warehousePlan);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Could not update care plan based on Act {0}: {1}", act, ex);
            }

        }

        /// <summary>
        /// Update the care plan for the specified patient
        /// </summary>
        private void UpdateCarePlan(Patient[] patients)
        {

            try
            {
                var warehouseService = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();

                List<Object> warehousePlan = new List<Object>();

                foreach (var p in patients)
                {
                    this.m_tracer.TraceVerbose("Calculating care plan for {0}", p.Key);
                    var data = p; //  ApplicationContext.Current.GetService<IDataPersistenceService<Patient>>().Get(p.Key.Value);


                    // First, we clear the warehouse
                    warehouseService.Delete(this.m_dataMart.Id, new { patient_id = data.Key.Value });
                    var careplanService = ApplicationContext.Current.GetService<ICarePlanService>();

                    // Now calculate
                    var carePlan = careplanService.CreateCarePlan(data, false, this.m_parameters);
                    warehousePlan.AddRange(carePlan.Action.Select(o => new
                    {
                        creation_date = DateTime.Now,
                        patient_id = data.Key.Value,
                        location_id = data.Relationships.FirstOrDefault(r => r.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation || r.RelationshipType?.Mnemonic == "DedicatedServiceDeliveryLocation")?.TargetEntityKey.Value,
                        act_id = o.Key.Value,
                        class_id = o.ClassConceptKey.Value,
                        type_id = o.TypeConceptKey.Value,
                        protocol_id = o.Protocols.FirstOrDefault()?.ProtocolKey,
                        min_date = o.StartTime?.Date,
                        max_date = o.StopTime?.Date,
                        act_date = o.ActTime.Date,
                        product_id = o.Participations?.FirstOrDefault(r => r.ParticipationRoleKey == ActParticipationKey.Product || r.ParticipationRole?.Mnemonic == "Product")?.PlayerEntityKey.Value,
                        sequence_id = o.Protocols.FirstOrDefault()?.Sequence,
                        dose_seq = (o as SubstanceAdministration)?.SequenceId
                    }));
                }

                // Insert plans
                warehouseService.Add(this.m_dataMart.Id, warehousePlan);

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Could not update care plan for Patient {0}: {1}", patients, ex);
            }

        }

        /// <summary>
        /// Ensure participations are loaded
        /// </summary>
        //private Patient EnsureParticipations(Patient patient)
        //{
        //    patient = patient.Clone() as Patient;
        //    var actService = ApplicationContext.Current.GetService<IActRepositoryService>();
        //    int tr = 0;
        //    IEnumerable<Act> acts = null;
        //    Guid searchState = Guid.Empty;

        //    acts = actService.Find<Act>(o => o.Participations.Any(guard => guard.ParticipationRole.Mnemonic == "RecordTarget" && guard.PlayerEntityKey == patient.Key), 0, 200, out tr);

        //    patient.Participations = acts.Select(a => new ActParticipation(ActParticipationKey.RecordTarget, patient) { Act = a, ParticipationRole = new SanteDB.Core.Model.DataTypes.Concept() { Mnemonic = "RecordTarget" } }).ToList();
        //    return patient;
        //}

        /// <summary>
        /// Stops the daemon service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.m_running = false;
            lock (this.m_lock)
                Monitor.Pulse(this.m_lock);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

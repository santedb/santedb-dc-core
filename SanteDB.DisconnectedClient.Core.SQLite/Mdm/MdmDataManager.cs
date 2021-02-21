/*
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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Services.Local;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.SQLite.Mdm
{

    /// <summary>
    /// Master entity
    /// </summary>
    public interface IMasterEntity { }

    /// <summary>
    /// Stub class for receiving MDM Entities
    /// </summary>
    [XmlType(Namespace = "http://santedb.org/model")]
    public class EntityMaster<T> : Entity, IMasterEntity
        where T : IdentifiedData, new()
    { }

    /// <summary>
    /// Stub class for receiving MDM Acts
    /// </summary>
    [XmlType(Namespace = "http://santedb.org/model")]
    public class ActMaster<T> : Act, IMasterEntity
        where T : IdentifiedData, new()
    { }

    /// <summary>
    /// When synchronizing with a server which has MDM enabled. 
    /// </summary>
    /// <remarks>The SQLite persistence layer is incompatible with the storage patterns of the master. Therefore, whenever the service 
    /// attempts to insert a record which is tagged / flagged as an MDM master this service will take over persistence of the data</remarks>
    public class MdmDataManager : IDaemonService
    {

        /// <summary>
        /// Get all types from core classes of entity and act and create shims in the model serialization binder
        /// </summary>
        static MdmDataManager()
        {
            foreach (var t in typeof(Entity).GetTypeInfo().Assembly.ExportedTypes.Where(o => typeof(Entity).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo())))
                ModelSerializationBinder.RegisterModelType(typeof(EntityMaster<>).MakeGenericType(t));
            foreach (var t in typeof(Act).GetTypeInfo().Assembly.ExportedTypes.Where(o => typeof(Act).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo())))
                ModelSerializationBinder.RegisterModelType(typeof(ActMaster<>).MakeGenericType(t));
        }

        // Get the tracer for this class
        private Tracer m_tracer = Tracer.GetTracer(typeof(MdmDataManager));

        /// <summary>
        /// Relationship used to represents a local/master relationship
        /// </summary>
        /// <remarks>Whenever the MDM persistence layer is used the system will link incoming records (dirty records)
        /// with a generated pristine record tagged as a master record.</remarks>
        public static readonly Guid MasterRecordRelationship = Guid.Parse("97730a52-7e30-4dcd-94cd-fd532d111578");

        /// <summary>
        /// Relationship used to represent that a local record has a high probability of being a duplicate with a master record
        /// </summary>
        public static readonly Guid CandidateLocalRelationship = Guid.Parse("56cfb115-8207-4f89-b52e-d20dbad8f8cc");

        /// <summary>
        /// Represents a record of truth, this is a record which is promoted on the master record such that it is the "true" version of the record
        /// </summary>
        public static readonly Guid MasterRecordOfTruthRelationship = Guid.Parse("1C778948-2CB6-4696-BC04-4A6ECA140C20");

        /// <summary>
        /// Master record classification
        /// </summary>
        public static readonly Guid MasterRecordClassification = Guid.Parse("49328452-7e30-4dcd-94cd-fd532d111578");

        /// <summary>
        /// Disposables
        /// </summary>
        private List<IDisposable> m_linkedDisposables;

        /// <summary>
        /// True if this service is running
        /// </summary>
        public bool IsRunning => true;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "MDM Synchronization Manager";

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired after the service has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired after the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Create a mdm repository 
        /// </summary>
        private IDisposable CreateRepositoryListener(Type bindType)
        {
            var repoService = typeof(INotifyRepositoryService<>).MakeGenericType(bindType);
            if (ApplicationServiceContext.Current.GetService(repoService) != null)
                return Activator.CreateInstance(typeof(MdmRepositoryListener<>).MakeGenericType(bindType)) as IDisposable;
            return null;
        }

        /// <summary>
        /// Start the service - monitor the incoming queues
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            // Subscribe to the local repository service
            var repo = ApplicationServiceContext.Current.GetService<LocalRepositoryService>();
            if (repo != null)
                repo.Started += (o, e) =>
                {
                    var qms = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
                    if (qms != null)
                        qms.Inbound.Enqueuing += OnEnqueueInbound;

                    var clinDi = ApplicationServiceContext.Current.GetService<IClinicalIntegrationService>();
                    if (clinDi != null)
                        clinDi.Responded += ClinicalRepositoryResponded;

                    // We want to attach to the repository services so we can redirect writes which are actually against a MDM controlled piece of data
                    // This is because the user interface or API callers on the dCDR may inadvertantly try to update from an old UUID, 
                    // not realizing that the MDM layer had rewritten the UUID to the master copy UUID
                    var types = ApplicationServiceContext.Current.GetService<IServiceManager>()
                        .GetAllTypes().Where(t => typeof(Entity).IsAssignableFrom(t)).ToArray();
                    this.m_linkedDisposables = types.Select(t => this.CreateRepositoryListener(t)).OfType<IDisposable>().ToList();
                    this.m_linkedDisposables.Add(new MdmRepositoryListener<Bundle>());
                };
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Clinical data repository has responded
        /// </summary>
        private void ClinicalRepositoryResponded(object sender, IntegrationResultEventArgs e)
        {
            if (e.ResponseData is Bundle bundle && e.SubmittedData != null && this.CorrectMdmData(bundle))
            {
                // Update the data
                ApplicationServiceContext.Current.GetService<IDataPersistenceService<Bundle>>().Insert(bundle, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
            }
            else if (e.ResponseData is Entity entity)
            {
                if (entity.Tags.Any(o => o.TagKey == "$generated" && o.Value == "true"))
                {
                    foreach (var rel in entity.Relationships.Where(o => o.RelationshipTypeKey == MasterRecordRelationship))
                        this.RewriteRelationships(entity, rel.SourceEntityKey, rel.TargetEntityKey);

                    // TODO: Save the resulting object here.
                }
                else if (entity.ClassConceptKey == MasterRecordClassification) // we fetched a master record directly - what is the local type? 
                {
                    Type bindType = null;
                    var mdmRel = entity.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == MasterRecordRelationship);
                    if (mdmRel == null)
                        bindType = (sender as IClinicalIntegrationService).Find<Entity>(o => o.Relationships.Where(r => r.RelationshipType.Mnemonic == "MDM-Master").Any(r => r.TargetEntityKey == entity.Key), 0, 1).Item.FirstOrDefault()?.GetType();
                    else
                        bindType = (sender as IClinicalIntegrationService).Get<Entity>(mdmRel.SourceEntityKey.Value, null)?.GetType();

                    if (bindType != null) // We have an MDM record 
                    {
                        e.ResponseData = (sender as IClinicalIntegrationService).Get(bindType, entity.Key.Value, null);
                        entity = e.ResponseData as Entity;
                        foreach (var rel in entity.Relationships.Where(o => o.RelationshipTypeKey == MasterRecordRelationship))
                            this.RewriteRelationships(entity, rel.SourceEntityKey, rel.TargetEntityKey);

                    }
                }
            }
        }


        /// <summary>
        /// Inbound queue object has been queued, ensure that the MDM references are corrected for proper operation of the mobile device queue
        /// </summary>
        private void OnEnqueueInbound(object sender, SanteDB.Core.Event.DataPersistingEventArgs<ISynchronizationQueueEntry> e)
        {
            if (this.CorrectMdmData(e.Data.Data))
            {
                // Update the data file
                var iqfp = ApplicationServiceContext.Current.GetService<IQueueFileProvider>();
                if (iqfp != null)
                {
                    try
                    {
                        var newKeyId = iqfp.SaveQueueData(e.Data.Data);
                        iqfp.RemoveQueueData(e.Data.DataFileKey);
                        e.Data.DataFileKey = newKeyId;
                    }
                    catch (Exception ex)
                    {
                        this.m_tracer.TraceError("Error enqueuing data {0}: {1}", e.Data.Data, ex);
                        throw new Exception($"Cannot enqueue data {e.Data.Data}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Correct MDM data
        /// </summary>
        private bool CorrectMdmData(IdentifiedData data)
        {
            IEnumerable<IdentifiedData> toProcess = null;
            if (data is Bundle bundle)
                toProcess = bundle.Item.ToArray();
            else
            {
                toProcess = new IdentifiedData[] { data };
                bundle = null;
            }

            bool hasChanged = false;
            // Process the objects
            foreach (var itm in toProcess)
            {
                if (itm is ITaggable taggable && taggable.Tags.Any(o => o.TagKey == "$mdm.type" && o.Value == "M")) // is a master record
                {
                    // First, get alternate keys by which this master is known
                    if (itm is Entity entity)
                    {
                        var masterRelation = entity.Relationships.Where(o => o.RelationshipTypeKey == MasterRecordRelationship);

                        // The source of these point to the master, we need to correct all inbound relationships to point to me instead of the local which originally pointed at
                        foreach (var rel in masterRelation.ToArray())
                        {
                            var currentLocal = this.HideCurrentLocal(rel);
                            if (currentLocal != null)
                                bundle?.Item.Add(currentLocal);
                            bundle?.Item.Add(rel.Clone()); // Add rel
                                                           // Rewrite all relationships
                            this.RewriteRelationships(itm, rel.SourceEntityKey, itm.Key);
                        }

                        // Remove all MDM links as these make no sense in this context
                        entity.Relationships.RemoveAll(o => o.RelationshipTypeKey == MasterRecordRelationship || o.RelationshipTypeKey == MasterRecordOfTruthRelationship || o.RelationshipTypeKey == CandidateLocalRelationship);
                        hasChanged = true;
                    }
                    else if (itm is Act act)
                    {
                        var masterRelation = act.Relationships.Where(o => o.RelationshipTypeKey == MasterRecordRelationship);

                        // The source of these point to the master, we need to correct all inbound relationships to point to me instead of the local which originally pointed at
                        foreach (var rel in masterRelation)
                        {
                            // Rewrite all relationships
                            bundle?.Item.Add(rel.Clone());
                            this.RewriteRelationships(itm, rel.SourceEntityKey, itm.Key);
                            // Do we have a local object? 
                            if (rel.LoadProperty<Entity>(nameof(EntityRelationship.SourceEntity)) != null)
                            {
                                rel.SourceEntity.ObsoletionTime = DateTime.Now;
                                rel.SourceEntity.ObsoletedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                                act.Relationships.Add(new ActRelationship(ActRelationshipTypeKeys.Replaces, rel.SourceEntityKey));
                            }
                        }

                        act.Relationships.RemoveAll(o => o.RelationshipTypeKey == MasterRecordRelationship || o.RelationshipTypeKey == MasterRecordOfTruthRelationship || o.RelationshipTypeKey == CandidateLocalRelationship);

                        hasChanged = true;
                    }
                }
                else if (itm is EntityRelationship er && er.RelationshipTypeKey == MasterRecordRelationship)
                {
                    // Is there a master entity this points to in the bundle? if so, we need to insert it as a patient
                    var master = bundle?.Item.FirstOrDefault(o => o.Key == er.TargetEntityKey);
                    // Master found?
                    if (master != null)
                    {
                        // Find the local in the bundle
                        var local = bundle?.Item.FirstOrDefault(o => o.Key == er.SourceEntityKey);
                        if (local != null)
                        {
                            bundle?.Item.Remove(master);
                            var newEntity = Activator.CreateInstance(local.GetType()) as IdentifiedData;
                            newEntity.CopyObjectData(master, false, true);
                            newEntity.SemanticCopy(local, master);
                            bundle?.Item.Add(newEntity);
                            this.RewriteRelationships(newEntity, local.Key, newEntity.Key);
                        }
                    }
                    else
                        master = EntitySource.Current.Get<Entity>(er.TargetEntityKey);
                    var existing = bundle?.Item.RemoveAll(o => o.Key == er.SourceEntityKey);
                    var currentLocal = this.HideCurrentLocal(er);
                    if (currentLocal != null)
                    {
                        bundle?.Item.Add(currentLocal);
                        // Store an MDM local relationship between the hidden local and the new mdm master
                        if (master != null)
                            bundle?.Item.Add(new EntityRelationship(MasterRecordRelationship, master.Key) { SourceEntityKey = currentLocal.Key });
                        else
                            bundle?.Item.Add(new EntityRelationship(MasterRecordRelationship, er.TargetEntityKey) { SourceEntityKey = currentLocal.Key });

                    }
                    hasChanged = true;
                }
            }

            return hasChanged;
        }

        /// <summary>
        /// Correct local data to hide it
        /// </summary>
        private IdentifiedData HideCurrentLocal(EntityRelationship rel)
        {

            // Do we have a local object? 
            var local = EntitySource.Current.Get<Entity>(rel.SourceEntityKey);
            if (local != null && !local.ObsoletionTime.HasValue)
            {
                //local.ObsoletionTime = DateTime.Now;
                //local.ObsoletedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                local.AddTag("$sys.hidden", "true");
                //entity.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, rel.SourceEntityKey));

            }
            return local;

        }

        /// <summary>
        /// Rewrite all relationships on the specified data entry 
        /// </summary>
        private void RewriteRelationships(IdentifiedData itm, Guid? localId, Guid? masterId)
        {
            foreach (var pi in itm.GetType().GetRuntimeProperties())
            {
                var existing = pi.GetValue(itm);
                if (existing is IList list)
                {
                    foreach (var i in list)
                        if (i is ISimpleAssociation association && association.SourceEntityKey == localId)
                            association.SourceEntityKey = masterId;
                }
                else if (existing is ISimpleAssociation association && association.SourceEntityKey == localId)
                    association.SourceEntityKey = masterId;
            }

            // Now correct relationships in the DB that might be pointing at any of the locals
            if (itm is Entity entity)
            {
                var entityRelation = ApplicationServiceContext.Current.GetService<IDataPersistenceService<EntityRelationship>>();
                foreach (var er in entityRelation.Query(o => o.TargetEntityKey == localId, AuthenticationContext.SystemPrincipal))
                {
                    // Re-write the relationship to the master
                    er.TargetEntityKey = masterId;
                    entityRelation.Update(er, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                }
            }
        }

        /// <summary>
        /// Stop this service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            var qms = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
            if (qms != null)
                qms.Inbound.Enqueuing -= OnEnqueueInbound;

            this.m_linkedDisposables.ForEach(o => o.Dispose());

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Mdm
{
    /// <summary>
    /// When synchronizing with a server which has MDM enabled. 
    /// </summary>
    /// <remarks>The SQLite persistence layer is incompatible with the storage patterns of the master. Therefore, whenever the service 
    /// attempts to insert a record which is tagged / flagged as an MDM master this service will take over persistence of the data</remarks>
    public class MdmDataManager : IDaemonService
    {

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
        /// True if this service is running
        /// </summary>
        public bool IsRunning => true;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "MDM Synchronization Correction Manager";

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
        /// Start the service - monitor the incoming queues
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                var qms = ApplicationServiceContext.Current.GetService<IQueueManagerService>();
                if (qms != null)
                {
                    qms.Inbound.Enqueuing += OnEnqueueInbound;
                }
            };
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Inbound queue object has been queued, ensure that the MDM references are corrected for proper operation of the mobile device queue
        /// </summary>
        private void OnEnqueueInbound(object sender, SanteDB.Core.Event.DataPersistingEventArgs<Core.Synchronization.ISynchronizationQueueEntry> e)
        {
            if(this.CorrectMdmData(e.Data.Data))
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
                    catch(Exception ex)
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
                toProcess = bundle.Item;
            else
                toProcess = new IdentifiedData[] { data };

            bool hasChanged = false;
            // Process the objects
            foreach (var itm in toProcess)
            {
                if (itm is ITaggable taggable && taggable.Tags.Any(o => o.TagKey == "mdm.type" && o.Value == "M")) // is a master record
                {
                    // First, get alternate keys by which this master is known
                    if (itm is Entity entity)
                    {
                        var masterRelation = entity.Relationships.Where(o => o.RelationshipTypeKey == MasterRecordRelationship);

                        // The source of these point to the master, we need to correct all inbound relationships to point to me instead of the local which originally pointed at
                        foreach (var rel in masterRelation)
                        {
                            // Rewrite all relationships
                            this.RewriteRelationships(itm, rel.SourceEntityKey, itm.Key);
                            // Do we have a local object? 
                            if (rel.LoadProperty<Entity>(nameof(EntityRelationship.SourceEntity)) != null)
                            {
                                rel.SourceEntity.ObsoletionTime = DateTime.Now;
                                rel.SourceEntity.ObsoletedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                                entity.Relationships.Add(new EntityRelationship(EntityRelationshipTypeKeys.Replaces, rel.SourceEntityKey));
                            }
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
            }

            return hasChanged;
        }

        /// <summary>
        /// Rewrite all relationships on the specified data entry 
        /// </summary>
        private void RewriteRelationships(IdentifiedData itm, Guid? from, Guid? to)
        {
            foreach(var pi in itm.GetType().GetRuntimeProperties())
            {
                var existing = pi.GetValue(itm);
                if (existing is IList list)
                {
                    foreach (var i in list)
                        if (i is ISimpleAssociation association && association.SourceEntityKey == from)
                            association.SourceEntityKey = to;
                }
                else if (existing is ISimpleAssociation association && association.SourceEntityKey == from)
                    association.SourceEntityKey = to;
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

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

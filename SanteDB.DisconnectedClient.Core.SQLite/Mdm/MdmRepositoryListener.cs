/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */
using SanteDB.Core;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Mdm
{
    /// <summary>
    /// Resource listener which redirects incoming repository requests
    /// </summary>
    internal class MdmRepositoryListener<T> : IDisposable where T : IdentifiedData
    {

        // Persistence service
        private INotifyRepositoryService<T> m_repositoryService;
        private IDataPersistenceService<T> m_persistenceService;

        /// <summary>
        /// Repository listener
        /// </summary>
        public MdmRepositoryListener()
        {
            this.m_repositoryService = ApplicationServiceContext.Current.GetService<INotifyRepositoryService<T>>();
            this.m_persistenceService = ApplicationServiceContext.Current.GetService<IDataPersistenceService<T>>();
            this.m_repositoryService.Inserting += this.OnPrePersistenceEvent;
            this.m_repositoryService.Saving += this.OnPrePersistenceEvent;
            this.m_repositoryService.Obsoleting += this.OnPrePersistenceEvent;
            this.m_repositoryService.Retrieving += this.OnRetrieveEvent;
        }

        /// <summary>
        /// Get the master redirection instruction
        /// </summary>
        private ITargetedAssociation GetMasterRedirect(Guid? sourceKey, Type forType = null)
        {
            if (typeof(Entity).IsAssignableFrom(forType ?? typeof(T)))
            {
                var erps = ApplicationServiceContext.Current.GetService<IDataPersistenceService<EntityRelationship>>();
                return erps.Query(o => o.SourceEntityKey == sourceKey && o.RelationshipTypeKey == MdmDataManager.MasterRecordRelationship && o.ObsoleteVersionSequenceId == null, 0, 1, out int _, AuthenticationContext.SystemPrincipal).FirstOrDefault();
            }
            // TODO: Acts
            return null;
        }

        /// <summary>
        /// Fire an on retrieving event
        /// </summary>
        private void OnRetrieveEvent(object sender, DataRetrievingEventArgs<T> e)
        {
            // Entity relationship for MDM?
            var masterRel = this.GetMasterRedirect(e.Id);
            if (masterRel != null)
            {
                e.Cancel = true;
                e.Result = this.m_persistenceService.Get(masterRel.TargetEntityKey.Value, null, true, AuthenticationContext.Current.Principal);
            }
        }

        /// <summary>
        /// Redirect persistence event
        /// </summary>
        private void OnPrePersistenceEvent(object sender, DataPersistingEventArgs<T> e)
        {
            if (e.Data is Bundle bundle)
            {
                foreach (var itm in bundle.Item.ToArray())
                {
                    var masterRel = this.GetMasterRedirect(itm.Key, itm.GetType());
                    if (masterRel != null)
                    {
                        if (itm is Entity entity) // Add the MDM rel back so it can be saved 
                            entity.Relationships.Add(masterRel as EntityRelationship);
                    }
                }
            }
            else if (e.Data.Key.HasValue) // update/insert 
            {
                var masterRel = this.GetMasterRedirect(e.Data.Key);
                if (masterRel != null) // has a master, so update that instead
                {
                    if (e.Data is Entity entity) // Add the MDM rel back so it can be saved
                        entity.Relationships.Add(masterRel as EntityRelationship);
                }
            }

        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        public void Dispose()
        {
            this.m_repositoryService.Inserting -= this.OnPrePersistenceEvent;
            this.m_repositoryService.Saving -= this.OnPrePersistenceEvent;
            this.m_repositoryService.Obsoleting -= this.OnPrePersistenceEvent;
            this.m_repositoryService.Retrieving -= this.OnRetrieveEvent;
        }
    }
}

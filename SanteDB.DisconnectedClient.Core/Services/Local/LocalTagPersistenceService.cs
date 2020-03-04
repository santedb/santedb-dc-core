/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Tag persistence service for act
    /// </summary>
    public class LocalTagPersistenceService : ITagPersistenceService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Default Tag Persistence Service";

        /// <summary>
        /// Save tag
        /// </summary>
        public void Save(Guid sourceKey, ITag tag)
        {
            if (tag == null || tag.TagKey.StartsWith("$"))
                return; // $ tags are not saved
            if (tag is EntityTag)
            {
                var idp = ApplicationContext.Current.GetService<IDataPersistenceService<EntityTag>>();
                var existing = idp.Query(o => o.SourceEntityKey == sourceKey && o.TagKey == tag.TagKey, AuthenticationContext.Current.Principal).FirstOrDefault();
                if (existing != null)
                {
                    existing.Value = tag.Value;
                    if (existing.Value == null)
                        idp.Obsolete(existing, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                    else
                        idp.Update(existing as EntityTag, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                }
                else
                    idp.Insert(tag as EntityTag, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            }
            else if (tag is ActTag)
            {
                var idp = ApplicationContext.Current.GetService<IDataPersistenceService<ActTag>>();
                var existing = idp.Query(o => o.SourceEntityKey == sourceKey && o.TagKey == tag.TagKey, AuthenticationContext.Current.Principal).FirstOrDefault();
                if (existing != null)
                {
                    existing.Value = tag.Value;
                    if (existing.Value == null)
                        idp.Obsolete(existing, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                    else
                        idp.Update(existing as ActTag, TransactionMode.Commit, AuthenticationContext.Current.Principal);
                }
                else
                    idp.Insert(tag as ActTag, TransactionMode.Commit, AuthenticationContext.Current.Principal);
            }
        }
    }


}

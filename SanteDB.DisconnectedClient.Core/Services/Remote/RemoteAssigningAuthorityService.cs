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
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// An assigning authority service for remotely fetching AA
    /// </summary>
    public class RemoteAssigningAuthorityService : AmiRepositoryBaseService, IRepositoryService<AssigningAuthority>

    {
        public string ServiceName => throw new NotImplementedException();


        /// <summary>
        /// Get AA
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Get(Guid key)
        {
            return ((IRepositoryService<AssigningAuthority>)this).Get(key, Guid.Empty);
        }

        /// <summary>
        /// Get assigning authority
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Get(Guid key, Guid versionKey)
        {
            using (var client = this.GetClient())
                try
                {
                    var retVal = client.Client.Get<AssigningAuthority>($"AssigningAuthority/{key}");
                    return retVal;
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Could not retrieve authority", e);
                }
        }

        /// <summary>
        /// Finds the specified assigning authority
        /// </summary>
        IEnumerable<AssigningAuthority> IRepositoryService<AssigningAuthority>.Find(Expression<Func<AssigningAuthority, bool>> query)
        {
            int tr = 0;
            return ((IRepositoryService<AssigningAuthority>)this).Find(query, 0, null, out tr);
        }

        IEnumerable<AssigningAuthority> IRepositoryService<AssigningAuthority>.Find(Expression<Func<AssigningAuthority, bool>> query, int offset, int? count, out int totalResults, params ModelSort<AssigningAuthority>[] orderBy)
        {
            using (var client = this.GetClient())
                try
                {
                    return client.Query(query, offset, count, out totalResults, orderBy: orderBy).CollectionItem.OfType<AssigningAuthority>();
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Could not query assigning authorities", e);
                }
        }

        /// <summary>
        /// Insert the specified data
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Insert(AssigningAuthority data)
        {
            using (var client = this.GetClient())
                try
                {
                    var retVal = client.CreateAssigningAuthority(data);
                    return retVal;
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Could not create application", e);
                }
        }

        /// <summary>
        /// Update assigning authority
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Save(AssigningAuthority data)
        {
            using (var client = this.GetClient())
                try
                {
                    var retVal = client.UpdateAssigningAuthority(data.Key.Value, data);
                    return retVal;
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Could not create authority", e);
                }
        }

        /// <summary>
        /// Obsolete the authority
        /// </summary>
        /// <returns></returns>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Obsolete(Guid key)
        {
            using (var client = this.GetClient())
                try
                {
                    var retVal = client.DeleteAssigningAuthority(key);
                    return retVal;
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException("Could not delete authority", e);
                }
        }
    }
}

﻿/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-22
 */
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SanteDB.Core.Model;
using System.Linq;
using SanteDB.Core.Services;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents a repository service for managing assigning authorities.
    /// </summary>
    public class LocalAssigningAuthorityRepository :
        GenericLocalMetadataRepository<AssigningAuthority>,
        IAssigningAuthorityRepositoryService
    {

        /// <summary>
        /// Get the specified assigning authority
        /// </summary>
        public AssigningAuthority Get(Uri assigningAutUri)
        {
            int tr = 0;

            if (assigningAutUri.Scheme == "urn" && assigningAutUri.LocalPath.StartsWith("oid:"))
            {
                var aaOid = assigningAutUri.LocalPath.Substring(4);
                return base.Find(o => o.Oid == aaOid, 0, 1, out tr, Guid.Empty).FirstOrDefault();
            }

            return base.Find(o => o.Url == assigningAutUri.OriginalString, 0, 1, out tr, Guid.Empty).FirstOrDefault();

        }

        /// <summary>
        /// Get assigning authority 
        /// </summary>
        public AssigningAuthority Get(string domain)
        {
            int tr = 0;
            return base.Find(o => o.DomainName == domain, 0, 1, out tr).FirstOrDefault();
        }
    }
}
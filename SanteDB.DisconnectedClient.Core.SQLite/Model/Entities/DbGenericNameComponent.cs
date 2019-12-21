/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SQLite.Net.Attributes;

namespace SanteDB.DisconnectedClient.SQLite.Model.Entities
{
    /// <summary>
    /// Generic name component
    /// </summary>
    public abstract class DbGenericNameComponent : DbIdentified
    {

        /// <summary>
        /// Gets or sets the type of the component
        /// </summary>
        [Column("type"), MaxLength(16), ForeignKey(typeof(DbConcept), nameof(DbConcept.Uuid))]
        public byte[] ComponentTypeUuid { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Column("value_id"), MaxLength(16), NotNull, Indexed]
        public virtual byte[] ValueUuid
        {
            get;
            set;
        }
    }
}
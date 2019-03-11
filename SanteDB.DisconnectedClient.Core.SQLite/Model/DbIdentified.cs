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
 * User: justi
 * Date: 2019-1-12
 */
using SanteDB.Core.Data.QueryBuilder.Attributes;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SQLite.Net.Attributes;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Model
{
    /// <summary>
    /// Represents data that is identified in some way
    /// </summary>
    public class DbIdentified
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.SQLite.Model.Model.DbIdentified"/> class.
        /// </summary>
        public DbIdentified()
        {
        }

        /// <summary>
        /// Gets or sets the universal identifier for the object
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull]
        public virtual byte[] Uuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the key (GUID) on the persistence item
        /// </summary>
        /// <value>The key.</value>
        [Ignore]
        public Guid Key
        {
            get { return this.Uuid.ToGuid() ?? Guid.Empty; }
            set { this.Uuid = value.ToByteArray(); }
        }


    }

    /// <summary>
    /// Act sub table
    /// </summary>
    public class DbActSubTable : DbIdentified
    {

        /// <summary>
        /// Gets or sets the uuid pointing to the act
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull, ForeignKey(typeof(DbAct), nameof(DbAct.Uuid)), AlwaysJoin]
        public override byte[] Uuid { get; set; }

    }

    /// <summary>
    /// Observation sub class table
    /// </summary>
    public class DbObservationSubTable : DbIdentified
    {

        /// <summary>
        /// Gets or sets the uuid pointing to the act
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull, ForeignKey(typeof(DbObservation), nameof(DbObservation.Uuid)), AlwaysJoin]
        public override byte[] Uuid { get; set; }

    }

    /// <summary>
    /// Entity sub table
    /// </summary>
    public class DbEntitySubTable : DbIdentified
    {

        /// <summary>
        /// Gets or sets the uuid pointing to the act
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull, ForeignKey(typeof(DbEntity), nameof(DbEntity.Uuid)), AlwaysJoin]
        public override byte[] Uuid { get; set; }

    }

    /// <summary>
    /// Person sub class table
    /// </summary>
    public class DbPersonSubTable : DbIdentified
    {

        /// <summary>
        /// Gets or sets the uuid pointing to the act
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull, ForeignKey(typeof(DbPerson), nameof(DbPerson.Uuid)), AlwaysJoin]
        public override byte[] Uuid { get; set; }

    }

    /// <summary>
    /// Materialsub class table
    /// </summary>
    public class DbMaterialSubTable : DbIdentified
    {

        /// <summary>
        /// Gets or sets the uuid pointing to the act
        /// </summary>
        [PrimaryKey, Column("uuid"), MaxLength(16), NotNull, ForeignKey(typeof(DbMaterial), nameof(DbMaterial.Uuid)), AlwaysJoin]
        public override byte[] Uuid { get; set; }

    }

}


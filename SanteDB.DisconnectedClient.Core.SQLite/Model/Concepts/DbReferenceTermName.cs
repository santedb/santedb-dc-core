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
using SanteDB.DisconnectedClient.SQLite.Query.Attributes;
using SQLite.Net.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Model.Concepts
{
    /// <summary>
    /// Reference term name
    /// </summary>
    [Table("reference_term_name")]
    public class DbReferenceTermName : DbIdentified
    {
        /// <summary>
        /// Gets or sets the ref term to which the nae applies
        /// </summary>
        [Column("reference_term_uuid"), ForeignKey(typeof(DbReferenceTerm), nameof(DbReferenceTerm.Uuid))]
        public byte[] ReferenceTermUuid { get; set; }


        /// <summary>
        /// Gets or sets the language code
        /// </summary>
        [Column("lang")]
        public String LanguageCode { get; set; }

        /// <summary>
        /// Gets orsets the value
        /// </summary>
        [Column("value")]
        public String Value { get; set; }
    }
}

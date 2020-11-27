/*
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
 * Date: 2020-8-31
 */
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Remove phonetic columns
    /// </summary>
    public class RemovePhoneticColumnsMigration : IDataMigration
    {
        /// <summary>
        /// Identifier
        /// </summary>
        public string Id => "999-langley-remove-phon-value";

        /// <summary>
        /// Description
        /// </summary>
        public string Description => "Removes phonetic values if not already removed";

        /// <summary>
        /// Install
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());
            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
            {

                try
                {
                    // Migration log create and check
                    try
                    {
                        db.Execute("CREATE TABLE _phonetic_value ( value varchar not null, uuid blob primary key not null );");
                        db.Execute("INSERT INTO _phonetic_value ( value, uuid ) SELECT value, uuid FROM phonetic_value;");
                        db.Execute("DROP TABLE phonetic_value;");
                        db.Execute("ALTER TABLE _phonetic_value RENAME TO phonetic_value");
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Cannot migrate table phonetic_value", e);
                    }

                    try
                    {
                        db.Execute("CREATE TABLE _concept_name ( concept_uuid blob not null, language varchar not null, value varchar not null, uuid blob primary key not null );");
                        db.Execute("INSERT INTO _concept_name ( concept_uuid, language, value, uuid ) SELECT concept_uuid, language, value, uuid FROM concept_name;");
                        db.Execute("DROP TABLE concept_name;");
                        db.Execute("ALTER TABLE _concept_name RENAME TO concept_name;");
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Cannot migrate table concept_name", e);
                    }

                    try
                    {
                        db.Execute("CREATE TABLE _entity_addr_val ( value varchar not null, uuid blob primary key not null );");
                        db.Execute("INSERT INTO _entity_addr_val ( value, uuid ) SELECT value, uuid FROM entity_addr_val;");
                        db.Execute("DROP TABLE entity_addr_val;");
                        db.Execute("ALTER TABLE _entity_addr_val RENAME TO entity_addr_val");
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Cannot migrate table entity_addr_val", e);
                    }

                    db.Commit();
                }
                catch (Exception e)
                {
#if DEBUG
                    tracer.TraceError("Unable to migrate tables: {0}", e.StackTrace);
#endif
                    tracer.TraceError("Unable to migrate tables: {0}", e.Message);
                }
            }
            return true;
        }
    }
}

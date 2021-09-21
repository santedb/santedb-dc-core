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
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Query.ExtendedFunctions;
using SQLite.Net;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Spell fix update
    /// </summary>
    public class SpellFixUpdate : IDataMigration
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(SpellFixUpdate));

        /// <summary>
        /// Database migration
        /// </summary>
        public string Id => "zz-spellfix";

        /// <summary>
        /// Spellfix update
        /// </summary>
        public string Description => "Initializes the spellfix data";

        /// <summary>
        /// Install the initial catalog
        /// </summary>
        public bool Install()
        {

            var search = ApplicationContext.Current?.ConfigurationManager.GetConnectionString("santeDbSearch");
            LockableSQLiteConnection db = null;
            // Database for the SQL Lite connection
            if (search != null)
            {
                db = SQLiteConnectionManager.Current.GetReadWriteConnection(search);
                using (db.Lock())
                    this.Install(db);
            }

            db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
                return this.Install(db);
        }

        /// <summary>
        /// Install to context
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public bool Install(SQLiteConnection db)
        {
            try
            {

                db.Execute(@"CREATE TABLE __sfEditCost (  iLang INT, cFrom TEXT, cTo TEXT, iCost INT );");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'','?', 1);");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'?','', 1);");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'?','?', 1);");
                new LevenshteinFilterFunction().Initialize(db);
            }
            catch (SQLiteException e) when (e.Message == "table __sfEditCost already exists")
            {
                this.m_tracer.TraceWarning("Spellfix already initialized");

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deploying Spellfix Update: {0}", e);
                throw new System.Data.DataException("Error deploying Spellfix Update", e);
            }
            return true;
        }
    }
}

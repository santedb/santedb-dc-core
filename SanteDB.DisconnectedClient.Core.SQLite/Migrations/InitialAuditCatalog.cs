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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Security.Audit.Model;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations
{
    /// <summary>
    /// Database migration for audit
    /// </summary>
    public class InitialAuditCatalog : IDataMigration
    {
        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description
        {
            get
            {
                return "Audit Catalog";
            }
        }

        /// <summary>
        /// Get the identifier of the migration
        /// </summary>
        public string Id
        {
            get
            {
                return "000-init-santedb-dalhouse-audit";
            }
        }

        /// <summary>
        /// Install the migration
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());
            // Database for the SQL Lite connection
            var connStr = ApplicationContext.Current?.ConfigurationManager.GetConnectionString("santeDbAudit");
            if (connStr==null)
                return true;
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(connStr);
            using (db.Lock())
            {
                try
                {

                    db.CreateTable<DbAuditData>();
                    db.CreateTable<DbAuditActor>();
                    db.CreateTable<DbAuditActorAssociation>();
                    db.CreateTable<DbAuditCode>();
                    db.CreateTable<DbAuditMetadata>();
                    db.CreateTable<DbAuditObject>();
                    return true;
                }
                catch (Exception e)
                {
                    tracer.TraceError("Error deploying Audit repository: {0}", e);
                    return false;
                }
            }
        }
    }
}
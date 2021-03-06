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
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Search;
using SanteDB.DisconnectedClient.SQLite.Search.Model;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations
{
    /// <summary>
    /// Data migration that inserts the initial search catalog
    /// </summary>
    public class InitialSearchCatalog : IDataMigration
    {
        // Initial search catalog
        private Tracer m_tracer = Tracer.GetTracer(typeof(InitialSearchCatalog));

        /// <summary>
        /// Gets the description of the migration
        /// </summary>
        public string Description
        {
            get
            {
                return "FreeText Search Catalog";
            }
        }

        /// <summary>
        /// Identifier of the migration
        /// </summary>
        public string Id
        {
            get
            {
                return "000-init-search";
            }
        }

        /// <summary>
        /// Perform the installation
        /// </summary>
        public bool Install()
        {
            try
            {

                var connStr = ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbSearch");

                // Is the search service registered?
                if (connStr == null)
                    return true;
                else if (ApplicationContext.Current.GetService<IFreetextSearchService>() == null)
                    ApplicationContext.Current.AddServiceProvider(typeof(SQLiteSearchIndexService), true);

                // Get a connection to the search database
                var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(connStr);
                using (conn.Lock())
                {
                    conn.CreateTable<SearchEntityType>();
                    conn.CreateTable<SearchTerm>();
                    conn.CreateTable<SearchTermEntity>();
                } // release lock

             
                return true;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error installing search tables {0}", e);
                throw;
            }
        }
    }
}

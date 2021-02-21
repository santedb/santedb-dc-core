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
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.DataType;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Extensibility;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.SQLite.Security;
using SQLite.Net;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SanteDB.Core.Security.Services;

namespace SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations
{
    /// <summary>
    /// This class is responsible for setting up an initial catalog of items in the SQL Lite database
    /// </summary>
    internal class InitialCatalog : IDataMigration
    {

        #region IDbMigration implementation

        /// <summary>
        /// Install the initial catalog
        /// </summary>
        public bool Install()
        {

            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
            {
                return this.Install(db, false);
            }
        }

        /// <summary>
        /// Install to context
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public bool Install(SQLiteConnection db, bool silent)
        {
            var tracer = Tracer.GetTracer(this.GetType());
            try
            {

                // Migration log create and check
                db.CreateTable<DbMigrationLog>();
                if (db.Table<DbMigrationLog>().Count(o => o.MigrationId == this.Id) > 0)
                {
                    tracer.TraceInfo("Migration already installed");
                    return true;
                }
                else
                    db.Insert(new DbMigrationLog()
                    {
                        MigrationId = this.Id,
                        InstallationDate = DateTime.Now
                    });

                if (!silent)
                    ApplicationContext.Current.SetProgress(Strings.locale_setting_table, 0);

                // Create tables
                tracer.TraceInfo("Installing Concept Tables...");
                db.CreateTable<DbConcept>();
                db.CreateTable<DbConceptName>();
                db.CreateTable<DbConceptClass>();
                db.CreateTable<DbConceptRelationship>();
                db.CreateTable<DbConceptRelationshipType>();
                db.CreateTable<DbConceptSet>();
                db.CreateTable<DbConceptSetConceptAssociation>();
                db.CreateTable<DbReferenceTerm>();
                db.CreateTable<DbReferenceTermName>();
                db.CreateTable<DbConceptReferenceTerm>();
                db.CreateTable<DbCodeSystem>();

                tracer.TraceInfo("Installing Identifiers Tables...");
                db.CreateTable<DbEntityIdentifier>();
                db.CreateTable<DbActIdentifier>();
                db.CreateTable<DbIdentifierType>();
                db.CreateTable<DbAssigningAuthority>();

                tracer.TraceInfo("Installing Extensibility Tables...");
                db.CreateTable<DbActExtension>();
                db.CreateTable<DbActNote>();
                db.CreateTable<DbActProtocol>();
                db.CreateTable<DbEntityExtension>();
                db.CreateTable<DbEntityNote>();
                db.CreateTable<DbExtensionType>();
                db.CreateTable<DbTemplateDefinition>();

                tracer.TraceInfo("Installing Security Tables...");
                db.CreateTable<DbSecurityApplication>();
                db.CreateTable<DbSecurityDevice>();
                db.CreateTable<DbSecurityPolicy>();
                db.CreateTable<DbSecurityDevicePolicy>();
                db.CreateTable<DbSecurityRolePolicy>();
                db.CreateTable<DbActSecurityPolicy>();
                db.CreateTable<DbEntitySecurityPolicy>();
                db.CreateTable<DbSecurityRole>();
                db.CreateTable<DbSecurityUser>();
                db.CreateTable<DbSecurityUserRole>();
                db.CreateTable<DbSecurityApplicationPolicy>();
                db.CreateTable<DbSecurityDevicePolicy>();

                // Anonymous user
                db.Insert(new DbSecurityUser()
                {
                    Key = Guid.Parse("C96859F0-043C-4480-8DAB-F69D6E86696C"),
                    Password = "XXX",
                    SecurityHash = "XXX",
                    Lockout = DateTime.MaxValue,
                    UserName = "ANONYMOUS",
                    CreationTime = DateTime.Now,
                    CreatedByKey = Guid.Empty
                });
                // System user
                db.Insert(new DbSecurityUser()
                {
                    Key = Guid.Parse("fadca076-3690-4a6e-af9e-f1cd68e8c7e8"),
                    Password = "XXXX",
                    SecurityHash = "XXXX",
                    Lockout = DateTime.MaxValue,
                    UserName = "SYSTEM",
                    CreationTime = DateTime.Now,
                    CreatedByKey = Guid.Empty
                });

                
                tracer.TraceInfo("Installing Entity Tables...");
                db.CreateTable<DbEntity>();
                db.CreateTable<DbApplicationEntity>();
                db.CreateTable<DbDeviceEntity>();
                db.CreateTable<DbEntityAddress>();
                db.CreateTable<DbEntityAddressComponent>();
                db.CreateTable<DbEntityName>();
                db.CreateTable<DbEntityNameComponent>();
                db.CreateTable<DbEntityRelationship>();
                db.CreateTable<DbMaterial>();
                db.CreateTable<DbManufacturedMaterial>();
                db.CreateTable<DbOrganization>();
                db.CreateTable<DbPerson>();
                db.CreateTable<DbPlace>();
                db.CreateTable<DbTelecomAddress>();
                db.CreateTable<DbEntityTag>();
                db.CreateTable<DbUserEntity>();
                db.CreateTable<DbPlaceService>();
                db.CreateTable<DbPersonLanguageCommunication>();
                db.CreateTable<DbAuthorityScope>();
                db.CreateTable<DbPhoneticValue>();
                db.CreateTable<DbAddressValue>();

                tracer.TraceInfo("Installing Role Tables...");
                db.CreateTable<DbPatient>();
                db.CreateTable<DbProvider>();

                tracer.TraceInfo("Installing Act Tables...");
                db.CreateTable<DbAct>();
                db.CreateTable<DbObservation>();
                db.CreateTable<DbCodedObservation>();
                db.CreateTable<DbQuantityObservation>();
                db.CreateTable<DbTextObservation>();
                db.CreateTable<DbSubstanceAdministration>();
                db.CreateTable<DbPatientEncounter>();
                db.CreateTable<DbControlAct>();
                db.CreateTable<DbActRelationship>();
                db.CreateTable<DbActTag>();
                db.CreateTable<DbActParticipation>();

                if (!silent)
                {
                    tracer.TraceInfo("Initializing Data & Views...");

                    // Run SQL Script
                    string[] resourceSql = {
                    "SanteDB.DisconnectedClient.SQLite.Sql.000_init_santedb_algonquin.sql",
                    "SanteDB.DisconnectedClient.SQLite.Sql.001_init_santedb_core_data.sql"
                };


                    foreach (var sql in resourceSql)
                    {
                        tracer.TraceInfo("Deploying {0}", sql);
                        ApplicationContext.Current.SetProgress(sql, 0);

                        using (StreamReader sr = new StreamReader(typeof(InitialCatalog).GetTypeInfo().Assembly.GetManifestResourceStream(sql)))
                        {
                            var stmts = sr.ReadToEnd().Split(';').Select(o => o.Trim()).ToArray();
                            for (int i = 0; i < stmts.Length; i++)
                            {
                                var stmt = stmts[i];
                                if (i % 10 == 0)
                                    ApplicationContext.Current.SetProgress(Strings.locale_setting_deploy, (float)i / stmts.Length);
                                if (String.IsNullOrEmpty(stmt)) continue;
                                tracer.TraceVerbose("EXECUTE: {0}", stmt);
                                db.Execute(stmt);
                            }
                        }
                    }
                }


                //ApplicationContext.Current.GetService<ISynchronizationService>().PullCompleted += (o, e) => {
                //    if (e.IsInitial && !ApplicationContext.Current.GetService<ISynchronizationService>().IsSynchronizing)
                //    {
                //        ApplicationContext.Current.SetProgress("Indexing...", 0.5f);
                //        try
                //        {
                //            tracer.TraceInfo("Will apply default indexes to Address and Name tables");
                //            db.CreateIndex<DbPhoneticValue>(x => x.Value);
                //            db.CreateIndex<DbAddressValue>(x => x.Value);
                //        }
                //        catch(Exception ex)
                //        {
                //            tracer.TraceError("Error creating primary indexes: {0}", ex.ToString());
                //        }
                //    }
                //};

            }
            catch (Exception e)
            {
                tracer.TraceError("Error deploying InitialCatalog: {0}", e);
                throw new System.Data.DataException("Error deploying InitialCatalog", e);
            }
            return true;
        }


        /// <summary>
        /// Configuration identifier
        /// </summary>
        /// <value>The identifier.</value>
        public string Id
        {
            get
            {
                return "000-init-santedb-algonquin";
            }
        }

        /// <summary>
        /// A human readable description of the migration
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get
            {
                return "SanteDB Mobile Algonquin (0.1.0.0) data model";
            }
        }


        #endregion
    }
}


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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Mail;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Mail;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Synchronization.Model;
using System;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Initial queue catalog migration.
    /// </summary>
    public class InitialQueueCatalog : IDataMigration
    {
        private Tracer tracer = Tracer.GetTracer(typeof(InitialQueueCatalog));

        #region IDbMigration implementation
        /// <summary>
        /// Install the migration package
        /// </summary>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());
            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName));
            using (db.Lock())
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

                tracer.TraceInfo("Installing queues...");

                db.CreateTable<InboundQueueEntry>();
                db.CreateTable<OutboundQueueEntry>();
                db.CreateTable<OutboundAdminQueueEntry>();
                db.CreateTable<DeadLetterQueueEntry>();
                db.CreateTable<SynchronizationLogEntry>();
                db.CreateTable<SynchronizationQuery>();

                var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

                try
                {
                    new LocalMailService()?.Save(new MailMessage()
                    {
                        Body = Strings.locale_welcomeMessageBody,
                        From = "SanteDB Team",
                        Flags = MailMessageFlags.None,
                        Subject = Strings.locale_welcomeMessageSubject,
                        TimeStamp = DateTime.Now,
                        RcptToXml = new System.Collections.Generic.List<Guid>()
                        {
                            Guid.Parse(AuthenticationContext.SystemUserSid)
                        }
                    });
                }
                catch (Exception e)
                {
#if DEBUG
                    this.tracer.TraceError("Unable to generate welcome message: {0}", e.StackTrace);
#endif
                    this.tracer.TraceError("Unable to generate welcome message: {0}", e.Message);
                }
            }

            return true;
        }
        /// <summary>
        /// Gets the identifier of the migration
        /// </summary>
        /// <value>The identifier.</value>
        public string Id
        {
            get
            {
                return "000-init-santedb-algonquin-queue";
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
                return "SanteDB offline sync queue catalog";
            }
        }
        #endregion
    }
}


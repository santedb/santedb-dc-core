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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Security.Audit.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit
{
    /// <summary>
    /// A job which prunes the audit log
    /// </summary>
    public class SQLiteAuditPruneJob : IJob
    {

        /// <summary>
        /// Get the unique identifier for this job
        /// </summary>
        public Guid Id => Guid.Parse("5059999F-3488-4A00-BD80-B5E36B3CE83F");

        // Trace writer for the job
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteAuditPruneJob));

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Audit Retention Job";

        /// <inheritdoc/>
        public string Description => "Prunes old audit events from the dCDR database";

        /// <summary>
        /// Can cancel?
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Gets the current state
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Parameters
        /// </summary>
        public IDictionary<string, Type> Parameters => null;

        /// <summary>
        /// When the job was last run
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// When the job was last finished
        /// </summary>
        public DateTime? LastFinished { get; private set; }

        /// <summary>
        /// Cancel the job
        /// </summary>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Run the audit prune job
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            var config = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();

            try
            {
                this.CurrentState = JobStateType.Running;
                this.LastStarted = DateTime.Now;

                this.m_tracer.TraceInfo("Prune audits older than {0}", config?.AuditRetention);
                if (config?.AuditRetention == null) return; // keep audits forever

                var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(
                    "santeDbAudit"
                ));

                using (conn.Lock())
                {
                    try
                    {
                        conn.BeginTransaction();
                        DateTime cutoff = DateTime.Now.Subtract(config.AuditRetention);
                        Expression<Func<DbAuditData, bool>> epred = o => o.CreationTime < cutoff;
                        conn.Table<DbAuditData>().Delete(epred);

                        // Delete objects
                        conn.Execute($"DELETE FROM {conn.GetMapping<DbAuditObject>().TableName} WHERE NOT({conn.GetMapping<DbAuditObject>().FindColumnWithPropertyName(nameof(DbAuditObject.AuditId)).Name} IN " +
                            $"(SELECT {conn.GetMapping<DbAuditData>().FindColumnWithPropertyName(nameof(DbAuditData.Id)).Name} FROM {conn.GetMapping<DbAuditData>().TableName})" +
                            ")");

                        conn.Commit();
                        this.LastFinished = DateTime.Now;
                        this.CurrentState = JobStateType.Completed;
                    }
                    catch (Exception ex)
                    {
                        ApplicationServiceContext.Current.GetService<ITickleService>().SendTickle(new Tickler.Tickle(Guid.Empty, Tickler.TickleType.Danger, String.Format(Strings.err_prune_audit_failed, ex.Message)));
                        this.CurrentState = JobStateType.Cancelled;
                        conn.Rollback();
                    }
                }

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error pruning audit database: {0}", ex);
                this.CurrentState = JobStateType.Aborted;
            }
        }
    }
}

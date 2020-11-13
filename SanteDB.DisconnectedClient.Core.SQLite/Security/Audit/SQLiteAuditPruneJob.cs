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
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Security.Audit
{
    /// <summary>
    /// A job which prunes the audit log
    /// </summary>
    public class SQLiteAuditPruneJob : IJob
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteAuditPruneJob));

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "Audit Retention Job";

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
        public DateTime? LastFinished { get; private set;}

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
                    catch(Exception ex)
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

using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Backup
{
    /// <summary>
    /// Represents a backup job
    /// </summary>
    public class DefaultBackupJob : IJob
    {

        /// <summary>
        /// Job is starting
        /// </summary>
        public DefaultBackupJob()
        {
            this.CurrentState = JobStateType.NotRun;
        }

        /// <summary>
        /// Tracer for backup job
        /// </summary>
        private Tracer m_tracer = Tracer.GetTracer(typeof(DefaultBackupJob));

        /// <summary>
        /// Gets the name of the job
        /// </summary>
        public string Name => "System Automatic Backup";

        /// <summary>
        /// Can cancel the job?
        /// </summary>
        public bool CanCancel => false;

        /// <summary>
        /// Gets the current state of the job
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Gets the parameters
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <summary>
        /// Last time the backup was started
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// Last time the backup finished
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
        /// Run the backup
        /// </summary>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                AuthenticationContext.Current = new AuthenticationContext(AuthenticationContext.SystemPrincipal);
                this.LastStarted = DateTime.Now;
                this.CurrentState = JobStateType.Running;

                var backupService = ApplicationServiceContext.Current.GetService<IBackupService>();
                var maxBackups = Int32.Parse(ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetAppSetting("autoBackup.max") ?? "5");

                // First attempt to backup
                backupService.Backup(BackupMedia.Private, ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName);

                // Now are there more backups than we like to retain?
                foreach (var descriptor in backupService.GetBackups(BackupMedia.Private).Skip(maxBackups)) {
                    this.m_tracer.TraceInfo("Retention of backups from backup job will remove {0}", descriptor);
                    backupService.RemoveBackup(BackupMedia.Private, descriptor);
                }
                    
                this.CurrentState = JobStateType.Completed;
                this.LastFinished = DateTime.Now;
                
            }
            catch(Exception ex)
            {
                this.m_tracer.TraceError("Error running backup job: {0}", ex);
                this.CurrentState = JobStateType.Aborted;
            }
        }
    }
}

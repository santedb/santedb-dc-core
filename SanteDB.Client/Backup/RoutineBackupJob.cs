/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2024-1-29
 */
using SanteDB.Client.Configuration;
using SanteDB.Client.Tickles;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.Backup
{
    /// <summary>
    /// Represents a <see cref="IJob"/> that runs regular backups of the solution
    /// </summary>
    public class RoutineBackupJob : IJob
    {

        /// <summary>
        /// Job identifier
        /// </summary>
        public static readonly Guid JOB_ID = Guid.Parse("17987F37-451B-4099-B66C-F53F3186547A");
        private readonly ILocalizationService m_localizationService;
        private readonly ITickleService m_tickleService;
        private readonly int m_maxBackups;
        private readonly IUpstreamManagementService m_upstreamManagementService;
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly IBackupService m_backupService;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(RoutineBackupJob));

        /// <summary>
        /// DI ctor
        /// </summary>
        public RoutineBackupJob(IConfigurationManager configurationManager,
            IUpstreamManagementService upstreamManagementService,
            IJobStateManagerService jobStateManagerService,
            ILocalizationService localizationService,
            ITickleService tickleService,
            IBackupService backupService)
        {
            m_localizationService = localizationService;
            m_tickleService = tickleService;
            m_maxBackups = configurationManager.GetSection<ClientConfigurationSection>().MaxAutoBackups;
            m_upstreamManagementService = upstreamManagementService;
            if (m_maxBackups == 0) { m_maxBackups = 5; }

            m_jobStateManager = jobStateManagerService;
            m_backupService = backupService;
            if (m_backupService is IReportProgressChanged irpc)
            {
                irpc.ProgressChanged += (o, e) => m_jobStateManager.SetProgress(this, e.State, e.Progress);
            }
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "Routine Backup Job";

        /// <inheritdoc/>
        public string Description => "Regularly backs up the application data directory";

        /// <inheritdoc/>
        public bool CanCancel => false;

        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters => new Dictionary<string, Type>();

        /// <inheritdoc/>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    m_jobStateManager.SetState(this, JobStateType.Running);

                    // Backup this device
                    m_tracer.TraceInfo("Performing routine system backup - ");
                    m_backupService.Backup(BackupMedia.Private, m_upstreamManagementService.GetSettings().LocalDeviceName);

                    // Remove any unnecessary backups
                    foreach (var backup in m_backupService.GetBackupDescriptors(BackupMedia.Private).Skip(m_maxBackups))
                    {
                        m_tracer.TraceInfo("Removing old backup {0}", backup);
                        m_backupService.RemoveBackup(BackupMedia.Private, backup.Label);
                    }

                    m_jobStateManager.SetState(this, JobStateType.Completed);
                    m_tickleService.SendTickle(new Tickle(Guid.Empty, TickleType.Toast | TickleType.Task, m_localizationService.GetString(UserMessageStrings.BACKUP_COMPLETE)));
                }
            }
            catch (Exception ex)
            {
                m_tracer.TraceError("Error running backup job: {0}", ex.ToHumanReadableString());
                m_jobStateManager.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                m_tickleService.SendTickle(new Tickle(Guid.Empty, TickleType.Danger, m_localizationService.GetString(UserMessageStrings.BACKUP_ERROR, new { error = ex.ToHumanReadableString() })));
            }
        }
    }
}

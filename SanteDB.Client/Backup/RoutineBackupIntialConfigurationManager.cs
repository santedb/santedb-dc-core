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
using SanteDB.Core;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;

namespace SanteDB.Client.Backup
{
    /// <summary>
    /// Initial configuration manager for setting up backups
    /// </summary>
    public class RoutineBackupIntialConfigurationManager : IInitialConfigurationProvider
    {

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(RoutineBackupIntialConfigurationManager));

        /// <inheritdoc/>
        public int Order => Int32.MaxValue;

        /// <inheritdoc/>
        public SanteDBConfiguration Provide(SanteDBHostType hostContextType, SanteDBConfiguration configuration)
        {
            this.m_tracer.TraceInfo("Configuring RoutineBackupJob...");
            var jobConfig = configuration.GetSection<JobConfigurationSection>();
            if (jobConfig == null)
            {
                jobConfig = configuration.AddSection(new JobConfigurationSection() { Jobs = new List<JobItemConfiguration>() });
            }

            switch (hostContextType)
            {
                case SanteDBHostType.Client:
                case SanteDBHostType.Gateway:
                case SanteDBHostType.Debugger:
                    jobConfig.Jobs.Add(new JobItemConfiguration()
                    {
                        Type = typeof(RoutineBackupJob),
                        Schedule = new List<JobItemSchedule>() { new JobItemSchedule(new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }, DateTime.MinValue, null) },
                        StartType = Core.Jobs.JobStartType.DelayStart
                    });
                    break;
                default:
                    jobConfig.Jobs.Add(new JobItemConfiguration()
                    {
                        Type = typeof(RoutineBackupJob),
                        Schedule = new List<JobItemSchedule>() { new JobItemSchedule(new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }, DateTime.MinValue, null) },
                        StartType = Core.Jobs.JobStartType.Never
                    });
                    break;
            }

            // Configure the backup locations
            var backupConfig = configuration.GetSection<BackupConfigurationSection>();
            if (backupConfig == null)
            {
                backupConfig = configuration.AddSection(new BackupConfigurationSection());
            }


            backupConfig.PrivateBackupLocation = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "backup");
            backupConfig.PublicBackupLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".santedb", "backup");
            backupConfig.RequireEncryptedBackups = true;
            return configuration;
        }
    }
}

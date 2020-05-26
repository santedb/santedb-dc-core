/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Gets or sets the backup media
    /// </summary>
    public enum BackupMedia
    {
        ExternalPublic,
        Public,
        Private
    }

    /// <summary>
    /// Represents a service that can back-up data to/from another location
    /// </summary>
    public interface IBackupService : IServiceImplementation
    {

        /// <summary>
        /// Backup media
        /// </summary>
        void Backup(BackupMedia media, String password = null);

        /// <summary>
        /// Restore from media
        /// </summary>
        void Restore(BackupMedia media, String backupDescriptor = null, String password = null);

        /// <summary>
        /// Has backup on the specified media
        /// </summary>
        bool HasBackup(BackupMedia media);

        /// <summary>
        /// Gets the backup descriptors for the specified media
        /// </summary>
        IEnumerable<String> GetBackups(BackupMedia media);

        /// <summary>
        /// Remove a backup
        /// </summary>
        void RemoveBackup(BackupMedia media, String backupDescriptor);
    }
}

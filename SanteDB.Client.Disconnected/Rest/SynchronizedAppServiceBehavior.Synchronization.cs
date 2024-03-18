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
 * Date: 2023-6-21
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// Application service behavior for synchronization log management
    /// </summary>
    public partial class SynchronizedAppServiceBehavior
    {

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.LoginAsService)]
        public List<ISynchronizationLogEntry> GetSynchronizationLogs()
        {
            return m_synchronizationLogService?.GetAll().ToList();
        }

        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void ResetSynchronizationStatus()
        {
            foreach (var entry in m_synchronizationLogService.GetAll())
            {
                m_synchronizationLogService.Delete(entry);
            }

        }

        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public void ResetSynchronizationStatus(string entryId)
        {
            if (Guid.TryParse(entryId, out var uuid))
            {
                var logEntry = m_synchronizationLogService.GetAll().FirstOrDefault(o => o.Key == uuid) ?? throw new KeyNotFoundException();
                m_synchronizationLogService.Delete(logEntry);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.INVALID_FORMAT, entryId, Guid.Empty));
            }
        }


    }
}

using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Security;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        [Demand(PermissionPolicyIdentifiers.LoginAsService)]
        public void ResetSynchronizationStatus(ParameterCollection parameters)
        {
            if (parameters.TryGet("resourceType", out string resourcetype))
            {
                foreach(var entry in m_synchronizationLogService.GetAll())
                {
                    if (resourcetype?.Equals(entry.ResourceType, StringComparison.InvariantCulture) == true)
                    {
                        m_synchronizationLogService.Delete(entry);
                    }
                }
            }
            else
            {
                foreach(var entry in m_synchronizationLogService.GetAll())
                {
                    m_synchronizationLogService.Delete(entry);
                }
            }
        }


        /// <inheritdoc />
        [Demand(PermissionPolicyIdentifiers.LoginAsService)]
        public void SynchronizeNow(ParameterCollection parameters)
        {
            m_tracer.TraceInfo("Manual Synchronization requested.");
            void push_completed(object sender, EventArgs e)
            {
                try
                {
                    m_tracer.TraceVerbose("Push Completed event fired. Executing manual pull.");
                    m_synchronizationService.PushCompleted -= push_completed;
                    m_synchronizationService.Pull(Core.Model.Subscription.SubscriptionTriggerType.Manual);
                }
                finally {
                    m_synchronizationService.PushCompleted -= push_completed;
                }
            };

            m_synchronizationService.PushCompleted += push_completed;
            m_synchronizationService.Push();
        }

    }
}

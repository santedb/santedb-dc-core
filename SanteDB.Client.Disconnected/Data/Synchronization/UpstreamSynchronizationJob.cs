/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Represents a synchronization job which pulls data and pushes data to the remote
    /// </summary>
    public class UpstreamSynchronizationJob : IJob
    {
        readonly Tracer _Tracer;

        private static Guid s_JobGuid = Guid.Parse("E511689A-98E1-47CD-8933-6A8CEF8AE014");

        readonly SynchronizationConfigurationSection _ConfigurationSection;
        readonly ISynchronizationService _Service;
        readonly IJobStateManagerService _JobStateManager;
        readonly ISynchronizationLogService _LogService;
        readonly ISynchronizationQueueManager _QueueManager;


        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamSynchronizationJob(IConfigurationManager configurationManager, IJobStateManagerService jobStateManager, ISynchronizationService synchronizationService, ISynchronizationLogService synchronizationLogService, ISynchronizationQueueManager synchronizationQueueManager)
        {
            _Tracer = new Tracer(nameof(UpstreamSynchronizationJob));
            _ConfigurationSection = configurationManager.GetSection<SynchronizationConfigurationSection>();
            _JobStateManager = jobStateManager;
            _Service = synchronizationService;
            _LogService = synchronizationLogService;
            _QueueManager = synchronizationQueueManager;
        }

        /// <inheritdoc />
        public Guid Id => s_JobGuid;

        /// <inheritdoc />
        public string Name => "Upstream Synchronization Job";

        /// <inheritdoc />
        public string Description => "Periodically synchronizes data from an upstream realm to the local instance.";

        /// <inheritdoc />
        public bool CanCancel => false;

        /// <inheritdoc />
        public IDictionary<string, Type> Parameters => null;

        /// <inheritdoc />
        public void Cancel()
        {
        }

        /// <inheritdoc />
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                if (_JobStateManager.GetJobState(this).IsRunning())
                {
                    _Tracer.TraceVerbose($"Attempt to run {nameof(UpstreamSynchronizationJob)} when it is already running.");
                    return;
                }

                _JobStateManager.SetState(this, JobStateType.Running);
                _Service.Pull(SubscriptionTriggerType.PeriodicPoll);
                _JobStateManager.SetState(this, JobStateType.Completed);

            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                _Tracer.TraceError("Error running Synchronization Job: {0}", ex);
                _JobStateManager.SetState(this, JobStateType.Aborted);
            }
        }
    }
}

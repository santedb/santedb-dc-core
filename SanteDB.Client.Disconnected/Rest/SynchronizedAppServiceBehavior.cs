/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Rest.AppService;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// An <see cref="AppServiceBehavior"/> which is extended to include sycnrhonization calls
    /// </summary>
    public partial class SynchronizedAppServiceBehavior : AppServiceBehavior, ISynchronizedAppServiceContract
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(SynchronizedAppServiceBehavior));

        /// <summary>
        /// The injected synchronization queue manager
        /// </summary>
        protected readonly ISynchronizationQueueManager m_synchronizationQueueManager;
        /// <summary>
        /// The injected synchornization controller server
        /// </summary>
        protected readonly ISynchronizationService m_synchronizationService;
        /// <summary>
        /// The injected synchronization logging server
        /// </summary>
        protected readonly ISynchronizationLogService m_synchronizationLogService;

        /// <summary>
        /// Synchronized app service behavior CTOR
        /// </summary>
        public SynchronizedAppServiceBehavior() :
            this(ApplicationServiceContext.Current.GetService<ISynchronizationQueueManager>(),
                  ApplicationServiceContext.Current.GetService<ISynchronizationService>(),
                  ApplicationServiceContext.Current.GetService<ISynchronizationLogService>())
        {
        }

        /// <summary>
        /// Synchronized app service behavior
        /// </summary>
        public SynchronizedAppServiceBehavior(ISynchronizationQueueManager synchronizationQueueManager, ISynchronizationService synchronizationService, ISynchronizationLogService synchronizationLogService)
        {
            this.m_synchronizationQueueManager = synchronizationQueueManager;
            this.m_synchronizationService = synchronizationService;
            this.m_synchronizationLogService = synchronizationLogService;
        }
    }
}

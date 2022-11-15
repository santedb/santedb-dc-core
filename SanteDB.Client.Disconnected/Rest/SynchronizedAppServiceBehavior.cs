using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Rest.AppService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Rest
{
    /// <summary>
    /// An <see cref="AppServiceBehavior"/> which is extended to include sycnrhonization calls
    /// </summary>
    public partial class SynchronizedAppServiceBehavior : AppServiceBehavior, ISynchronizedAppServiceContract
    {
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(SynchronizedAppServiceBehavior));
        protected readonly ISynchronizationQueueManager m_synchronizationQueueManager;
        protected readonly ISynchronizationService m_synchronizationService;
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

﻿using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Diagnostics.Performance
{

    /// <summary>
    /// Represents a thread pool performance counter
    /// </summary>
    public class ThreadPoolPerformanceProbe : ICompositeDiagnosticsProbe
    {


        // Performance counters
        private IDiagnosticsProbe[] m_performanceCounters =
        {
            new NonPooledWorkersProbe(),
            new PoolConcurrencyProbe(),
            new ErroredWorkersProbe(),
            new PooledWorkersProbe()
        };

        /// <summary>
        /// Generic performance counter
        /// </summary>
        private class NonPooledWorkersProbe : DiagnosticsProbeBase<int>
        {
            /// <summary>
            /// Non pooled workers counter
            /// </summary>
            public NonPooledWorkersProbe() : base("ThreadPool: Non-Pooled Workers", "Shows the number of active threads which are not in the thread pool")
            {

            }

            /// <summary>
            /// Gets the identifier for the pool
            /// </summary>
            public override Guid Uuid => PerformanceConstants.ThreadPoolNonQueuedWorkerCounter;

            /// <summary>
            /// Gets the value
            /// </summary>
            public override int Value => ApplicationServiceContext.Current.GetService<SanteDBThreadPool>().NonQueueThreads;

        }

        /// <summary>
        /// Generic performance counter
        /// </summary>
        private class PooledWorkersProbe : DiagnosticsProbeBase<int>
        {
            /// <summary>
            /// Non pooled workers counter
            /// </summary>
            public PooledWorkersProbe() : base("ThreadPool: Pooled Workers", "Shows the number of active threads in the thread pool")
            {

            }

            /// <summary>
            /// Gets the identifier for the pool
            /// </summary>
            public override Guid Uuid => PerformanceConstants.ThreadPoolWorkerCounter;

            /// <summary>
            /// Gets the value
            /// </summary>
            public override int Value => ApplicationServiceContext.Current.GetService<SanteDBThreadPool>().ActiveThreads;

        }

        /// <summary>
        /// Generic performance counter
        /// </summary>
        private class PoolConcurrencyProbe : DiagnosticsProbeBase<int>
        {
            /// <summary>
            /// Non pooled workers counter
            /// </summary>
            public PoolConcurrencyProbe() : base("ThreadPool: Thread pool size", "Shows the total number of threads which are allocated to the thread pool")
            {

            }

            /// <summary>
            /// Gets the identifier for the pool
            /// </summary>
            public override Guid Uuid => PerformanceConstants.ThreadPoolConcurrencyCounter;


            /// <summary>
            /// Gets the value
            /// </summary>
            public override int Value => ApplicationServiceContext.Current.GetService<SanteDBThreadPool>().Concurrency;

        }

        /// <summary>
        /// Generic performance counter
        /// </summary>
        private class ErroredWorkersProbe : DiagnosticsProbeBase<int>
        {
            /// <summary>
            /// Non pooled workers counter
            /// </summary>
            public ErroredWorkersProbe() : base("ThreadPool: Worker Errors", "Shows the total number of workers that didn't successfully complete due to an uncaught exception")
            {

            }

            /// <summary>
            /// Gets the identifier for the pool
            /// </summary>
            public override Guid Uuid => PerformanceConstants.ThreadPoolErrorWorkerCounter;

            /// <summary>
            /// Gets the value
            /// </summary>
            public override int Value => 0;

        }

        /// <summary>
        /// Get the UUID of the thread pool
        /// </summary>
        public Guid Uuid => PerformanceConstants.ThreadPoolPerformanceCounter;

        /// <summary>
        /// Gets the value of the 
        /// </summary>
        public IEnumerable<IDiagnosticsProbe> Value => this.m_performanceCounters;

        /// <summary>
        /// Gets thename of hte composite
        /// </summary>
        public string Name => "Thread Pool";

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => "The primary SanteDB thread pool performance monitor";

        /// <summary>
        /// Gets the type of the performance counter
        /// </summary>
        public Type Type => typeof(Array);

        /// <summary>
        /// Gets the value
        /// </summary>
        object IDiagnosticsProbe.Value => this.Value;
    }
}

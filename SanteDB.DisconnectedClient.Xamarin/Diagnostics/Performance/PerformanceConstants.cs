using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Diagnostics.Performance
{
    /// <summary>
    /// Constants used for performance 
    /// </summary>
    static class PerformanceConstants
    {

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ThreadPoolPerformanceCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A480");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ThreadPoolConcurrencyCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A481");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ThreadPoolWorkerCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A482");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ThreadPoolNonQueuedWorkerCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A483");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ThreadPoolErrorWorkerCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A484");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid MachinePerformanceCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A485");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid ProcessorUseCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A486");

        /// <summary>
        /// Gets the thread pooling performance counter
        /// </summary>
        public static readonly Guid MemoryUseCounter = new Guid("9E77D692-1F71-4442-BDA1-056D3DB1A487");
    }
}

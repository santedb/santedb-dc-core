﻿using SanteDB.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Diagnostics.Performance
{
    /// <summary>
    /// A machine performance probe
    /// </summary>
    public class MachinePerformanceProbe : ICompositeDiagnosticsProbe
    {

        private IDiagnosticsProbe[] m_values =
        {
            new WindowsPerformanceCounterProbe(PerformanceConstants.ProcessorUseCounter, "Machine: CPU Utilization", "Shows the % of active time for CPU", "Processor Information", "% Processor Time", "_Total"),
            new WindowsPerformanceCounterProbe(PerformanceConstants.MemoryUseCounter, "Machine: Memory Use", "Shows the amount of memory used", "Memory", "% Committed Bytes In Use", null)
        };


        /// <summary>
        /// Gets the value of this probe
        /// </summary>
        public IEnumerable<IDiagnosticsProbe> Value => this.m_values;

        /// <summary>
        /// Gets the identifier of this counter
        /// </summary>
        public Guid Uuid => PerformanceConstants.MachinePerformanceCounter;

        /// <summary>
        /// Gets the name of this counter
        /// </summary>
        public string Name => "Machine";

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description => "Shows metrics related to the server host environment";

        /// <summary>
        /// Gets the type of measure
        /// </summary>
        public Type Type => typeof(Array);

        /// <summary>
        /// Gets the value
        /// </summary>
        object IDiagnosticsProbe.Value => this.Value;
    }
}

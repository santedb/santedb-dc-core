using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Services
{
    /// <summary>
    /// Represents a service that can provide auto-recovery services
    /// </summary>
    public interface IAutoRecoveryProviderService : IServiceImplementation
    {

        /// <summary>
        /// Gets the dates of the recovery points
        /// </summary>
        IEnumerable<DateTime> GetRecoveryPoints();

        /// <summary>
        /// Restores the recovery point
        /// </summary>
        void RestoreRecoveryPoint(DateTime recoveryDate);

        /// <summary>
        /// Create a recovery point of the current data
        /// </summary>
        void CreateRecoveryPoint();

    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
{
    /// <summary>
    /// The type of synchronization event that is trigger a pull/push
    /// </summary>
    public enum SynchronizationTriggerEvent
    {
        /// <summary>
        /// There is no event which should trigger or did trigger the event
        /// </summary>
        None = 0x0,
        /// <summary>
        /// All events trigger a synchronization event
        /// </summary>
        All = OnStart | OnCommit | OnStop |  OnNetworkChange | PeriodicPoll,
        /// <summary>
        /// The starting of the application triggers the synchronization event
        /// </summary>
        OnStart = 0x01,
        /// <summary>
        /// The committing of data triggers the synchronization event
        /// </summary>
        OnCommit = 0x02,
        /// <summary>
        /// The shutdown of the application triggers the synchronization event
        /// </summary>
        OnStop = 0x04,
        /// <summary>
        /// Whenever the network status changes
        /// </summary>
        OnNetworkChange = 0x08,
        /// <summary>
        /// Whenever a periodic polling event occurs
        /// </summary>
        PeriodicPoll = 0x10,
        /// <summary>
        /// Whenever a manual request to synchronize is triggered
        /// </summary>
        Manual = 0x20
    }
}

using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace SanteDB.Disconnected.Data.Synchronization
{
    /// <summary>
    /// A service which is responsible for executing subscriptions on events with the upstream 
    /// integration service
    /// </summary>
    public interface ISynchronizationService : IServiceImplementation
    {

        /// <summary>
        /// Fired when a pull has completed and imported data
        /// </summary>
        event EventHandler<SynchronizationEventArgs> PullCompleted;

        /// <summary>
        /// Fired when the push has been completed
        /// </summary>
        event EventHandler<SynchronizationEventArgs> PushCompleted;

        /// <summary>
        /// Get whether the service is syncing
        /// </summary>
        bool IsSynchronizing { get; }

        /// <summary>
        /// Fetch to see if there are any particular changes on the specified model type
        /// </summary>
        bool Fetch(Type modelType);

        /// <summary>
        /// Perform a pull with the specified trigger
        /// </summary>
        void Pull(SubscriptionTriggerType trigger);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        int Pull(Type modelType);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        int Pull(Type modelType, NameValueCollection filter);

        /// <summary>
        /// Pull data from the remove server and place it on the inbound queue
        /// </summary>
        int Pull(Type modelType, NameValueCollection filter, bool always);

        /// <summary>
        /// Push data to the server
        /// </summary>
        void Push();
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Represents a class which can handle receiving a P2P trigger message.
    /// </summary>
    /// <remarks>The <see cref="IPeerToPeerShareService"/> should use these classes
    /// to handle the execution of actions which are received by handler</remarks>
    public interface IPeerToPeerTriggerHandler
    {


        /// <summary>
        /// Gets the trigger which this handler can operate on
        /// </summary>
        String[] Triggers { get; }

        /// <summary>
        /// Instructs the trigger handler to execute its application logic
        /// </summary>
        /// <param name="request">The request message which was received</param>
        /// <returns>The response to the request.</returns>
        IPeerToPeerMessage Execute(IPeerToPeerMessage request);
    }
}

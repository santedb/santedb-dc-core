using SanteDB.Core.Model;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Represents a peer to peer sharing service which allows the host to share data between one or more hosts via an appropriate protocol
    /// </summary>
    /// <remarks>This should use the native operating system's BlueTooth operation for data transfer over bluetooth.</remarks>
    public interface IPeerToPeerShareService : IServiceImplementation
    {

        /// <summary>
        /// Fired after the data transfer has completed
        /// </summary>
        event EventHandler<PeerToPeerDataEventArgs> Received;

        /// <summary>
        /// Fired when the data transfer is about to begin
        /// </summary>
        event EventHandler<PeerToPeerEventArgs> Receiving;

        /// <summary>
        /// Fired when the data has been sent
        /// </summary>
        event EventHandler<PeerToPeerDataEventArgs> Sent;

        /// <summary>
        /// Fired when the data is about to be sent
        /// </summary>
        event EventHandler<PeerToPeerDataEventArgs> Sending;

        /// <summary>
        /// Send the specified entity to the remote host
        /// </summary>
        /// <param name="entity">The entity which should be sent</param>
        /// <param name="recipient">The recipient of the data</param>
        /// <returns>The acknowledgement or error from the remote device</returns>
        IdentifiedData Send(String recipient, IdentifiedData entity);

        /// <summary>
        /// Get a list of recipients which this share service can send to
        /// </summary>
        /// <returns>The list of recipients</returns>
        IEnumerable<String> GetRecipientList();

    }

    /// <summary>
    /// Represents an event where peer to peer transfer is being initiated
    /// </summary>
    public class PeerToPeerEventArgs : EventArgs
    {
        /// <summary>
        /// Peer to peer event args
        /// </summary>
        public PeerToPeerEventArgs(String remoteDevice, bool isReceiving)
        {
            this.RemoteDevice = remoteDevice;
            this.RemoteIsInitiator = isReceiving;
        }

        /// <summary>
        /// The recipient of the data
        /// </summary>
        public String RemoteDevice { get; }

        /// <summary>
        /// The initiator of the data is the remote machine
        /// </summary>
        public bool RemoteIsInitiator { get; }

        /// <summary>
        /// Cancel the event
        /// </summary>
        public bool Cancel { get; set; }

    }

    /// <summary>
    /// Represents an event where peer to peer data has been sent or received
    /// </summary>
    public class PeerToPeerDataEventArgs : PeerToPeerEventArgs
    {

        /// <summary>
        /// Peer to peer data event args
        /// </summary>
        public PeerToPeerDataEventArgs(String recipient, bool isReceiving, IdentifiedData resource) : base (recipient, isReceiving)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Gets the object that was sent
        /// </summary>
        IdentifiedData Resource { get; set; } 

    }
}

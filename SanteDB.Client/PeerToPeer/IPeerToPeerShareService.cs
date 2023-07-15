/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 * User: fyfej
 * Date: 2023-5-19
 */
using HeyRed.Mime;
using SanteDB.Core.Model;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.Client.PeerToPeer
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
        /// <param name="recipientNode">The peer the message should be sent to</param>
        /// <param name="message">The payload of the message to be sent</param>
        /// <returns>The acknowledgement or error from the remote device</returns>
        IPeerToPeerMessage Send(IPeerToPeerNode recipientNode, IPeerToPeerMessage message);

        /// <summary>
        /// Get a list of recipients which this share service can send to
        /// </summary>
        /// <returns>The list of recipients</returns>
        IEnumerable<IPeerToPeerNode> GetPairedNodes();

        /// <summary>
        /// Discover recipients that are available for this provider
        /// </summary>
        /// <returns>The list of available recipients</returns>
        IEnumerable<IPeerToPeerNode> DiscoverNodes();

        /// <summary>
        /// Establishes a secure link between this node and another node
        /// </summary>
        /// <param name="node">The other peer to be paired</param>
        /// <param name="authorizingPassword">The password of the user on the remote node which is authorizing this pairing</param>
        /// <param name="authorizingUserName">The user which is authorizing this pairing</param>
        /// <returns>The paired node registration</returns>
        IPeerToPeerNode PairNode(IPeerToPeerNode node, string authorizingUserName, string authorizingPassword);

        /// <summary>
        /// Remove the pairing information for the peer on this device and the remote device
        /// </summary>
        /// <param name="node">The peer to unpair</param>
        /// <param name="authorizingPassword">The password of the user on the remote node which is authorizing this pairing</param>
        /// <param name="authorizingUserName">The user which is authorizing this pairing</param>
        /// <returns>The unpaired peer record</returns>
        IPeerToPeerNode UnpairNode(IPeerToPeerNode node, string authorizingUserName, string authorizingPassword);

        /// <summary>
        /// Remove the pairing information from this node only
        /// </summary>
        /// <param name="node">The node to be removed</param>
        /// <returns>The removed peer record</returns>
        IPeerToPeerNode RemovePairedNode(IPeerToPeerNode node);

        /// <summary>
        /// Gets the local peer registration
        /// </summary>
        IPeerToPeerNode LocalNode { get; }
    }

    /// <summary>
    /// Represents an event where peer to peer transfer is being initiated
    /// </summary>
    public class PeerToPeerEventArgs : EventArgs
    {
        /// <summary>
        /// Peer to peer event args
        /// </summary>
        public PeerToPeerEventArgs(IPeerToPeerNode node, bool remoteIsInitiator)
        {
            RemoteNode = node;
            RemoteIsInitiator = remoteIsInitiator;
        }

        /// <summary>
        /// The recipient of the data
        /// </summary>
        public IPeerToPeerNode RemoteNode { get; }

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
        public PeerToPeerDataEventArgs(IPeerToPeerNode node, bool remoteIsInitiator, IPeerToPeerMessage message) : base(node, remoteIsInitiator)
        {
            Message = message;
        }

        /// <summary>
        /// Gets the object that was sent
        /// </summary>
        IPeerToPeerMessage Message { get; }

    }
}

/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Core.Model;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;

namespace SanteDB.Client.Services
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
        public PeerToPeerDataEventArgs(String recipient, bool isReceiving, IdentifiedData resource) : base(recipient, isReceiving)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Gets the object that was sent
        /// </summary>
        IdentifiedData Resource { get; set; }

    }
}

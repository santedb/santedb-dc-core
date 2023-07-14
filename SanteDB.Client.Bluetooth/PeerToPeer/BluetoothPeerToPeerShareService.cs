using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Model;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using Newtonsoft.Json;
using SanteDB.Client.Bluetooth.PeerToPeer.Messages;
using SanteDB.Client.Exceptions;
using SanteDB.Client.PeerToPeer;
using SanteDB.Client.PeerToPeer.Messages;
using SanteDB.Client.Services;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SanteDB.Client.Bluetooth.PeerToPeer
{
    /// <summary>
    /// An implementation of the <see cref="IPeerToPeerShareService"/> which operates over bluetooth
    /// </summary>
    public class BluetoothPeerToPeerShareService : IPeerToPeerShareService, IDaemonService, IDisposable
    {
        // Upstream manager
        private readonly IUpstreamManagementService m_upstreamManagementService;
        private readonly IDeviceIdentityProviderService m_deviceIdentityProvider;
        private readonly IRepositoryService<SecurityDevice> m_deviceRepository;
        private readonly IOperatingSystemPermissionService m_operatingSystemSecurity;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IThreadPoolService m_threadPoolService;
        private readonly ITfaCodeProvider m_tfaCodeProvider; // TODO: Use a different, more dedicated time based one-time-key provider
        private readonly ManualResetEventSlim m_listenerResetEvent = new ManualResetEventSlim(false); // The MRE that signals between the main thread and the listener thread
        private readonly List<BluetoothPeerNode> m_registeredNodes = new List<BluetoothPeerNode>();

        /// <summary>
        /// Listener thread
        /// </summary>
        private Thread m_listenerThread;

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(BluetoothPeerToPeerShareService));
        // The listener 
        private BluetoothListener m_listener;

        /// <summary>
        /// DI constructor
        /// </summary>
        public BluetoothPeerToPeerShareService(IUpstreamManagementService upstreamManagementService,
            IDeviceIdentityProviderService deviceIdentityProviderService,
            IPolicyEnforcementService pepService,
            IOperatingSystemPermissionService operatingSystemSecurity,
            ITfaCodeProvider tfaCodeProvider,
            IThreadPoolService threadPoolService,
            IRepositoryService<SecurityDevice> securityDeviceRepository)
        {
            this.m_threadPoolService = threadPoolService;
            this.m_tfaCodeProvider = tfaCodeProvider;
            this.m_operatingSystemSecurity = operatingSystemSecurity;
            this.m_upstreamManagementService = upstreamManagementService;
            this.m_deviceIdentityProvider = deviceIdentityProviderService;
            this.m_deviceRepository = securityDeviceRepository;
            this.m_pepService = pepService;
        }

        /// <summary>
        /// Throw if not supported
        /// </summary>
        private void ThrowIfNotSupported()
        {
            if (!this.m_operatingSystemSecurity.HasPermission(OperatingSystemPermissionType.Bluetooth) || BluetoothRadio.Default == null)
                throw new NotSupportedException();
        }

        /// <summary>
        /// True if bluetooth is available and on
        /// </summary>
        private bool IsBluetoothAvailable() => BluetoothRadio.Default?.Mode == RadioMode.Connectable;

        /// <inheritdoc/>
        public IPeerToPeerNode LocalNode
        {
            get
            {
                this.ThrowIfNotSupported();
                return new BluetoothPeerNode(this.m_upstreamManagementService.GetSettings().LocalDeviceSid,
                    BluetoothRadio.Default.Name,
                    BluetoothRadio.Default.LocalAddress);
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "SanteDB dCDR Bluetooth Peer-to-Peer Communication Service";

        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public event EventHandler<PeerToPeerDataEventArgs> Received;
        /// <inheritdoc/>
        public event EventHandler<PeerToPeerEventArgs> Receiving;
        /// <inheritdoc/>
        public event EventHandler<PeerToPeerDataEventArgs> Sent;
        /// <inheritdoc/>
        public event EventHandler<PeerToPeerDataEventArgs> Sending;
        /// <inheritdoc/>
        public event EventHandler Starting;
        /// <inheritdoc/>
        public event EventHandler Started;
        /// <inheritdoc/>
        public event EventHandler Stopping;
        /// <inheritdoc/>
        public event EventHandler Stopped;

        /// <summary>
        /// Get my device identity
        /// </summary>
        /// <returns></returns>
        private IDeviceIdentity GetMyDeviceIdentity() => this.m_deviceIdentityProvider.GetIdentity(this.LocalNode.Uuid);

        /// <inheritdoc/>
        public IEnumerable<IPeerToPeerNode> DiscoverNodes()
        {
            this.ThrowIfNotSupported();
            var knownPeers = this.GetPairedNodes().OfType<BluetoothPeerNode>();
            using (var client = new BluetoothClient())
            {
                return client.PairedDevices.Where(o => o.Authenticated && o.ClassOfDevice.Service.HasFlag(ServiceClass.ObjectTransfer) && !knownPeers.Any(k => k.BluetoothAddress == o.DeviceAddress))
                    .Select(o => new BluetoothPeerNode(Guid.Empty, o.DeviceName, o.DeviceAddress));
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IPeerToPeerNode> GetPairedNodes()
        {
            if (!this.m_registeredNodes.Any())
            {
                foreach (var dev in this.m_deviceRepository.Find(o => o.ObsoletionTime == null))
                {
                    var claims = this.m_deviceIdentityProvider.GetClaims(dev.Name);
                    var btClaim = claims.FirstOrDefault(c => c.Type.Equals(BluetoothConstants.BluetoothDeviceClaim));
                    if (btClaim != null)
                    {
                        var node = JsonConvert.DeserializeObject<BluetoothPeerNode>(btClaim.Value);
                        this.m_registeredNodes.Add(node);
                        yield return node;
                    }
                }
            }
            else
            {
                foreach(var node in this.m_registeredNodes)
                {
                    yield return node;
                }
            }
        }


        /// <inheritdoc/>
        public IPeerToPeerNode PairNode(IPeerToPeerNode node, string authorizingUserName, string authorizingPassword)
        {
            this.ThrowIfNotSupported();
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            else if (String.IsNullOrEmpty(authorizingUserName))
            {
                throw new ArgumentNullException(nameof(authorizingUserName));
            }
            else if (String.IsNullOrEmpty(authorizingPassword))
            {
                throw new ArgumentNullException(nameof(authorizingPassword));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice);

            // Does the pairing information already exist?
            var existingNode = this.GetPairedNodes().FirstOrDefault(p => p.Name == node.Name);
            if (existingNode != null)
            {
                return existingNode;
            }

            // Generate a request
            var requestMessage = BluetoothPeerToPeerMessage.NodePairRequest(authorizingUserName, authorizingPassword, node);
            var response = this.Send(node, requestMessage);
            switch (response.Payload)
            {
                case PeerAcknowledgmentPayload nack: // NACK
                    throw new DetectedIssueException(nack.Details);
                case NodePairingResponse ack:
                    // If the device already exists - then we just want to update it 
                    var existingSid = this.m_deviceIdentityProvider.GetSid(node.Name);
                    if (existingSid == Guid.Empty)
                    {
                        this.m_deviceIdentityProvider.CreateIdentity(node.Name, Guid.NewGuid().ToString(), AuthenticationContext.Current.Principal);
                    }
                    else if (existingSid != ack.Node.Uuid)
                    {
                        throw new InvalidOperationException(String.Format(ErrorMessages.ASSERTION_MISMATCH, ack.Node.Uuid, existingSid));
                    }

                    // Add the claim
                    this.m_deviceIdentityProvider.RemoveClaim(node.Name, BluetoothConstants.BluetoothDeviceClaim, AuthenticationContext.Current.Principal);
                    this.m_deviceIdentityProvider.AddClaim(node.Name, new SanteDBClaim(BluetoothConstants.BluetoothDeviceClaim, JsonConvert.SerializeObject(ack.Node)), AuthenticationContext.Current.Principal);

                    // Add the bluetooth TFA claim
                    this.m_deviceIdentityProvider.RemoveClaim(node.Name, SanteDBClaimTypes.SanteDBRfc4226Secret, AuthenticationContext.Current.Principal);
                    this.m_deviceIdentityProvider.AddClaim(node.Name, new SanteDBClaim(SanteDBClaimTypes.SanteDBRfc4226Secret, JsonConvert.SerializeObject(ack.AuthenticationSecret)), AuthenticationContext.Current.Principal);

                    var tfaCode = this.m_tfaCodeProvider.GenerateTfaCode(this.m_deviceIdentityProvider.GetIdentity(node.Name));
                    response = this.Send(node, BluetoothPeerToPeerMessage.NodePairConfirm(this.LocalNode.Uuid, tfaCode));
                    this.m_registeredNodes.Add(ack.Node);
                    if (response.Payload is PeerAcknowledgmentPayload confirmAck && confirmAck.OutcomeStatus == PeerToPeerAcknowledgementCode.Ok)
                    {
                        return ack.Node;
                    }
                    else
                    {
                        this.m_registeredNodes.Remove(ack.Node);
                        this.m_deviceIdentityProvider.RemoveClaim(node.Name, BluetoothConstants.BluetoothDeviceClaim, AuthenticationContext.Current.Principal);
                        this.m_deviceIdentityProvider.RemoveClaim(node.Name, SanteDBClaimTypes.SanteDBRfc4226Secret, AuthenticationContext.Current.Principal);
                        throw new InvalidOperationException(String.Format(ErrorMessages.ASSERTION_MISMATCH, PeerToPeerAcknowledgementCode.Ok, PeerToPeerAcknowledgementCode.Error));
                    }
                default:
                    throw new InvalidOperationException(String.Format(ErrorMessages.INVALID_FORMAT, response.Payload, nameof(NodePairingResponse)));
            }
        }

        /// <inheritdoc/>
        public IPeerToPeerMessage Send(IPeerToPeerNode recipientNode, IPeerToPeerMessage message)
        {
            this.ThrowIfNotSupported();

            if (recipientNode == null || !(recipientNode is BluetoothPeerNode btNode))
            {
                throw new ArgumentNullException(nameof(recipientNode));
            }
            else if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            else if (!this.GetPairedNodes().Any(o => recipientNode.Uuid == o.Uuid))
            {
                throw new InvalidOperationException(ErrorMessages.PEER_UNKNOWN);
            }
            try
            {

                var eventArgs = new PeerToPeerDataEventArgs(recipientNode, false, message);
                this.Sending?.Invoke(this, eventArgs);
                if (eventArgs.Cancel)
                {
                    this.m_tracer.TraceWarning("Pre-event hook indicates cancel");
                    return null;
                }

                using (var client = new BluetoothClient())
                {
                    client.Encrypt = true;
                    client.Authenticate = true;
                    client.Connect(btNode.BluetoothAddress, BluetoothConstants.P2P_SERVICE_ID);
                    var stream = client.GetStream();
                    try
                    {
                        if (stream == null || !client.Connected)
                        {
                            throw new InvalidOperationException(ErrorMessages.PEER_NOT_CONNECTED);
                        }

                        var oneTimeKey = this.GetOneTimeKey(this.LocalNode);
                        message.OriginNode = this.LocalNode.Uuid;
                        message.DestinationNode = recipientNode.Uuid;
                        PeerToPeerUtils.WriteMessage(message, stream, PeerTransferEncodingFlags.Compressed, oneTimeKey);
                        var response = PeerToPeerUtils.ReadMessage<BluetoothPeerToPeerMessage>(stream, oneTimeKey, false);

                        if(response.DestinationNode != this.LocalNode.Uuid)
                        {
                            throw new InvalidOperationException(String.Format(ErrorMessages.ASSERTION_MISMATCH, this.LocalNode.Uuid, response.DestinationNode));
                        }
                        return response;
                    }
                    finally
                    {
                        stream?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error sending data to {0} - {1}", recipientNode, e);
                throw new PeerToPeerException(this.LocalNode, recipientNode, message.TriggerEvent, e); 
            }
        }

        /// <summary>
        /// Get the one time key to communicate with the device identity
        /// </summary>
        private byte[] GetOneTimeKey(IPeerToPeerNode node) {
            
            if(node is BluetoothPeerNode btp)
            {
                var deviceIdentity = this.m_deviceIdentityProvider.GetIdentity(btp.Uuid);
                return SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(this.m_tfaCodeProvider.GenerateTfaCode(deviceIdentity)));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc/>
        public IPeerToPeerNode UnpairNode(IPeerToPeerNode node, String authorizingUserName, String authorizingPassword)
        {
            this.ThrowIfNotSupported();
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            else if(!this.GetPairedNodes().Any(o=>o.Uuid == node.Uuid))
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, node.Uuid));
            }
            else if (String.IsNullOrEmpty(authorizingUserName))
            {
                throw new ArgumentNullException(nameof(authorizingUserName));
            }
            else if (String.IsNullOrEmpty(authorizingPassword))
            {
                throw new ArgumentNullException(nameof(authorizingPassword));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice);

            // First we want to indicate to the remote that we want to unpair
            switch (this.Send(node, BluetoothPeerToPeerMessage.NodeUnPairRequest(authorizingUserName, authorizingPassword, this.LocalNode)).Payload)
            {
                case PeerAcknowledgmentPayload ack:
                    if (ack.OutcomeStatus != PeerToPeerAcknowledgementCode.Ok)
                    {
                        throw new DetectedIssueException(ack.Details);
                    }
                    return this.RemovePairedNode(node);
                default:
                    throw new InvalidOperationException(ErrorMessages.INVALID_FORMAT);
            }
        }

        /// <inheritdoc/>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (this.m_operatingSystemSecurity.HasPermission(OperatingSystemPermissionType.Bluetooth) || this.m_operatingSystemSecurity.RequestPermission(OperatingSystemPermissionType.Bluetooth))
            {
                this.IsRunning = true;
                this.m_listenerThread = new Thread(this.BluetoothListener)
                {
                    IsBackground = true,
                    Name = "BluetoothP2PReceiver"
                };
                this.m_listenerThread.Start();
            }

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// The logic for the bluetooth listener
        /// </summary>
        private void BluetoothListener()
        {
            try
            {
                this.m_listener = new BluetoothListener(BluetoothConstants.P2P_SERVICE_ID)
                {
                    ServiceName = "SanteDB P2P Receiver"
                };
                this.m_listener.Start();

                while(this.IsRunning)
                {
                    if (this.m_listener.Pending()) // There is a waiting request
                    {
                        try
                        {
                            using (var connection = this.m_listener.AcceptBluetoothClient())
                            {
                                // Is this a registered bluetooth partner?
                                connection.Encrypt = true;
                                if (!this.IsRunning) break;

                                // Read the message from the buffer no matter 
                                using (var btStream = connection.GetStream())
                                {
                                    // Attempt to determine the node we're talking to
                                    var node = this.GetPairedNodes().OfType<BluetoothPeerNode>().FirstOrDefault(o => o.Name == connection.RemoteMachineName);
                                    if (node != null) // is registered
                                    {
                                        using (var request = new MemoryStream())
                                        {
                                            btStream.CopyTo(request);
                                            request.Seek(0, SeekOrigin.Begin);

                                            var otk = this.GetOneTimeKey(node);

                                            var eventArgs = new PeerToPeerEventArgs(node, true);
                                            this.Receiving?.Invoke(this, eventArgs);
                                            if (eventArgs.Cancel)
                                            {
                                                PeerToPeerUtils.WriteMessage(BluetoothPeerToPeerMessage.Nack("Cancelled"), btStream, PeerTransferEncodingFlags.Compressed, otk);
                                                continue;
                                            }

                                            // Read the message
                                            try
                                            {
                                                var requestMessage = PeerToPeerUtils.ReadMessage<BluetoothPeerToPeerMessage>(request, otk, true);
                                                var responseMessage = PeerToPeerUtils.ExecuteTrigger(requestMessage);
                                                PeerToPeerUtils.WriteMessage(responseMessage, btStream, PeerTransferEncodingFlags.Compressed, otk);
                                            }
                                            catch (Exception e)
                                            {
                                                this.m_tracer.TraceWarning("BluetoothReceiverError: Message interaction failed Client={0}, E={1}", connection.RemoteMachineName, e);
                                                PeerToPeerUtils.WriteMessage(BluetoothPeerToPeerMessage.Nack(e.ToHumanReadableString()), btStream, PeerTransferEncodingFlags.Compressed, otk);
                                            }
                                        }
                                    }

                                }

                            }
                        }
                        catch(Exception e)
                        {
                            this.m_tracer.TraceError("BluetoothReceiverError: Connection Accept Failed - {0}", e);
                        }
                    }
                }
            }
            finally
            {
                this.m_listener?.Stop();
                this.m_listenerResetEvent.Set();
            }

        }

        /// <inheritdoc/>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.IsRunning = false;
            this.m_tracer.TraceInfo("Waiting for Bluetooth Receiver to Exit...");
            this.m_listenerResetEvent.Reset();
            this.m_listenerResetEvent.Wait();
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.m_listener?.Dispose();
            this.m_listenerResetEvent.Dispose();
        }

        public IPeerToPeerNode RemovePairedNode(IPeerToPeerNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            else if (!this.GetPairedNodes().Any(o => o.Uuid == node.Uuid))
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.OBJECT_NOT_FOUND, node.Uuid));
            }
            
            this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateDevice);

            // Remove the registration for this node
            this.m_deviceIdentityProvider.RemoveClaim(node.Name, SanteDBClaimTypes.SanteDBRfc4226Secret, AuthenticationContext.Current.Principal);
            this.m_deviceIdentityProvider.RemoveClaim(node.Name, BluetoothConstants.BluetoothDeviceClaim, AuthenticationContext.Current.Principal);
            return node;
        }
    }
}

using InTheHand.Net.Sockets;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.PeerToPeer;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace SanteDB.DisconnectedClient.UI.Services.Bluetooth
{


    /// <summary>
    /// Represents a basic bluetooth peer to peer service
    /// </summary>
    /// TODO: Test this in Android and Linux and if they work move over to the core interface
    public class BluetoothPeerToPeerShareService : IPeerToPeerShareService, IDaemonService, IDisposable
    {

        /// <summary>
        /// Service identifier for the P2P service
        /// </summary>
        public readonly Guid SERVICE_ID = Guid.Parse("17FB1E15-D219-4192-91FD-CC00D5481254");

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(BluetoothPeerToPeerShareService));
        // The bluetooth client
        private BluetoothClient m_client;
        // The bluetooth listener
        private BluetoothListener m_listener;

        // List of remote clients to trust when siganture fails
        private List<String> m_trustSignatureFailures = new List<string>();

        // Signing service
        private IDataSigningService m_signingSerivce = ApplicationServiceContext.Current.GetService<IDataSigningService>();

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Bluetooth Peer-to-peer Data Transfer";

        /// <summary>
        /// True if the service is running
        /// </summary>
        public bool IsRunning => this.m_client != null;

        /// <summary>
        /// Data has been received
        /// </summary>
        public event EventHandler<PeerToPeerDataEventArgs> Received;
        /// <summary>
        /// Data is being recieved
        /// </summary>
        public event EventHandler<PeerToPeerEventArgs> Receiving;
        /// <summary>
        /// Data has been sent
        /// </summary>
        public event EventHandler<PeerToPeerDataEventArgs> Sent;
        /// <summary>
        /// Data is being sent
        /// </summary>
        public event EventHandler<PeerToPeerDataEventArgs> Sending;
        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the service is started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Throw if not supported
        /// </summary>
        private void ThrowIfNotSupported()
        {
            if (this.m_client == null)
                throw new NotSupportedException();
        }
        /// <summary>
        /// Get all paired devices
        /// </summary>
        public IEnumerable<string> GetRecipientList()
        {
            this.ThrowIfNotSupported();
            return this.m_client.PairedDevices.Where(o => o.Authenticated).Select(o => o.DeviceName);
        }

        /// <summary>
        /// Send the specified data to a recipient
        /// </summary>
        /// <param name="recipient">The device name of the recipient</param>
        /// <param name="entity">The entity to be sent</param>
        /// <returns>The acknowledgement</returns>
        public IdentifiedData Send(string recipient, IdentifiedData entity)
        {
            this.ThrowIfNotSupported();

            try
            {
                this.m_tracer.TraceInfo("Initiating data transfer to {0}", recipient);
                var device = this.m_client.PairedDevices.Where(o => o.DeviceName.Equals(recipient, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (device == null)
                    throw new KeyNotFoundException($"Remote recipient {recipient} not found");
                this.m_tracer.TraceVerbose("Remove device is address {0}", device.DeviceAddress);

                var eventArgs = new PeerToPeerDataEventArgs(device.DeviceName, false, entity);
                this.Sending?.Invoke(this, eventArgs);
                if(eventArgs.Cancel)
                {
                    this.m_tracer.TraceWarning("Pre-event hook indicates cancel");
                    return null;
                }

                using (var client = new BluetoothClient())
                {
                    client.Encrypt = true;
                    client.Authenticate = true;

                    client.Connect(device.DeviceAddress, SERVICE_ID);
                    var stream = client.GetStream();
                    try
                    {
                        if (stream == null || !client.Connected)
                            throw new InvalidOperationException("Cannot send data to remote host - Not Connected");

                        new PeerToPeer.PeerTransferPayload(entity, PeerTransferEncodingFlags.Compressed).Write(stream, this.m_signingSerivce);

                        // read response back from stream
                        var response = PeerToPeer.PeerTransferPayload.Read(stream, this.m_signingSerivce, false);
                        this.Sent?.Invoke(this, new PeerToPeerDataEventArgs(device.DeviceName, false, response.Payload));
                        return response.Payload;
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error sending data to {0} - {1}", recipient, e);
                throw new Exception($"Error sending {entity} to {recipient}",e); // TODO: Make this an exception of its own class PeerToPeerException
            }
        }

        /// <summary>
        /// The bluetooth listener thread
        /// </summary>
        private void BluetoothListenerThread(object state)
        {
            try
            {
                while (this.IsRunning)
                {
                    using (var connection = this.m_listener.AcceptBluetoothClient())
                    {
                        connection.Encrypt = true;
                        if (!this.IsRunning) return;

                        var eventArgs = new PeerToPeerEventArgs(connection.RemoteMachineName, true);
                        this.Receiving?.Invoke(this, eventArgs);
                        if(!eventArgs.Cancel) 
                            // Read the payload
                            using (var stream = connection.GetStream())
                            {
                                using (var ms = new MemoryStream()) // Buffer
                                {
                                    stream.CopyTo(ms);
                                    PeerTransferPayload payload = null;
                                    try
                                    {
                                        payload = PeerToPeer.PeerTransferPayload.Read(ms, this.m_signingSerivce, !m_trustSignatureFailures.Contains(connection.RemoteMachineName));
                                    }
                                    catch(Exception e)
                                    {
                                        if (m_trustSignatureFailures.Contains(connection.RemoteMachineName) || ApplicationContext.Current.Confirm(Strings.err_signature_failed_ignore))
                                        {
                                            if (ApplicationContext.Current.Confirm(String.Format(Strings.locale_ignore_signatures_from_host, connection.RemoteMachineName)))
                                                m_trustSignatureFailures.Add(connection.RemoteMachineName);
                                            ms.Seek(0, SeekOrigin.Begin);
                                            PeerToPeer.PeerTransferPayload.Read(ms, this.m_signingSerivce, false);
                                        }
                                        else
                                            continue; // ignore the data
                                    }

                                    // Raise notification
                                    this.Received?.Invoke(this, new PeerToPeerDataEventArgs(connection.RemoteMachineName, true, payload.Payload));

                                    // Send the payload back
                                    try
                                    {
                                        payload.Write(stream, this.m_signingSerivce);
                                    }
                                    catch(Exception e)
                                    {
                                        this.m_tracer.TraceWarning("Could not send response back {0}", e);
                                    }
                                }
                            }   
                    }
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Fatal Error in Bluetooth Receiver: {0}", e);
                
            }
        }

        /// <summary>
        /// Start this service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            var ossec = ApplicationServiceContext.Current.GetService<IOperatingSystemSecurityService>();
            if (ossec.HasPermission(PermissionType.Bluetooth) || ossec.RequestPermission(PermissionType.Bluetooth))
            {
                this.m_tracer.TraceInfo("Starting Bluetooth Client...");
                this.m_client = new BluetoothClient();
                this.m_listener = new BluetoothListener(SERVICE_ID) {
                    ServiceName = "SanteDB Peer-to-Peer Receiver"
                };
                this.m_listener.Start();
            }
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stop this service
        /// </summary>
        public bool Stop()
        {
            this.ThrowIfNotSupported();
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.m_tracer.TraceInfo("Stopping Receiver...");
            this.m_listener.Dispose();
            this.m_tracer.TraceInfo("Stopping Client...");
            this.m_client.Dispose();

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        public void Dispose()
        {
            this.Stop();
        }
    }
}

/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Tickler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace SanteDB.DisconnectedClient.Xamarin.Net
{
    /// <summary>
    /// Implementation of the network information service
    /// </summary>
    public class NetworkInformationService : INetworkInformationService
    {

        // Net available
        private bool m_networkAvailable = true;

        /// <summary>
        /// Network availability changed
        /// </summary>
        public NetworkInformationService()
        {
            NetworkChange.NetworkAvailabilityChanged += (o, e) =>
            {
                ApplicationContext.Current.GetService<ITickleService>()?.SendTickle(new Tickle(Guid.Empty, TickleType.Information | TickleType.Toast, "Your network status changed", DateTime.Now.AddMinutes(2)));
                this.m_networkAvailable = e.IsAvailable;
                this.NetworkStatusChanged?.Invoke(this, e);
            };

            // TODO: Discuss the ramifications of this
            // this.NetworkStatusChanged += NetworkInformationService_NetworkStatusChanged;
        }

        /// <summary>
        /// Updates the registered services in the application context when the network status changes.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void NetworkInformationService_NetworkStatusChanged(object sender, EventArgs e)
        {
            INetworkInformationService networkInformationService = (INetworkInformationService)sender;

            //         // Because we may have network connectivity
            //if (networkInformationService.IsNetworkAvailable)
            //{
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.RemoveAll(o => o == typeof(LocalPolicyInformationService).AssemblyQualifiedName);
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(AmiPolicyInformationService).AssemblyQualifiedName);
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(OAuthIdentityProvider).AssemblyQualifiedName);
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(HdsiPersistenceService).AssemblyQualifiedName);
            //}
            //else
            //{
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalPersistenceService).AssemblyQualifiedName);
            //	ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().ServiceTypes.Add(typeof(LocalIdentityService).AssemblyQualifiedName);
            //}
        }

        /// <summary>
        /// Returns true if the network is wifi
        /// </summary>
        public virtual bool IsNetworkWifi
        {
            get
            {
                return NetworkInterface.GetAllNetworkInterfaces().Any(o => o.OperationalStatus == OperationalStatus.Up &&
                    (o.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || o.NetworkInterfaceType == NetworkInterfaceType.Ethernet));
            }
        }
        /// <summary>
        /// Return whether the network is available
        /// </summary>
        public virtual bool IsNetworkAvailable
        {
            get
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
        }

        /// <summary>
        /// Gets whether the network is connected.
        /// </summary>
        public bool IsNetworkConnected
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string ServiceName => throw new NotImplementedException();

        /// <summary>
        /// Network status has changed
        /// </summary>
        public event EventHandler NetworkStatusChanged;

        /// <summary>
        /// Gets all available interfaces
        /// </summary>
        public virtual IEnumerable<NetworkInterfaceInfo> GetInterfaces()
        {

            return NetworkInterface.GetAllNetworkInterfaces().Select(o => new NetworkInterfaceInfo(
                o.Name, o.GetPhysicalAddress().ToString(), o.OperationalStatus == OperationalStatus.Up, o.Description, o.GetIPProperties().UnicastAddresses.FirstOrDefault()?.ToString(), o.GetIPProperties().GatewayAddresses.FirstOrDefault()?.ToString()
            ));

        }

        /// <summary>
        /// Perform a DNS lookup
        /// </summary>
        public string Nslookup(string address)
        {
            try
            {
                System.Uri uri = null;
                if (System.Uri.TryCreate(address, UriKind.RelativeOrAbsolute, out uri))
                    address = uri.Host;
                var resolution = System.Net.Dns.GetHostEntry(address);
                return resolution.AddressList.First().ToString();
            }
            catch
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Retrieves the ping time to the specified host
        /// </summary>
        public long Ping(string hostName)
        {
            try
            {
                System.Uri uri = null;
                if (System.Uri.TryCreate(hostName, UriKind.RelativeOrAbsolute, out uri))
                    hostName = uri.Host;
                System.Net.NetworkInformation.Ping p = new System.Net.NetworkInformation.Ping();
                var reply = p.Send(hostName);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }
        /// <summary>
        /// Get the hostname
        /// </summary>
        public virtual string GetHostName()
        {
            return Dns.GetHostName();
        }

        /// <summary>
        /// Get machine name
        /// </summary>
        public virtual string GetMachineName()
        {
            return ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName;
        }
    }
}
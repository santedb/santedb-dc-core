/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Rest.Common.Behaviors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Ags.Behaviors;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Web Hosting Service
    /// </summary>
    [ServiceBehavior(Name = "WWW", InstanceMode = ServiceInstanceMode.PerCall)]
    public class WwwServiceBehavior : IWwwServiceContract
    {
        // Cached applets
        private static Dictionary<String, AppletAsset> m_cacheApplets = new Dictionary<string, AppletAsset>();
        // Lock object
        private static object m_lockObject = new object();


        /// <summary>
        /// Throw exception if not running
        /// </summary>
        private void ThrowIfNotRunning()
        {
            if (!ApplicationServiceContext.Current.GetService<AgsService>().IsRunning)
                throw new DomainStateException();
        }

        /// <summary>
        /// Get the icon for the SanteDB server
        /// </summary>
        public Stream GetIcon()
        {
            
            var ms = new MemoryStream();
            typeof(WwwServiceBehavior).Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Ags.Resources.icon.ico").CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        /// <summary>
        /// Get the asset
        /// </summary>
        public Stream Get()
        {
            this.ThrowIfNotRunning();

            try
            {
                if (!RestOperationContext.Current.Data.TryGetValue("lang", out object lang))
                    lang = AuthenticationContext.Current.Principal.GetClaimValue(SanteDBClaimTypes.Language) ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                else if (lang is String[] ls)
                    lang = ls[0];
                if (!RestOperationContext.Current.Data.TryGetValue(AgsAuthorizationServiceBehavior.SessionPropertyName, out object sessionId))
                    sessionId = AuthenticationContext.Current.Principal.GetClaimValue(SanteDBClaimTypes.SanteDBSessionIdClaim);
                else if (sessionId is ISession ses)
                    sessionId = BitConverter.ToString(ses.Id);

                var etag = $"{ApplicationContext.Current.ExecutionUuid}.{lang}.{sessionId}";

                if(RestOperationContext.Current.IncomingRequest.Headers["If-None-Match"] == etag)
                {
                    RestOperationContext.Current.OutgoingResponse.StatusCode = 304; /// not modified
                    return null;
                }    

                // Navigate asset
                AppletAsset navigateAsset = null;
                var appletManagerService = ApplicationContext.Current.GetService<IAppletManagerService>();

                String appletPath = RestOperationContext.Current.IncomingRequest.Url.AbsolutePath.ToLower();
                if (!m_cacheApplets.TryGetValue(appletPath, out navigateAsset))
                {

                    if (appletPath == "/") // startup asset
                        navigateAsset = appletManagerService.Applets.DefaultApplet?.Assets.FirstOrDefault(o => o.Name == "index.html");
                    else
                        navigateAsset = appletManagerService.Applets.ResolveAsset(appletPath);

                    if (navigateAsset == null)
                        throw new FileNotFoundException(appletPath);

                    lock (m_lockObject)
                    {
                        if (!m_cacheApplets.ContainsKey(appletPath) && appletManagerService.Applets.CachePages)
                        {
                            m_cacheApplets.Add(appletPath, navigateAsset);
                        }
                    }
                }

                // Navigate policy?
                if (navigateAsset.Policies != null)
                {
                    foreach (var policy in navigateAsset.Policies)
                    {
                        new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policy).Demand();
                    }
                }

                
                RestOperationContext.Current.OutgoingResponse.AddHeader("ETag", etag);
                RestOperationContext.Current.OutgoingResponse.ContentType = navigateAsset.MimeType;

                // Write asset
                var content = appletManagerService.Applets.RenderAssetContent(navigateAsset, lang?.ToString(), bindingParameters: new Dictionary<String, String>()
            {
                { "csp_nonce", RestOperationContext.Current.ServiceEndpoint.Behaviors.OfType<SecurityPolicyHeadersBehavior>().FirstOrDefault()?.Nonce },
#if DEBUG
                { "env_type", "debug" },
#else
                { "env_type", "release" },
#endif
                { "host_type", ApplicationServiceContext.Current.HostType.ToString() }

            });
                return new MemoryStream(content);
            }
            catch(RemoteOperationException ex) // The page demanded something upstream but the upstream service isn't responding
            {
                throw new DomainStateException($"The remote server is currently unavailable", ex);
            }
        }
    }
}

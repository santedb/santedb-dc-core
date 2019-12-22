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
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// WWW Service Behavior of AGS
    /// </summary>
    [ServiceBehavior(Name = "WWW", InstanceMode = ServiceInstanceMode.PerCall)]
    public class WwwServiceBehavior : IWwwServiceContract
    {
        // Cached applets
        private static Dictionary<String, AppletAsset> m_cacheApplets = new Dictionary<string, AppletAsset>();
        // Lock object
        private static object m_lockObject = new object();
        

        /// <summary>
        /// Get the asset
        /// </summary>
        public Stream Get()
        {
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

#if DEBUG
            RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "no-cache");
#else
            if (RestOperationContext.Current.IncomingRequest.Url.ToString().EndsWith(".js") || RestOperationContext.Current.IncomingRequest.Url.ToString().EndsWith(".css") ||
                RestOperationContext.Current.IncomingRequest.Url.ToString().EndsWith(".png") || RestOperationContext.Current.IncomingRequest.Url.ToString().EndsWith(".woff2"))
            {
                RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "public");
                RestOperationContext.Current.OutgoingResponse.AddHeader("Expires", DateTime.UtcNow.AddHours(1).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
            }
            else
                RestOperationContext.Current.OutgoingResponse.AddHeader("Cache-Control", "no-cache");
#endif

            // Navigate policy?
            if (navigateAsset.Policies != null)
            {
                foreach (var policy in navigateAsset.Policies)
                {
                    new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policy).Demand();
                }
            }

            RestOperationContext.Current.OutgoingResponse.ContentType = navigateAsset.MimeType;

            // Write asset
            var content = appletManagerService.Applets.RenderAssetContent(navigateAsset, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, bindingParameters: new Dictionary<String, String>()
            {
                { "csp_nonce", BitConverter.ToString(ApplicationContext.Current.ExecutionUuid.ToByteArray()).Replace("-","") }
            });
            return new MemoryStream(content);
        }
    }
}

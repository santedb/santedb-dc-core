﻿/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-6-28
 */
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
{

    /// <summary>
    /// Represents user interface handlers
    /// </summary>
    [RestService("/ui")]
    [Anonymous]
    public class UserInterface
    {

        // Cached routes file
        private byte[] m_routes = null;

        /// <summary>
        /// Calculates an Angular Routes file and returns it
        /// </summary>
        /// <returns></returns>
        [RestOperation(Method = "GET", UriPath = "/routes.js")]
        [return: RestMessage(RestMessageFormat.Raw)]
        public byte[] GetRoutes()
        {

            // Ensure response makes sense
            MiniHdsiServer.CurrentContext.Response.ContentType = "text/javascript";
            IAppletManagerService appletService = ApplicationContext.Current.GetService<IAppletManagerService>();

            // Calculate routes
#if !DEBUG
            if (this.m_routes == null)
#endif
                using (MemoryStream ms = new MemoryStream())
                {
                    using (StreamWriter sw = new StreamWriter(ms))
                    {
                        sw.WriteLine("SanteDB = SanteDB || {}");
                        sw.WriteLine("SanteDB.UserInterface = SanteDB.UserInterface || {}");
                        sw.WriteLine("SanteDB.UserInterface.states = [");
                        // Collect routes
                        foreach (var itm in appletService.Applets.ViewStateAssets)
                        {
                            var htmlContent = (itm.Content ?? appletService.Applets.Resolver?.Invoke(itm)) as AppletAssetHtml;
                            var viewState = htmlContent.ViewState;
                            sw.WriteLine($"{{ name: '{viewState.Name}', url: '{viewState.Route}', abstract: {viewState.IsAbstract.ToString().ToLower()}");
                            if (viewState.View.Count > 0)
                            {
                                sw.Write(", views: {");
                                foreach (var view in viewState.View)
                                {
                                    sw.Write($"'{view.Name}' : {{ controller: '{view.Controller}', templateUrl: '{view.Route ?? itm.ToString() }'");
                                    var dynScripts = appletService.Applets.GetLazyScripts(itm);
                                    if (dynScripts.Any())
                                    {
                                        int i = 0;
                                        sw.Write($", lazy: [ {String.Join(",", dynScripts.Select(o => $"'{appletService.Applets.ResolveAsset(o.Reference, itm)}'"))}  ]");
                                    }
                                    sw.WriteLine(" }, ");
                                }
                                sw.WriteLine("}");
                            }
                            sw.WriteLine("} ,");
                        }
                        sw.Write("];");
                    }
                    this.m_routes = ms.ToArray();
                }
            return this.m_routes;
        }

    }
}

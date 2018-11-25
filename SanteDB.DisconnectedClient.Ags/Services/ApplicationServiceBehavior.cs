/*
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
 * Date: 2018-11-23
 */
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Xamarin.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// The application services behavior
    /// </summary>
    [ServiceBehavior(Name = "APP", InstanceMode = ServiceInstanceMode.PerCall)]
    public partial class ApplicationServiceBehavior : IApplicationServiceContract
    {

        /// <summary>
        /// Get storage providers
        /// </summary>
        public List<StorageProviderViewModel> GetDataStorageProviders()
        {
            return StorageProviderUtil.GetProviders().Select(o => new StorageProviderViewModel()
            {
                Invariant = o.Invariant,
                Name = o.Name,
                Options = o.Options
            }).ToList();
        }

        /// <summary>
        /// Get the routes for the Angular Application
        /// </summary>
        public Stream GetRoutes()
        {
            // Ensure response makes sense
            RestOperationContext.Current.OutgoingResponse.ContentType = "text/javascript";
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
                return new MemoryStream(ms.ToArray());
            }
        }

        /// <summary>
        /// Get locale assets
        /// </summary>
        public Dictionary<String, String[]> GetLocaleAssets()
        {

            // Get all locales from the asset manager
            var retVal = new Dictionary<String, String[]>();
            foreach (var locale in ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.Locales).GroupBy(o => o.Code))
            {
                retVal.Add(locale.Key, locale.SelectMany(o => o.Assets).ToArray());
            }
            return retVal;

        }

        /// <summary>
        /// Get subscription definitions
        /// </summary>
        /// <returns></returns>
        public List<AppletSubscriptionDefinition> GetSubscriptionDefinitions()
        {
            return ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.SubscriptionDefinition).ToList();
        }


    }
}

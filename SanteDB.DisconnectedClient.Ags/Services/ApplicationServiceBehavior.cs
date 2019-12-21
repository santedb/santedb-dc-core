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
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Data;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Routes
        private byte[] m_routes;

        /// <summary>
        /// Get storage providers
        /// </summary>
        public List<StorageProviderViewModel> GetDataStorageProviders()
        {
            return StorageProviderUtil.GetProviders().Select(o => new StorageProviderViewModel(o)).ToList();
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
                            var displayName = htmlContent.GetTitle(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                            if(!String.IsNullOrEmpty(displayName))
                                sw.Write($", displayName: '{displayName }'");
                            if (itm.Policies.Count > 0)
                                sw.Write($", demand: [{String.Join(",", itm.Policies.Select(o=>$"'{o}'"))}] ");
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
            return new MemoryStream(this.m_routes);
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
        /// Perform an update
        /// </summary>
        /// <param name="appId"></param>
        public void PerformUpdate(string appId)
        {
            // Update
            ApplicationContext.Current.GetService<IUpdateManager>().Install(appId);
        }

        /// <summary>
        /// Get a new UUID
        /// </summary>
        public Guid GetUuid()
        {
            // TODO: Sequential UUIDs
            return Guid.NewGuid();
        }

        /// <summary>
        /// Performs the retrieval of menu items from the application
        /// </summary>
        /// <returns></returns>
        [Demand(PermissionPolicyIdentifiers.Login)]
        public List<MenuInformation> GetMenus()
        {
            try
            {

                // Cannot have menus if not logged in
                if (!AuthenticationContext.Current.Principal.Identity.IsAuthenticated) return null;

                var context = RestOperationContext.Current.IncomingRequest.QueryString["context"];

                var rootMenus = ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.Menus).OrderBy(o => o.Order).ToArray();
                List<MenuInformation> retVal = new List<MenuInformation>();

                // Create menus
                foreach (var mnu in rootMenus)
                    this.ProcessMenuItem(mnu, retVal, context);
                retVal.RemoveAll(o => o.Action == null && o.Menu?.Count == 0);


                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error retrieving menus: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Process menu item
        /// </summary>
        private void ProcessMenuItem(AppletMenu menu, List<MenuInformation> retVal, String context)
        {
            // TODO: Demand permission
            if (menu.Context != context || menu.Asset != null &&
                !ApplicationContext.Current.GetService<IAppletManagerService>().Applets.ResolveAsset(menu.Asset, menu.Manifest.Assets[0])?.Policies?.Any(p => ApplicationContext.Current.PolicyDecisionService.GetPolicyOutcome(AuthenticationContext.Current.Principal, p) == SanteDB.Core.Model.Security.PolicyGrantType.Deny) == false)
                return;

            // Get text for menu item
            string menuText = menu.GetText(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            var existing = retVal.Find(o => o.Text == menuText && o.Icon == menu.Icon);
            if (existing == null)
            {
                existing = new MenuInformation()
                {
                    Action = menu.Launch,
                    Icon = menu.Icon,
                    Text = menuText
                };
                retVal.Add(existing);
            }
            if (menu.Menus.Count > 0)
            {
                existing.Menu = new List<MenuInformation>();
                foreach (var child in menu.Menus)
                    this.ProcessMenuItem(child, existing.Menu, context);
            }
        }
    }
}

/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Data;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SanteDB.Core.Security.Claims;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Application Services Interface Behavior
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
            using (MemoryStream ms = new MemoryStream())
            {
                using (StreamWriter sw = new StreamWriter(ms))
                {
                    IEnumerable<AppletAsset> viewStates = appletService.Applets.ViewStateAssets.Select(o => new { Asset = o, Html = (o.Content ?? appletService.Applets.Resolver?.Invoke(o)) as AppletAssetHtml }).GroupBy(o => o.Html.ViewState.Name).Select(g => g.OrderByDescending(o => o.Html.ViewState.Priority).First().Asset);

                    sw.WriteLine("// Generated Routes ");
                    sw.WriteLine("// Loaded Applets");
                    foreach (var apl in appletService.Applets)
                    {
                        sw.WriteLine("// \t {0}", apl.Info.Id);
                        foreach(var ast in apl.Assets)
                        {
                            var cont = ast.Content ?? appletService.Applets.Resolver?.Invoke(ast);
                            if(cont is AppletAssetHtml html && html.ViewState != null)
                                sw.WriteLine("// \t\t {0}", html.ViewState?.Name);
                        }
                    }
                    sw.WriteLine("// Include States: ");
                    foreach (var vs in viewStates)
                        sw.WriteLine("// \t{0}", vs.Name);

                    sw.WriteLine("SanteDB = SanteDB || {}");
                    sw.WriteLine("SanteDB.UserInterface = SanteDB.UserInterface || {}");
                    sw.WriteLine("SanteDB.UserInterface.states = [");


                    // Collect routes
                    foreach (var itm in viewStates)
                    {
                        var htmlContent = (itm.Content ?? appletService.Applets.Resolver?.Invoke(itm)) as AppletAssetHtml;
                        var viewState = htmlContent.ViewState;
                        sw.WriteLine($"{{ name: '{viewState.Name}', url: '{viewState.Route}', abstract: {viewState.IsAbstract.ToString().ToLower()}");
                        var displayName = htmlContent.GetTitle(AuthenticationContext.Current.Principal.GetClaimValue(SanteDBClaimTypes.Language) ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                        if (!String.IsNullOrEmpty(displayName))
                            sw.Write($", displayName: '{displayName }'");
                        if (itm.Policies.Count > 0)
                            sw.Write($", demand: [{String.Join(",", itm.Policies.Select(o => $"'{o}'"))}] ");
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
            var asset = ApplicationContext.Current.GetService<IAppletManagerService>().Applets.ResolveAsset(menu.Asset, menu.Manifest.Assets[0]);

            if (menu.Context != context || menu.Asset != null &&
                !asset?.Policies?.Any(p => ApplicationContext.Current.PolicyDecisionService.GetPolicyOutcome(AuthenticationContext.Current.Principal, p) == SanteDB.Core.Model.Security.PolicyGrantType.Deny) == false)
                return;

            // Get text for menu item
            string menuText = menu.GetText(AuthenticationContext.Current.Principal.GetClaimValue(SanteDBClaimTypes.Language) ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
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

        /// <summary>
        /// Get online status
        /// </summary>
        public Dictionary<string, bool> GetOnlineState()
        {
            try
            {
                return new Dictionary<string, bool>()
                {
                    // Connected to internet
                    { "online", ApplicationServiceContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable },
                    { "ami", ApplicationServiceContext.Current.GetService<IAdministrationIntegrationService>()?.IsAvailable() ?? true},
                    { "hdsi", ApplicationServiceContext.Current.GetService<IClinicalIntegrationService>()?.IsAvailable()?? true }
                };
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Cannot determine online state: {0}", e.Message);
                return new Dictionary<string, bool>();
            }
        }

       
    }
}

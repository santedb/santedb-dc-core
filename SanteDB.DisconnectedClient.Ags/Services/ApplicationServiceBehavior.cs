using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Xamarin;
using SanteDB.DisconnectedClient.Xamarin.Data;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// The application services behavior
    /// </summary>
    [ServiceBehavior(Name = "APP", InstanceMode = ServiceInstanceMode.PerCall)]
    public class ApplicationServiceBehavior : IApplicationServiceContract
    {
        /// <summary>
        /// Get the configuration
        /// </summary>
        public ConfigurationViewModel GetConfiguration()
        {
            return new ConfigurationViewModel(XamarinApplicationContext.Current.Configuration);
        }

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

        [Demand(PermissionPolicyIdentifiers.Login)]
        public ConfigurationViewModel GetUserConfiguration(string userId)
        {
            throw new NotImplementedException();
        }

        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public ConfigurationViewModel JoinRealm(ConfigurationViewModel configData)
        {
            throw new NotImplementedException();
        }

        [Demand(PermissionPolicyIdentifiers.Login)]
        public ConfigurationViewModel SaveUserConfiguration(ConfigurationViewModel configuration)
        {
            throw new NotImplementedException();
        }

        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public ConfigurationViewModel UpdateConfiguration(ConfigurationViewModel configuration)
        {
            throw new NotImplementedException();
        }
    }
}

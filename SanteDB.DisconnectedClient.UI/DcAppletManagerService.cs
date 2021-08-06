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
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Ags.Util;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.UI.Services;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.UI
{
    /// <summary>
    /// Disconnected client applet manager service
    /// </summary>
    public class DcAppletManagerService : LocalAppletManagerService
    {
        
        /// <summary>
        /// Default ctor
        /// </summary>
        public DcAppletManagerService()
        {
            this.m_appletCollection.Resolver = this.ResolveAppletAsset;
            this.m_appletCollection.CachePages = true;
        }

        /// <summary>
        /// Resolve asset
        /// </summary>
        public object ResolveAppletAsset(AppletAsset navigateAsset)
        {
            String itmPath = System.IO.Path.Combine(
                                        ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().AppletDirectory,
                                        "assets",
                                        navigateAsset.Manifest.Info.Id,
                                        navigateAsset.Name);

            if (navigateAsset.MimeType == "text/javascript" ||
                        navigateAsset.MimeType == "text/css" ||
                        navigateAsset.MimeType == "application/json" ||
                navigateAsset.MimeType == "text/json" ||

                        navigateAsset.MimeType == "text/xml")
            {
                var script = File.ReadAllText(itmPath);
                if (itmPath.Contains("santedb.js") || itmPath.Contains("santedb.min.js"))
                    script += this.GetShimMethods();
                return script;
            }
            else
                using (MemoryStream response = new MemoryStream())
                using (var fs = File.OpenRead(itmPath))
                {
                    int br = 8096;
                    byte[] buffer = new byte[8096];
                    while (br == 8096)
                    {
                        br = fs.Read(buffer, 0, 8096);
                        response.Write(buffer, 0, br);
                    }

                    return response.ToArray();
                }
        }

        /// <summary>
        /// Get the SHIM methods
        /// </summary>
        /// <returns></returns>
        private String GetShimMethods()
        {
            var shimGen = ApplicationContext.Current.GetService<IShimGenerator>();
            if (shimGen == null) // legacy - default SHIM 
                using (StringWriter tw = new StringWriter())
                {
                    tw.WriteLine("/// START SANTEDB MINI IMS SHIM");
                    // Version
                    tw.WriteLine("__SanteDBAppService.ExecutionEnvironment ='{0}';", ApplicationContext.Current.HostType);
                    tw.WriteLine("__SanteDBAppService.GetMagic = function() {{ return '{0}'; }}", ApplicationContext.Current.ExecutionUuid);
                    tw.WriteLine("__SanteDBAppService.GetVersion = function() {{ return '{0} ({1})'; }}", typeof(SanteDBConfiguration).Assembly.GetName().Version, typeof(SanteDBConfiguration).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
                    tw.WriteLine("__SanteDBAppService.GetString = function(key) {");
                    tw.WriteLine("\tvar strData = __SanteDBAppService._stringData[__SanteDBAppService.GetLocale()] || __SanteDBAppService._stringData['en'];");
                    tw.WriteLine("\treturn strData[key] || key;");
                    tw.WriteLine("}");

                    tw.WriteLine("__SanteDBAppService._stringData = {};");
                    var languages = this.Applets.SelectMany(a => a.Strings).Select(o => o.Language).Distinct();
                    foreach (var lang in languages)
                    {
                        tw.WriteLine("\t__SanteDBAppService._stringData['{0}'] = {{", lang);
                        foreach (var itm in ApplicationContext.Current.GetService<ILocalizationService>().GetStrings(lang))
                        {
                            tw.WriteLine("\t\t'{0}': '{1}',", itm.Key, itm.Value?.EncodeAscii().Replace("'", "\\'").Replace("\r", "").Replace("\n", ""));
                        }
                        tw.WriteLine("\t\t'none':'none' };");
                    }


                    tw.WriteLine("__SanteDBAppService.GetTemplateForm = function(templateId) {");
                    tw.WriteLine("\tswitch(templateId) {");

                    foreach (var itm in this.Applets.SelectMany(o => o.Templates))
                    {
                        tw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Mnemonic.ToLowerInvariant(), itm.Form);
                    }
                    tw.WriteLine("\t}");
                    tw.WriteLine("}");

                    tw.WriteLine("__SanteDBAppService.GetTemplateView = function(templateId) {");
                    tw.WriteLine("\tswitch(templateId) {");
                    foreach (var itm in this.Applets.SelectMany(o => o.Templates))
                    {
                        tw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Mnemonic.ToLowerInvariant(), itm.View);
                    }
                    tw.WriteLine("\t}");
                    tw.WriteLine("}");

                    tw.WriteLine("__SanteDBAppService.GetTemplates = function() {");
                    tw.WriteLine("return '[{0}]'", String.Join(",", this.Applets
                        .SelectMany(o => o.Templates)
                        .GroupBy(o => o.Mnemonic)
                        .Select(o => o.OrderByDescending(t => t.Priority).FirstOrDefault())
                        .Where(o => o.Public).Select(o => $"\"{o.Mnemonic}\"")));
                    tw.WriteLine("}");

                    tw.WriteLine("__SanteDBAppService.GetDataAsset = function(assetId) {");
                    tw.WriteLine("\tswitch(assetId) {");
                    foreach (var itm in this.Applets.SelectMany(o => o.Assets).Where(o => o.Name.StartsWith("data/")))
                        tw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Name.Replace("data/", ""), Convert.ToBase64String(this.Applets.RenderAssetContent(itm)).Replace("'", "\\'"));
                    tw.WriteLine("\t}");
                    tw.WriteLine("}");

                    // Read the static shim
                    var manifestName = Assembly.GetEntryAssembly().GetManifestResourceNames().FirstOrDefault(o => o.EndsWith("shim.js"));
                    using (StreamReader shim = new StreamReader(Assembly.GetEntryAssembly().GetManifestResourceStream(manifestName)))
                        tw.Write(shim.ReadToEnd());

                    return tw.ToString();
                }
            else // Load the provided shim
                return shimGen.GetShimMethods();
        }


    }
}

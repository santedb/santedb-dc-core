using SanteDB.Client.Services;
using SanteDB.Core;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SanteDB.Client.UserInterface
{
    /// <summary>
    /// Web applet host bridge provider
    /// </summary>
    public class WebAppletHostBridgeProvider : IAppletHostBridgeProvider
    {
        private string m_shim;
        private IAppletManagerService m_appletService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public WebAppletHostBridgeProvider()
        {


        }

        /// <summary>
        /// Get the bridge script
        /// </summary>
        public string GetBridgeScript()
        {

            if (this.m_shim == null)
            {
                // Prevent against circular dependency
                if (this.m_appletService == null)
                {
                    this.m_appletService = ApplicationServiceContext.Current.GetService<IAppletManagerService>();
                    this.m_appletService.Changed += (o, e) => this.m_shim = null;
                }
                var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();

                using (var sw = new StringWriter())
                {
                    sw.WriteLine("/// START SANTEDB SHIM");
                    // Version
                    sw.WriteLine("__SanteDBAppService.GetMagic = function() {{ return '{0}'; }}", ApplicationServiceContext.Current.ActivityUuid.ToByteArray().HexEncode());
                    sw.WriteLine("__SanteDBAppService.GetVersion = function() {{ return '{0} ({1})'; }}", Assembly.GetEntryAssembly().GetName().Version, Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
                    sw.WriteLine("__SanteDBAppService.GetString = function(key) {");
                    sw.WriteLine("\tvar strData = __SanteDBAppService._stringData[__SanteDBAppService.GetLocale()] || __SanteDBAppService._stringData['en'];");
                    sw.WriteLine("\treturn strData[key] || key;");
                    sw.WriteLine("}");

                    sw.WriteLine("__SanteDBAppService._stringData = {};");
                    var languages = localizationService.GetAvailableLocales();
                    foreach (var lang in languages)
                    {
                        sw.WriteLine("\t__SanteDBAppService._stringData['{0}'] = {{", lang);
                        foreach (var itm in localizationService.GetStrings(lang))
                        {
                            sw.WriteLine("\t\t'{0}': '{1}',", itm.Key, itm.Value?.EncodeAscii().Replace("'", "\\'").Replace("\r", "").Replace("\n", ""));
                        }
                        sw.WriteLine("\t\t'none':'none' };");
                    }


                    sw.WriteLine("__SanteDBAppService.GetTemplateForm = function(templateId) {");
                    sw.WriteLine("\tswitch(templateId) {");
                    foreach (var itm in m_appletService.Applets.SelectMany(o => o.Templates))
                    {
                        sw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Mnemonic.ToLowerInvariant(), itm.Form);
                    }
                    sw.WriteLine("\t}");
                    sw.WriteLine("}");

                    sw.WriteLine("__SanteDBAppService.GetTemplateView = function(templateId) {");
                    sw.WriteLine("\tswitch(templateId) {");
                    foreach (var itm in m_appletService.Applets.SelectMany(o => o.Templates))
                    {
                        sw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Mnemonic.ToLowerInvariant(), itm.View);
                    }
                    sw.WriteLine("\t}");
                    sw.WriteLine("}");

                    sw.WriteLine("__SanteDBAppService.GetTemplates = function() {");
                    sw.WriteLine("return '[{0}]'", String.Join(",", m_appletService.Applets.SelectMany(o => o.Templates).Where(o => o.Public).Select(o => $"\"{o.Mnemonic}\"")));
                    sw.WriteLine("}");
                    sw.WriteLine("__SanteDBAppService.GetDataAsset = function(assetId) {");
                    sw.WriteLine("\tswitch(assetId) {");
                    foreach (var itm in m_appletService.Applets.SelectMany(o => o.Assets).Where(o => o.Name.StartsWith("data/")))
                        sw.WriteLine("\t\tcase '{0}': return '{1}'; break;", itm.Name.Replace("data/", ""), Convert.ToBase64String(m_appletService.Applets.RenderAssetContent(itm)).Replace("'", "\\'"));
                    sw.WriteLine("\t}");
                    sw.WriteLine("}");
                    using (var streamReader = new StreamReader(typeof(WebAppletHostBridgeProvider).Assembly.GetManifestResourceStream("SanteDB.Client.Resources.WebAppletBridge.js")))
                    {
                        sw.Write(streamReader.ReadToEnd());
                    }
                    this.m_shim = sw.ToString();
                }
            }
            return this.m_shim;
        }
    }
}

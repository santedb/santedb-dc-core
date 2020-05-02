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
using SanteDB.Core.Applets;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// File based applet manager
    /// </summary>
    public class LocalAppletManagerService : IAppletManagerService, IAppletSolutionManagerService
    {
        // Applet collection
        protected AppletCollection m_appletCollection = new AppletCollection();

        // RO applet collection
        private ReadonlyAppletCollection m_readonlyAppletCollection;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalAppletManagerService));

        // This collection has changed
        public event EventHandler Changed;

        /// <summary>
        /// Local applet manager ctor
        /// </summary>
        public LocalAppletManagerService()
        {
            this.m_appletCollection = new AppletCollection();
            this.m_readonlyAppletCollection = this.m_appletCollection.AsReadonly();
            this.m_readonlyAppletCollection.CollectionChanged += (o, e) => this.Changed?.Invoke(o, e);

        }

        /// <summary>
        /// Gets the loaded applets from the manager
        /// </summary>
        public ReadonlyAppletCollection Applets
        {
            get
            {
                return this.m_readonlyAppletCollection;
            }
        }

        public IEnumerable<AppletSolution> Solutions => new AppletSolution[0];

        /// <summary>
        /// Get the specified package data
        /// </summary>
        public byte[] GetPackage(String appletId)
        {
            return null;
        }


        /// <summary>
        /// Get applet by id
        /// </summary>
        /// <returns>The applet.</returns>
        /// <param name="id">Identifier.</param>
        public virtual AppletManifest GetApplet(String id)
        {
            return this.m_appletCollection.FirstOrDefault(o => o.Info.Id == id);
        }

        /// <summary>
		/// Register applet
		/// </summary>
		/// <param name="applet">Applet.</param>
		public virtual bool LoadApplet(AppletManifest applet)
        {
            if (applet.Info.Id == (ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().StartupAsset ?? "org.santedb.uicore"))
            {
                this.m_appletCollection.DefaultApplet = applet;
                this.m_readonlyAppletCollection.DefaultApplet = applet;
            }
            applet.Initialize();
            this.m_appletCollection.Add(applet);
            AppletCollection.ClearCaches();
            return true;
        }



        /// <summary>
        /// Verifies the manifest against it's recorded signature
        /// </summary>
        /// <returns><c>true</c>, if manifest was verifyed, <c>false</c> otherwise.</returns>
        /// <param name="manifest">Manifest.</param>
        protected bool VerifyPackage(AppletPackage package)
        {
            // First check: Hash - Make sure the HASH is ok
            if (Convert.ToBase64String(SHA256.Create().ComputeHash(package.Manifest)) != Convert.ToBase64String(package.Meta.Hash))
                throw new InvalidOperationException($"Package contents of {package.Meta.Id} appear to be corrupt!");

            if (package.Meta.Signature != null)
            {
                this.m_tracer.TraceInfo("Will verify package {0}", package.Meta.Id.ToString());

                // Get the public key - first, is the publisher in the trusted publishers store?
                var x509Store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
                try
                {
                    x509Store.Open(OpenFlags.ReadOnly);
                    var cert = x509Store.Certificates.Find(X509FindType.FindByThumbprint, package.Meta.PublicKeyToken, false);

                    // Not in the central store, perhaps the cert is embedded?
                    if (cert.Count == 0)
                    {
                        // Embedded CER
                        if (package.PublicKey != null)
                        {
                            // Attempt to load
                            cert = new X509Certificate2Collection(new X509Certificate2(package.PublicKey));
                            var config = ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>();
                            // Build the certificate chain
                            if (!config.Security.TrustedPublishers.Contains(cert[0].Thumbprint))
                            {
                                var chain = new X509Chain();
                                chain.Build(cert[0]);

                                // Validate the chain elements
                                bool isTrusted = false;
                                foreach (var itm in chain.ChainElements)
                                    isTrusted |= config.Security.TrustedPublishers.Contains(itm.Certificate.Thumbprint);

                                if (!isTrusted || chain.ChainStatus.Any(o => o.Status != X509ChainStatusFlags.RevocationStatusUnknown))
                                {
                                    if (!ApplicationContext.Current.Confirm(String.Format(Strings.locale_untrustedPublisherPrompt, package.Meta.Names.First().Value, this.ExtractDNPart(cert[0].Subject, "CN"))))
                                        return false;
                                    else
                                    {
                                        ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().Security.TrustedPublishers.Add(cert[0].Thumbprint);
                                        if (ApplicationContext.Current.ConfigurationPersister.IsConfigured)
                                            ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);
                                    }
                                }
                            }
                        }
                        else
                        {
                            this.m_tracer.TraceError($"Cannot find public key of publisher information for {package.Meta.PublicKeyToken} or the local certificate is invalid");
                            throw new SecurityException(Strings.locale_invalidSignature);
                        }
                    }

                    // Certificate is not yet valid or expired 
                    if ((cert[0].NotAfter < DateTime.Now || cert[0].NotBefore > DateTime.Now) &&
                        !ApplicationContext.Current.Confirm(String.Format(Strings.locale_certificateExpired, this.ExtractDNPart(cert[0].Subject, "CN"), cert[0].NotAfter)))
                    {
                        this.m_tracer.TraceError($"Cannot find public key of publisher information for {package.Meta.PublicKeyToken} or the local certificate is invalid");
                        throw new SecurityException(Strings.locale_invalidSignature);
                    }

                    RSACryptoServiceProvider rsa = cert[0].PublicKey.Key as RSACryptoServiceProvider;

                    var retVal = rsa.VerifyData(package.Manifest, CryptoConfig.MapNameToOID("SHA1"), package.Meta.Signature);
                    if (!retVal)
                        throw new SecurityException(Strings.locale_invalidSignature);
                    return retVal;
                }
                finally
                {
                    x509Store.Close();
                }
            }
            else if (ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>().Security.AllowUnsignedApplets)
            {
                return ApplicationContext.Current.Confirm(String.Format(Strings.locale_unsignedAppletPrompt, package.Meta.Names.First().Value));
            }
            else
            {
                this.m_tracer.TraceError("Package {0} v.{1} (publisher: {2}) is not signed and cannot be installed", package.Meta.Id, package.Meta.Version, package.Meta.Author);
                throw new SecurityException(String.Format(Strings.locale_unsignedAppletsNotAllowed, package.Meta.Id));
            }
        }

        /// <summary>
        /// Extract a common name
        /// </summary>
        protected String ExtractDNPart(string subject, string part)
        {
            Regex cnParse = new Regex(@"([A-Za-z]{1,2})=(.*?),\s?");
            var matches = cnParse.Matches(subject + ",");
            foreach (Match m in matches)
                if (m.Groups[1].Value == part)
                    return m.Groups[2].Value;
            return String.Empty;
        }


        /// <summary>
        /// Uninstall the applet package
        /// </summary>
        public virtual bool UnInstall(String packageId)
        {

            this.m_tracer.TraceWarning("Un-installing {0}", packageId);
            // Applet check
            var applet = this.m_appletCollection.FirstOrDefault(o => o.Info.Id == packageId);
            if (applet == null)
                throw new FileNotFoundException($"Applet {packageId} is not installed");

            // Dependency check
            var dependencies = this.m_appletCollection.Where(o => o.Info.Dependencies.Any(d => d.Id == packageId));
            if (dependencies.Any())
                throw new InvalidOperationException($"Uninstalling {packageId} would break : {String.Join(", ", dependencies.Select(o => o.Info))}");

            this.UnInstallInternal(applet);

            return true;
        }

        /// <summary>
        /// Uninstall
        /// </summary>
        private void UnInstallInternal(AppletManifest applet)
        {

            // We're good to go!
            this.m_appletCollection.Remove(applet);

            var appletConfig = ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>();

            // Delete the applet registration data
            appletConfig.Applets.RemoveAll(o => o.Id == applet.Info.Id);

            if (ApplicationContext.Current.ConfigurationPersister.IsConfigured)
                ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);

            if (File.Exists(Path.Combine(appletConfig.AppletDirectory, applet.Info.Id)))
                File.Delete(Path.Combine(appletConfig.AppletDirectory, applet.Info.Id));
            if (Directory.Exists(Path.Combine(appletConfig.AppletDirectory, "assets", applet.Info.Id)))
                Directory.Delete(Path.Combine(appletConfig.AppletDirectory, "assets", applet.Info.Id), true);

            AppletCollection.ClearCaches();
        }

        /// <summary>
        /// Performs an installation 
        /// </summary>
        public virtual bool Install(AppletPackage package, bool isUpgrade = false)
        {
            this.m_tracer.TraceWarning("Installing {0}", package.Meta);

            // TODO: Verify package hash / signature
            if (!this.VerifyPackage(package))
                throw new SecurityException("Applet failed validation");
            else if (!this.m_appletCollection.VerifyDependencies(package.Meta))
            {
                this.m_tracer.TraceWarning($"Applet {package.Meta} depends on : [{String.Join(", ", package.Meta.Dependencies.Select(o => o.ToString()))}] which are missing or incompatible");
            }
            var appletSection = ApplicationContext.Current.Configuration.GetSection<AppletConfigurationSection>();
            String appletPath = Path.Combine(appletSection.AppletDirectory, package.Meta.Id);

            try
            {
                // Desearialize an prep for install

                this.m_tracer.TraceInfo("Installing applet {0} (IsUpgrade={1})", package.Meta, isUpgrade);

                ApplicationContext.Current.SetProgress(package.Meta.GetName("en"), 0.0f);
                // TODO: Verify the package

                // Copy
                if (!Directory.Exists(appletSection.AppletDirectory))
                    Directory.CreateDirectory(appletSection.AppletDirectory);

                if (File.Exists(appletPath))
                {
                    if (!isUpgrade)
                        throw new InvalidOperationException(Strings.err_duplicate_package_name);

                    // Unload the loaded applet version
                    var existingApplet = this.m_appletCollection.FirstOrDefault(o => o.Info.Id == package.Meta.Id);
                    if (existingApplet != null)
                        this.UnInstallInternal(existingApplet);
                }

                var mfst = package.Unpack();
                // Migrate data.
                if (mfst.DataSetup != null)
                {
                    foreach (var itm in mfst.DataSetup.Action)
                    {
                        Type idpType = typeof(IDataPersistenceService<>);
                        idpType = idpType.MakeGenericType(new Type[] { itm.Element.GetType() });
                        var svc = ApplicationContext.Current.GetService(idpType);
                        idpType.GetMethod(itm.ActionName).Invoke(svc, new object[] { itm.Element, TransactionMode.Commit, AuthenticationContext.SystemPrincipal  });
                    }
                }

                // Now export all the binary files out
                var assetDirectory = Path.Combine(appletSection.AppletDirectory, "assets", mfst.Info.Id);
                if (!Directory.Exists(assetDirectory))
                    Directory.CreateDirectory(assetDirectory);
                else
                    Directory.Delete(assetDirectory, true);

                for (int i = 0; i < mfst.Assets.Count; i++)
                {
                    var itm = mfst.Assets[i];
                    var itmPath = Path.Combine(assetDirectory, itm.Name);
                    ApplicationContext.Current.SetProgress($"Installing {package.Meta.GetName("en")}", 0.1f + (float)(0.8 * (float)i / mfst.Assets.Count));

                    // Get dir name and create
                    if (!Directory.Exists(Path.GetDirectoryName(itmPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(itmPath));

                    // Extract content
                    if (itm.Content is byte[])
                    {
                        if (Encoding.UTF8.GetString(itm.Content as byte[], 0, 4) == "LZIP")
                            using (var fs = File.Create(itmPath))
                            using (var ms = new MemoryStream(itm.Content as byte[]))
                            using (var lzs = new LZipStream(new NonDisposingStream(ms), SharpCompress.Compressors.CompressionMode.Decompress))
                                lzs.CopyTo(fs);
                        else
                            File.WriteAllBytes(itmPath, itm.Content as byte[]);
                        itm.Content = null;
                    }
                    else if (itm.Content is String)
                    {
                        File.WriteAllText(itmPath, itm.Content as String);
                        itm.Content = null;
                    }
                }

                // Serialize the data to disk
                using (FileStream fs = File.Create(appletPath))
                    mfst.Save(fs);

                // For now sign with SHA256
                SHA256 sha = SHA256.Create();
                package.Meta.Hash = sha.ComputeHash(File.ReadAllBytes(appletPath));
                // HACK: Re-re-remove 
                appletSection.Applets.RemoveAll(o => o.Id == package.Meta.Id);
                appletSection.Applets.Add(package.Meta.AsReference());

                ApplicationContext.Current.SetProgress(package.Meta.GetName("en"), 0.98f);

                if (ApplicationContext.Current.ConfigurationPersister.IsConfigured)
                    ApplicationContext.Current.ConfigurationPersister.Save(ApplicationContext.Current.Configuration);

                this.LoadApplet(mfst);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error installing applet {0} : {1}", package.Meta.ToString(), e);

                // Remove
                if (File.Exists(appletPath))
                {
                    File.Delete(appletPath);
                }

                throw;
            }

            return true;
        }

        /// <summary>
        /// Gets applets for the solution
        /// </summary>
        public ReadonlyAppletCollection GetApplets(string solutionId)
        {
            if (String.IsNullOrEmpty(solutionId))
                return this.m_readonlyAppletCollection;
            else
                throw new KeyNotFoundException($"Solution {solutionId} not found");
        }

        /// <summary>
        /// Install a applet solution
        /// </summary>
        public bool Install(AppletSolution solution, bool isUpgrade = false)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get the specified applet
        /// </summary>
        public AppletManifest GetApplet(string solutionId, string appletId)
        {
            if (String.IsNullOrEmpty(solutionId))
                return this.GetApplet(appletId);
            else
                throw new KeyNotFoundException($"Applet {solutionId}/{appletId} not found");
        }

        /// <summary>
        /// Get the specified package contents
        /// </summary>
        public byte[] GetPackage(string solutionId, string appletId)
        {
            if (String.IsNullOrEmpty(solutionId))
                return this.GetPackage(appletId);
            else
                throw new KeyNotFoundException($"Applet {solutionId}/{appletId} not found");
        }
    }
}

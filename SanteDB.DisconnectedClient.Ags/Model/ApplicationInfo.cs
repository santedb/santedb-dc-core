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
 * User: justi
 * Date: 2019-1-12
 */
using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Configuration.Data;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.DisconnectedClient.Xamarin.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SanteDB.DisconnectedClient.Ags.Model
{
    /// <summary>
    /// Application information
    /// </summary>
    [JsonObject("ApplicationInfo")]
    public class ApplicationInfo : DiagnosticApplicationInfo
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(ApplicationInfo));

        /// <summary>
        /// Updates
        /// </summary>
        [JsonProperty("update")]
        public List<AppletInfo> Updates { get; private set; }

        /// <summary>
        /// Application information
        /// </summary>
        public ApplicationInfo(bool checkForUpdates) : base(ApplicationServiceContext.Current.GetService<IServiceManager>().GetAllTypes().FirstOrDefault(t => t.Name == "SplashActivity")?.Assembly ?? typeof(SanteDBConfiguration).Assembly)
        {
            this.SanteDB = new DiagnosticVersionInfo(typeof(SanteDB.DisconnectedClient.Core.ApplicationContext).Assembly);

            var appService = ApplicationContext.Current.GetService<IAppletManagerService>();
            try
            {
                this.Applets = appService?.Applets?.Select(o => o.Info).ToList();

                if (checkForUpdates)
                    try
                    {
                        this.Updates = appService.Applets.Select(o => ApplicationContext.Current.GetService<IUpdateManager>().GetServerVersion(o.Info.Id)).ToList();
                        this.Updates.RemoveAll(o => new Version(appService.GetApplet(o.Id).Info.Version).CompareTo(new Version(o.Version)) >= 0);
                    }
                    catch { }

                this.Assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(o => new DiagnosticVersionInfo(o)).ToList();

                this.ServiceInfo = new List<DiagnosticServiceInfo>();
                IEnumerable<Type> types = null;
                var asmLoc = Assembly.GetEntryAssembly()?.Location;
                // Assembly load file
                if (!String.IsNullOrEmpty(asmLoc))
                    types = Directory.GetFiles(Path.GetDirectoryName(asmLoc), "*.dll").Select(o =>
                    {
                        try
                        {
                            return Assembly.LoadFile(o);
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceWarning("Could not load {0} due to {1}", o, e);
                            return null;
                        }
                    }).Where(a => a?.IsDynamic == false).SelectMany(a => a.ExportedTypes).ToList();
                else
                    types = ApplicationServiceContext.Current.GetService<IServiceManager>().GetAllTypes();


                foreach (var t in types.Where(o => o.GetInterfaces().Any(i => i.FullName == typeof(IServiceImplementation).FullName) && !o.IsGenericTypeDefinition && !o.IsAbstract && !o.IsInterface))
                {
                    this.ServiceInfo.Add(new DiagnosticServiceInfo()
                    {
                        IsRunning = (ApplicationServiceContext.Current.GetService(Type.GetType(t.AssemblyQualifiedName)) as IDaemonService)?.IsRunning ?? false,
                        Active = ApplicationServiceContext.Current.GetService(Type.GetType(t.AssemblyQualifiedName)) != null,
                        Description = t.CustomAttributes.FirstOrDefault(o => o.AttributeType.FullName == typeof(ServiceProviderAttribute).FullName)?.ConstructorArguments[0].Value?.ToString() ?? t.FullName,
                        Class = t.GetInterfaces().Any(o => o.Name.Contains("IDaemonService")) ? ServiceClass.Daemon :
                                t.GetInterfaces().Any(o => o.Name.Contains("IDataPersistenceService")) ? ServiceClass.Data :
                                t.GetInterfaces().Any(o => o.Name.Contains("IRepositoryService")) ? ServiceClass.Repository :
                                ServiceClass.Passive,
                        Type = t.AssemblyQualifiedName
                    });
                }

                this.Configuration = ApplicationContext.Current.Configuration;
                this.EnvironmentInfo = new DiagnosticEnvironmentInfo()
                {
                    OSVersion = String.Format("{0} v{1}", System.Environment.OSVersion.Platform, System.Environment.OSVersion.Version),
                    Is64Bit = System.Environment.Is64BitOperatingSystem,
                    ProcessorCount = System.Environment.ProcessorCount,
                    Version = System.Environment.Version.ToString(),
                    UsedMemory = GC.GetTotalMemory(false)
                };


                // Configuration files
                var logFileName = ApplicationContext.Current.Configuration.GetSection<DiagnosticsConfigurationSection>().TraceWriter.FirstOrDefault(o => o.TraceWriter.GetType() == typeof(FileTraceWriter)).InitializationData;

                logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", logFileName + ".log");
                var logFile = new FileInfo(logFileName);

                // File information
                this.FileInfo = new List<DiagnosticAttachmentInfo>()
                {
                    new DiagnosticAttachmentInfo() { FileDescription = "Log File", FileSize = logFile.Length, FileName = logFile.Name, Id = "log", LastWriteDate = logFile.LastWriteTime }
                };

                foreach (var con in ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().ConnectionString)
                {
                    var fi = new FileInfo(con.GetComponent("dbfile"));
                    this.FileInfo.Add(new DiagnosticAttachmentInfo() { FileDescription = con.Name, FileName = fi.FullName, LastWriteDate = fi.LastWriteTime, FileSize = fi.Length, Id = "db" });

                    // Existing path
                    if (File.Exists(Path.ChangeExtension(con.Value, "bak")))
                    {
                        fi = new FileInfo(Path.ChangeExtension(con.Value, "bak"));
                        this.FileInfo.Add(new DiagnosticAttachmentInfo() { FileDescription = con.Name + " Backup", FileName = fi.FullName, LastWriteDate = fi.LastWriteTime, FileSize = fi.Length, Id = "bak" });
                    }
                }


                this.SyncInfo = ApplicationContext.Current.GetService<ISynchronizationLogService>().GetAll().Select(o => new DiagnosticSyncInfo()
                {
                    Etag = o.LastETag,
                    LastSync = o.LastSync,
                    ResourceName = o.ResourceType,
                    Filter = o.Filter
                }).ToList();

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error gathering system info {0}", e);
            }
        }

        /// <summary>
        /// Create raw diagnostic report information
        /// </summary>
        /// <returns></returns>
        internal DiagnosticApplicationInfo ToDiagnosticReport()
        {
            return new DiagnosticApplicationInfo()
            {
                Applets = this.Applets,
                Assemblies = this.Assemblies,
                Copyright = this.Copyright,
                EnvironmentInfo = this.EnvironmentInfo,
                FileInfo = this.FileInfo,
                Info = this.Info,
                InformationalVersion = this.InformationalVersion,
                Name = this.Name,
                SanteDB = this.SanteDB,
                Product = this.Product,
                SyncInfo = this.SyncInfo,
                Version = this.Version
            };
        }

        /// <summary>
        /// Configuration
        /// </summary>
        [JsonProperty("config")]
        public SanteDBConfiguration Configuration { get; set; }

    }

}

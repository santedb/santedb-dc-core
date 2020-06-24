/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
using RestSrvr.Exceptions;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.AMI.Logging;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Configuration;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.AMI.Wcf;
using SanteDB.Rest.AMI;
using SanteDB.Rest.AMI.Resources;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Administration Management Interface Service
    /// </summary>
    /// <remarks>This class implements the service behaviors for the Disconnected Gateway</remarks>
    public class AmiServiceBehavior : AmiServiceBehaviorBase
    {

        // Resource handler tool
        private ResourceHandlerTool m_resourceHandler;

        /// <summary>
        /// Resource handler
        /// </summary>
        /// <returns></returns>
        protected override ResourceHandlerTool GetResourceHandler()
        {
            if (this.m_resourceHandler == null)
                this.m_resourceHandler = new Rest.Common.ResourceHandlerTool(
                    typeof(SecurityUserResourceHandler).Assembly.ExportedTypes
                    .Union(AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a => a.ExportedTypes))
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IApiResourceHandler).IsAssignableFrom(t)), typeof(IAmiServiceContract));
            return this.m_resourceHandler;
        }
        /// <summary>
        /// AMI service behavior
        /// </summary>
        public AmiServiceBehavior()
        {

        }

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AmiServiceBehavior));

        /// <summary>
        /// Submits a diagnostic report
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public override DiagnosticReport CreateDiagnosticReport(DiagnosticReport report)
        {
            report.ApplicationInfo = new ApplicationInfo(false);
            var attachments = new List<DiagnosticAttachmentInfo>();
            foreach (var att in report.Attachments)
            {
                // User wishes to attach configuration
                if (att.FileName == "SanteDB.config")
                {
                    // Compress
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress))
                            ApplicationContext.Current.Configuration.Save(gz);
                        attachments.Add(new DiagnosticBinaryAttachment()
                        {
                            Content = ms.ToArray(),
                            FileDescription = "Configuration file",
                            FileName = "SanteDB.config.gz",
                            Id = "config"
                        });
                    }
                }
                else // Some other file
                {
                    var logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", att.FileName);
                    if (!File.Exists(logFileName))
                        return null;

                    // Memmory stream
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress))
                        using (FileStream fs = File.OpenRead(logFileName))
                            fs.CopyTo(gz);
                        var logFile = new FileInfo(logFileName);
                        DiagnosticBinaryAttachment retVal = new DiagnosticBinaryAttachment()
                        {
                            Id = Path.GetFileName(logFileName),
                            FileDescription = Path.GetFileName(logFileName) + ".gz",
                            FileName = logFile.FullName + ".gz",
                            LastWriteDate = logFile.LastWriteTime,
                            FileSize = logFile.Length,
                            Content = ms.ToArray()
                        };
                    }
                }
            }

            // submit
            AmiServiceClient amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
            try
            {
                return amiClient.SubmitDiagnosticReport(new DiagnosticReport()
                {
                    ApplicationInfo = (report.ApplicationInfo as ApplicationInfo)?.ToDiagnosticReport(),
                    CreatedBy = report.CreatedBy,
                    CreatedByKey = report.CreatedByKey,
                    CreationTime = DateTime.Now,
                    Attachments = attachments,
                    Note = report.Note,
                    Submitter = ApplicationContext.Current.GetService<ISecurityRepositoryService>().GetUserEntity(AuthenticationContext.Current.Principal.Identity)
                });

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error filing bug report: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Get a single log file from the service
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public override LogFileInfo GetLog(string logId)
        {
            int offset = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_offset"] ?? "0"),
                    count = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_count"] ?? "2048");

            // Determine if the log file is local or from server
            var logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", logId);
            if (!File.Exists(logFileName)) // Server
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                {
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Requesting += (o, e) =>
                    {
                        e.Query.Add("_offset", offset.ToString());
                        e.Query.Add("_count", count.ToString());
                    };
                    return amiClient.GetLog(logId);
                }
                else
                    return null;
            }
            else
            {
                var logFile = new FileInfo(logFileName);


                // Verify offset
                if (offset > logFile.Length) throw new ArgumentOutOfRangeException($"Maximum size of {logFileName} is {logFile.Length}, offset is {offset}");

                using (var fs = File.OpenRead(logFileName))
                {

                    // Is count specified 
                    byte[] buffer;
                    if (offset + count > logFile.Length)
                        buffer = new byte[logFile.Length - offset];
                    else
                        buffer = new byte[count];

                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Read(buffer, 0, buffer.Length);

                    return new LogFileInfo()
                    {
                        Contents = buffer,
                        LastWrite = logFile.LastWriteTime,
                        Name = logFile.Name,
                        Size = logFile.Length
                    };
                }
            }
        }

        /// <summary>
        /// Download the log contents as a stream
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public override Stream DownloadLog(string logId)
        {
            // Determine if the log file is local or from server
            var logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", logId);
            if (!File.Exists(logFileName)) // Server
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                {
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.Headers.Add("Content-Disposition", e.Headers["Content-Disposition"] ?? $"attachment; filename={logId}.log");
                    return new MemoryStream(amiClient.Client.Get($"Log/Stream/{logId}"));
                }
                else
                    return null;
            }
            else
            {
                RestOperationContext.Current.OutgoingResponse.AddHeader("Content-Disposition", $"attachment; filename={Path.GetFileName(logFileName)}");
                RestOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
                return File.OpenRead(logFileName);
            }
        }

        /// <summary>
        /// Get logs for the application
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public override AmiCollection GetLogs()
        {
            IEnumerable<LogFileInfo> hits = new List<LogFileInfo>();
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                hits = amiClient.GetLogs().CollectionItem.OfType<LogFileInfo>();
            }
            else
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log");
                foreach (var itm in Directory.GetFiles(logDir))
                {
                    var logFile = new FileInfo(itm);
                    (hits as IList).Add(new LogFileInfo()
                    {
                        Name = Path.GetFileName(itm),
                        LastWrite = logFile.LastWriteTime,
                        Size = logFile.Length
                    });
                }
            }

            // Now compile any matching 
            int offset = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_offset"] ?? "0"),
                count = Int32.Parse(RestOperationContext.Current.IncomingRequest.QueryString["_count"] ?? "100");

            var filterExpr = QueryExpressionParser.BuildLinqExpression<LogFileInfo>(RestOperationContext.Current.IncomingRequest.QueryString.ToQuery());

            // Filter hits
            hits = hits.Where(filterExpr.Compile());

            if (RestOperationContext.Current.IncomingRequest.QueryString["_orderBy"] != null)
            {
                var parts = RestOperationContext.Current.IncomingRequest.QueryString["_orderBy"].Split(':');
                var expr = QueryExpressionParser.BuildPropertySelector<LogFileInfo>(parts[0]);
                var parm = Expression.Parameter(typeof(LogFileInfo));
                expr = Expression.Lambda<Func<LogFileInfo, dynamic>>(Expression.Convert(Expression.Invoke(expr, parm), typeof(Object)), parm);
                if (parts.Length == 0 || parts[1] == "asc")
                    hits = hits.OrderBy((Func<LogFileInfo, dynamic>)expr.Compile());
                else
                    hits = hits.OrderByDescending((Func<LogFileInfo, dynamic>)expr.Compile());
            }

            return new AmiCollection()
            {
                CollectionItem = hits.Skip(offset).Take(count).OfType<Object>().ToList(),
                Size = hits.Count(),
                Offset = offset
            };
        }

        /// <summary>
        /// Get a diagnostic report 
        /// </summary>
        /// <returns></returns>
        [Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction)]
        public override DiagnosticReport GetServerDiagnosticReport()
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.Client.Get<DiagnosticReport>("Sherlock");
            }
            else
                try
                {
                    var netSvc = ApplicationContext.Current.GetService<INetworkInformationService>();
                    var appInfo = new ApplicationInfo(RestOperationContext.Current.IncomingRequest.QueryString["_includeUpdates"] == "true" && netSvc.IsNetworkAvailable);
                    return new DiagnosticReport()
                    {
                        ApplicationInfo = appInfo,
                        Tags = new List<DiagnosticReportTag>()
                        {
                            new DiagnosticReportTag("sync.allow.offline", ApplicationContext.Current.Modes.HasFlag(SynchronizationMode.Offline).ToString()),
                            new DiagnosticReportTag("sync.allow.online", ApplicationContext.Current.Modes.HasFlag(SynchronizationMode.Online).ToString()),
                            new DiagnosticReportTag("sync.allow.sync", ApplicationContext.Current.Modes.HasFlag(SynchronizationMode.Sync).ToString())

                        }
                    };
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not retrieve app info {0}...", e);
                    throw;
                }
        }

        /// <summary>
        /// Get allowed TFA mechanisms
        /// </summary>
        public override AmiCollection GetTfaMechanisms()
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.Client.Get<AmiCollection>("Tfa");
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting TFA mechanisms: {0}", e.Message);
                throw;
            }
        }

        /// <summary>
        /// Get the available service options
        /// </summary>
        public override ServiceOptions Options()
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.Options();
            }
            else
            {
                RestOperationContext.Current.OutgoingResponse.StatusCode = (int)200;
                RestOperationContext.Current.OutgoingResponse.Headers.Add("Allow", $"GET, PUT, POST, OPTIONS, HEAD, DELETE{(ApplicationServiceContext.Current.GetService<IPatchService>() != null ? ", PATCH" : null)}");

                if (ApplicationServiceContext.Current.GetService<IPatchService>() != null)
                {
                    RestOperationContext.Current.OutgoingResponse.Headers.Add("Accept-Patch", "application/xml+oiz-patch");
                }

                var serviceOptions = new ServiceOptions
                {
                    InterfaceVersion = "1.0.0.0",
                    Resources = new List<ServiceResourceOptions>(),
                    Endpoints = (ApplicationServiceContext.Current as IServiceManager).GetServices().OfType<IApiEndpointProvider>().Select(o =>
                        new ServiceEndpointOptions
                        {
                            BaseUrl = o.Url,
                            ServiceType = o.ApiType,
                            Capabilities = o.Capabilities
                        }
                    ).ToList()
                };

                serviceOptions.Endpoints.RemoveAll(o => o.ServiceType == ServiceEndpointType.AdministrationIntegrationService);
                serviceOptions.Endpoints.RemoveAll(o => o.ServiceType == ServiceEndpointType.HealthDataService);
                serviceOptions.Endpoints.RemoveAll(o => o.ServiceType == ServiceEndpointType.AuthenticationService);

                // Get the resources which are supported
                foreach (var itm in this.m_resourceHandler.Handlers)
                {
                    var svc = this.ResourceOptions(itm.ResourceName);
                    serviceOptions.Resources.Add(svc);
                }

                serviceOptions.Endpoints.AddRange(ApplicationContext.Current.Configuration.GetSection<AgsConfigurationSection>().Services.Select(s => new ServiceEndpointOptions()
                {
                    BaseUrl = s.Endpoints.Select(o =>
                    {
                        var configHost = new Uri(o.Address);
                        this.m_traceSource.TraceVerbose("Rewriting OPTION {0}:{1} > {2}", configHost.Host, configHost.Port, RestOperationContext.Current.IncomingRequest.UserHostAddress);
                        return o.Address.Replace(configHost.Host + ":" + configHost.Port, RestOperationContext.Current.IncomingRequest.UserHostAddress);
                    }).ToArray(),
                    Capabilities = ServiceEndpointCapabilities.BearerAuth | ServiceEndpointCapabilities.Compression,
                    ServiceType = s.ServiceType == typeof(AmiServiceBehavior) ? ServiceEndpointType.AdministrationIntegrationService :
                    s.ServiceType == typeof(HdsiServiceBehavior) ? ServiceEndpointType.HealthDataService :
                    s.ServiceType == typeof(AuthenticationServiceBehavior) ? ServiceEndpointType.AuthenticationService :
                    (ServiceEndpointType)0
                }).Where(o => o.ServiceType > 0));
                return serviceOptions;
            }
        }


        /// <summary>
        /// Throw if the service is not ready
        /// </summary>
        protected override void ThrowIfNotReady()
        {
        }


        /// <summary>
        /// Create the specified resource
        /// </summary>
        public override object Create(string resourceType, object data)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<Object, Object>($"{resourceType}", restClient.Accept, data);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Create(resourceType, data);
            }
        }

        public override object CreateUpdate(string resourceType, string key, object data)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<Object, Object>($"{resourceType}/{key}", restClient.Accept, data);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.CreateUpdate(resourceType, key, data);
            }
        }

        /// <summary>
        /// Delete the specified object
        /// </summary>
        public override object Delete(string resourceType, string key)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Delete<Object>($"{resourceType}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Delete(resourceType, key);
            }
        }

        /// <summary>
        /// Get the specified object
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public override object Get(string resourceType, string key)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<Object>($"{resourceType}/{key}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Get(resourceType, key);
            }
        }

        /// <summary>
        /// Get version
        /// </summary>
        public override object GetVersion(string resourceType, string key, string versionKey)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<Object>($"{resourceType}/{key}/history/{versionKey}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.GetVersion(resourceType, key, versionKey);
            }
        }

        /// <summary>
        /// Get the history of the object
        /// </summary>
        public override AmiCollection History(string resourceType, string key)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<AmiCollection>($"{resourceType}/{key}/history");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.History(resourceType, key);
            }
        }

        /// <summary>
        /// Get resource options
        /// </summary>
        /// <param name="resourceType"></param>
        /// <returns></returns>
        public override ServiceResourceOptions ResourceOptions(string resourceType)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        return restClient.Options<ServiceResourceOptions>($"{resourceType}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.ResourceOptions(resourceType);
            }
        }

        /// <summary>
        /// Perform a search on the AMI
        /// </summary>
        public override AmiCollection Search(string resourceType)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        return restClient.Get<AmiCollection>($"{resourceType}", RestOperationContext.Current.IncomingRequest.QueryString.ToList().ToArray());
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Search(resourceType);
            }
        }

        /// <summary>
        /// Update the object
        /// </summary>
        public override object Update(string resourceType, string key, object data)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Put<Object, Object>($"{resourceType}/{key}", restClient.Accept, data);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Update(resourceType, key, data);
            }
        }

        /// <summary>
        /// Lock the specified object
        /// </summary>
        public override object Lock(string resourceType, string key)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Lock<Object>($"{resourceType}/{key}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Lock(resourceType, key);
            }
        }

        /// <summary>
        /// Unlock the resource
        /// </summary>
        public override object UnLock(string resourceType, string key)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Unlock<Object>($"{resourceType}/{key}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }

                else
                    throw new FaultException(502);
            }
            else
            {
                return base.UnLock(resourceType, key);
            }
        }

    }
}

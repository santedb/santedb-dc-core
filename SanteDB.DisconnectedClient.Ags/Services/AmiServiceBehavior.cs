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
using SanteDB.DisconnectedClient.Core.Interop;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.AMI.Logging;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.Messaging.AMI.Client;
using SanteDB.Messaging.AMI.Wcf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanteDB.Rest.AMI.Resources;
using SanteDB.Rest.Common;
using SanteDB.DisconnectedClient.Core.Services;
using RestSrvr;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Security;
using RestSrvr.Exceptions;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// AMI Service behavior base
    /// </summary>
    public class AmiServiceBehavior : AmiServiceBehaviorBase
    {

        /// <summary>
        /// AMI service behavior
        /// </summary>
        public AmiServiceBehavior() : base(new Rest.Common.ResourceHandlerTool(typeof(SecurityUserResourceHandler).Assembly.ExportedTypes.Where(t => !t.IsAbstract && !t.IsInterface && typeof(IResourceHandler).IsAssignableFrom(t))))
        {
        }

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AmiServiceBehavior));

        /// <summary>
        /// Submits a diagnostic report
        /// </summary>
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
                    Submitter = AuthenticationContext.Current.Session.UserEntity
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
        public override LogFileInfo GetLog(string logId)
        {
            // Determine if the log file is local or from server
            var logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", logId);
            if (!File.Exists(logFileName)) // Server
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                {
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    return amiClient.GetLog(logId);
                }
                else
                    return null;
            }
            else
            {
                var logFile = new FileInfo(logFileName);
                return new LogFileInfo()
                {
                    Contents = File.ReadAllBytes(logFileName),
                    LastWrite = logFile.LastWriteTime,
                    Name = logFile.Name,
                    Size = logFile.Length
                };
            }
        }

        /// <summary>
        /// Get logs for the application
        /// </summary>
        public override AmiCollection GetLogs()
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.GetLogs();
            }
            else
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log");
                AmiCollection retVal = new AmiCollection();
                foreach (var itm in Directory.GetFiles(logDir))
                {
                    var logFile = new FileInfo(itm);
                    retVal.CollectionItem.Add(new LogFileInfo()
                    {
                        Name = Path.GetFileName(itm),
                        LastWrite = logFile.LastWriteTime,
                        Size = logFile.Length
                    });
                }
                return retVal;
            }
        }

        /// <summary>
        /// Get a diagnostic report 
        /// </summary>
        /// <returns></returns>
        public override DiagnosticReport GetServerDiagnosticReport()
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.GetServerDiagnoticReport();
            }
            else
                try
                {
                    var netSvc = ApplicationContext.Current.GetService<INetworkInformationService>();
                    var appInfo = new ApplicationInfo(RestOperationContext.Current.IncomingRequest.QueryString["_includeUpdates"] == "true" && netSvc.IsNetworkAvailable);
                    return new DiagnosticReport()
                    {
                        ApplicationInfo = appInfo
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
                var resetService = ApplicationContext.Current.GetService<ITwoFactorRequestService>();
                if (resetService == null)
                    throw new InvalidOperationException(Strings.err_reset_not_supported);
                return new AmiCollection()
                {
                    CollectionItem = resetService.GetResetMechanisms().OfType<Object>().ToList()
                };
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
            if(RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                return amiClient.Options();
            }
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// Send a TFA secret
        /// </summary>
        public override void SendTfaSecret(TfaRequestInfo resetInfo)
        {
            try
            {
                var resetService = ApplicationContext.Current.GetService<ITwoFactorRequestService>();
                if (resetService == null)
                    throw new InvalidOperationException(Strings.err_reset_not_supported);
                resetService.SendVerificationCode(resetInfo.ResetMechanism, resetInfo.Verification, resetInfo.UserName, resetInfo.Purpose);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting sending secret: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Throw if the service is not ready
        /// </summary>
        protected override void ThrowIfNotReady()
        {
        }

        /// <summary>
        /// Demand permission
        /// </summary>
        protected override void Demand(string policyId)
        {
            new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, policyId).Demand();
        }

        /// <summary>
        /// Create the specified resource
        /// </summary>
        public override object Create(string resourceType, object data)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<Object, Object>($"/{resourceType}", restClient.Accept, data);
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<Object, Object>($"/{resourceType}/{key}", restClient.Accept, data);
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Delete<Object>($"/{resourceType}");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<Object>($"/{resourceType}/{key}");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<Object>($"/{resourceType}/{key}/history/{versionKey}");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<AmiCollection>($"/{resourceType}/{key}/history");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        return restClient.Options<ServiceResourceOptions>($"/{resourceType}");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        return restClient.Get<AmiCollection>($"/{resourceType}", RestOperationContext.Current.IncomingRequest.QueryString.ToList().ToArray());
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Put<Object, Object>($"/{resourceType}/{key}", restClient.Accept, data);
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Lock<Object>($"/{resourceType}/{key}");
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
            if (RestOperationContext.Current.IncomingRequest.QueryString["_extern"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("ami");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Unlock<Object>($"/{resourceType}/{key}");
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

using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Client.Exceptions;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Services;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Diagnostic report service which can collect bug reports in an offline environment for later sending
    /// </summary>
    public class QueuedDiagnosticReportService : IDataPersistenceService<DiagnosticReport>
    {
        private readonly ISynchronizationQueueManager m_queueManager;
        private readonly IConfigurationManager m_configurationManager;
        private readonly ILogManagerService m_logManagerService;
        private readonly IAppletManagerService m_appletManagerService;
        private readonly IOperatingSystemInfoService m_operatingSystemInfoService;
        private readonly INetworkInformationService m_networkInformationService;

        /// <summary>
        /// DI ctor
        /// </summary>
        public QueuedDiagnosticReportService(ISynchronizationQueueManager queueManager, INetworkInformationService networkInformationService, IOperatingSystemInfoService operatingSystemInfoService, IAppletManagerService appletManagerService, ILogManagerService logManagerService, IConfigurationManager configurationManager)
        {
            this.m_queueManager = queueManager;
            this.m_configurationManager = configurationManager;
            this.m_logManagerService = logManagerService;
            this.m_appletManagerService = appletManagerService;
            this.m_operatingSystemInfoService = operatingSystemInfoService;
            this.m_networkInformationService = networkInformationService;
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Offline Diagnostics Report Submitter";

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Inserted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Inserting;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Updated;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Updating;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Deleted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Deleting;
        /// <inheritdoc/>
        public event EventHandler<QueryResultEventArgs<DiagnosticReport>> Queried;
        /// <inheritdoc/>
        public event EventHandler<QueryRequestEventArgs<DiagnosticReport>> Querying;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievingEventArgs<DiagnosticReport>> Retrieving;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievedEventArgs<DiagnosticReport>> Retrieved;
#pragma warning restore 

        /// <inheritdoc/>
        public long Count(Expression<Func<DiagnosticReport, bool>> query, IPrincipal authContext = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Delete(Guid key, TransactionMode transactionMode, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Get(Guid key, Guid? versionKey, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Insert(DiagnosticReport data, TransactionMode transactionMode, IPrincipal principal)
        {
            try
            {
                for (int i = 0; i < data.Attachments.Count; i++)
                {
                    using (AuthenticationContext.EnterSystemContext())
                    {
                        if (data.Attachments[i].GetType().Name == nameof(DiagnosticAttachmentInfo))
                        {
                            switch (data.Attachments[i].FileName)
                            {
                                case "SanteDB.config":
                                    using (var ms = new MemoryStream())
                                    {
                                        this.m_configurationManager.Configuration.Save(ms);
                                        data.Attachments[i] = new DiagnosticBinaryAttachment()
                                        {
                                            Content = ms.ToArray(),
                                            ContentType = "text/xml",
                                            FileDescription = "Configuration",
                                            FileName = "santedb.config.xml",
                                            FileSize = ms.Length
                                        };
                                    }
                                    break;
                                case "SanteDB.log":
                                    var newestFile = this.m_logManagerService.GetLogFiles().OrderByDescending(o => o.LastWriteTime).First();
                                    using (var fr = newestFile.OpenText())
                                    {
                                        data.Attachments[i] = new DiagnosticTextAttachment()
                                        {
                                            Content = fr.ReadToEnd(),
                                            ContentType = "text/plain",
                                            FileDescription = "Log File",
                                            FileName = newestFile.Name,
                                            LastWriteDate = newestFile.LastWriteTime
                                        };
                                    }
                                    break;
                            }
                        }
                    }
                }

                data.ApplicationInfo = new DiagnosticApplicationInfo(Assembly.GetEntryAssembly() ?? this.GetType().Assembly);
                data.ApplicationInfo.Applets = this.m_appletManagerService.Applets.Select(o => o.Info).ToList();
                data.Tags = new List<DiagnosticReportTag>();
                data.Tags.Add(new DiagnosticReportTag("user.name", AuthenticationContext.Current.Principal.Identity.Name));
                if (AuthenticationContext.Current.Principal is IClaimsPrincipal icp) {
                    data.Tags.AddRange(icp.Claims.Select(o => new DiagnosticReportTag("ses.claim", $"{o.Type}={o.Value}")));
                }
                data.Tags.Add(new DiagnosticReportTag("os.type", this.m_operatingSystemInfoService.OperatingSystem.ToString()));
                data.Tags.Add(new DiagnosticReportTag("os.version", this.m_operatingSystemInfoService.VersionString));
                data.Tags.Add(new DiagnosticReportTag("os.manufacturer", this.m_operatingSystemInfoService.ManufacturerName));
                data.Tags.Add(new DiagnosticReportTag("dev.name", this.m_operatingSystemInfoService.MachineName));
                data.Tags.Add(new DiagnosticReportTag("net.wifi", this.m_networkInformationService.IsNetworkWifi.ToString()));
                data.Tags.Add(new DiagnosticReportTag("net.name", this.m_networkInformationService.GetHostName()));
                data.Tags.Add(new DiagnosticReportTag("net.ip", String.Join(",", this.m_networkInformationService.GetInterfaces().Select(o => $"{o.InterfaceType} {o.IpAddress} - {o.MacAddress}"))));
                this.m_queueManager.GetAdminQueue().Enqueue(data, SynchronizationQueueEntryOperation.Insert);
                return data;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error enqueuing the bug report", e);

            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<DiagnosticReport> Query(Expression<Func<DiagnosticReport, bool>> query, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<DiagnosticReport> Query(Expression<Func<DiagnosticReport, bool>> query, int offset, int? count, out int totalResults, IPrincipal principal, params ModelSort<DiagnosticReport>[] orderBy)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Update(DiagnosticReport data, TransactionMode transactionMode, IPrincipal principal)
        {
            throw new NotSupportedException();
        }
    }
}

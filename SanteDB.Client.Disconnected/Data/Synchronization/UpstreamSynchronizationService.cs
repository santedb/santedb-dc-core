using SanteDB.Client.Disconnected.Data.Synchronization.Configuration;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class UpstreamSynchronizationService : ISynchronizationService, IDaemonService
    {
        readonly Tracer _Tracer;

        readonly SynchronizationConfigurationSection _Configuration;

        readonly IUpstreamIntegrationService _UpstreamIntegrationService;
        readonly IUpstreamAvailabilityProvider _UpstreamAvailabilityProvider;
        readonly IThreadPoolService _ThreadPool;
        readonly ISynchronizationLogService _SynchronizationLogService;
        readonly ISynchronizationQueueManager _QueueManager;
        readonly ISubscriptionRepository _SubscriptionRepository;
        readonly IJobManagerService _JobManager;
        readonly IServiceManager _ServiceManager;
        readonly IRestClientFactory _RestClientFactory;

        public bool IsRunning => true;

        public string ServiceName => "Remote Data Synchronization Service";

        public bool IsSynchronizing { get; private set; }

        public event EventHandler Starting;
        public event EventHandler Started;
        public event EventHandler Stopping;
        public event EventHandler Stopped;
        public event EventHandler<SynchronizationEventArgs> PullCompleted;
        public event EventHandler<SynchronizationEventArgs> PushCompleted;

        public UpstreamSynchronizationService(
            IConfigurationManager configurationManager, 
            IUpstreamIntegrationService upstreamIntegrationService, 
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider, 
            IThreadPoolService threadPool, 
            ISynchronizationLogService synchronizationLogService, 
            ISynchronizationQueueManager queueManager,
            ISubscriptionRepository subscriptionRepository,
            IJobManagerService jobManagerService,
            IServiceManager serviceManager,
            IRestClientFactory restClientFactory)
        {
            _Tracer = new Tracer(nameof(UpstreamSynchronizationService));
            _Configuration = configurationManager.GetSection<SynchronizationConfigurationSection>();
            _UpstreamIntegrationService = upstreamIntegrationService;
            _UpstreamAvailabilityProvider = upstreamAvailabilityProvider;
            _ThreadPool = threadPool;
            _SynchronizationLogService = synchronizationLogService;
            _QueueManager = queueManager;
            _JobManager = jobManagerService;
            _ServiceManager = serviceManager;
            _RestClientFactory = restClientFactory;
        }

        protected virtual void GetSubscriptionDefinitions()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                using (var restclient = _RestClientFactory?.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                {

                    using (var client = new AmiServiceClient(restclient))
                    {
                        var coll = client.GetSubscriptionDefinitions();

                        var subscriptions = coll?.CollectionItem?.OfType<SubscriptionDefinition>();
                    }
                }
            }
        }

        public bool Fetch(Type modelType)
        {
            throw new NotImplementedException();
        }

        public void Pull(SubscriptionTriggerType trigger)
        {
            throw new NotImplementedException();
        }

        public int Pull(Type modelType)
        {
            throw new NotImplementedException();
        }

        public int Pull(Type modelType, NameValueCollection filter)
        {
            throw new NotImplementedException();
        }

        public int Pull(Type modelType, NameValueCollection filter, bool always)
        {
            throw new NotImplementedException();
        }

        public void Push()
        {
            throw new NotImplementedException();
        }

        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            if (_Configuration?.Mode != SynchronizationMode.Online && _Configuration.PollInterval != default(TimeSpan))
            {
                ApplicationServiceContext.Current.Started += (s, e) =>
                {
                    try
                    {
                        this.Pull(SubscriptionTriggerType.OnStart);

                        var job = _ServiceManager.CreateInjected<UpstreamSynchronizationJob>();
                        _JobManager.AddJob(job, JobStartType.DelayStart);
                        _JobManager.SetJobSchedule(job, _Configuration.PollInterval);

                    }
                    catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                    {
                        _Tracer.TraceError("Error Adding Upstream Sync Job: {0}", ex);
                    }

                };
            }

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }


    }
}

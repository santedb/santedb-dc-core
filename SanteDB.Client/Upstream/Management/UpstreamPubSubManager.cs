/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2023-3-10
 */
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Query;
using SanteDB.Core.PubSub;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream publish and subscribe 
    /// </summary>
    public class UpstreamPubSubManager : UpstreamServiceBase, IPubSubManagerService
    {

        private readonly ILocalizationService m_localizationService;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(UpstreamJobManager));

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamPubSubManager(ILocalizationService localizationService,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Publish/Subscribe Manager";

        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> Subscribing;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> Subscribed;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> UnSubscribing;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> UnSubscribed;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> Activating;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<PubSubSubscriptionDefinition>> DeActivating;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> Activated;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<PubSubSubscriptionDefinition>> DeActivated;

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition ActivateSubscription(Guid key, bool isActive)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Post<ParameterCollection, PubSubSubscriptionDefinition>($"{typeof(PubSubSubscriptionDefinition).GetSerializationName()}/{key}/$activate", new ParameterCollection(new Parameter("status", isActive)));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubChannelDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<PubSubChannelDefinition> FindChannel(Expression<Func<PubSubChannelDefinition, bool>> filter)
        {
            return new UpstreamQueryResultSet<PubSubChannelDefinition, AmiCollection>(this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal), filter);
        }

        /// <inheritdoc/>
        [Obsolete("Use Find(Expression)", true)]
        public IEnumerable<PubSubChannelDefinition> FindChannel(Expression<Func<PubSubChannelDefinition, bool>> filter, int offset, int count, out int totalResults)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<PubSubSubscriptionDefinition> FindSubscription(Expression<Func<PubSubSubscriptionDefinition, bool>> filter)
        {
            return new UpstreamQueryResultSet<PubSubSubscriptionDefinition, AmiCollection>(this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal), filter);
        }

        /// <inheritdoc/>
        [Obsolete("Use Find(Expression)", true)]
        public IEnumerable<PubSubSubscriptionDefinition> FindSubscription(Expression<Func<PubSubSubscriptionDefinition, bool>> filter, int offset, int count, out int totalResults)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public PubSubChannelDefinition GetChannel(Guid id)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Get<PubSubChannelDefinition>($"{typeof(PubSubChannelDefinition).GetSerializationName()}/{id}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(PubSubChannelDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition GetSubscription(Guid id)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Get<PubSubSubscriptionDefinition>($"{typeof(PubSubSubscriptionDefinition).GetSerializationName()}/{id}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(PubSubSubscriptionDefinition) }), e);
            }

        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition GetSubscriptionByName(string name)
        {
            return this.FindSubscription(o => o.Name == name && o.ObsoletionTime == null).FirstOrDefault();
        }

        /// <inheritdoc/>
        public PubSubChannelDefinition RegisterChannel(string name, Type dispatcherFactoryType, Uri endpoint, IDictionary<string, string> settings)
        {
            return this.RegisterChannel(name, DispatcherFactoryUtil.FindDispatcherFactoryByType(dispatcherFactoryType)?.Id, endpoint, settings);
        }
        /// <inheritdoc/>
        public PubSubChannelDefinition RegisterChannel(string name, string dispatchFactoryId, Uri endpoint, IDictionary<string, string> settings)
        {

            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Post<PubSubChannelDefinition, PubSubChannelDefinition>($"{typeof(PubSubChannelDefinition).GetSerializationName()}", new PubSubChannelDefinition()
                    {
                        Name = name,
                        Endpoint = endpoint.ToString(),
                        IsActive = true,
                        DispatcherFactoryId = dispatchFactoryId,
                        Settings = settings.Select(o => new PubSubChannelSetting() { Name = o.Key, Value = o.Value }).ToList()
                    });
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubChannelDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition RegisterSubscription<TModel>(string name, string description, PubSubEventType events, Expression<Func<TModel, bool>> filter, Guid channelId, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            var filterHdsi = QueryExpressionBuilder.BuildQuery(filter);
            return this.RegisterSubscription(typeof(TModel), name, description, events, filterHdsi.ToHttpString(), channelId, supportAddress, notBefore, notAfter);
        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition RegisterSubscription(Type modelType, string name, string description, PubSubEventType events, string hdsiFilter, Guid channelId, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            try
            {
                using(var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Post<PubSubSubscriptionDefinition, PubSubSubscriptionDefinition>($"{typeof(PubSubSubscriptionDefinition).GetSerializationName()}", new PubSubSubscriptionDefinition()
                    {
                        ChannelKey = channelId,
                        Description = description,
                        Event = events,
                        Filter = new List<string>() { hdsiFilter },
                        Name = name,
                        NotAfter = notAfter?.DateTime,
                        NotBefore = notBefore?.DateTime,
                        ResourceTypeName = modelType.GetSerializationName(),
                        SupportContact = supportAddress
                    });
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubSubscriptionDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubChannelDefinition RemoveChannel(Guid id)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Delete<PubSubChannelDefinition>($"{typeof(PubSubChannelDefinition).GetSerializationName()}/{id}");
                }
            }
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubChannelDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition RemoveSubscription(Guid id)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Delete<PubSubSubscriptionDefinition>($"{typeof(PubSubSubscriptionDefinition).GetSerializationName()}/{id}");
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubSubscriptionDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubChannelDefinition UpdateChannel(Guid key, string name, Uri endpoint, IDictionary<string, string> settings)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Put<PubSubChannelDefinition, PubSubChannelDefinition>($"{typeof(PubSubChannelDefinition).GetSerializationName()}/{key}", new PubSubChannelDefinition()
                    {
                        Name = name,
                        Endpoint = endpoint.ToString(),
                        Settings = settings.Select(o => new PubSubChannelSetting() { Name = o.Key, Value = o.Value }).ToList()
                    });
                }
            }  
            catch(Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubChannelDefinition) }), e);
            }
        }

        /// <inheritdoc/>
        public PubSubSubscriptionDefinition UpdateSubscription(Guid key, string name, string description, PubSubEventType events, string hdsiFilter, string supportAddress = null, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    return client.Put<PubSubSubscriptionDefinition, PubSubSubscriptionDefinition>($"{typeof(PubSubSubscriptionDefinition).GetSerializationName()}/{key}", new PubSubSubscriptionDefinition()
                    {
                        Description = description,
                        Event = events,
                        Filter = new List<string>() { hdsiFilter },
                        Name = name,
                        NotAfter = notAfter?.DateTime,
                        NotBefore = notBefore?.DateTime,
                        SupportContact = supportAddress
                    });
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = nameof(PubSubSubscriptionDefinition) }), e);
            }

        }
    }
}

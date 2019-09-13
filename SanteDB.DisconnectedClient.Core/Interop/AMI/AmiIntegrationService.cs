﻿/*
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Security;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.Core.Interop.AMI
{
    /// <summary>
    /// Represents an integration service for administrative data
    /// </summary>
	public class AmiIntegrationService : IAdministrationIntegrationService
    {

        /// <summary>
        /// AMI Integration Service
        /// </summary>
        public String ServiceName => "AMI Integration Service";

        // Cached credential
        private IPrincipal m_cachedCredential = null;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AmiIntegrationService));

        /// <summary>
        /// Fired on response
        /// </summary>
        public event EventHandler<RestResponseEventArgs> Responding;

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Gets current credentials
        /// </summary>
        private Credentials GetCredentials(IRestClient client)
        {
            try
            {
                var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

                AuthenticationContext.Current = new AuthenticationContext(this.m_cachedCredential ?? AuthenticationContext.Current.Principal);

                // TODO: Clean this up - Login as device account
                if (!AuthenticationContext.Current.Principal.Identity.IsAuthenticated ||
                    ((AuthenticationContext.Current.Principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                {
                    AuthenticationContext.Current = new AuthenticationContext(ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret));
                    this.m_cachedCredential = AuthenticationContext.Current.Principal;
                }
                return client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Find the specified type
        /// </summary>
        public Bundle Find(Type modelType, NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null)
        {
            try
            {
                var method = this.GetType().GetRuntimeMethod("Find", new Type[] { typeof(NameValueCollection), typeof(int), typeof(int?), typeof(IntegrationQueryOptions) }).MakeGenericMethod(new Type[] { modelType });
                return method.Invoke(this, new object[] { filter, offset, count, options }) as Bundle;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

        /// <summary>
        /// Find the specified object
        /// </summary>
        public Bundle Find<TModel>(Expression<Func<TModel, bool>> predicate, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Requesting += IntegrationQueryOptions.CreateRequestingHandler(options);
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);

                if (amiClient.Client.Credentials == null) return null;

                switch (typeof(TModel).Name)
                {
                    case "SecurityUser":
                        return new Bundle()
                        {
                            Item = amiClient.GetUsers((Expression<Func<SecurityUser, bool>>)(Expression)predicate).CollectionItem.OfType<SecurityUserInfo>().Select(o => o.Entity as IdentifiedData).ToList()
                        };
                    default:
                        throw new NotSupportedException($"AMI servicing not supported for {typeof(TModel).Name}");
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Find the specified object with query filters
        /// </summary>
        public Bundle Find<TModel>(NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            var predicate = QueryExpressionParser.BuildLinqExpression<TModel>(filter);
            return this.Find<TModel>(predicate, offset, count, options);
        }

        /// <summary>
        /// Get the specified data
        /// </summary>
        public IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, IntegrationQueryOptions options = null)
        {
            try
            {
                var method = this.GetType().GetRuntimeMethod("Get", new Type[] { typeof(Guid), typeof(Guid?), typeof(IntegrationQueryOptions) }).MakeGenericMethod(new Type[] { modelType });
                return method.Invoke(this, new object[] { key, versionKey, options }) as IdentifiedData;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }
        }

        /// <summary>
        /// Perform the GET operation
        /// </summary>
        public TModel Get<TModel>(Guid key, Guid? versionKey, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Requesting += IntegrationQueryOptions.CreateRequestingHandler(options);
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                if (amiClient.Client.Credentials == null) return null;

                switch (typeof(TModel).Name)
                {
                    case "SecurityUser":
                        return amiClient.GetUser(key) as TModel;
                    default:
                        throw new NotSupportedException($"AMI servicing not supported for {typeof(TModel).Name}");
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Inserts the specified identified data in the back-end
        /// </summary>
        public void Insert(IdentifiedData data)
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                if (amiClient.Client.Credentials == null) return;

                switch (data.GetType().Name)
                {
                    case "AuditSubmission":
                        // Only send audits over wifi
                        if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkWifi ||
                            ApplicationContext.Current.GetService<IQueueManagerService>().Admin.Count() > 10)
                        {
                            if (data is AuditSubmission)
                                foreach (var a in (data as AuditSubmission).Audit)
                                {
                                    AuditUtil.AddLocalDeviceActor(a);
                                }
                            amiClient.SubmitAudit(data as AuditSubmission);
                        }
                        break;
                    default:
                        throw new NotSupportedException($"AMI servicing not supported for {data.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Returns true if the service is available
        /// </summary>
        /// <returns></returns>
        public bool IsAvailable()
        {
            try
            {
                //var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                var networkInformationService = ApplicationContext.Current.GetService<INetworkInformationService>();
                if (networkInformationService.IsNetworkAvailable)
                {
                    var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                    amiClient.Client.Credentials = new NullCredentials();
                    amiClient.Client.Description.Endpoint[0].Timeout = 5000;
                    return amiClient.Ping();
                }
                else
                    return false;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Unable to determine network state: {e}");
                return false;
            }
        }

        /// <summary>
        /// Attempt an obsolete on the specified resource
        /// </summary>
        public void Obsolete(IdentifiedData data, bool forceObsolete = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attempt an update on the specified resource
        /// </summary>
        public void Update(IdentifiedData data, bool forceUpdate = false)
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);
                if (amiClient.Client.Credentials == null) return;

                switch (data.GetType().Name)
                {
                    case "SecurityUser":
                        amiClient.UpdateUser(data.Key.Value, new SanteDB.Core.Model.AMI.Auth.SecurityUserInfo(data as SecurityUser));
                        break;
                    default:
                        throw new NotSupportedException($"AMI servicing not supported for {data.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the security user.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Returns the security user for the given key or null if no security user is found.</returns>
        public SecurityUser GetSecurityUser(Guid key)
        {
            try
            {
                var amiClient = new AmiServiceClient(ApplicationContext.Current.GetRestClient("ami"));
                amiClient.Client.Requesting += IntegrationQueryOptions.CreateRequestingHandler(null);
                amiClient.Client.Credentials = this.GetCredentials(amiClient.Client);

                return amiClient.GetUser(key)?.Entity;
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error contacting AMI: {0}", ex);
                throw;
            }
        }
    }
}

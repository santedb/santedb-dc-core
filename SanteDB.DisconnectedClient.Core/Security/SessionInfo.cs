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
using SanteDB.Core.Api.Security;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Core.Security
{
    /// <summary>
    /// Session information
    /// </summary>
    [JsonObject("SessionInfo"), XmlType("SessionInfo", Namespace = "http://santedb.org/model")]
    public class SessionInfo : IdentifiedData, ISession
    {

        // The entity 
        private UserEntity m_entity;

        // Lock
        private object m_syncLock = new object();

        /// <summary>
        /// Default ctor
        /// </summary>
        public SessionInfo()
        {
            this.Key = Guid.NewGuid();
        }

        // The tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SessionInfo));

        /// <summary>
        /// Create the session object from the principal
        /// </summary>
        internal SessionInfo(IPrincipal principal, DateTime? expiry)
        {
            this.ProcessPrincipal(principal, expiry);
        }

        private object ApplicationContextIDataPersistenceService<T>()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the principal of the session
        /// </summary>
        [JsonIgnore, DataIgnore]
        public IPrincipal Principal { get; private set; }

        /// <summary>
        /// Clear the set cache
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void ClearCached() { this.m_entity = null; }

        /// <summary>
        /// Gets the user entity
        /// </summary>
        [JsonProperty("entity")]
        public UserEntity UserEntity
        {
            get
            {
                if (this.m_entity != null || this.Principal == null)
                    return this.m_entity;

                // HACK: Find a better way
                var userService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

                UserEntity entity = null;
                try
                {
                    entity = userService.GetUserEntity(this.Principal.Identity);

                    if (entity == null && this.SecurityUser != null)
                        entity = new UserEntity()
                        {
                            SecurityUserKey = this.SecurityUser.Key,
                            LanguageCommunication = new List<PersonLanguageCommunication>() { new PersonLanguageCommunication(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, true) },
                            Telecoms = new List<EntityTelecomAddress>()
                            {
                                                    new EntityTelecomAddress(TelecomAddressUseKeys.Public, this.SecurityUser.Email ?? this.SecurityUser.PhoneNumber)
                            },
                            Names = new List<EntityName>()
                            {
                                                    new EntityName() { NameUseKey =  NameUseKeys.OfficialRecord, Component = new List<EntityNameComponent>() { new EntityNameComponent(NameComponentKeys.Given, this.SecurityUser.UserName) } }
                            }
                        };
                    else
                        this.m_entity = entity;
                    return entity;
                }
                catch { return null; }

            }
        }

        /// <summary>
        /// Gets or sets the security user information
        /// </summary>
        [JsonProperty("user")]
        public SecurityUser SecurityUser { get; set; }

        /// <summary>
        /// Gets the user name
        /// </summary>
        [JsonProperty("username")]
        public string UserName { get; set; }

        /// <summary>
        /// Gets the roles to which the identity belongs
        /// </summary>
        [JsonProperty("roles")]
        public List<String> Roles { get; set; }

        /// <summary>
        /// True if authenticated
        /// </summary>
        [JsonProperty("isAuthenticated")]
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets or sets the mechanism
        /// </summary>
        [JsonProperty("method")]
        public String AuthenticationType { get; set; }

        /// <summary>
        /// Expiry time
        /// </summary>
        [JsonProperty("exp")]
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Issued time
        /// </summary>
        [JsonProperty("nbf")]
        public DateTime Issued { get; set; }

        /// <summary>
        /// Gets or sets the access token
        /// </summary>
        [JsonProperty("access_token")]
        public String Token { get; set; }

        /// <summary>
        /// Gets or sets the JWT token
        /// </summary>
        [JsonProperty("id_token")]
        public string IdentityToken { get; set; }

        /// <summary>
        /// Gets or sets the refresh token
        /// </summary>
        [JsonProperty("refresh_token")]
        public String RefreshToken { get; set; }

        /// <summary>
        /// Gets the display name
        /// </summary>
        [JsonProperty("displayName")]
        public String DisplayName =>
             this.UserEntity?.Names.FirstOrDefault()?.ToString() ?? this.UserName;

        /// <summary>
        /// Issue date
        /// </summary>
        public override DateTimeOffset ModifiedOn
        {
            get
            {
                return this.Issued;
            }
        }

        /// <summary>
        /// Gets the session ID
        /// </summary>
        byte[] ISession.Id => Encoding.UTF8.GetBytes(this.Token);

        /// <summary>
        /// Not before
        /// </summary>
        DateTimeOffset ISession.NotBefore => this.Issued;

        /// <summary>
        /// Not after
        /// </summary>
        DateTimeOffset ISession.NotAfter => this.Expiry;

        /// <summary>
        /// Refresh token
        /// </summary>
        byte[] ISession.RefreshToken => Encoding.UTF8.GetBytes(this.RefreshToken);
        
        /// <summary>
        /// Process a principal
        /// </summary>
        /// <param name="principal"></param>
        private void ProcessPrincipal(IPrincipal principal, DateTime? expiry)
        {
            this.UserName = principal.Identity.Name;
            this.IsAuthenticated = principal.Identity.IsAuthenticated;
            this.AuthenticationType = principal.Identity.AuthenticationType;
            this.Principal = principal;
            if (principal is IClaimsPrincipal)
                this.Token = principal.ToString();
            this.RefreshToken = BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", ""); // TODO: Sign this

            Guid sid = Guid.Empty;

            // Expiry / etc
            if (principal is IClaimsPrincipal)
            {
                var cp = principal as IClaimsPrincipal;

                this.Issued = (cp.FindFirst(SanteDBClaimTypes.AuthenticationInstant)?.AsDateTime().ToLocalTime() ?? DateTime.Now);
                this.Expiry = expiry ?? (cp.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MaxValue);
                this.Roles = cp.Claims.Where(o => o.Type == SanteDBClaimTypes.DefaultRoleClaimType)?.Select(o => o.Value)?.ToList();
                this.AuthenticationType = cp.FindFirst(SanteDBClaimTypes.AuthenticationMethod)?.Value;
                if (cp.HasClaim(o => o.Type == SanteDBClaimTypes.Sid))
                    Guid.TryParse(cp.FindFirst(SanteDBClaimTypes.Sid)?.Value, out sid);
            }
            else
            {
                IRoleProviderService rps = ApplicationContext.Current.GetService<IRoleProviderService>();
                this.Roles = rps.GetAllRoles(this.UserName).ToList();
                this.Issued = DateTime.Now;
                this.Expiry = expiry ?? DateTime.MaxValue;
            }

            /// Identity token (not signed by this class)
            this.IdentityToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                nbf = this.Issued,
                exp = this.Expiry,
                role = this.Roles.FirstOrDefault(),
                authmethod = this.AuthenticationType,
                unique_name = this.UserName,
                scope = (principal as IClaimsPrincipal)?.Claims.Where(o=>o.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(o=>o.Value).ToArray(),
                sub = sid.ToString()
            })));

            // Grab the user entity
            String errDetail = String.Empty;

            // Try to get user entity
            try
            {
                var userService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
                var securityUser = userService.GetUser(principal.Identity);
                if (securityUser == null) // Not yet persisted, get from server
                    this.SecurityUser = new SecurityUser()
                    {
                        Key = Guid.Parse((principal as IClaimsPrincipal).FindFirst(SanteDBClaimTypes.Sid).Value),
                        UserName = principal.Identity.Name
                    };
                else
                    this.SecurityUser = securityUser;

                // User entity available?
                this.m_entity = userService.GetUserEntity(principal.Identity);

                // Attempt to download if the user entity is null
                // Or if there are no relationships of type dedicated service dedicated service delivery location to force a download of the user entity 
                var amiService = ApplicationContext.Current.GetService<IClinicalIntegrationService>();
                if (this.m_entity == null || amiService != null && amiService.IsAvailable() || this.m_entity?.Relationships.All(r => r.RelationshipTypeKey != EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation) == true)
                {
                    int t = 0;
                    sid = Guid.Parse((principal as IClaimsPrincipal)?.FindFirst(SanteDBClaimTypes.Sid)?.Value ?? ApplicationContext.Current.GetService<IFastQueryDataPersistenceService<SecurityUser>>().QueryFast(o => o.UserName == principal.Identity.Name, Guid.Empty , 0, 1, out t).FirstOrDefault()?.Key.ToString());
                    this.m_entity = amiService?.Find<UserEntity>(o => o.SecurityUser.Key == sid, 0, 1, null).Item?.OfType<UserEntity>().FirstOrDefault();

                    ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(o =>
                    {
                        var persistence = ApplicationContext.Current.GetService<IDataPersistenceService<Entity>>();
                        try
                        {
                            if (persistence?.Get((o as Entity).Key.Value, null, true, AuthenticationContext.SystemPrincipal) == null)
                                persistence?.Insert(o as Entity, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                            else
                                persistence?.Update(o as Entity, TransactionMode.Commit, AuthenticationContext.SystemPrincipal);
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceError("Could not create / update user entity for logged in user: {0}", e);
                        }
                    }, this.m_entity);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting extended session information: {0}", e);
                errDetail = String.Format("dbErr={0}", e.Message);
            }

            // Only subscribed faciliites
            if (ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().OnlySubscribedFacilities)
            {
                var subFacl = ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>().SubscribeTo;
                var isInSubFacility = this.m_entity?.LoadCollection<EntityRelationship>("Relationships").Any(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation && subFacl.Contains(o.TargetEntityKey.ToString())) == true;
                if (!isInSubFacility && ApplicationContext.Current.PolicyDecisionService.GetPolicyOutcome(principal, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction) != PolicyGrantType.Grant)
                {
                    if (this.m_entity == null)
                    {
                        this.m_tracer.TraceError("User facility check could not be done : entity null");
                        errDetail += " entity_null";
                    }
                    else
                    {
                        this.m_tracer.TraceError("User is in facility {0} but tablet only allows login from {1}",
                            String.Join(",", this.m_entity?.LoadCollection<EntityRelationship>("Relationships").Where(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation).Select(o => o.TargetEntityKey).ToArray()),
                            String.Join(",", subFacl)
                            );
                        errDetail += String.Format(" entity={0}, facility={1}", String.Join(",", this.m_entity?.LoadCollection<EntityRelationship>("Relationships").Where(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation).Select(o => o.TargetEntityKey).ToArray()),
                            String.Join(",", subFacl));
                    }
                    throw new SecurityException(String.Format(Strings.locale_loginFromUnsubscribedFacility, errDetail));
                }
            }

        }

    }
}
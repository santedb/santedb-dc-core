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
using Newtonsoft.Json;
using SanteDB.Core;
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
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Xamarin.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Model
{

    /// <summary>
    /// Session information
    /// </summary>
    [JsonObject("SessionInfo"), XmlType("SessionInfo", Namespace = "http://santedb.org/model")]
    public class SessionInfo
    {

        // The principal
        private IPrincipal m_cachedPrincipal;

        // The entity 
        private UserEntity m_entity;

        // The user
        private SecurityUser m_user;

        // The roles of the user
        private string[] m_roles;

        // The general token
        private string m_idToken;

        // Lock
        private object m_syncLock = new object();

        // Security configuration section
        private SecurityConfigurationSection m_securityConfiguration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>();

        /// <summary>
        /// Get the underlying principal for this session information
        /// </summary>
        private IPrincipal GetPrincipal()
        {
            if (this.m_cachedPrincipal == null)
                this.m_cachedPrincipal = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>().Authenticate(this.Session);
            return this.m_cachedPrincipal;
        }

        /// <summary>
        /// Serialization ctor
        /// </summary>
        public SessionInfo() { }

        // The tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SessionInfo));

        /// <summary>
        /// Create the session object from the principal
        /// </summary>
        internal SessionInfo(ISession session)
        {
            this.Session = session;
            this.ProcessSession();
        }

        /// <summary>
        /// Gets the underlying session this wraps
        /// </summary>
        [JsonIgnore, DataIgnore]
        public ISession Session { get; private set; }

        /// <summary>
        /// Gets the user entity
        /// </summary>
        [JsonProperty("entity"), XmlElement("entity")]
        public UserEntity UserEntity
        {
            get
            {
                if (this.m_entity != null)
                    return this.m_entity;

                // HACK: Find a better way
                var userService = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();
                var sessionService = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>();

                if (ApplicationContext.Current.ConfigurationPersister.IsConfigured)
                {
                    UserEntity entity = null;
                    try
                    {
                        var identities = sessionService.GetIdentities(this.Session);
                        entity = userService.GetUserEntity(identities.First());

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
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets or sets the security user information
        /// </summary>
        [JsonProperty("user"), XmlElement("user")]
        public SecurityUser SecurityUser
        {
            get
            {
                if (this.m_user == null)
                {
                    var sessionService = ApplicationContext.Current.GetService<ISessionIdentityProviderService>();
                    var identities = sessionService.GetIdentities(this.Session);
                    var userService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
                    this.m_user = userService.GetUser(identities.First());
                }
                return this.m_user;
            }
            set { }
        }

        /// <summary>
        /// Gets the user name
        /// </summary>
        [JsonProperty("username"), XmlElement("username")]
        public string UserName
        {
            get => this.SecurityUser.UserName;
            set { }
        }

        /// <summary>
        /// Gets or sets the mechanism
        /// </summary>
        [JsonProperty("method"), XmlElement("method")]
        public String AuthenticationType
        {
            get => this.Session.Claims.FirstOrDefault(o => o.Type == SanteDBClaimTypes.AuthenticationMethod)?.Value;
            set { }
        }

        /// <summary>
        /// Expiry time
        /// </summary>
        [JsonProperty("exp"), XmlElement("exp")]
        public DateTime Expiry
        {
            get => this.Session.NotAfter.DateTime;
            set { }
        }

        /// <summary>
        /// Issued time
        /// </summary>
        [JsonProperty("nbf")]
        public DateTime Issued
        {
            get => this.Session.NotBefore.DateTime;
            set { }
        }

        /// <summary>
        /// Gets the display name
        /// </summary>
        [JsonProperty("displayName")]
        public String DisplayName => this.UserEntity?.Names.FirstOrDefault()?.ToString() ?? this.UserName;

        /// <summary>
        /// Gets the identity token
        /// </summary>
        [XmlIgnore, JsonIgnore]
        public string IdentityToken { get; private set; }

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public string RefreshToken { get; private set; }

        /// <summary>
        /// Process a principal
        /// </summary>
        private void ProcessSession()
        {

            // Grab the user entity
            String errDetail = String.Empty;
            var sessionService = ApplicationServiceContext.Current.GetService<ISessionIdentityProviderService>();

            // Try to get user entity
            try
            {
                var userService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

                var sid = Guid.Parse(this.Session.Claims.First(o => o.Type == SanteDBClaimTypes.NameIdentifier).Value);
                var securityUser = userService.GetUser(this.GetPrincipal().Identity);

                this.RefreshToken = BitConverter.ToString(this.Session.RefreshToken).Replace("-", "");
                this.IdentityToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    nbf = this.Issued,
                    exp = this.Expiry,
                    role = this.Session.Claims.Where(o => o.Type == SanteDBClaimTypes.DefaultRoleClaimType)?.Select(o => o.Value)?.ToList(),
                    authmethod = this.AuthenticationType,
                    unique_name = this.UserName,
                    scope = this.Session.Claims.Where(o => o.Type == SanteDBClaimTypes.SanteDBGrantedPolicyClaim).Select(o => o.Value).ToArray(),
                    sub = sid.ToString()
                })));

                if (securityUser == null) // Not yet persisted, get from server
                    this.SecurityUser = new SecurityUser()
                    {
                        Key = sid,
                        UserName = this.GetPrincipal().Identity.Name
                    };
                else
                    this.SecurityUser = securityUser;

                // User entity available?
                this.m_entity = userService.GetUserEntity(this.GetPrincipal().Identity);

                // Attempt to download if the user entity is null
                // Or if there are no relationships of type dedicated service dedicated service delivery location to force a download of the user entity 
                if (this.m_entity == null || this.m_entity?.Relationships.All(r => r.RelationshipTypeKey != EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation) == true)
                {
                    var amiService = ApplicationContext.Current.GetService<IClinicalIntegrationService>();
                    if (amiService != null && amiService.IsAvailable())
                    {
                        this.m_entity = amiService?.Find<UserEntity>(o => o.SecurityUser.Key == sid, 0, 1, null).Item?.OfType<UserEntity>().FirstOrDefault();
                        // Update the local user 
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
                    else
                        this.m_entity = ApplicationContext.Current.GetService<IRepositoryService<UserEntity>>().Find(o => o.SecurityUserKey == sid, 0, 1, out int t).FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting extended session information: {0}", e);
                errDetail = String.Format("dbErr={0}", e.Message);
            }

            // Only subscribed faciliites
            if (ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().RestrictLoginToFacilityUsers)
            {
                var subFacl = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Facilities;
                var isInSubFacility = this.m_entity?.LoadCollection<EntityRelationship>("Relationships").Any(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation && subFacl.Contains(o.TargetEntityKey.Value)) == true;
                if (!isInSubFacility && ApplicationContext.Current.PolicyDecisionService.GetPolicyOutcome(this.GetPrincipal(), PermissionPolicyIdentifiers.AccessClientAdministrativeFunction) != PolicyGrantType.Grant)
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
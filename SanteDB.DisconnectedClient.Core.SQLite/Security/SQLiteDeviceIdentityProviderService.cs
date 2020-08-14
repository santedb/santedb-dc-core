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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Security
{

    /// <summary>
    /// SQLite identity
    /// </summary>
    public class SQLiteDeviceIdentity : SanteDBClaimsIdentity, IDeviceIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Security.SQLiteIdentity"/> class.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="authenticated">If set to <c>true</c> authenticated.</param>
        public SQLiteDeviceIdentity(String deviceName, bool authenticated, DateTime? issueTime = null, DateTime? expiry = null, IEnumerable<IClaim> additionalClaims = null) : base(deviceName, authenticated, "LOCAL_AUTHORITY",
            new IClaim[] {
                new SanteDBClaim(SanteDBClaimTypes.AuthenticationInstant, issueTime?.ToString("o")),
                new SanteDBClaim(SanteDBClaimTypes.Expiration, expiry?.ToString("o"))
            }.Union(additionalClaims ?? new List<IClaim>()))
        {
        }

    }

    /// <summary>
    /// Represents a SQL.NET PCL Device identity provider
    /// </summary>
    [ServiceProvider("SQLite-NET PCL Device Identity Provider")]
    public class SQLiteDeviceIdentityProviderService : IOfflineDeviceIdentityProviderService
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteDeviceIdentityProviderService));

        // Configuration
        private DcDataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>();

        /// <summary>
        /// Gets the name of the service
        /// </summary>
        public string ServiceName => "SQLite-NET PCL Device Identity Provider";

        /// <summary>
        /// Authenticated
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        /// <summary>
        /// Authenticating
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName));
        }

        /// <summary>
        /// Authenticate the device
        /// </summary>
        public IPrincipal Authenticate(string deviceId, string deviceSecret, AuthenticationMethod authMethod = AuthenticationMethod.Any)
        {
            var config = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            if (!authMethod.HasFlag(AuthenticationMethod.Local))
                throw new InvalidOperationException("Identity provider only supports local auth");

            // Pre-event
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(deviceId) { };
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event hook indicates cancel {0}", deviceId);
                return e.Principal;
            }

            IPrincipal retVal = null;
            try
            {
                // Connect to the db
                var connection = this.CreateConnection();
                using (connection.Lock())
                {
                    // Password service
                    IPasswordHashingService passwordHash = ApplicationContext.Current.GetService(typeof(IPasswordHashingService)) as IPasswordHashingService;

                    DbSecurityDevice dbd = connection.Table<DbSecurityDevice>().FirstOrDefault(o => o.PublicId.ToLower() == deviceId.ToLower());
                    if (dbd == null)
                        throw new SecurityException(Strings.locale_authenticationFailure);
                    else if (config?.MaxInvalidLogins.HasValue == true && dbd.Lockout.HasValue && dbd.Lockout > DateTime.Now)
                        throw new SecurityException(Strings.locale_accountLocked);
                    else if (dbd.ObsoletionTime != null)
                        throw new SecurityException(Strings.locale_accountObsolete);
                    else if (!String.IsNullOrEmpty(deviceSecret) && passwordHash.ComputeHash(deviceSecret) != dbd.DeviceSecret)
                    {
                        dbd.InvalidAuthAttempts++;
                        connection.Update(dbd);
                        throw new SecurityException(Strings.locale_authenticationFailure);
                    }
                    else if (config?.MaxInvalidLogins.HasValue == true && dbd.InvalidAuthAttempts > config?.MaxInvalidLogins)
                    { //s TODO: Make this configurable
                        dbd.Lockout = DateTime.Now.AddSeconds(30 * (dbd.InvalidAuthAttempts - config.MaxInvalidLogins.Value));
                        connection.Update(dbd);
                        throw new SecurityException(Strings.locale_accountLocked);
                    } // TODO: Lacks login permission
                    else
                    {
                        dbd.LastAuthTime = DateTime.Now;
                        dbd.InvalidAuthAttempts = 0;
                        connection.Update(dbd);

                        IPolicyDecisionService pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();
                        IPolicyInformationService pip = ApplicationContext.Current.GetService<IPolicyInformationService>();
                        List<IClaim> additionalClaims = new List<IClaim>();
                        additionalClaims.AddRange(pip.GetActivePolicies(dbd).Where(o => o.Rule == PolicyGrantType.Grant).Select(o => new SanteDBClaim(SanteDBClaimTypes.SanteDBGrantedPolicyClaim, o.Policy.Oid)));
                        additionalClaims.Add(new SanteDBClaim(SanteDBClaimTypes.SanteDBDeviceIdentifierClaim, dbd.Key.ToString()));
                        // Create the principal
                        retVal = new SQLitePrincipal(new SQLiteDeviceIdentity(dbd.PublicId, true, DateTime.Now, DateTime.Now.Add(config?.MaxLocalSession ?? new TimeSpan(0, 15, 0)), additionalClaims), new string[] { });

                        ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.LoginAsService, retVal);

                    }
                }

                // Post-event
                this.Authenticated?.Invoke(e, new AuthenticatedEventArgs(deviceId, retVal, true));

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error establishing device session ({1}): {0}", ex, deviceSecret);
                this.Authenticated?.Invoke(e, new AuthenticatedEventArgs(deviceId, retVal, false));

                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Change the device secret
        /// </summary>
        public void ChangeSecret(string deviceName, string deviceSecret, IPrincipal principal)
        {
            // We must demand the change password permission
            try
            {
                var pep = ApplicationContext.Current.GetService<IPolicyEnforcementService>();
                if (pep == null)
                    throw new InvalidOperationException("Cannot find the PolicyEnforcementService");
                if (deviceName != principal.Identity.Name) 
                    pep.Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var dbu = conn.Table<DbSecurityDevice>().Where(o => o.PublicId == deviceName).FirstOrDefault();
                    if (dbu == null)
                        throw new KeyNotFoundException();
                    else
                    {
                        IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();

                        if (hash == null)
                            throw new InvalidOperationException("Cannot find Password Hashing Service");

                        dbu.DeviceSecret = hash.ComputeHash(deviceSecret);
                        dbu.UpdatedByUuid = conn.Table<DbSecurityUser>().First(u => u.UserName == principal.Identity.Name).Uuid;
                        dbu.UpdatedTime = DateTime.Now;
                        conn.Update(dbu);
                    }
                }
            }
            catch (Exception e)
            {

                this.m_tracer.TraceError("Error changing secret for device {0} : {1}", deviceName, e);
                throw;
            }
        }
        
        /// <summary>
        /// Create the identity
        /// </summary>
        public IIdentity CreateIdentity(Guid sid, string name, string deviceSecret, IPrincipal principal)
        {
            try
            {
                ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, principal);

                var conn = this.CreateConnection();
                IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();

                using (conn.Lock())
                {
                    DbSecurityDevice dbu = new DbSecurityDevice()
                    {
                        DeviceSecret = hash.ComputeHash(deviceSecret),
                        PublicId = name,
                        Key = sid,
                        CreationTime = DateTime.Now,
                        CreatedByUuid = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName == AuthenticationContext.Current?.Principal?.Identity?.Name)?.Uuid ?? Guid.Parse("fadca076-3690-4a6e-af9e-f1cd68e8c7e8").ToByteArray()
                    };
                    conn.Insert(dbu);
                }
                return new SQLiteDeviceIdentity(name, false);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get the specified identity
        /// </summary>
        public IIdentity GetIdentity(string name)
        {
            try
            {
                var conn = this.CreateConnection();
                using(conn.Lock())
                {
                    var dbd = conn.Table<DbSecurityDevice>().Where(o => o.PublicId == name).FirstOrDefault();
                    if(dbd != null)
                        return new SQLiteDeviceIdentity(name, false);
                    return null;
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error retrieving identity {0} : {1}", name, e);
                throw;
            }
        }

        /// <summary>
        /// Set the lockout of the specified object
        /// </summary>
        public void SetLockout(string name, bool lockoutState, IPrincipal principal)
        {
            try
            {
                var conn = this.CreateConnection();
                using(conn.Lock())
                {
                    var dbi = conn.Table<DbSecurityDevice>().Where(o => o.PublicId == name).FirstOrDefault();
                    if (dbi == null)
                        throw new KeyNotFoundException($"Device {name} not found");
                    dbi.Lockout = lockoutState ? (DateTime?)DateTime.MaxValue.AddDays(-10) : null;
                    conn.Update(dbi);
                    conn.Commit();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error setting lockout on device {name}", e);
                throw;
            }
        }
    }
}

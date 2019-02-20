﻿using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.Core.Security;
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
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.Security.SQLiteIdentity"/> class.
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
    public class SQLiteDeviceIdentityProviderService : IOfflineDeviceIdentityProviderService, ISecurityAuditEventSource
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteDeviceIdentityProviderService));

        // Configuration
        private DataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DataConfigurationSection>();

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
        public event EventHandler<SecurityAuditDataEventArgs> SecurityAttributesChanged;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceCreated;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceDeleted;

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName).ConnectionString);
        }

        /// <summary>
        /// Authenticate the device
        /// </summary>
        public IPrincipal Authenticate(string deviceId, string deviceSecret, AuthenticationMethod authMethod = AuthenticationMethod.Any)
        {
            var config = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            if (authMethod.HasFlag(AuthenticationMethod.Local))
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
                        throw new SecurityException(Strings.locale_invalidUserNamePassword);
                    else if (config?.MaxInvalidLogins.HasValue == true && dbd.Lockout.HasValue && dbd.Lockout > DateTime.Now)
                        throw new SecurityException(Strings.locale_accountLocked);
                    else if (dbd.ObsoletionTime != null)
                        throw new SecurityException(Strings.locale_accountObsolete);
                    else if (!String.IsNullOrEmpty(deviceSecret) && passwordHash.ComputeHash(deviceSecret) != dbd.Secret)
                    {
                        dbd.InvalidAuthAttempts++;
                        connection.Update(dbd);
                        throw new SecurityException(Strings.locale_invalidUserNamePassword);
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
                        
                        // Create the principal
                        retVal = new SQLitePrincipal(new SQLiteDeviceIdentity(dbd.PublicId, true, DateTime.Now, DateTime.Now.Add(config?.MaxLocalSession ?? new TimeSpan(0, 15, 0)), additionalClaims), new string[] { });
                        if (pdp.GetPolicyOutcome(retVal, PermissionPolicyIdentifiers.LoginAsService) != PolicyGrantType.Grant)
                            throw new PolicyViolationException(retVal, PermissionPolicyIdentifiers.LoginAsService, PolicyGrantType.Deny);

                    }
                }

                // Post-event
                this.Authenticated?.Invoke(e, new AuthenticatedEventArgs(deviceId, retVal, true));

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error establishing device session: {0}", ex);
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
                IPolicyDecisionService pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();

                if (deviceName != principal.Identity.Name &&
                    pdp.GetPolicyOutcome(principal, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction) == SanteDB.Core.Model.Security.PolicyGrantType.Deny)
                    throw new SecurityException("User cannot change device secrets");
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var dbu = conn.Table<DbSecurityDevice>().Where(o => o.PublicId == deviceName).FirstOrDefault();
                    if (dbu == null)
                        throw new KeyNotFoundException();
                    else
                    {
                        IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();
                        dbu.Secret = hash.ComputeHash(deviceSecret);
                        dbu.UpdatedByUuid = conn.Table<DbSecurityUser>().First(u => u.UserName == principal.Identity.Name).Uuid;
                        dbu.UpdatedTime = DateTime.Now;
                        conn.Update(dbu);
                        this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(dbu, "password"));
                    }
                }
            }
            catch (Exception e)
            {

                this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(new SecurityDevice() { Key = Guid.Empty, Name = deviceName }, "password") { Success = false });
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
                var pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();
                if (pdp.GetPolicyOutcome(principal ?? AuthenticationContext.Current.Principal, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction) != PolicyGrantType.Grant)
                    throw new PolicyViolationException(principal, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, PolicyGrantType.Deny);

                var conn = this.CreateConnection();
                IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();

                using (conn.Lock())
                {
                    DbSecurityDevice dbu = new DbSecurityDevice()
                    {
                        Secret = hash.ComputeHash(deviceSecret),
                        PublicId = name,
                        Key = sid,
                        CreationTime = DateTime.Now,
                        CreatedByUuid = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName == AuthenticationContext.Current?.Principal?.Identity?.Name)?.Uuid ?? Guid.Parse("fadca076-3690-4a6e-af9e-f1cd68e8c7e8").ToByteArray()
                    };
                    conn.Insert(dbu);
                    this.SecurityResourceCreated?.Invoke(this, new SecurityAuditDataEventArgs(dbu, "created"));
                }
                return new SQLiteDeviceIdentity(name, false);
            }
            catch
            {
                this.SecurityResourceCreated?.Invoke(this, new SecurityAuditDataEventArgs(new SecurityDevice() { Name = name }) { Success = false });
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
    }
}
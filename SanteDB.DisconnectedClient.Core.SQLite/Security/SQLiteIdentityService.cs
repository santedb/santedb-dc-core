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
 * User: fyfej
 * Date: 2017-9-1
 */
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Core.Serices;
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Core.Tickler;
using System.Text;
using SanteDB.Core.Security;

namespace SanteDB.DisconnectedClient.SQLite.Security
{
    /// <summary>
    /// Local identity service.
    /// </summary>
    public class SQLiteIdentityService : IOfflineIdentityProviderService, ISecurityAuditEventSource, IPinAuthenticationService
    {
        // Configuration
        private DataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DataConfigurationSection>();

        // Local tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteIdentityService));

        #region IIdentityProviderService implementation

        /// <summary>
        /// Occurs when authenticated.
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Fired on authenticating
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityAttributesChanged;
        public event EventHandler<AuditDataEventArgs> DataCreated;
        public event EventHandler<AuditDataEventArgs> DataUpdated;
        public event EventHandler<AuditDataEventArgs> DataObsoleted;
        public event EventHandler<AuditDataDisclosureEventArgs> DataDisclosed;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceCreated;
        public event EventHandler<SecurityAuditDataEventArgs> SecurityResourceDeleted;

        /// <summary>
        /// Authenticate the user
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        public System.Security.Principal.IPrincipal Authenticate(string userName, string password)
        {
            if (String.IsNullOrEmpty(userName))
                throw new ArgumentNullException(nameof(userName));
            if (String.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            return this.AuthenticateInternal(userName, password, null);
        }

        /// <summary>
        /// Authenticate the user
        /// </summary>
        /// <param name="principal">Principal.</param>
        /// <param name="password">Password.</param>
        public IPrincipal Authenticate(IPrincipal principal, String password)
        {
            var config = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            else if (String.IsNullOrEmpty(password))
            {
                if (principal.Identity.IsAuthenticated)
                {
                    // Refresh
                    if (principal is SQLitePrincipal) /// extend the existing session 
                        (principal as SQLitePrincipal).Expires = DateTime.Now.Add(config?.MaxLocalSession ?? new TimeSpan(0, 15, 0));
                    else if (principal is ClaimsPrincipal) // switch them to a SQLitePrincipal
                    {
                        var sid = (principal as ClaimsPrincipal).FindClaim(ClaimTypes.Sid)?.Value;
                        var uname = (principal as ClaimsPrincipal).FindClaim(ClaimsIdentity.DefaultNameClaimType)?.Value;
                        if (!String.IsNullOrEmpty(uname))
                        {
                            ApplicationContext.Current.GetService<ITickleService>()?.SendTickle(new Tickle(Guid.Parse(sid), TickleType.SecurityInformation | TickleType.Toast, Strings.locale_securitySwitchedMode, DateTime.Now.AddSeconds(10)));
                            return new SQLitePrincipal(new SQLiteIdentity(uname, true, DateTime.Now, DateTime.Now.Add(config?.MaxLocalSession ?? new TimeSpan(0, 15, 0))), (principal as ClaimsPrincipal).Claims.Where(o => o.Type == ClaimsIdentity.DefaultRoleClaimType).Select(o => o.Value).ToArray());
                        }
                        else
                            throw new SecurityException(Strings.locale_sessionError);
                    }
                    return principal;
                }
                else
                    throw new ArgumentNullException(nameof(password));
            }

            return this.AuthenticateInternal(principal.Identity.Name, password, null);
        }

        /// <summary>
        /// Authenticate this user with a local PIN number
        /// </summary>
        /// <param name="userName">The name of the user</param>
        /// <param name="password">The password of the user</param>
        /// <param name="pin">The PIN number for PIN based authentication</param>
        /// <returns>The authenticated principal</returns>
        private IPrincipal AuthenticateInternal(String userName, string password, byte[] pin) {
            var config = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

            // Pre-event
            AuthenticatingEventArgs e = new AuthenticatingEventArgs(userName, password) { };
            this.Authenticating?.Invoke(this, e);
            if (e.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Event hook indicates cancel {0}", userName);
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

                    DbSecurityUser dbs = connection.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName.ToLower() == userName.ToLower());
                    if (dbs == null)
                        throw new SecurityException(Strings.locale_invalidUserNamePassword);
                    else if (config?.MaxInvalidLogins.HasValue == true && dbs.Lockout.HasValue && dbs.Lockout > DateTime.Now)
                        throw new SecurityException(Strings.locale_accountLocked);
                    else if (dbs.ObsoletionTime != null)
                        throw new SecurityException(Strings.locale_accountObsolete);
                    else if (!String.IsNullOrEmpty(password) && passwordHash.ComputeHash(password) != dbs.Password ||
                        pin != null && passwordHash.ComputeHash(Encoding.UTF8.GetString(pin.Select(o => (byte)(o + 48)).ToArray(), 0, pin.Length)) != dbs.PinHash)
                    {
                        dbs.InvalidLoginAttempts++;
                        connection.Update(dbs);
                        throw new SecurityException(Strings.locale_invalidUserNamePassword);
                    }
                    else if (config?.MaxInvalidLogins.HasValue == true && dbs.InvalidLoginAttempts > config?.MaxInvalidLogins)
                    { //s TODO: Make this configurable
                        dbs.Lockout = DateTime.Now.AddSeconds(30 * (dbs.InvalidLoginAttempts - config.MaxInvalidLogins.Value));
                        connection.Update(dbs);
                        throw new SecurityException(Strings.locale_accountLocked);
                    } // TODO: Lacks login permission
                    else
                    {
                        dbs.LastLoginTime = DateTime.Now;
                        dbs.InvalidLoginAttempts = 0;
                        connection.Update(dbs);

                        // Create the principal
                        retVal = new SQLitePrincipal(new SQLiteIdentity(dbs.UserName, true, DateTime.Now, DateTime.Now.Add(config?.MaxLocalSession ?? new TimeSpan(0, 15, 0))),
                            connection.Query<DbSecurityRole>("SELECT security_role.* FROM security_user_role INNER JOIN security_role ON (security_role.uuid = security_user_role.role_id) WHERE security_user_role.user_id = ?",
                            dbs.Uuid).Select(o => o.Name).ToArray());

                    }
                }

                // Post-event
                this.Authenticated?.Invoke(e, new AuthenticatedEventArgs(userName, password, true) { Principal = retVal });

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error establishing session: {0}", ex);
                this.Authenticated?.Invoke(e, new AuthenticatedEventArgs(userName, password, false) { Principal = retVal });

                throw;
            }

            return retVal;
        }

        /// <summary>
        /// Authenticate the user using a TwoFactorAuthentication secret
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="tfaSecret">Tfa secret.</param>
        public System.Security.Principal.IPrincipal Authenticate(string userName, string password, string tfaSecret)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Authenticates a user with a local device PIN
        /// </summary>
        /// <param name="userName">The user to authenticate</param>
        /// <param name="pin">The PIN number for that user</param>
        /// <returns>The authenticated user</returns>
        public IPrincipal Authenticate(string userName, byte[] pin)
        {
            if (String.IsNullOrEmpty(userName))
                throw new ArgumentNullException(nameof(userName));
            if (pin.Length < 4 || pin.Length > 8 || pin.Any(o=>o > 9 || o < 0))
                throw new ArgumentOutOfRangeException(nameof(pin));

            return this.AuthenticateInternal(userName, null, pin);
        }

        /// <summary>
        /// Change the user's password
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="newPassword">New password.</param>
        /// <param name="principal">Principal.</param>
        public void ChangePassword(string userName, string password, System.Security.Principal.IPrincipal principal)
        {
            // We must demand the change password permission
            try
            {
                IPolicyDecisionService pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();

                if (userName != principal.Identity.Name &&
                    pdp.GetPolicyOutcome(principal, PermissionPolicyIdentifiers.ChangePassword) == SanteDB.Core.Model.Security.PolicyGrantType.Deny)
                    throw new SecurityException("User cannot change specified users password");
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var dbu = conn.Table<DbSecurityUser>().Where(o => o.UserName == userName).FirstOrDefault();
                    if (dbu == null)
                        throw new KeyNotFoundException();
                    else
                    {
                        IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();
                        dbu.Password = hash.ComputeHash(password);
                        dbu.SecurityHash = Guid.NewGuid().ToString();
                        dbu.UpdatedByUuid = conn.Table<DbSecurityUser>().First(u => u.UserName == principal.Identity.Name).Uuid;
                        dbu.UpdatedTime = DateTime.Now;
                        conn.Update(dbu);
                        this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(dbu, "password"));
                    }
                }
            }
            catch (Exception e)
            {

                this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(new SecurityUser() { Key = Guid.Empty, UserName = userName }, "password") { Success = false });

                this.m_tracer.TraceError("Error changing password for user {0} : {1}", userName, e);
                throw;
            }
        }

        /// <summary>
        /// Change the user's password
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        public void ChangePassword(string userName, string password)
        {
            this.ChangePassword(userName, password, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Create specified identity
        /// </summary>
        public IIdentity CreateIdentity(String userName, String password)
        {
            return this.CreateIdentity(Guid.NewGuid(), userName, password, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Create specified identity
        /// </summary>
        public IIdentity CreateIdentity(Guid sid, String userName, String password)
        {
            return this.CreateIdentity(sid, userName, password, AuthenticationContext.Current.Principal);
        }

        /// <summary>
        /// Creates an identity for the user
        /// </summary>
        public IIdentity CreateIdentity(Guid sid, string userName, string password, IPrincipal principal)
        {
            return this.CreateIdentity(new SecurityUser
            {
                Key = sid,
                UserName = userName
            }, password, principal);
        }

        /// <summary>
        /// Creates the identity.
        /// </summary>
        /// <param name="securityUser">The security user.</param>
        /// <param name="password">The password.</param>
        /// <param name="principal">The principal.</param>
        /// <returns>Returns the created user identity.</returns>
        /// <exception cref="PolicyViolationException"></exception>
        public IIdentity CreateIdentity(SecurityUser securityUser, string password, IPrincipal principal)
        {
            try
            {
                var pdp = ApplicationContext.Current.GetService<IPolicyDecisionService>();
                if (pdp.GetPolicyOutcome(principal ?? AuthenticationContext.Current.Principal, PermissionPolicyIdentifiers.AccessClientAdministrativeFunction) != PolicyGrantType.Grant)
                    throw new PolicyViolationException(PermissionPolicyIdentifiers.AccessClientAdministrativeFunction, PolicyGrantType.Deny);

                var conn = this.CreateConnection();
                IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();

                using (conn.Lock())
                {
                    DbSecurityUser dbu = new DbSecurityUser()
                    {
                        Password = hash.ComputeHash(password),
                        SecurityHash = Guid.NewGuid().ToString(),
                        PhoneNumber = securityUser.PhoneNumber,
                        Email = securityUser.Email,
                        CreationTime = DateTime.Now,
                        CreatedByUuid = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName == AuthenticationContext.Current?.Principal?.Identity?.Name)?.Uuid ?? Guid.Parse("fadca076-3690-4a6e-af9e-f1cd68e8c7e8").ToByteArray(),
                        UserName = securityUser.UserName,
                        Key = securityUser.Key.Value
                    };
                    conn.Insert(dbu);
                    this.DataCreated?.Invoke(this, new AuditDataEventArgs(dbu));
                }
                return new SQLiteIdentity(securityUser.UserName, false);
            }
            catch
            {
                this.DataCreated?.Invoke(this, new AuditDataEventArgs(new SecurityUser()) { Success = false });
                throw;
            }
        }


        public void DeleteIdentity(string userName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an un-authenticated identity
        /// </summary>
        /// <returns>The identity.</returns>
        /// <param name="userName">User name.</param>
        public System.Security.Principal.IIdentity GetIdentity(string userName)
        {
            try
            {
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var userData = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName.ToLower() == userName.ToLower());
                    if (userData == null)
                        return null;
                    else
                        return new SQLiteIdentity(userName, false, DateTime.MinValue, DateTime.MinValue);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting identity {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Set the lockout 
        /// </summary>
        public void SetLockout(string userName, bool v)
        {
            try
            {
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var userData = conn.Table<DbSecurityUser>().FirstOrDefault(o => o.UserName == userName);
                    if (userData == null)
                        throw new KeyNotFoundException(userName);
                    else
                    {
                        if (v)
                            userData.Lockout = DateTime.MaxValue;
                        else
                        {
                            userData.Lockout = null;
                            userData.InvalidLoginAttempts = 0;
                        }
                        conn.Update(userData);
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error getting identity {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.Configuration.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName).Value);
        }

        /// <summary>
        /// Change the PIN for the user
        /// </summary>
        /// <param name="userName">The name of the user to change the PIN for</param>
        /// <param name="pin">The PIN to change to</param>
        /// <remarks>Only the currently logged in credential can change a PIN number for themselves</remarks>
        public void ChangePin(string userName, byte[] pin)
        {
            // We must demand the change password permission
            try
            {
                if (userName != AuthenticationContext.Current.Principal.Identity.Name)
                    throw new SecurityException("Can only change PIN number of your own account");
                else if (pin.Length < 4 || pin.Length > 8 || pin.Any(o=>o < 0 || o > 9))
                    throw new ArgumentOutOfRangeException("PIN numbers must be between 4 and 8 digits");
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var dbu = conn.Table<DbSecurityUser>().Where(o => o.UserName == userName).FirstOrDefault();
                    if (dbu == null)
                        throw new KeyNotFoundException();
                    else
                    {
                        IPasswordHashingService hash = ApplicationContext.Current.GetService<IPasswordHashingService>();
                        dbu.PinHash = hash.ComputeHash(Encoding.UTF8.GetString(pin.Select(o=>(byte)(o + 48)).ToArray(), 0, pin.Length));
                        dbu.SecurityHash = Guid.NewGuid().ToString();
                        dbu.UpdatedByUuid = conn.Table<DbSecurityUser>().First(u => u.UserName == AuthenticationContext.Current.Principal.Identity.Name).Uuid;
                        dbu.UpdatedTime = DateTime.Now;
                        conn.Update(dbu);
                        this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(dbu, "pin"));
                    }
                }
            }
            catch (Exception e)
            {

                this.SecurityAttributesChanged?.Invoke(this, new SecurityAuditDataEventArgs(new SecurityUser() { Key = Guid.Empty, UserName = userName }, "pin") { Success = false });

                this.m_tracer.TraceError("Error changing password for user {0} : {1}", userName, e);
                throw;
            }
        }

        public IPrincipal Authenticate(IPrincipal principal, string password, string tfaSecret)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Authenticate the user with pin
        /// </summary>
        public IPrincipal Authenticate(IPrincipal principal, byte[] pin)
        {
            return this.AuthenticateInternal(principal.Identity.Name, null, pin);

        }

 
        #endregion IIdentityProviderService implementation
    }

    /// <summary>
    /// SQLite identity
    /// </summary>
    public class SQLiteIdentity : ClaimsIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.Security.SQLiteIdentity"/> class.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="authenticated">If set to <c>true</c> authenticated.</param>
        public SQLiteIdentity(String userName, bool authenticated, DateTime? issueTime = null, DateTime? expiry = null) : base(userName, authenticated, new Claim[] {
            new Claim(ClaimTypes.AuthenticationInstant, issueTime?.ToString("o")),
            new Claim(ClaimTypes.Expiration, expiry?.ToString("o")),
            new Claim(ClaimTypes.AuthenticationMethod, "LOCAL")
        })
        {
            
        }

    }

    /// <summary>
    /// SQLite principal.
    /// </summary>
    public class SQLitePrincipal : ClaimsPrincipal, IPrincipal, IOfflinePrincipal
    {
        private String[] m_roles;

        /// <summary>
        /// The time that the principal was issued
        /// </summary>
        public DateTime IssueTime {
            get
            {
                return this.FindClaim(ClaimTypes.AuthenticationInstant).AsDateTime();
            }
            set
            {
                this.FindClaim(ClaimTypes.AuthenticationInstant).Value = value.ToString("o");
            }
        }

        /// <summary>
        /// Expiration time
        /// </summary>
        public DateTime Expires {
            get
            {
                return this.FindClaim(ClaimTypes.Expiration).AsDateTime();
            }
            set
            {
                this.FindClaim(ClaimTypes.Expiration).Value = value.ToString("o");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Core.Security.SQLitePrincipal"/> class.
        /// </summary>
        public SQLitePrincipal(SQLiteIdentity identity, String[] roles) : base(identity)
        {
            this.m_roles = roles;
            this.Identity = identity;
        }

        #region IPrincipal implementation

        /// <summary>
        /// Gets the identity of the current principal.
        /// </summary>
        /// <value>The identity.</value>
        public IIdentity Identity
        {
            get;
            private set;
        }

        /// <summary>
        /// Determines whether the current principal belongs to the specified role.
        /// </summary>
        /// <returns>true if the current principal is a member of the specified role; otherwise, false.</returns>
        /// <param name="role">The name of the role for which to check membership.</param>
        public bool IsInRole(string role)
        {
            return this.m_roles.Contains(role);
        }

        #endregion IPrincipal implementation
    }
}
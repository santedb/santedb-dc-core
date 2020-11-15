using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Security
{
    /// <summary>
    /// Security challenge service which authenticates and stores security challenges against the primary database
    /// </summary>
    public class SQLiteSecurityChallengeService : IOfflineSecurityChallengeService, IOfflineSecurityChallengeIdentityService
    {
        /// <summary>
        /// Gets the name of this service
        /// </summary>
        public string ServiceName => throw new NotImplementedException();

        // Configuration
        private DcDataConfigurationSection m_configuration = ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>();

        // Local tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteSecurityChallengeService));

        // Security configuration
        private SecurityConfigurationSection m_securityConfiguration = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

        // The randomizer
        private Random m_random = new Random();

        /// <summary>
        /// Fired before authentication occurs
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;
        /// <summary>
        /// Fired after authentication is successful
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateConnection()
        {
            return SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName));
        }

        /// <summary>
        /// Creates a connection to the local database
        /// </summary>
        /// <returns>The connection.</returns>
        private LockableSQLiteConnection CreateReadonlyConnection()
        {
            return SQLiteConnectionManager.Current.GetReadonlyConnection(ApplicationContext.Current.ConfigurationManager.GetConnectionString(this.m_configuration.MainDataSourceConnectionStringName));
        }

        /// <summary>
        /// Authenticates the specified user in the database against the selected challenge and selected response
        /// </summary>
        public IPrincipal Authenticate(string userName, Guid challengeKey, string response, string tfaSecret)
        {
            try
            {
                var authArgs = new AuthenticatingEventArgs(userName);
                this.Authenticating?.Invoke(this, authArgs);
                if (authArgs.Cancel)
                    throw new SecurityException("Authentication cancelled");

                var hashService = ApplicationServiceContext.Current.GetService<IPasswordHashingService>();
                var responseHash = hashService.ComputeHash(response);

                // Connection to perform auth
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var query = "select * from security_user inner join security_user_challenge on (security_user.uuid = security_user_challenge.user_uuid) where lower(security_user.username) = lower(?) and challenge_uuid = ? and security_user.obsoletionTime is null and (security_user_challenge.expiry is null or security_user_challenge.expiry > ?)";
                    var dbUser = conn.Query<DbSecurityUserChallengeAssoc.QueryResult>(query, userName, challengeKey.ToByteArray(), DateTime.Now).FirstOrDefault();

                    // User found?
                    if (dbUser == null)
                        throw new SecurityException("AUTH_INV");

                    // TFA? 
                    if (dbUser.Lockout > DateTime.Now)
                        throw new SecurityException("AUTH_LCK");
                    else if (dbUser.ChallengeResponse != responseHash || dbUser.Lockout.GetValueOrDefault() > DateTime.Now) // Increment invalid
                    {
                        dbUser.InvalidLoginAttempts++;
                        if (dbUser.InvalidLoginAttempts > this.m_securityConfiguration.MaxInvalidLogins)
                            dbUser.Lockout = DateTime.Now.Add(new TimeSpan(0, 0, dbUser.InvalidLoginAttempts * 30));
                        dbUser.UpdatedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                        dbUser.UpdatedTime = DateTimeOffset.Now;

                        conn.Update(dbUser, typeof(DbSecurityUser));
                        if (dbUser.Lockout > DateTime.Now)
                            throw new AuthenticationException("AUTH_LCK");
                        else
                            throw new AuthenticationException("AUTH_INV");
                    }
                    else
                    {
                        dbUser.LastLoginTime = DateTime.Now;
                        dbUser.InvalidLoginAttempts = 0;
                        conn.Update(dbUser, typeof(DbSecurityUser));

                        var roles = conn.Query<DbSecurityRole>("SELECT security_role.* FROM security_user_role INNER JOIN security_role ON (security_role.uuid = security_user_role.role_id) WHERE lower(security_user_role.user_id) = lower(?)",
                            dbUser.Uuid).Select(o => o.Name).ToArray();

                        var additionalClaims = new List<IClaim>()
                        {
                            new SanteDBClaim(SanteDBClaimTypes.NameIdentifier, dbUser.Key.ToString()),
                            new SanteDBClaim(SanteDBClaimTypes.DefaultNameClaimType, dbUser.UserName),
                            new SanteDBClaim(SanteDBClaimTypes.SanteDBApplicationIdentifierClaim, ApplicationContext.Current.Application.Key.ToString()), // Local application only allows
                            new SanteDBClaim(SanteDBClaimTypes.SanteDBDeviceIdentifierClaim, ApplicationContext.Current.Device.Key.ToString())
                        };

                        // Create the principal
                        var principal = new SQLitePrincipal(new SQLiteIdentity(dbUser.UserName, true, DateTime.Now, DateTime.Now.Add(this.m_securityConfiguration.MaxLocalSession), additionalClaims), roles);
                        ApplicationServiceContext.Current.GetService<IPolicyEnforcementService>().Demand(PermissionPolicyIdentifiers.Login, principal);

                        new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.Login, principal).Demand(); // must still be allowed to login

                        (principal.Identity as IClaimsIdentity).AddClaim(new SanteDBClaim(SanteDBClaimTypes.PurposeOfUse, PurposeOfUseKeys.SecurityAdmin.ToString()));
                        (principal.Identity as IClaimsIdentity).AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.ReadMetadata));
                        (principal.Identity as IClaimsIdentity).AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.LoginPasswordOnly));
                        this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, principal, true));
                        return principal;
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Challenge authentication failed: {0}", e);
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false));
                throw new AuthenticationException($"Challenge authentication failed");
            }
        }

        /// <summary>
        /// Gets the security challenges for the specified user
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(string userName, IPrincipal principal)
        {
            try
            {
                // Connection to perform auth
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var sql = "select security_challenge.* from security_user inner join security_user_challenge on (security_user.uuid = security_user_challenge.user_uuid) inner join security_challenge on (security_challenge.uuid = security_user_challenge.challenge_uuid) WHERE lower(username) = lower(?) and security_user.obsoletionTime is null";
                    var retVal = conn.Query<DbSecurityChallenge>(sql, userName, DateTime.Now)
                        .ToList()
                        .Select(this.MapDbSecurityChallenge)
                        .ToList();

                    // Only the current user can fetch their own security challenge questions 
                    if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                        || !principal.Identity.IsAuthenticated)
                        return retVal.Skip(this.m_random.Next(0, retVal.Count)).Take(1);
                    else // Only a random option can be returned  
                        return retVal;

                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Getting challenge for user {0} failed: {1}", userName, e);
                throw new Exception($"Getting challenge for user {userName} failed", e);
            }
        }

        /// <summary>
        /// Gets the security challenges for the specified user
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(Guid key, IPrincipal principal)
        {
            try
            {
                // Connection to perform auth
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    var userUuid = key.ToByteArray();

                    var sql = "select security_challenge.* from security_challenge inner join security_user_challenge on (security_challenge.uuid = security_user_challenge.challenge_uuid) WHERE user_uuid = ?";
                    var retVal = conn.Query<DbSecurityChallenge>(sql, userUuid, DateTime.Now)
                        .ToList()
                        .Select(this.MapDbSecurityChallenge)
                        .ToList();

                    var userProfile = conn.Table<DbSecurityUser>().Where(o => o.Uuid == userUuid).FirstOrDefault();

                    // Only the current user can fetch their own security challenge questions 
                    if (!userProfile.UserName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                        || !principal.Identity.IsAuthenticated)
                        return retVal.Skip(this.m_random.Next(0, retVal.Count)).Take(1);
                    else // Only a random option can be returned  
                        return retVal;

                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Getting challenge for user {0} failed: {1}", key, e);
                throw new Exception($"Getting challenge for user {key} failed", e);
            }
        }

        /// <summary>
        /// Map database security challenge
        /// </summary>
        private SecurityChallenge MapDbSecurityChallenge(DbSecurityChallenge o) => new SecurityChallenge()
        {
            ChallengeText = o.ChallengeText,
            CreatedByKey = o.CreatedByKey,
            CreationTime = o.CreationTime.GetValueOrDefault(),
            Key = o.Key,
            LoadState = Core.Model.LoadState.FullLoad,
            ObsoletedByKey = o.ObsoletedByKey,
            ObsoletionTime = o.ObsoletionTime,
            UpdatedByKey = o.UpdatedByKey,
            UpdatedTime = o.UpdatedTime
        };

        /// <summary>
        /// Remove the specified challenge key from the user
        /// </summary>
        public void Remove(string userName, Guid challengeKey, IPrincipal principal)
        {
            if (!userName.Equals(principal.Identity.Name)
                || !principal.Identity.IsAuthenticated)
                throw new SecurityException($"Users may only modify their own security challenges");
           
            try
            {
                // Connection to perform auth
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    userName = userName.ToLower();
                    var user = conn.Table<DbSecurityUser>().Where(o => o.UserName.ToLower() == userName).FirstOrDefault();

                    if (user == null)
                        throw new KeyNotFoundException($"Unable to locate local user {userName}");

                    // TODO: Verify user is local user
                    byte[] challenge = challengeKey.ToByteArray();
                    conn.Table<DbSecurityUserChallengeAssoc>().Delete(o => o.ChallengeUuid == challenge && o.UserUuid == user.Uuid);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Removing challenge for user {0} failed: {1}", userName, e);
                throw new Exception($"Removing challenge for user {userName} failed", e);
            }
        }

        /// <summary>
        /// Set the specified security challenge
        /// </summary>
        public void Set(string userName, Guid challengeKey, string response, IPrincipal principal)
        {
            if (!userName.Equals(principal.Identity.Name)
                || !principal.Identity.IsAuthenticated)
                throw new SecurityException($"Users may only modify their own security challenges");
            else if (String.IsNullOrEmpty(response))
                throw new ArgumentNullException(nameof(response), "Response to challenge must be provided");

            try
            {

                // Connection to perform auth
                var conn = this.CreateConnection();
                using (conn.Lock())
                {
                    userName = userName.ToLower();
                    var user = conn.Table<DbSecurityUser>().Where(o => o.UserName.ToLower() == userName).FirstOrDefault();

                    if (user == null)
                        throw new KeyNotFoundException($"Unable to locate local user {userName}");
                    // TODO: Verify user is local user

                    var responseHash = ApplicationServiceContext.Current.GetService<IPasswordHashingService>().ComputeHash(response);
                    conn.Insert(new DbSecurityUserChallengeAssoc()
                    {
                        ChallengeResponse = responseHash,
                        ChallengeUuid = challengeKey.ToByteArray(),
                        UserUuid = user.Uuid,
                        ExpiryTime = null
                    });
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Setting challenge for user {0} failed: {1}", userName, e);
                throw new Exception($"Setting challenge for user {userName} failed", e);
            }
        }
    }
}

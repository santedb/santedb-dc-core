using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.SQLite.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Local administrator activation
    /// </summary>
    public class LocalAdministratorActivation : IDataMigration
    {
        /// <summary>
        /// Description
        /// </summary>
        public string Description => "Activates the local administrator account";

        /// <summary>
        /// ID of the admin
        /// </summary>
        public string Id => "999-activate-local-admin";

        /// <summary>
        /// Install
        /// </summary>
        /// <returns></returns>
        public bool Install()
        {
            var tracer = Tracer.GetTracer(this.GetType());
            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetReadWriteConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
            {
                try
                {

                    // Need to create a local admin?
                    if (ApplicationContext.Current.GetService<SQLiteIdentityService>() != null)
                    {
                        Guid uid = Guid.NewGuid();

                        // App setting
                        var activateLocalAdmin = ApplicationContext.Current.GetService<IConfigurationManager>().GetAppSetting("security.enableLocalAdmin") == "true";
                        ApplicationContext.Current.GetService<IConfigurationManager>().SetAppSetting("security.enableLocalAdmin", "false");

                        if (!db.Table<DbSecurityUser>().Where(o => o.UserName == "LocalAdministrator").Any())
                        {
                            // System user
                            db.Insert(new DbSecurityUser()
                            {
                                Key = uid,
                                Password = ApplicationContext.Current.GetService<IPasswordHashingService>().ComputeHash("ChangeMe123"),
                                SecurityHash = Guid.NewGuid().ToString(),
                                Lockout = activateLocalAdmin ? null : (DateTime?)DateTime.MaxValue,
                                UserName = "LocalAdministrator",
                                CreationTime = DateTime.Now,
                                CreatedByKey = Guid.Empty
                            });
                            db.Table<DbSecurityRole>().Where(o => o.Name == "LOCAL_ADMINISTRATORS" || o.Name == "LOCAL_USERS").ToList()
                                .ForEach(r => db.Insert(new DbSecurityUserRole()
                                {
                                    Key = Guid.NewGuid(),
                                    RoleUuid = r.Uuid,
                                    UserUuid = uid.ToByteArray()
                                }));
                        }

                        // GRANT LOGIN TO LOCAL USERS
                        var adminIsGod = ApplicationContext.Current.GetService<IConfigurationManager>().GetAppSetting("security.adminIsGod") == "true";
                        if (adminIsGod)
                        {
                            var adminRole = db.Table<DbSecurityRole>().Where(o => o.Name == "LOCAL_ADMINISTRATORS").FirstOrDefault();

                            var policy = db.Table<DbSecurityPolicy>().Where(o => o.Oid == "1.3.6.1.4.1.33349.3.1.5.9.2").FirstOrDefault();
                            db.Table<DbSecurityRolePolicy>().Delete(o => o.RoleId == adminRole.Uuid);
                            db.Insert(new DbSecurityRolePolicy()
                            {
                                GrantType = 2,
                                PolicyId = policy.Uuid,
                                RoleId = adminRole.Uuid,
                                Key = Guid.NewGuid()
                            });

                        }

                    }
                    return true;
                }
                catch (Exception e)
                {
                    tracer.TraceError("Could not activate local administrative account");
                    return false;
                }
            }
        }
    }
}

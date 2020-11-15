using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.SQLite.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Add challenge 
    /// </summary>
    public class AddSecurityChallenge : IDataMigration
    {
        // Initial search catalog
        private Tracer m_tracer = Tracer.GetTracer(typeof(AddSecurityChallenge));

        /// <summary>
        /// Gets the description of the migration
        /// </summary>
        public string Description
        {
            get
            {
                return "Add Security Offline Password Reset";
            }
        }

        /// <summary>
        /// Identifier of the migration
        /// </summary>
        public string Id
        {
            get
            {
                return "000-offline-reset";
            }
        }

        /// <summary>
        /// Perform the installation
        /// </summary>
        public bool Install()
        {
            try
            {

                var connStr = ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbData");

                // Is the search service registered?
                if (ApplicationContext.Current.GetService<SQLiteSecurityChallengeService>() == null)
                    ApplicationContext.Current.AddServiceProvider(typeof(SQLiteSecurityChallengeService), true);

                // Get a connection to the search database
                var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(connStr);
                using (conn.Lock())
                {
                    conn.CreateTable<DbSecurityChallenge>();
                    conn.CreateTable<DbSecurityUserChallengeAssoc>();
                } // release lock


                return true;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error installing challenge tables {0}", e);
                throw;
            }
        }
    }
}

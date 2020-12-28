using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Add occupational codes
    /// </summary>
    public class AddOccupationalCodes : IDataMigration
    {

        private Tracer m_tracer = Tracer.GetTracer(typeof(AddOccupationalCodes));

        /// <summary>
        /// Description of the migration
        /// </summary>
        public string Description => "Adds missing fields to patient and person";

        /// <summary>
        /// Identifier
        /// </summary>
        public string Id => "999-patient-occ-code";

        /// <summary>
        /// Install the objects
        /// </summary>
        public bool Install()
        {
            try
            {

                var connStr = ApplicationContext.Current.ConfigurationManager.GetConnectionString("santeDbData");

                // Get a connection to the search database
                var conn = SQLiteConnectionManager.Current.GetReadWriteConnection(connStr);
                using (conn.Lock())
                {
                    conn.MigrateTable<DbPatient>();
                    conn.MigrateTable<DbPerson>();
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

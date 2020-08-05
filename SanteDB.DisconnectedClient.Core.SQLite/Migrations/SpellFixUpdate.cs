using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Configuration.Data.Migrations;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.SQLite.Model;
using SanteDB.DisconnectedClient.SQLite.Model.Acts;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using SanteDB.DisconnectedClient.SQLite.Model.DataType;
using SanteDB.DisconnectedClient.SQLite.Model.Entities;
using SanteDB.DisconnectedClient.SQLite.Model.Extensibility;
using SanteDB.DisconnectedClient.SQLite.Model.Roles;
using SanteDB.DisconnectedClient.SQLite.Model.Security;
using SanteDB.DisconnectedClient.SQLite.Query.ExtendedFunctions;
using SanteDB.DisconnectedClient.SQLite.Search;
using SanteDB.DisconnectedClient.SQLite.Search.Model;
using SanteDB.DisconnectedClient.SQLite.Security;
using SanteDB.Matcher.Matchers;
using SanteDB.Matcher.Services;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.SQLite.Migrations
{
    /// <summary>
    /// Spell fix update
    /// </summary>
    public class SpellFixUpdate : IConfigurationMigration
    {
        private Tracer m_tracer = Tracer.GetTracer(typeof(SpellFixUpdate));

        /// <summary>
        /// Database migration
        /// </summary>
        public string Id => "zz-spellfix";

        /// <summary>
        /// Spellfix update
        /// </summary>
        public string Description => "Initializes the spellfix data";

        /// <summary>
        /// Install the initial catalog
        /// </summary>
        public bool Install()
        {

            // Database for the SQL Lite connection
            var db = SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString("santeDbSearch"));
            using (db.Lock())
                this.Install(db);

            db = SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current?.ConfigurationManager.GetConnectionString(ApplicationContext.Current?.Configuration.GetSection<DcDataConfigurationSection>().MainDataSourceConnectionStringName));
            using (db.Lock())
                return this.Install(db);
        }

        /// <summary>
        /// Install to context
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public bool Install(SQLiteConnection db)
        {
            try
            {

                db.Execute(@"CREATE TABLE __sfEditCost (  iLang INT, cFrom TEXT, cTo TEXT, iCost INT );");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'','?', 1);");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'?','', 1);");
                db.Execute(@"INSERT INTO __sfEditCost VALUES (0,'?','?', 1);");
                new LevenshteinFilterFunction().Initialize(db);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deploying Spellfix Update: {0}", e);
                throw new System.Data.DataException("Error deploying Spellfix Update", e);
            }
            return true;
        }
    }
}

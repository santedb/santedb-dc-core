using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.SQLite.Query.ExtendedFunctions
{
    /// <summary>
    /// Phonehash (loosely based on Metaphone) filter function
    /// </summary>
    public class MetaphoneFilterFunction : IDbFilterFunction
    {
        /// <summary>
        /// Name of the filter function
        /// </summary>
        public string Name => "metaphone";

        /// <summary>
        /// Create SQL statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {
            var match = new Regex(@"^([<>]?=?)(.*?)$").Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op)) op = "=";

            if (parms.Length == 1) // There is a threshold
                return current.Append($"editdist3(spellfix1_phonehash({filterColumn}), spellfix1_phonehash(?)) {op} ?", QueryBuilder.CreateParameterValue(parms[0], operandType), QueryBuilder.CreateParameterValue(value, operandType));
            else
                return current.Append($"spellfix1_phonehash({filterColumn}) {op} spellfix1_phonehash(?)", QueryBuilder.CreateParameterValue(value, operandType));

        }

        /// <summary>
        /// True if the extension is installed
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
            if (File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SpellFix.dll")) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "spellfix.so")))
            {
                try
                {
                    connection.Platform.SQLiteApi.EnableLoadExtension(connection.Handle, 1);
                    if (connection.ExecuteScalar<Int32>("SELECT sqlite_compileoption_used('SQLITE_ENABLE_LOAD_EXTENSION')") == 1)
                    {
                        try
                        {
                            connection.Execute("SELECT load_extension('spellfix');");
                        }
                        catch
                        { }
                        var diff = connection.ExecuteScalar<Int32>("SELECT editdist3('test','test1');");
                        if (diff > 1)
                            connection.ExecuteScalar<Int32>("SELECT editdist3('__sfEditCost');");
                    }
                }
                catch (Exception e) when (e.Message == "SQL logic error")
                {
                }
                catch (Exception e)
                {
                    Tracer.GetTracer(typeof(LevenshteinFilterFunction)).TraceWarning("Could not initialize SpellFix - {0}", e);
                }
            }
        }
    }
}

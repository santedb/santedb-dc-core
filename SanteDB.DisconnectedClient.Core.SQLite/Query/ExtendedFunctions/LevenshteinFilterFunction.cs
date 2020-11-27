/*
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
 * Date: 2020-8-15
 */
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
    /// Substring filter function
    /// </summary>
    public class LevenshteinFilterFunction : IDbFilterFunction
    {
        /// <summary>
        /// Name of the filter function
        /// </summary>
        public string Name => "levenshtein";

        /// <summary>
        /// Create SQL statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {
            var match = new Regex(@"^([<>]?=?)(.*?)$").Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op)) op = "=";

            switch (parms.Length)
            {
                case 1:
                    return current.Append($"editdist3(TRIM(LOWER({filterColumn})), TRIM(LOWER(?))) {op} ?", QueryBuilder.CreateParameterValue(parms[0], operandType), QueryBuilder.CreateParameterValue(value, typeof(Int32)));
                default:
                    throw new ArgumentOutOfRangeException("Invalid number of parameters of string diff");
            }
        }

        /// <summary>
        /// True if the extension is installed
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
            if (Assembly.GetEntryAssembly() != null &&
                !String.IsNullOrEmpty(Assembly.GetEntryAssembly().Location) &&
                (File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SpellFix.dll")) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "spellfix.so"))))
            {
                try
                {
                    if (connection.ExecuteScalar<Int32>("SELECT sqlite_compileoption_used('SQLITE_ENABLE_LOAD_EXTENSION')") == 1)
                    {
                        connection.Platform.SQLiteApi.EnableLoadExtension(connection.Handle, 1);
                        try // It might already be loaded
                        {
                            connection.ExecuteScalar<Int32>("SELECT editdist3('test','test1');");
                        }
                        catch
                        {
                            connection.ExecuteScalar<String>("SELECT load_extension('spellfix');");
                        }

                        var diff = connection.ExecuteScalar<Int32>("SELECT editdist3('test','test1');");
                        if (diff > 1)
                            connection.ExecuteScalar<Int32>("SELECT editdist3('__sfEditCost');");
                    }
                }
                catch(Exception e) when (e.Message == "SQL logic error")
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

/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Diagnostics;
using SQLite.Net;
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

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

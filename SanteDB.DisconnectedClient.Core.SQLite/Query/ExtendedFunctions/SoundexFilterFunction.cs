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
using System.Text;
using System.Text.RegularExpressions;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.SQLite.Query.ExtendedFunctions
{
    /// <summary>
    /// Soundex filter function
    /// </summary>
    public class SoundexFilterFunction : IDbFilterFunction
    {

        // Whether SQLite has soundex
        private static bool? m_hasSoundex;

        /// <summary>
        /// Get the name of the function
        /// </summary>
        public string Name => "soundex";

        /// <summary>
        /// Create SQL statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {
            var match = new Regex(@"^([<>]?=?)(.*?)$").Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op)) op = "=";

            if (parms.Length == 1 ) // There is a threshold
                return current.Append($"soundex({filterColumn}) {op} soundex(?)", QueryBuilder.CreateParameterValue(parms[0], operandType));
            else
                return current.Append($"soundex({filterColumn}) {op} soundex(?)", QueryBuilder.CreateParameterValue(value, operandType));
        }

        /// <summary>
        /// Initialize the soundex algorithm
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
            if (!m_hasSoundex.HasValue)
                try
                {
                    m_hasSoundex = connection.ExecuteScalar<Int32>("select sqlite_compileoption_used('SQLITE_SOUNDEX');") == 1;
                }
                catch
                {
                    m_hasSoundex = false;
                }
        }
    }
}

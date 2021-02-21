/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
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
    /// Date difference function
    /// </summary>
    public class DateDiffFunction : IDbFilterFunction
    {
        /// <summary>
        /// Date difference name
        /// </summary>
        public string Name => "date_diff";

        /// <summary>
        /// Create sql statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {
            var match = new Regex(@"^([<>]?=?)(.*?)$").Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op)) op = "=";

            match = new Regex(@"^(\d*?)([yMdwhms])$").Match(value);
            if (match.Success)
            {
                String qty = match.Groups[1].Value,
                    unit = match.Groups[2].Value;
                long qtyParse = long.Parse(qty);

                switch (unit)
                {
                    case "y":
                        qtyParse *= TimeSpan.TicksPerDay * 365;
                        break;
                    case "M":
                        qtyParse *= TimeSpan.TicksPerDay * 30;
                        break;
                    case "d":
                        qtyParse *= TimeSpan.TicksPerDay;
                        break;
                    case "w":
                        qtyParse *= TimeSpan.TicksPerDay * 7;
                        break;
                    case "h":
                        qtyParse *= TimeSpan.TicksPerHour;
                        break;
                    case "m":
                        qtyParse *= TimeSpan.TicksPerMinute;
                        break;
                    case "s":
                        qtyParse *= TimeSpan.TicksPerSecond;
                        break;
                }
                return current.Append($"ABS({filterColumn} - ?) {op} {qtyParse}", QueryBuilder.CreateParameterValue(parms[0], operandType), QueryBuilder.CreateParameterValue(parms[0], operandType));
            }
            else if (TimeSpan.TryParse(value, out TimeSpan timespan))
            {
                return current.Append($"ABS({filterColumn} - ?) {op} {timespan.Ticks}", QueryBuilder.CreateParameterValue(parms[0], operandType), QueryBuilder.CreateParameterValue(parms[0], operandType));
            }
            else
                throw new InvalidOperationException("Date difference needs to have whole number distance and single character unit or be a valid TimeSpan");
        }

        /// <summary>
        /// Initialize
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
        }
    }
}

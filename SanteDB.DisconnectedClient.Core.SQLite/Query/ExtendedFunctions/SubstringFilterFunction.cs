using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.SQLite.Query.ExtendedFunctions
{
    /// <summary>
    /// Substring filter
    /// </summary>
    public class SubstringFilterFunction : IDbFilterFunction
    {
        /// <summary>
        /// Gets the name of the function
        /// </summary>
        public string Name => "substr";

        /// <summary>
        /// Create the sql statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {
            var match = new Regex(@"^([<>]?=?)(.*?)$").Match(operand);
            String op = match.Groups[1].Value, value = match.Groups[2].Value;
            if (String.IsNullOrEmpty(op)) op = "=";

            switch (parms.Length)
            {
                case 1:
                    return current.Append($"substr({filterColumn}, {parms[0]}) {op} substr(?, {parms[0]})", QueryBuilder.CreateParameterValue(value, operandType));
                case 2:
                    return current.Append($"substr({filterColumn}, {parms[0]}, {parms[1]}) {op} substr(?, {parms[0]}, {parms[1]})", QueryBuilder.CreateParameterValue(value, operandType));
            }
            return current.Append($"substr({filterColumn}, {parms[0]}) {op} substr(?, {parms[0]})", QueryBuilder.CreateParameterValue(value, operandType));
        }

        /// <summary>
        /// Initialize the filter
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
        }
    }
}

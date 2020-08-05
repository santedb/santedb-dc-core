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
    public class SoundslikeFunction : IDbFilterFunction
    {
        /// <summary>
        /// Get the name of the function
        /// </summary>
        public string Name => "soundslike";

        /// <summary>
        /// Create SQL statement
        /// </summary>
        public SqlStatement CreateSqlStatement(SqlStatement current, string filterColumn, string[] parms, string operand, Type operandType)
        {

            if (parms.Length == 1)
                return current.Append($"soundex({filterColumn}) = soundex(?)", QueryBuilder.CreateParameterValue(parms[0], operandType));
            else
            {
                switch (parms[1])
                {
                    case "soundex":
                        return current.Append($"soundex({filterColumn}) = soundex(?)", QueryBuilder.CreateParameterValue(parms[0], operandType));
                    default:
                        throw new NotSupportedException($"Sounds-like algorithm {parms[1]} is not supported");
                }
            }
        }

        /// <summary>
        /// Initialize the soundex algorithm
        /// </summary>
        public void Initialize(SQLiteConnection connection)
        {
        }
    }
}

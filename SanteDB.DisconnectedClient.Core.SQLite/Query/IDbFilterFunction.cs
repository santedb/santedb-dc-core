using System;
using System.Collections.Generic;
using System.Text;
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net;

namespace SanteDB.DisconnectedClient.SQLite.Query
{
    /// <summary>
    /// Represents a filter function for database
    /// </summary>
    public interface IDbFilterFunction
    {

        /// <summary>
        /// Is installed?
        /// </summary>
        void Initialize(SQLiteConnection connection);

        /// <summary>
        /// Gets the name of the filter function
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Creates the SQL Statement which implements the filter
        /// </summary>
        /// <param name="current">The current SQLStatement</param>
        /// <param name="filterColumn">The column being filtered on</param>
        /// <param name="parms">The parameters to the function</param>
        /// <param name="operand">The provided operand on the query string</param>
        /// <returns>The constructed / updated SQLStatement</returns>
        SqlStatement CreateSqlStatement(SqlStatement current, String filterColumn, String[] parms, String operand, Type operandType);

    }
}

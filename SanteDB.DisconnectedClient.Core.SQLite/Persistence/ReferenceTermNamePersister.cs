using SanteDB.Core;
using SanteDB.Core.Interfaces;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.DisconnectedClient.SQLite.Model.Concepts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Persistence
{
    /// <summary>
    /// Persistence service for reftermname
    /// </summary>
    public class ReferenceTermNamePersister : IdentifiedPersistenceService<ReferenceTermName, DbReferenceTermName>
    {

        /// <summary>
        /// Performs the actual insert.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="data">Data.</param>
        /// <param name="principal">The principal.</param>
        /// <returns>Returns the inserted reference term name.</returns>
        protected override ReferenceTermName InsertInternal(SQLiteDataContext context, ReferenceTermName data)
        {
            // set the key if we don't have one
            if (!data.Key.HasValue || data.Key == Guid.Empty)
                data.Key = Guid.NewGuid();

            // set the creation time if we don't have one
            if (data.CreationTime == default(DateTimeOffset))
                data.CreationTime = DateTimeOffset.Now;

            return base.InsertInternal(context, data);
        }
    }
}

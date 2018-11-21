using RestSrvr.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Contracts
{
    /// <summary>
    /// Represents a WWW service contract
    /// </summary>
    [ServiceContract(Name = "WWW")]
    public interface IWwwServiceContract
    {

        /// <summary>
        /// Get specified object
        /// </summary>
        [Get("*")]
        Stream Get();

    }
}

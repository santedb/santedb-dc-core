using SanteDB.Core.Model.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services
{

    
    /// <summary>
    /// Represents a geo-tagging service to get device and position
    /// </summary>
    public interface IGeoTaggingService
    {

        /// <summary>
        /// Gets the current position
        /// </summary>
        GeoTag GetCurrentPosition();

    }
}

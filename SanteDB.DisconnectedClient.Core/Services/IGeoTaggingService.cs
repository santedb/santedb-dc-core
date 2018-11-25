using SanteDB.Core.Model.DataTypes;

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

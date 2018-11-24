using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Services;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents a generic resource repository factory
    /// </summary>
    public class LocalRepositoryFactoryService : IRepositoryServiceFactory
    {
        /// <summary>
        /// Create the specified resource service factory
        /// </summary>
        public IRepositoryService<T> CreateRepository<T>() where T : IdentifiedData
        {
            Tracer.GetTracer(typeof(LocalRepositoryFactoryService)).TraceWarning("Creating generic repository for {0}. Security may be compromised! Please register an appropriate repository service with the host", typeof(T).FullName);
            return new GenericLocalRepository<T>();
        }

    }
}

using SanteDB.Cdss.Xml.Model;
using SanteDB.Cdss.Xml;
using SanteDB.Core.Cdss;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Templates;
using SanteDB.Core.Templates.Definition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using SanteDB.Core.Security.Services;
using SanteDB.Core.i18n;
using System.Linq;
using SanteDB.Core.Security;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Implementation of the <see cref="IDataTemplateManagementService"/> which uses a file system location
    /// </summary>
    public class FileSystemDataTemplateManager : IDataTemplateManagementService
    {

        // Location of the template library
        private readonly string m_libraryLocation;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ConcurrentDictionary<Guid, DataTemplateDefinition> m_library = new ConcurrentDictionary<Guid, DataTemplateDefinition>();
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FileSystemDataTemplateManager));
        
        /// <summary>
        /// Policy enforcement service
        /// </summary>
        public FileSystemDataTemplateManager(IPolicyEnforcementService pepService)
        {
            this.m_pepService = pepService;
            this.m_libraryLocation = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "templates");
            if (!Directory.Exists(this.m_libraryLocation))
            {
                Directory.CreateDirectory(this.m_libraryLocation);
            }
            this.ProcessLibraryDirectory();
        }

        /// <summary>
        /// Process the library directory
        /// </summary>
        private void ProcessLibraryDirectory()
        {
            foreach (var d in Directory.EnumerateFiles(this.m_libraryLocation, "*.xml"))
            {
                using (var fs = File.OpenRead(d))
                {
                    var defn = DataTemplateDefinition.Load(fs);
                    this.m_library.TryAdd(defn.Uuid, defn);
                }
            }
        }

        /// <summary>
        /// Create a directory path
        /// </summary>
        private string CreatePath(DataTemplateDefinition definition) => Path.ChangeExtension(Path.Combine(this.m_libraryLocation, definition.Key.ToString()), "xml");

        /// <inheritdoc/>
        public string ServiceName => "File System Data Template Manager";
        
        /// <inheritdoc/>
        public DataTemplateDefinition AddOrUpdate(DataTemplateDefinition definition)
        {
            
            if(definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if(definition.Key == Guid.Empty)
            {
                definition.Key = Guid.NewGuid();
            }

            // Check for existing as readonly
            if(this.m_library.TryGetValue(definition.Uuid, out var existing) && existing.Readonly)
            {
                throw new InvalidOperationException(ErrorMessages.OBJECT_READONLY);
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);
            try
            {
                using (var fs = File.Create(this.CreatePath(definition))) {
                    definition.Save(fs);

                    // Now add to library
                    this.m_library.TryRemove(definition.Uuid, out _);
                    this.m_library.TryAdd(definition.Uuid, definition);
                    return definition;
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Could not add or update {0} - {1}", definition, e.ToHumanReadableString());
                throw;
            }

        }

        /// <inheritdoc/>
        public IQueryResultSet<DataTemplateDefinition> Find(Expression<Func<DataTemplateDefinition, bool>> query) => this.m_library.Values.Where(query.Compile()).AsResultSet();

        /// <inheritdoc/>
        public DataTemplateDefinition Get(Guid key)
        {
            _ = this.m_library.TryGetValue(key, out var retVal);
            return retVal;
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Remove(Guid key)
        {
            if (!this.m_library.TryGetValue(key, out var existing))
            {
                throw new KeyNotFoundException(key.ToString());
            }

            if(existing.Readonly)
            {
                throw new InvalidOperationException(ErrorMessages.OBJECT_READONLY);
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);

            File.Delete(this.CreatePath(existing));
            this.m_library.TryRemove(key, out _);
            return existing;
        }
    }
}

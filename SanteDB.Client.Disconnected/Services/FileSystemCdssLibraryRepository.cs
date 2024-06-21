using SanteDB.Cdss.Xml;
using SanteDB.Cdss.Xml.Model;
using SanteDB.Core.Cdss;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// A CDSS library repository that uses a file system for storage
    /// </summary>
    public class FileSystemCdssLibraryRepository : ICdssLibraryRepository
    {
        // Location of the CDSS library
        private readonly string m_cdssLibraryLocation;
        private readonly ConcurrentDictionary<Guid, ICdssLibrary> m_cdssLibrary = new ConcurrentDictionary<Guid, ICdssLibrary>();
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FileSystemCdssLibraryRepository));

        /// <summary>
        /// DI constructor
        /// </summary>
        public FileSystemCdssLibraryRepository()
        {
            this.m_cdssLibraryLocation = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "cdss");
            if (!Directory.Exists(this.m_cdssLibraryLocation))
            {
                Directory.CreateDirectory(this.m_cdssLibraryLocation);
            }

            this.ProcessCdssDirectory();
        }

        /// <summary>
        /// Process the CDSS directory
        /// </summary>
        private void ProcessCdssDirectory()
        {
            foreach (var d in Directory.EnumerateFiles(this.m_cdssLibraryLocation, "*.xml"))
            {
                using (var fs = File.OpenRead(d))
                {
                    var defn = CdssLibraryDefinition.Load(fs);
                    this.m_cdssLibrary.TryAdd(defn.Uuid, new XmlProtocolLibrary(defn));
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "File System Based CDSS Library";

        /// <inheritdoc/>
        public IQueryResultSet<ICdssLibrary> Find(Expression<Func<ICdssLibrary, bool>> filter)
        {
            var compl = filter.Compile();
            return new MemoryQueryResultSet<ICdssLibrary>(this.m_cdssLibrary.Values.Where(compl));
        }

        /// <inheritdoc/>
        public ICdssLibrary Get(Guid libraryUuid, Guid? versionUuid)
        {
            if(this.m_cdssLibrary.TryGetValue(libraryUuid, out var retVal))
            {
                return retVal;
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        /// <inheritdoc/>
        public ICdssLibrary InsertOrUpdate(ICdssLibrary libraryToInsert)
        {
            if(!(libraryToInsert is XmlProtocolLibrary xprotoLib))
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(XmlProtocolLibrary), libraryToInsert.GetType()));
            }

            // First we want to insert the CDSS definition
            this.m_cdssLibrary.TryAdd(libraryToInsert.Uuid, libraryToInsert);
            try
            {
                var fn = this.GetFilePath(libraryToInsert.Uuid);
                this.m_tracer.TraceInfo("Writing CDSS library to {0}", fn);
                using (var fs = File.Create(fn))
                {
                    xprotoLib.Save(fs);        
                }
                return libraryToInsert;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Could not save CDSS library: {0}", e);
                throw;
            }

        }

        /// <summary>
        /// Get the file path
        /// </summary>
        private string GetFilePath(Guid uuid) => Path.Combine(this.m_cdssLibraryLocation, uuid.ToString()) + ".xml";

        /// <inheritdoc/>
        public ICdssLibrary Remove(Guid libraryUuid)
        {
            if(this.m_cdssLibrary.TryRemove(libraryUuid, out var retVal) )
            {
                var fn = this.GetFilePath(libraryUuid);
                if(File.Exists(fn))
                {
                    File.Delete(fn);
                }
                return retVal;
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }
    }
}

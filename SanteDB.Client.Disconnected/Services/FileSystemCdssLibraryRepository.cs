/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you
 * may not use this file except in compliance with the License. You may
 * obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 *
 */
using SanteDB.Cdss.Xml;
using SanteDB.Cdss.Xml.Model;
using SanteDB.Core.Cdss;
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
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
    public class FileSystemCdssLibraryRepository : ICdssLibraryRepository, IProvideBackupAssets, IRestoreBackupAssets
    {
        /// <summary>
        /// Memory cdss entry data
        /// </summary>
        private class MemoryCdssEntry : ICdssLibraryRepositoryMetadata
        {

            public MemoryCdssEntry(CdssLibraryDefinition defn, DateTimeOffset lastUpdateTime)
            {
                this.Key = defn.Uuid;
                this.CreationTime = lastUpdateTime;
                this.CreatedByKey = Guid.Parse(AuthenticationContext.SystemApplicationSid);
            }

            public long? VersionSequence { get; set; }
            public Guid? VersionKey { get; set; }
            public Guid? PreviousVersionKey { get; set; }
            public bool IsHeadVersion { get; set; }

            public Guid? CreatedByKey { get; private set; }

            public Guid? ObsoletedByKey => null;

            public DateTimeOffset CreationTime { get; private set; }

            public DateTimeOffset? ObsoletionTime => null;

            public Guid? Key { get; set; }

            public string Tag => $"{this.Key}/{this.CreationTime.Ticks}";

            public DateTimeOffset ModifiedOn => this.CreationTime;
        }

        public static Guid FILE_CDSS_BACKUP_ASSET_ID = Guid.Parse("3c530ba8-7664-4144-9561-d65514399312");

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
                var fi = new FileInfo(d);
                using (var fs = File.OpenRead(d))
                {
                    var defn = CdssLibraryDefinition.Load(fs);
                    this.m_cdssLibrary.TryAdd(defn.Uuid, new XmlProtocolLibrary(defn)
                    {
                        StorageMetadata = new MemoryCdssEntry(defn, fi.LastWriteTime)
                    });
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "File System Based CDSS Library";

        /// <inheritdoc/>
        public Guid[] AssetClassIdentifiers => new Guid[] { FILE_CDSS_BACKUP_ASSET_ID };

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

            xprotoLib.StorageMetadata = new MemoryCdssEntry(xprotoLib.Library, DateTimeOffset.Now);
            this.m_cdssLibrary.TryAdd(xprotoLib.Uuid, xprotoLib);
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

        /// <inheritdoc/>
        public IEnumerable<IBackupAsset> GetBackupAssets()
        {
            foreach (var d in Directory.EnumerateFiles(this.m_cdssLibraryLocation, "*.xml"))
            {
                yield return new FileBackupAsset(FILE_CDSS_BACKUP_ASSET_ID, Path.GetFileName(d), d);
            }
        }

        /// <inheritdoc/>
        public bool Restore(IBackupAsset backupAsset)
        {
            using(var fs = File.Create(Path.Combine(m_cdssLibraryLocation, backupAsset.Name)))
            {
                using (var ins = backupAsset.Open())
                {
                    ins.CopyTo(fs);
                }
            }
            return true;
        }
    }
}

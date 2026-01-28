/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Services;
using SanteDB.Core.Model;
using SanteDB.Core.Data.Backup;
using System.Runtime.CompilerServices;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// Implementation of the <see cref="IDataTemplateManagementService"/> which uses a file system location
    /// </summary>
    public class FileSystemDataTemplateManager : IDataTemplateManagementService, IProvideBackupAssets, IRestoreBackupAssets
    {

        public static readonly Guid FILE_SYSTEM_BACKUP_ASSET_ID = Guid.Parse("bf146238-66da-449d-9301-1f52f15d0dc3");

        // Location of the template library
        private readonly string m_libraryLocation;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ConcurrentDictionary<Guid, DataTemplateDefinition> m_library = new ConcurrentDictionary<Guid, DataTemplateDefinition>();
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FileSystemDataTemplateManager));
        private readonly ITemplateDefinitionRepositoryService m_templateDefinitionRepository;

        /// <summary>
        /// Policy enforcement service
        /// </summary>
        public FileSystemDataTemplateManager(IPolicyEnforcementService pepService, ITemplateDefinitionRepositoryService templateDefinitionRepositoryService)
        {
            this.m_pepService = pepService;
            this.m_libraryLocation = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "templates");
            this.m_templateDefinitionRepository = templateDefinitionRepositoryService;
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
                    this.RegisterTemplateDefinition(defn);
                }
            }
        }

        /// <summary>
        /// Register the template definition with the template repository
        /// </summary>
        private void RegisterTemplateDefinition(DataTemplateDefinition dataTemplateDefinition)
        {
            // Validate database 
            if (!this.m_templateDefinitionRepository.Find(o => o.Key == dataTemplateDefinition.Uuid).Any())
            {
                this.m_templateDefinitionRepository.Save(new Core.Model.DataTypes.TemplateDefinition()
                {
                    Key = dataTemplateDefinition.Uuid,
                    Oid = dataTemplateDefinition.Oid,
                    Description = dataTemplateDefinition.Description,
                    Name = dataTemplateDefinition.Name,
                    Mnemonic = dataTemplateDefinition.Mnemonic,
                });
            }
        }

        /// <summary>
        /// Create a directory path
        /// </summary>
        private string CreatePath(DataTemplateDefinition definition) => Path.ChangeExtension(Path.Combine(this.m_libraryLocation, definition.Key.ToString()), "xml");

        /// <inheritdoc/>
        public string ServiceName => "File System Data Template Manager";

        /// <inheritdoc/>
        public Guid[] AssetClassIdentifiers => new Guid[] { FILE_SYSTEM_BACKUP_ASSET_ID };

        /// <inheritdoc/>
        public DataTemplateDefinition AddOrUpdate(DataTemplateDefinition definition)
        {
            
            if(definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            else if(definition.Key == Guid.Empty)
            {
                definition.Key = Guid.NewGuid();
            }

            // Check for existing as readonly
            if (AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);
                if (this.m_library.TryGetValue(definition.Uuid, out var existing) && existing.Readonly)
                {
                    throw new InvalidOperationException(ErrorMessages.OBJECT_READONLY);
                }
            }

            try
            {
                using (var fs = File.Create(this.CreatePath(definition))) {
                    definition.Save(fs, true);

                    // Now add to library
                    this.m_library.TryRemove(definition.Uuid, out _);
                    this.m_library.TryAdd(definition.Uuid, definition);
                    this.RegisterTemplateDefinition(definition);
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
            else if(existing.Readonly)
            {
                throw new InvalidOperationException(ErrorMessages.OBJECT_READONLY);
            }
            else if (AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);
            }

            File.Delete(this.CreatePath(existing));
            this.m_library.TryRemove(key, out _);

            // Remove registration
            this.m_templateDefinitionRepository.Delete(key);
            return existing;
        }


        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IQueryResultSet<DataTemplateDefinition> IRepositoryService<DataTemplateDefinition>.Find(Expression<Func<DataTemplateDefinition, bool>> query) => this.Find(query);

        /// <inheritdoc/>
        IQueryResultSet IRepositoryService.Find(Expression query)
        {
            if (query is Expression<Func<DataTemplateDefinition, bool>> qr)
            {
                return this.Find(qr);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Expression<Func<DataTemplateDefinition, bool>>), query.GetType()));
            }
        }

        /// <inheritdoc/>
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key, Guid versionKey) => this.Get(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Insert(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Insert(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Save(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Save(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }

        /// <inheritdoc/>
        public bool Restore(IBackupAsset backupAsset)
        {
            using (var outs = File.Create(Path.Combine(this.m_libraryLocation, backupAsset.Name)))
            {
                using(var ins = backupAsset.Open())
                {
                    ins.CopyTo(outs);
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public IEnumerable<IBackupAsset> GetBackupAssets()
        {
            foreach (var d in Directory.EnumerateFiles(this.m_libraryLocation, "*.xml"))
            {
                yield return new FileBackupAsset(FILE_SYSTEM_BACKUP_ASSET_ID, Path.GetFileName(d), d);
            }
        }
    }
}

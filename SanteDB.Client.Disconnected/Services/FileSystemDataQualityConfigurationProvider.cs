using SanteDB.Cdss.Xml.Model;
using SanteDB.Cdss.Xml;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Data.Quality.Configuration;
using SanteDB.Core.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace SanteDB.Client.Disconnected.Services
{
    /// <summary>
    /// File system data quality provider
    /// </summary>
    public class FileSystemDataQualityConfigurationProvider : IDataQualityConfigurationProviderService
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FileSystemDataQualityConfigurationProvider));
        private readonly ConcurrentDictionary<String, DataQualityRulesetConfiguration> m_rulesetLibrary = new ConcurrentDictionary<string, DataQualityRulesetConfiguration>();
        private readonly string m_libraryLocation;

        public FileSystemDataQualityConfigurationProvider()
        {
            this.m_libraryLocation = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "dq");
            if (!Directory.Exists(this.m_libraryLocation))
            {
                Directory.CreateDirectory(this.m_libraryLocation);
            }

            this.ProcessDqDirectory();
        }


        /// <summary>
        /// Process the DQ directory
        /// </summary>
        private void ProcessDqDirectory()
        {
            foreach (var d in Directory.EnumerateFiles(this.m_libraryLocation, "*.xml"))
            {
                using (var fs = File.OpenRead(d))
                {
                    var defn = DataQualityRulesetConfiguration.Load(fs);
                    this.m_rulesetLibrary.TryAdd(defn.Id, defn);
                }
            }
        }
        
        /// <inheritdoc/>
        public string ServiceName => "File System Data Quality Ruleset Provider";

        /// <inheritdoc/>
        public DataQualityRulesetConfiguration GetRuleSet(string id)
        {
            if(this.m_rulesetLibrary.TryGetValue(id, out var retVal))
            {
                return retVal;
            }
            else
            {
                throw new KeyNotFoundException(id);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<DataQualityRulesetConfiguration> GetRuleSets(bool includeObsoleted = false) => this.m_rulesetLibrary.Values;

        /// <inheritdoc/>
        public IEnumerable<DataQualityResourceConfiguration> GetRulesForType<T>() => this.GetRulesForType(typeof(T));

        /// <inheritdoc/>
        public IEnumerable<DataQualityResourceConfiguration> GetRulesForType(Type forType) => this.GetRuleSets().SelectMany(o => o.Resources).Where(r => r.ResourceType == forType);

        /// <inheritdoc/>
        public void RemoveRuleSet(string id)
        {
            if (this.m_rulesetLibrary.TryRemove(id, out _))
            {
                var pathName = Path.Combine(this.m_libraryLocation, id) + ".xml";
                if (File.Exists(pathName))
                {
                    File.Delete(pathName);
                }
            }
        }

        /// <inheritdoc/>
        public DataQualityRulesetConfiguration SaveRuleSet(DataQualityRulesetConfiguration configuration)
        {
            this.m_rulesetLibrary.TryAdd(configuration.Id, configuration);
            try
            {
                var pathName = Path.Combine(this.m_libraryLocation, configuration.Id) + ".xml";
                this.m_tracer.TraceInfo("Saving DQ library {0} to {1}", configuration.Id, pathName);
                using(var fs = File.Create(pathName))
                {
                    configuration.Save(fs);
                }
                return configuration;
            }
            catch(Exception e)
            {
                this.m_tracer.TraceError("Error saving rule set configuration: {0}", e);
                throw;
            }
        }
    }
}

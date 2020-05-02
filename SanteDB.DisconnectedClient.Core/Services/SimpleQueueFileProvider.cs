/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * User: fyfej
 * Date: 2019-11-27
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Configuration.Data;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Services
{
    /// <summary>
    /// Represents a file provider which can be used to store/retrieve queue objects
    /// </summary>
    public class SimpleQueueFileProvider : IQueueFileProvider
    {

        // Queue cache (in memory queue)
        private Dictionary<String, IdentifiedData> m_queueCache = new Dictionary<string, IdentifiedData>();

        /// <summary>
        /// Copy queue data 
        /// </summary>
        public string CopyQueueData(string data)
        {
            var sqlitePath = ApplicationContext.Current.ConfigurationManager.GetConnectionString(ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName).GetComponent("dbfile");

            // Create blob path
            var blobPath = Path.Combine(Path.GetDirectoryName(sqlitePath), "blob");
            if (!Directory.Exists(blobPath))
                Directory.CreateDirectory(blobPath);

            data = Path.Combine(blobPath, data);
            blobPath = Path.Combine(blobPath, Guid.NewGuid().ToString() + ".dat");
            File.Copy(data, blobPath);
            return Path.GetFileName(blobPath);
        }

        /// <summary>
        /// Get Queue Data
        /// </summary>
        public IdentifiedData GetQueueData(string pathSpec, Type typeSpec)
        {
#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
#endif
            XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(typeSpec);

            var sqlitePath = ApplicationContext.Current.ConfigurationManager.GetConnectionString(ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName).GetComponent("dbfile");

            // Create blob path
            var blobPath = Path.Combine(Path.GetDirectoryName(sqlitePath), "blob");
            if (!Directory.Exists(blobPath))
                Directory.CreateDirectory(blobPath);

            blobPath = Path.Combine(blobPath, pathSpec);

            IdentifiedData cached = null;
            if (!this.m_queueCache.TryGetValue(blobPath, out cached))
            {
                using (FileStream fs = File.OpenRead(blobPath))
                using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                using (TextReader tr = new StreamReader(gz))
                    return xsz.Deserialize(tr) as IdentifiedData;
            }
            return cached;
#if PERFMON
            }
            finally
            {
                sw.Stop();
                ApplicationContext.Current.PerformanceLog(nameof(SimpleQueueFileProvider), nameof(GetQueueData), typeSpec.Name, sw.Elapsed);
            }
#endif
        }


        /// <summary>
        /// Get Queue Data
        /// </summary>
        public byte[] GetQueueData(string pathSpec)
        {
#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
#endif


            var sqlitePath = ApplicationContext.Current.ConfigurationManager.GetConnectionString(ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName).GetComponent("dbfile");

            // Create blob path
            var blobPath = Path.Combine(Path.GetDirectoryName(sqlitePath), "blob");
            if (!Directory.Exists(blobPath))
                Directory.CreateDirectory(blobPath);

            blobPath = Path.Combine(blobPath, pathSpec);
            using (var fs = File.OpenRead(blobPath))
            using (var gzs = new GZipStream(fs, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                gzs.CopyTo(ms);
                ms.Flush();
                return ms.ToArray();
            }
#if PERFMON
            }
            finally
            {
                sw.Stop();
                ApplicationContext.Current.PerformanceLog(nameof(SimpleQueueFileProvider), nameof(GetQueueData), "Raw", sw.Elapsed);
            }
#endif
        }

        /// <summary>
        /// Remove queue data
        /// </summary>
        public void RemoveQueueData(String pathSpec)
        {
            var sqlitePath = ApplicationContext.Current.ConfigurationManager.GetConnectionString(ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName).GetComponent("dbfile");

            var blobPath = Path.Combine(Path.GetDirectoryName(sqlitePath), "blob");
            if (!Directory.Exists(blobPath))
                Directory.CreateDirectory(blobPath);

            blobPath = Path.Combine(blobPath, pathSpec);
            if (File.Exists(blobPath))
                File.Delete(blobPath);
            if (this.m_queueCache.ContainsKey(blobPath))
                lock (this.m_queueCache)
                    this.m_queueCache.Remove(blobPath);
        }

        /// <summary>
        /// Save queue data
        /// </summary>
        public string SaveQueueData(IdentifiedData data)
        {
#if PERFMON
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
#endif
            XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(data.GetType());

            var sqlitePath = ApplicationContext.Current.ConfigurationManager.GetConnectionString(ApplicationContext.Current.Configuration.GetSection<DcDataConfigurationSection>().MessageQueueConnectionStringName).GetComponent("dbfile");

            // Create blob path
            var blobPath = Path.Combine(Path.GetDirectoryName(sqlitePath), "blob");
            if (!Directory.Exists(blobPath))
                Directory.CreateDirectory(blobPath);

            blobPath = Path.Combine(blobPath, Guid.NewGuid().ToString() + ".dat");
            using (FileStream fs = File.Create(blobPath))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
            using (TextWriter tw = new StreamWriter(gz))
                xsz.Serialize(tw, data);

            lock (m_queueCache)
                if (!this.m_queueCache.ContainsKey(blobPath))
                    this.m_queueCache.Add(blobPath, data);

            return Path.GetFileName(blobPath);
#if PERFMON
            }
            finally
            {
                sw.Stop();
                ApplicationContext.Current.PerformanceLog(nameof(SimpleQueueFileProvider), nameof(SaveQueueData), data.GetType().Name, sw.Elapsed);
            }
#endif
        }
    }
}

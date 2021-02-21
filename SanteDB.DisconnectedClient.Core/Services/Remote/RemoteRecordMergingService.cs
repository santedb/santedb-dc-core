/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.Messaging.HDSI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represents a record merging service which uses the remote HDSI for fetching and controlling merges
    /// </summary>
    /// <typeparam name="T">The type of record that this is merging</typeparam>
    public class RemoteRecordMergingService<T> : IRecordMergingService<T>
        where T : IdentifiedData, new()
    {

        // Tracer for log files
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteRecordMergingService<T>));

        /// <summary>
        /// Gets the name of the record merging services
        /// </summary>
        public string ServiceName => $"Remote Record Merging Service for {typeof(T).Name}";

        /// <summary>
        /// Fired when this action is merging records
        /// </summary>
        public event EventHandler<DataMergingEventArgs<T>> Merging;

        /// <summary>
        /// Fired after this service has merged a record
        /// </summary>
        public event EventHandler<DataMergeEventArgs<T>> Merged;
        public event EventHandler<DataMergingEventArgs<T>> UnMerging;
        public event EventHandler<DataMergeEventArgs<T>> UnMerged;


        /// <summary>
        /// Get the client
        /// </summary>
        public HdsiServiceClient GetClient()
        {
            var retVal = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
            retVal.Client.Credentials = retVal.Client.Description.Binding.Security.CredentialProvider.GetCredentials(AuthenticationContext.Current.Principal);
            return retVal;
        }

        /// <summary>
        /// Perform a diff for the specified object
        /// </summary>
        public Patch Diff(Guid masterKey, Guid linkedDuplicateKey)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Get<Patch>($"{typeof(T).GetSerializationName()}/{masterKey}/_duplicate/{linkedDuplicateKey}");
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error fetching remote diff between {0} and {1} : {2}", masterKey, linkedDuplicateKey, e);
                throw new Exception($"Error fetching remote diff between {masterKey} and {linkedDuplicateKey}", e);
            }
        }

        /// <summary>
        /// Flag all duplicates according to the configuration name
        /// </summary>
        public void FlagDuplicates(string configurationName = null)
        {
            if (!String.IsNullOrEmpty(configurationName))
                throw new NotSupportedException("Flagging duplicates with specific configuration is not supported in remote mode yet");

            try
            {
                using (var client = this.GetClient())
                {
                    client.Client.Post<Object, Bundle>($"{typeof(T).GetSerializationName()}/{Guid.Empty}/_flag", "application/json", new Bundle());
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error flagging duplicates for globals - {0}", e);
                throw new Exception("Error flagging duplicates on remote server", e);
            }
        }

        /// <summary>
        /// Flag duplicates for a specific object
        /// </summary>
        /// <param name="key">The object to flag duplicates for</param>
        /// <param name="configurationName">The configuration name to use</param>
        public T FlagDuplicates(Guid key, string configurationName = null)
        {
            if (!String.IsNullOrEmpty(configurationName))
                throw new NotSupportedException("Flagging duplicates with specific configuration is not supported in remote mode yet");

            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Post<Object, T>($"{typeof(T).GetSerializationName()}/{key}/_flag", "application/json", new Bundle());
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error flagging duplicates for globals for {0} - {1}", key, e);
                throw new Exception($"Error flagging duplicates on remote server for {key}", e);
            }
        }

        /// <summary>
        /// Get the duplicates for the specified object
        /// </summary>
        /// <param name="masterKey">The key of the master to flag duplicates for</param>
        /// <returns>The duplicates</returns>
        public IEnumerable<T> GetDuplicates(Guid masterKey)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Get<Bundle>($"{typeof(T).GetSerializationName()}/{masterKey}/_duplicate").Item.OfType<T>();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error fetching remote duplicates for {0} #{1} : {2}", typeof(T).GetSerializationName(), masterKey, e);
                throw new Exception($"Error fetching remote duplicates for {typeof(T).GetSerializationName()} #{masterKey}", e);
            }
        }

        /// <summary>
        /// Get all ignored objects
        /// </summary>
        /// <param name="masterKey">The master key for which ignored objects should be fetched</param>
        /// <returns>The ignored objects</returns>
        public IEnumerable<T> GetIgnored(Guid masterKey)
        {
            try
            {
                using (var client = this.GetClient())
                {
                    return client.Client.Get<Bundle>($"{typeof(T).GetSerializationName()}/{masterKey}/_gnored").Item.OfType<T>();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error fetching remote ignored records for {0} #{1} : {2}", typeof(T).GetSerializationName(), masterKey, e);
                throw new Exception($"Error fetching remote ignored records for {typeof(T).GetSerializationName()} #{masterKey}", e);
            }
        }

        /// <summary>
        /// Instruct the remote service to ignore the specified duplicate
        /// </summary>
        /// <param name="masterKey">The master to ignore duplicate on</param>
        /// <param name="falsePositives">The false positives to ignore</param>
        /// <returns>The new master</returns>
        public T Ignore(Guid masterKey, IEnumerable<Guid> falsePositives)
        {
            try
            {
                T retVal = default(T);
                using (var client = this.GetClient())
                {
                    foreach (var itm in falsePositives)
                        retVal = client.Client.Delete<T>($"{typeof(T).GetSerializationName()}/{masterKey}/_duplicate/{itm}");
                }
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error ignoring duplicates on {0} - {1}", masterKey, e);
                throw new Exception($"Error ignoring duplicates on {typeof(T).GetSerializationName()} #{masterKey}", e);
            }
        }

        /// <summary>
        /// Instructs the remote service to merge the linked duplicates into the specified master
        /// </summary>
        /// <param name="masterKey">The master to be merged</param>
        /// <param name="linkedDuplicates">The duplicates to be merged</param>
        /// <returns>The update master</returns>
        public T Merge(Guid masterKey, IEnumerable<Guid> linkedDuplicates)
        {
            try
            {
                var preEvt = new DataMergingEventArgs<T>(masterKey, linkedDuplicates);
                this.Merging?.Invoke(this, preEvt);
                if(preEvt.Cancel)
                {
                    throw new InvalidOperationException("Pre-Event Signals Cancel for Merge"); // TODO: Is this the best way to handle this?
                }

                using (var client = this.GetClient())
                {
                    var retVal = client.Client.Post<Bundle, T>($"{typeof(T).GetSerializationName()}/{masterKey}/_merge", "application/xml", new Bundle()
                    {
                        Item = linkedDuplicates.Select(o => new T()
                        {
                            Key = o
                        }).OfType<IdentifiedData>().ToList()
                    });

                    this.Merged?.Invoke(this, new DataMergeEventArgs<T>(masterKey, linkedDuplicates));
                    return retVal;
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error merging records with {0} - {1}", masterKey, e);
                throw new Exception($"Error merging records {String.Join(",", linkedDuplicates.Select(o => o.ToString()))} of type {typeof(T).GetSerializationName()} with {masterKey}");
            }
        }

        /// <summary>
        /// Unignore the specified ignored key
        /// </summary>
        public T UnIgnore(Guid masterKey, IEnumerable<Guid> ignoredKeys)
        {
            try
            {
                T retVal = default(T);
                using (var client = this.GetClient())
                {
                    foreach (var itm in ignoredKeys)
                        retVal = client.Client.Delete<T>($"{typeof(T).GetSerializationName()}/{masterKey}/_ignore/{itm}");
                }
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error un-ignoring duplicates on {0} - {1}", masterKey, e);
                throw new Exception($"Error un-ignoring duplicates on {typeof(T).GetSerializationName()} #{masterKey}", e);
            }
        }

        /// <summary>
        /// Unmerge the specified object
        /// </summary>
        /// <param name="masterKey">The master key to be unmerged</param>
        /// <param name="unmergeDuplicateKey">The duplicate to be unmerged from the master</param>
        /// <returns>The newly unmerged object</returns>
        public T Unmerge(Guid masterKey, Guid unmergeDuplicateKey)
        {
            try
            {
                var preEvt = new DataMergingEventArgs<T>(masterKey, new Guid[] { unmergeDuplicateKey });
                this.UnMerging?.Invoke(this, preEvt);
                if(preEvt.Cancel)
                {
                    throw new InvalidOperationException("Pre-Event Signals Cancel on Unmerge"); // TODO: Is this the best way to handle this?
                }
                using (var client = this.GetClient())
                {
                    var retVal = client.Client.Delete<T>($"{typeof(T).GetSerializationName()}/{masterKey}/_merge/{unmergeDuplicateKey}");
                    this.UnMerged?.Invoke(this, new DataMergeEventArgs<T>(masterKey, new Guid[] { unmergeDuplicateKey }));
                    return retVal;
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error unmerging duplicates on {0} - {1}", masterKey, e);
                throw new Exception($"Error unmerging {typeof(T).GetSerializationName()} #{masterKey}", e);
            }
        }
    }
}

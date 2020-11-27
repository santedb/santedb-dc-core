/*
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
using SanteDB.BI;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.DisconnectedClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;
using SanteDB.Core.Model.Query;
using System.Net;
using System.IO;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// A BI metadata repository which works remotely
    /// </summary>
    public class RemoteBiService : IBiMetadataRepository, IBiDataSource, IBiRenderService
    {
        /// <summary>
        /// Gets the BI metadata repository
        /// </summary>
        public string ServiceName => "Remote BI Metadata Repository";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteBiService));

        /// <summary>
        /// Gets the rest client
        /// </summary>
        /// <returns></returns>
        public IRestClient GetRestClient()
        {
            var retVal = ApplicationContext.Current.GetRestClient("bis"); 
            retVal.Accept = "application/json";
            return retVal;
        }

        /// <summary>
        /// Gets the specified BIS definition
        /// </summary>
        public TBisDefinition Get<TBisDefinition>(string id) where TBisDefinition : BiDefinition
        {
            try
            {
                var rootAtt = typeof(TBisDefinition).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                using (var client = this.GetRestClient())
                    return client.Get<TBisDefinition>($"{rootAtt.ElementName}/{id}");
            }
            catch(System.Net.WebException e)
            {
                var wr = e.Response as HttpWebResponse;
                this.m_tracer.TraceWarning("Remote service indicated failure: {0}", e);

                if (wr?.StatusCode == HttpStatusCode.NotFound)
                    return null;
                else
                    throw new Exception($"Error fetching BIS definition {id}", e);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error fetching BIS definition: {0}", e);
                throw new Exception($"Error fetching BIS definition {id}", e);
            }
        }

        /// <summary>
        /// Insert the specified object in the definition 
        /// </summary>
        public TBisDefinition Insert<TBisDefinition>(TBisDefinition metadata) where TBisDefinition : BiDefinition
        {
            try
            {
                var rootAtt = typeof(TBisDefinition).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                using (var client = this.GetRestClient())
                    return client.Post<TBisDefinition, TBisDefinition>($"{rootAtt.ElementName}", client.Accept, metadata);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error inserting BIS definition: {0}", e);
                throw new Exception($"Error inserting BIS definition {metadata}", e);
            }
        }

        /// <summary>
        /// Query the server endpoint for BIS metadata
        /// </summary>
        public IEnumerable<TBisDefinition> Query<TBisDefinition>(Expression<Func<TBisDefinition, bool>> filter, int offset, int? count) where TBisDefinition : BiDefinition
        {
            try
            {
                var rootAtt = typeof(TBisDefinition).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                using (var client = this.GetRestClient())
                    return client.Get<BiDefinitionCollection>($"{rootAtt.ElementName}", QueryExpressionBuilder.BuildQuery(filter).ToArray())?.Items.OfType<TBisDefinition>().ToList();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error querying BIS definition with query {0}: {1}", filter, e);
                throw new Exception($"Error executing BIS query {filter}", e);
            }
        }

        /// <summary>
        /// Removes the specified BIS object
        /// </summary>
        public void Remove<TBisDefinition>(string id) where TBisDefinition : BiDefinition
        {
            try
            {
                var rootAtt = typeof(TBisDefinition).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                using (var client = this.GetRestClient())
                    client.Delete<TBisDefinition>($"{rootAtt.ElementName}/{id}");
            }
            catch (System.Net.WebException e)
            {
                var wr = e.Response as HttpWebResponse;
                this.m_tracer.TraceWarning("Remote service indicated failure: {0}", e);

                if (wr?.StatusCode == HttpStatusCode.NotFound)
                    throw new KeyNotFoundException($"Could not find definition with id {id}", e);
                else
                    throw new Exception($"Error fetching BIS definition {id}", e);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error deleting BIS definition {0} #{1}: {2}", typeof(TBisDefinition), id, e);
                throw new Exception($"Error deleting BIS definition {typeof(TBisDefinition).FullName} #{id}", e);
            }
        }

        /// <summary>
        /// Execute the specified query definition remotely
        /// </summary>
        public BisResultContext ExecuteQuery(BiQueryDefinition queryDefinition, IDictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            try
            {
                var parmDict = parameters.ToDictionary(o => o.Key, o => o.Value);

                if (!parmDict.ContainsKey("_count"))
                    parmDict.Add("_count", count);
                if (!parmDict.ContainsKey("_offset"))
                    parmDict.Add("_offset", offset);

                var startTime = DateTime.Now;
                using (var client = this.GetRestClient())
                {
                    var results = client.Get<IEnumerable<dynamic>>($"Query/{queryDefinition.Id}", parameters.ToArray());
                    return new BisResultContext(queryDefinition, parameters, this, results, startTime);
                }
            }
            catch (System.Net.WebException e)
            {
                var wr = e.Response as HttpWebResponse;
                this.m_tracer.TraceWarning("Remote service indicated failure: {0}", e);

                if (wr?.StatusCode == HttpStatusCode.NotFound)
                    throw new KeyNotFoundException($"Could not find definition with id {queryDefinition.Id}", e);
                else
                    throw new Exception($"Error fetching BIS definition {queryDefinition.Id}", e);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error executing BIS query {queryDefinition.Name} - {e}");
                throw new Exception($"Error executing BIS query {queryDefinition.Name}", e);
                throw;
            }
        }

        /// <summary>
        /// Execute query identity
        /// </summary>
        public BisResultContext ExecuteQuery(string queryId, IDictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute the specified view
        /// </summary>
        public BisResultContext ExecuteView(BiViewDefinition viewDef, IDictionary<string, object> parameters, int offset, int? count)
        {
            try
            {
                var parmDict = parameters.ToDictionary(o => o.Key, o => o.Value);

                if(!parmDict.ContainsKey("_count"))
                    parmDict.Add("_count", count);
                if(!parmDict.ContainsKey("_offset"))
                    parmDict.Add("_offset", offset);

                var startTime = DateTime.Now;
                using (var client = this.GetRestClient())
                {
                    var results = client.Get<IEnumerable<dynamic>>($"Query/{viewDef.Id}", parameters.ToArray());
                    return new BisResultContext(viewDef.Query, parameters, this, results, startTime);
                }
            }
            catch (System.Net.WebException e)
            {
                var wr = e.Response as HttpWebResponse;
                this.m_tracer.TraceWarning("Remote service indicated failure: {0}", e);

                if (wr?.StatusCode == HttpStatusCode.NotFound)
                    throw new KeyNotFoundException($"Could not find definition with id {viewDef.Id}", e);
                else
                    throw new Exception($"Error fetching BIS definition {viewDef.Id }", e);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error executing BIS query {viewDef.Name} - {e}");
                throw new Exception($"Error executing BIS query {viewDef.Name}", e);
                throw;
            }
        }

        /// <summary>
        /// Render the specified report
        /// </summary>
        public Stream Render(string reportId, string viewName, string formatName, IDictionary<string, object> parameters, out string mimeType)
        {
            try 
            {
                if(!parameters.ContainsKey("_view"))
                    parameters.Add("_view", viewName);

                using (var client = this.GetRestClient())
                {
                    var contentType = String.Empty;
                    client.Responding += (o, e) => contentType = e.ContentType;
                    client.ProgressChanged += (o, e) => ApplicationContext.Current.SetProgress(reportId, e.Progress);
                    var retVal = client.Get($"Report/{formatName}/{reportId}", parameters.ToArray());
                    mimeType = contentType;
                    return new MemoryStream(retVal);
                }

            }
            catch (System.Net.WebException e)
            {
                var wr = e.Response as HttpWebResponse;
                this.m_tracer.TraceWarning("Remote service indicated failure: {0}", e);

                if (wr?.StatusCode == HttpStatusCode.NotFound)
                    throw new KeyNotFoundException($"Could not find definition with id {reportId}", e);
                else
                    throw new Exception($"Error fetching BIS report {reportId}", e);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error rendering BIS report {reportId} - {e}");
                throw new Exception($"Error executing BIS report {reportId}", e);
                throw;
            }
        }
    }
}

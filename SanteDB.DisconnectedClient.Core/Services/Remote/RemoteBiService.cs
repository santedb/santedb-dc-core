using SanteDB.BI;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.DisconnectedClient.Core.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Reflection;
using SanteDB.Core.Model.Query;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// A BI metadata repository which works remotely
    /// </summary>
    public class RemoteBiService : IBiMetadataRepository, IBiDataSource
    {
        /// <summary>
        /// Gets the BI metadata repository
        /// </summary>
        public string ServiceName => "Remote BI Metadata Repository";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteBiService));

        // The rest client to the server
        private IRestClient m_restClient;

        /// <summary>
        /// Creates a new instance of hte metadata repository
        /// </summary>
        public RemoteBiService()
        {
            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                this.m_restClient = ApplicationContext.Current.GetRestClient("bis");
                this.m_restClient.Accept = "application/json";
            };
        }

        /// <summary>
        /// Gets the specified BIS definition
        /// </summary>
        public TBisDefinition Get<TBisDefinition>(string id) where TBisDefinition : BiDefinition
        {
            try
            {
                var rootAtt = typeof(TBisDefinition).GetTypeInfo().GetCustomAttribute<XmlRootAttribute>();
                return this.m_restClient.Get<TBisDefinition>($"{rootAtt.ElementName}/{id}");
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
                return this.m_restClient.Post<TBisDefinition, TBisDefinition>($"{rootAtt.ElementName}", this.m_restClient.Accept, metadata);
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
                return this.m_restClient.Get<BiDefinitionCollection>($"{rootAtt.ElementName}", QueryExpressionBuilder.BuildQuery(filter).ToArray())?.Items.OfType<TBisDefinition>().ToList();
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
                this.m_restClient.Delete<TBisDefinition>($"{rootAtt.ElementName}/{id}");
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
        public BisResultContext ExecuteQuery(BiQueryDefinition queryDefinition, Dictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            try
            {
                var parmDict = parameters.ToDictionary(o => o.Key, o => o.Value);

                if (!parmDict.ContainsKey("_count"))
                    parmDict.Add("_count", count);
                if (!parmDict.ContainsKey("_offset"))
                    parmDict.Add("_offset", offset);

                var startTime = DateTime.Now;
                var results = this.m_restClient.Get<IEnumerable<dynamic>>($"Query/{queryDefinition.Id}", parameters.ToArray());
                return new BisResultContext(queryDefinition, parameters, this, results, startTime);
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
        public BisResultContext ExecuteQuery(string queryId, Dictionary<string, object> parameters, BiAggregationDefinition[] aggregation, int offset, int? count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Execute the specified view
        /// </summary>
        public BisResultContext ExecuteView(BiViewDefinition viewDef, Dictionary<string, object> parameters, int offset, int? count)
        {
            try
            {
                var parmDict = parameters.ToDictionary(o => o.Key, o => o.Value);

                if(!parmDict.ContainsKey("_count"))
                    parmDict.Add("_count", count);
                if(!parmDict.ContainsKey("_offset"))
                    parmDict.Add("_offset", offset);

                var startTime = DateTime.Now;
                var results = this.m_restClient.Get<IEnumerable<dynamic>>($"Query/{viewDef.Id}", parameters.ToArray());
                return new BisResultContext(viewDef.Query, parameters, this, results, startTime);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Error executing BIS query {viewDef.Name} - {e}");
                throw new Exception($"Error executing BIS query {viewDef.Name}", e);
                throw;
            }
        }
    }
}

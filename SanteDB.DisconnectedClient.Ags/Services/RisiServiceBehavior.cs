using RestSrvr.Attributes;
using SanteDB.Core.Model.RISI;
using SanteDB.Core.Model.Warehouse;
using SanteDB.Rest.RISI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// RISI service behavior
    /// </summary>
    [ServiceBehavior(Name = "RISI", InstanceMode = ServiceInstanceMode.PerCall)]
    public class RisiServiceBehavior : IRisiServiceContract
    {
        public DatamartDefinition CreateDatamart(DatamartDefinition definition)
        {
            throw new NotImplementedException();
        }

        public ParameterType CreateParameterType(ParameterType parameterType)
        {
            throw new NotImplementedException();
        }

        public ReportDefinition CreateReportDefinition(ReportDefinition reportDefinition)
        {
            throw new NotImplementedException();
        }

        public ReportFormat CreateReportFormat(ReportFormat reportFormat)
        {
            throw new NotImplementedException();
        }

        public DatamartStoredQuery CreateStoredQuery(string datamartId, DatamartStoredQuery queryDefinition)
        {
            throw new NotImplementedException();
        }

        public DataWarehouseObject CreateWarehouseObject(string datamartId, DataWarehouseObject obj)
        {
            throw new NotImplementedException();
        }

        public void DeleteDatamart(string id)
        {
            throw new NotImplementedException();
        }

        public ParameterType DeleteParameterType(string id)
        {
            throw new NotImplementedException();
        }

        public ReportDefinition DeleteReportDefinition(string id)
        {
            throw new NotImplementedException();
        }

        public ReportFormat DeleteReportFormat(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<DataWarehouseObject> ExecuteAdhocQuery(string datamartId)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<DataWarehouseObject> ExecuteStoredQuery(string datamartId, string queryId)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<ParameterType> GetAllReportParameterTypes()
        {
            throw new NotImplementedException();
        }

        public DatamartDefinition GetDatamart(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<DatamartDefinition> GetDatamarts()
        {
            throw new NotImplementedException();
        }

        public ReportDefinition GetReportDefinition(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<ReportDefinition> GetReportDefinitions()
        {
            throw new NotImplementedException();
        }

        public ReportFormat GetReportFormat(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<ReportFormat> GetReportFormats()
        {
            throw new NotImplementedException();
        }

        public ReportParameter GetReportParameter(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<ReportParameter> GetReportParameters(string id)
        {
            throw new NotImplementedException();
        }

        public AutoCompleteSourceDefinition GetReportParameterValues(string id, string parameterId)
        {
            throw new NotImplementedException();
        }

        public AutoCompleteSourceDefinition GetReportParameterValuesCascading(string id, string parameterId, string parameterValue)
        {
            throw new NotImplementedException();
        }

        public Stream GetReportSource(string id)
        {
            throw new NotImplementedException();
        }

        public RisiCollection<DatamartStoredQuery> GetStoredQueries(string datamartId)
        {
            throw new NotImplementedException();
        }

        public DataWarehouseObject GetWarehouseObject(string datamartId, string objectId)
        {
            throw new NotImplementedException();
        }

        public Stream RunReport(string id, string format, ReportBundle bundle)
        {
            throw new NotImplementedException();
        }

        public ParameterType UpdateParameterType(string id, ParameterType parameterType)
        {
            throw new NotImplementedException();
        }

        public ReportDefinition UpdateReportDefinition(string id, ReportDefinition reportDefinition)
        {
            throw new NotImplementedException();
        }

        public ReportFormat UpdateReportFormat(string id, ReportFormat reportFormat)
        {
            throw new NotImplementedException();
        }
    }
}

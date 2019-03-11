/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using RestSrvr.Attributes;
using SanteDB.Core.Model.RISI;
using SanteDB.Core.Model.Warehouse;
using SanteDB.Rest.RISI;
using System;
using System.IO;

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

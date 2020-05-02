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
 * Date: 2020-2-26
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Rules
{
    /// <summary>
    /// Represents a JNI interface for interacting with javascript business rules
    /// </summary>
    public class DisconnectedGatewayJni
    {

        private Tracer m_tracer = Tracer.GetTracer(typeof(DisconnectedGatewayJni));
        private Regex date_regex = new Regex(@"(\d{4})-(\d{2})-(\d{2})");
        // View model serializer
        private JsonViewModelSerializer m_modelSerializer = new JsonViewModelSerializer();

        /// <summary>
        /// Gets the current facility which is related to this DCG instance
        /// </summary>
        public ExpandoObject GetFacilities()
        {
            var facilities = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().Facilities
                .Select(o => ApplicationContext.Current.GetService<IRepositoryService<Place>>().Get(o));

            return this.ToViewModel(new Bundle() { Item = facilities.OfType<IdentifiedData>().ToList() });
            
        }

        /// <summary>
        /// Simplifies an HDSI object
        /// </summary>
        private ExpandoObject ToViewModel(IdentifiedData data)
        {
            try
            {
                // Serialize to a view model serializer
                using (MemoryStream ms = new MemoryStream())
                {
                    using (TextWriter tw = new StreamWriter(ms, Encoding.UTF8, 2048, true))
                        this.m_modelSerializer.Serialize(tw, data);
                    ms.Seek(0, SeekOrigin.Begin);

                    // Parse
                    JsonSerializer jsz = new JsonSerializer() { DateFormatHandling = DateFormatHandling.IsoDateFormat, TypeNameHandling = TypeNameHandling.None };
                    using (JsonReader reader = new JsonTextReader(new StreamReader(ms)))
                    {
                        var retVal = jsz.Deserialize<Newtonsoft.Json.Linq.JObject>(reader);
                        return this.ConvertToJint(retVal);
                        ///return retVal;
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error converting to view model: {0}", e);
                throw;
            }
        }

        /// <summary>
        /// Convert to Jint object expando
        /// </summary>
        private ExpandoObject ConvertToJint(JObject source)
        {
            try
            {
                var retVal = new ExpandoObject();

                if (source == null)
                    return retVal;

                var expandoDic = (IDictionary<String, Object>)retVal;
                foreach (var kv in source)
                {
                    if (kv.Value is JObject)
                        expandoDic.Add(kv.Key, ConvertToJint(kv.Value as JObject));
                    else if (kv.Value is JArray)
                        expandoDic.Add(kv.Key == "item" ? "$item" : kv.Key, (kv.Value as JArray).Select(o => o is JValue ? (o as JValue).Value : ConvertToJint(o as JObject)).ToArray());
                    else
                    {
                        object jValue = (kv.Value as JValue).Value;
                        if (jValue is String && date_regex.IsMatch(jValue.ToString())) // Correct dates
                        {
                            var dValue = date_regex.Match(jValue.ToString());
                            expandoDic.Add(kv.Key, new DateTime(Int32.Parse(dValue.Groups[1].Value), Int32.Parse(dValue.Groups[2].Value), Int32.Parse(dValue.Groups[3].Value)));
                        }
                        else
                            expandoDic.Add(kv.Key, (kv.Value as JValue).Value);
                    }
                }
                return retVal;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error converting to JINT : {0}", e);
                throw;
            }
        }

    }
}

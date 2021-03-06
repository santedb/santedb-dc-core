﻿/*
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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RestSrvr;
using RestSrvr.Attributes;
using RestSrvr.Message;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Applets.ViewModel.Description;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Json.Formatter;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Behaviors;
using SanteDB.DisconnectedClient;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Ags.Formatter
{
    /// <summary>
    /// Represents the AGS dispatch formatter
    /// </summary>
    /// <remarks>This formatter is common to many different services, therefore the formatter in SanteDB.Rest.Common is not used</remarks>
    public abstract class AgsMessageDispatchFormatter : IDispatchMessageFormatter
    {

        // Formatters
        private static Dictionary<Type, AgsMessageDispatchFormatter> m_formatters = new Dictionary<Type, AgsMessageDispatchFormatter>();

        /// <summary>
        /// Create a formatter for the specified contract type
        /// </summary>
        public static AgsMessageDispatchFormatter CreateFormatter(Type contractType)
        {
            try
            {
                AgsMessageDispatchFormatter retVal = null;
                if (!m_formatters.TryGetValue(contractType, out retVal))
                {
                    lock (m_formatters)
                    {
                        if (!m_formatters.ContainsKey(contractType))
                        {
                            var typeFormatter = typeof(AgsDispatchFormatter<>).MakeGenericType(contractType);
                            retVal = Activator.CreateInstance(typeFormatter) as AgsMessageDispatchFormatter;
                            m_formatters.Add(contractType, retVal);
                        }
                    }
                }
                return retVal;
            }
            catch(Exception e)
            {
                Tracer.GetTracer(typeof(AgsMessageDispatchFormatter)).TraceError("Error creating dispatch formatter : {0}", e);
                throw new Exception("Could not create AGS Dispatch Formatter: {0}", e);
            }
        }

        /// <summary>
        /// Implemented below
        /// </summary>
        public abstract void DeserializeRequest(EndpointOperation operation, RestRequestMessage request, object[] parameters);

        /// <summary>
        /// Implemented below
        /// </summary>
        public abstract void SerializeResponse(RestResponseMessage response, object[] parameters, object result);
    }

    /// <summary>
    /// Represents a dispatch message formatter which uses the JSON.NET serialization
    /// </summary>
    public class AgsDispatchFormatter<TContract> : AgsMessageDispatchFormatter
    {

        // Trace source
        private Tracer m_traceSource = Tracer.GetTracer(typeof(AgsDispatchFormatter<TContract>));
        // Known types
        private static Type[] s_knownTypes = typeof(TContract).GetCustomAttributes<ServiceKnownResourceAttribute>().Select(t => t.Type).ToArray();
       
        // Default view model
        private static ViewModelDescription m_defaultViewModel = null;

        // Static ctor
        static AgsDispatchFormatter()
        {
            m_defaultViewModel = ViewModelDescription.Load(Assembly.Load("SanteDB.Rest.Common").GetManifestResourceStream("SanteDB.Rest.Common.Resources.ViewModel.xml"));
            var tracer = Tracer.GetTracer(typeof(AgsDispatchFormatter<TContract>));
            foreach (var t in s_knownTypes)
                ModelSerializationBinder.RegisterModelType(t);
            tracer.TraceInfo("Will generate serializer for {0}", typeof(TContract).FullName);

        }

        /// <summary>
        /// Deserialize the request
        /// </summary>
        public override void DeserializeRequest(EndpointOperation operation, RestRequestMessage request, object[] parameters)
        {

            try
            {
#if DEBUG
                this.m_traceSource.TraceInfo("Received request from: {0}", RestOperationContext.Current.IncomingRequest.RemoteEndPoint);
#endif

                var httpRequest = RestOperationContext.Current.IncomingRequest;
                string contentType = httpRequest.Headers["Content-Type"]?.ToLowerInvariant();

                for (int pNumber = 0; pNumber < parameters.Length; pNumber++)
                {
                    var parm = operation.Description.InvokeMethod.GetParameters()[pNumber];

                    // Simple parameter
                    if (parameters[pNumber] != null)
                    {
                        continue; // dispatcher already populated
                    }
                    // Use XML Serializer
                    else if (contentType?.StartsWith("application/xml") == true)
                    {
                        using (XmlReader bodyReader = XmlReader.Create(request.Body))
                        {
                            while (bodyReader.NodeType != XmlNodeType.Element)
                                bodyReader.Read();

                            Type eType = s_knownTypes.FirstOrDefault(o => o.GetCustomAttribute<XmlRootAttribute>()?.ElementName == bodyReader.LocalName &&
                                o.GetCustomAttribute<XmlRootAttribute>()?.Namespace == bodyReader.NamespaceURI);
                            var serializer = XmlModelSerializerFactory.Current.CreateSerializer(eType);
                            parameters[pNumber] = serializer.Deserialize(request.Body);
                        }

                    }
                    else if (contentType?.StartsWith("application/json+sdb-viewmodel") == true && typeof(IdentifiedData).IsAssignableFrom(parm.ParameterType))
                    {
                        var viewModel = httpRequest.Headers["X-SanteDB-ViewModel"] ?? httpRequest.QueryString["_viewModel"];

                        // Create the view model serializer
                        var viewModelSerializer = new JsonViewModelSerializer();
                        viewModelSerializer.LoadSerializerAssembly(typeof(ActExtensionViewModelSerializer).Assembly);

                        if (!String.IsNullOrEmpty(viewModel))
                        {
                            var viewModelDescription = ApplicationContext.Current.GetService<IAppletManagerService>()?.Applets.GetViewModelDescription(viewModel);
                            viewModelSerializer.ViewModel = viewModelDescription;
                        }
                        else
                        {
                            viewModelSerializer.ViewModel = m_defaultViewModel;
                        }

                        using (var sr = new StreamReader(request.Body))
                            parameters[pNumber] = viewModelSerializer.DeSerialize(sr, parm.ParameterType);
                    }
                    else if (contentType?.StartsWith("application/json") == true)
                    {

                        using (var sr = new StreamReader(request.Body))
                        using (var jsr = new JsonTextReader(sr))
                        {
                            JsonSerializer jsz = new JsonSerializer()
                            {
                                SerializationBinder = new ModelSerializationBinder(parm.ParameterType),
                                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                                TypeNameHandling = TypeNameHandling.All
                            };
                            jsz.Converters.Add(new StringEnumConverter());

                            // Can the binder resolve the type from the message?
                            parameters[pNumber] = jsz.Deserialize(jsr, parm.ParameterType);
                        }
                    }
                    else if (contentType == "application/octet-stream")
                    {
                        parameters[pNumber] = request.Body;
                    }
                    else if (contentType == "application/x-www-form-urlencoded")
                    {
                        NameValueCollection nvc = new NameValueCollection();
                        using (var sr = new StreamReader(request.Body))
                        {
                            var ptext = sr.ReadToEnd();
                            var parms = ptext.Split('&');
                            foreach (var p in parms)
                            {
                                var parmData = p.Split('=');
                                nvc.Add(WebUtility.UrlDecode(parmData[0]), WebUtility.UrlDecode(parmData[1]));
                            }
                        }
                        parameters[pNumber] = nvc;
                    }
                    else if (contentType != null)// TODO: Binaries
                        throw new InvalidOperationException("Invalid request format");
                }
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceError("Error de-serializing dispatch request: {0}", e.ToString());
                throw;
            }

        }

        /// <summary>
        /// Serialize the reply
        /// </summary>
        public override void SerializeResponse(RestResponseMessage response, object[] parameters, object result)
        {
            try
            {
                // Outbound control
                var httpRequest = RestOperationContext.Current.IncomingRequest;
                string accepts = httpRequest.Headers["Accept"]?.ToLower(),
                    contentType = httpRequest.Headers["Content-Type"]?.ToLower();

                // The request was in JSON or the accept is JSON
                if (result is Stream) // TODO: This is messy, clean it up
                {
                    contentType = "application/octet-stream";
                    response.Body = result as Stream;
                }
                else if (accepts?.StartsWith("application/json+sdb-viewmodel") == true &&
                    typeof(IdentifiedData).IsAssignableFrom(result?.GetType()))
                {
                    var viewModel = httpRequest.Headers["X-SanteDB-ViewModel"] ?? httpRequest.QueryString["_viewModel"];

                    // Create the view model serializer
                    var viewModelSerializer = new JsonViewModelSerializer();
                    viewModelSerializer.LoadSerializerAssembly(typeof(ActExtensionViewModelSerializer).Assembly);

                    if (!String.IsNullOrEmpty(viewModel))
                    {
                        var viewModelDescription = ApplicationContext.Current.GetService<IAppletManagerService>()?.Applets.GetViewModelDescription(viewModel);
                        viewModelSerializer.ViewModel = viewModelDescription;
                    }
                    else
                    {
                        viewModelSerializer.ViewModel = m_defaultViewModel;
                    }

                    using (var tms = new MemoryStream())
                    using (StreamWriter sw = new StreamWriter(tms, Encoding.UTF8))
                    using (JsonWriter jsw = new JsonTextWriter(sw))
                    {
                        viewModelSerializer.Serialize(jsw, result as IdentifiedData);
                        jsw.Flush();
                        sw.Flush();
                        response.Body = new MemoryStream(tms.ToArray());
                    }

                    contentType = "application/json+sdb-viewModel";
                }
                // The request was in XML and/or the accept is JSON
                else if ((accepts?.StartsWith("application/xml") == true ||
                    contentType?.StartsWith("application/xml") == true) &&
                    result?.GetType().GetCustomAttribute<XmlTypeAttribute>() != null)
                {
                    XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(result.GetType());
                    MemoryStream ms = new MemoryStream();
                    xsz.Serialize(ms, result);
                    contentType = "application/xml";
                    ms.Seek(0, SeekOrigin.Begin);
                    response.Body = ms;
                }
                else if (result is XmlSchema)
                {
                    MemoryStream ms = new MemoryStream();
                    (result as XmlSchema).Write(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    contentType = "text/xml";
                    response.Body = ms;
                }
                else 
                {
                    // Prepare the serializer
                    JsonSerializer jsz = new JsonSerializer();
                    jsz.Converters.Add(new StringEnumConverter());

                    // Write json data
                    using (MemoryStream ms = new MemoryStream())
                    using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))
                    using (JsonWriter jsw = new JsonTextWriter(sw))
                    {
                        jsz.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                        jsz.NullValueHandling = NullValueHandling.Ignore;
                        jsz.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                        jsz.TypeNameHandling = TypeNameHandling.Auto;
                        jsz.Converters.Add(new StringEnumConverter());
                        jsz.Serialize(jsw, result);
                        jsw.Flush();
                        sw.Flush();
                        response.Body = new MemoryStream(ms.ToArray());

                    }

                    // Prepare reply for the WCF pipeline
                    contentType = "application/json";
                }

                RestOperationContext.Current.OutgoingResponse.ContentType = RestOperationContext.Current.OutgoingResponse.ContentType ?? contentType;
                AuthenticationContext.Current = null;
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceError("Error Serializing Dispatch Reply: {0}", e.ToString());
                new AgsErrorHandlerServiceBehavior().ProvideFault(e, response);
            }
        }
    }
}

﻿/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Rest.Common.Fault;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.DisconnectedClient.Http
{
    /// <summary>
    /// Represents an android enabled rest client
    /// </summary>
    public class RestClient : RestClientBase
    {
        /// <summary>
        /// Identified data
        /// </summary>
        [XmlType(nameof(ErrorResult), Namespace = "http://santedb.org/hdsi")]
        [XmlRoot(nameof(ErrorResult), Namespace = "http://santedb.org/hdsi")]
        public class ErrorResult : IdentifiedData
        {

            /// <summary>
            /// Gets the date this was modified
            /// </summary>
            public override DateTimeOffset ModifiedOn
            {
                get
                {
                    return DateTimeOffset.Now;
                }
            }

            /// <summary>
            /// Represents an error result
            /// </summary>
            public ErrorResult()
            {
                this.Details = new List<ResultDetail>();
            }

            /// <summary>
            /// Gets or sets the details of the result
            /// </summary>
            [XmlElement("detail")]
            public List<ResultDetail> Details { get; set; }

            /// <summary>
            /// String representation
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return String.Join("\r\n", Details.Select(o => $">> {o.Type} : {o.Text}"));
            }
        }

        /// <summary>
        /// Gets or sets the detail type
        /// </summary>
        [XmlType(nameof(DetailType), Namespace = "http://santedb.org/hdsi")]
        public enum DetailType
        {
            [XmlEnum("I")]
            Information,
            [XmlEnum("W")]
            Warning,
            [XmlEnum("E")]
            Error
        }

        /// <summary>
        /// A single result detail
        /// </summary
        [XmlType(nameof(ResultDetail), Namespace = "http://santedb.org/hdsi")]
        public class ResultDetail
        {
            /// <summary>
            /// Default ctor
            /// </summary>
            public ResultDetail()
            { }

            /// <summary>
            /// Creates a new result detail
            /// </summary>
            public ResultDetail(DetailType type, string text)
            {
                this.Type = type;
                this.Text = text;
            }
            /// <summary>
            /// Gets or sets the type of the error
            /// </summary>
            [XmlAttribute("type")]
            public DetailType Type { get; set; }

            /// <summary>
            /// Gets or sets the text of the error
            /// </summary>
            [XmlText]
            public string Text { get; set; }
        }

        // Config section
        private ServiceClientConfigurationSection m_configurationSection;

        // Tracer
        private Tracer m_tracer;

        // Trusted certificates
        private static HashSet<String> m_trustedCerts = new HashSet<String>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Http.RestClient"/> class.
        /// </summary>
        public RestClient() : base()
        {
            this.m_tracer = Tracer.GetTracer(this.GetType());
            this.m_configurationSection = ApplicationContext.Current?.Configuration?.GetSection<ServiceClientConfigurationSection>();

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Http.RestClient"/> class.
        /// </summary>
        public RestClient(ServiceClientDescription config) : base(config)
        {
            this.m_configurationSection = ApplicationContext.Current?.Configuration?.GetSection<ServiceClientConfigurationSection>();
            this.m_tracer = Tracer.GetTracer(this.GetType());
            // Find the specified certificate
            if (config.Binding.Security?.ClientCertificate != null)
            {
                this.ClientCertificates = new X509Certificate2Collection();
                var cert = X509CertificateUtils.FindCertificate(config.Binding.Security.ClientCertificate.FindType,
                              config.Binding.Security.ClientCertificate.StoreLocation,
                              config.Binding.Security.ClientCertificate.StoreName,
                              config.Binding.Security.ClientCertificate.FindValue);
                if (cert == null)
                    throw new SecurityException(String.Format("Certificate described by {0} could not be found in {1}/{2}",
                        config.Binding.Security.ClientCertificate.FindValue,
                        config.Binding.Security.ClientCertificate.StoreLocation,
                        config.Binding.Security.ClientCertificate.StoreName));
                this.ClientCertificates.Add(cert);
            }


        }

        /// <summary>
        /// Create HTTP Request object
        /// </summary>
        protected override WebRequest CreateHttpRequest(string url, NameValueCollection query)
        {
            var retVal = (HttpWebRequest)base.CreateHttpRequest(url, query);

            // Certs?
            if (this.ClientCertificates != null)
                retVal.ClientCertificates.AddRange(this.ClientCertificates);

            // Proxy?
            if (!String.IsNullOrEmpty(this.m_configurationSection?.ProxyAddress))
                retVal.Proxy = new WebProxy(this.m_configurationSection.ProxyAddress);

            try
            {
                retVal.ServerCertificateValidationCallback = this.RemoteCertificateValidation;
            }
            catch
            {
                this.m_tracer.TraceWarning("Cannot assign certificate validtion callback, will set servicepointmanager");
                ServicePointManager.ServerCertificateValidationCallback = this.RemoteCertificateValidation;
            }

            // Set appropriate header
            if (this.Description.Binding.Optimize)
            {
                switch ((this.Description.Binding as ServiceClientBinding)?.OptimizationMethod)
                {
                    case OptimizationMethod.Lzma:
                        retVal.Headers[HttpRequestHeader.AcceptEncoding] = "lzma,bzip2,gzip,deflate";
                        break;
                    case OptimizationMethod.Bzip2:
                        retVal.Headers[HttpRequestHeader.AcceptEncoding] = "bzip2,gzip,deflate";
                        break;
                    case OptimizationMethod.Gzip:
                        retVal.Headers[HttpRequestHeader.AcceptEncoding] = "gzip,deflate";
                        break;
                    case OptimizationMethod.Deflate:
                        retVal.Headers[HttpRequestHeader.AcceptEncoding] = "deflate";
                        break;
                    case OptimizationMethod.None:
                        retVal.Headers[HttpRequestHeader.AcceptEncoding] = null;
                        break;

                }
            }

#if DEBUG
            this.m_tracer.TraceInfo("Created request to {0}", retVal.RequestUri);
#endif
            // Set user agent
            var asm = Assembly.GetEntryAssembly() ?? typeof(RestClient).Assembly;
            retVal.UserAgent = String.Format("{0} {1} ({2})", asm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title, asm.GetName().Version, asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            return retVal;
        }

        /// <summary>
        /// Remote certificate validation errors
        /// </summary>
        private bool RemoteCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            lock (m_trustedCerts)
            {
                if (m_trustedCerts.Contains(certificate.Subject))
                    return true;
                else if (ApplicationContext.Current.Confirm(String.Format(Strings.locale_certificateValidation, certificate.Subject, certificate.Issuer)))
                {
                    m_trustedCerts.Add(certificate.Subject);
                    return true;
                }
                else
                    return false;
            }
        }

        /// <summary>
        /// Invokes the specified method against the url provided
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="url">URL.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="body">Body.</param>
        /// <param name="query">Query.</param>
        /// <typeparam name="TBody">The 1st type parameter.</typeparam>
        /// <typeparam name="TResult">The 2nd type parameter.</typeparam>
        protected override TResult InvokeInternal<TBody, TResult>(string method, string url, string contentType, WebHeaderCollection additionalHeaders, out WebHeaderCollection responseHeaders, TBody body, NameValueCollection query)
        {

            if (String.IsNullOrEmpty(method))
                throw new ArgumentNullException(nameof(method));
            //if (String.IsNullOrEmpty(url))
            //    throw new ArgumentNullException(nameof(url));

            // Three times:
            // 1. With provided credential
            // 2. With challenge
            // 3. With challenge again
            for (int i = 0; i < 2; i++)
            {
                // Credentials provided ?
                HttpWebRequest requestObj = this.CreateHttpRequest(url, query) as HttpWebRequest;
                if (!String.IsNullOrEmpty(contentType))
                    requestObj.ContentType = contentType;
                requestObj.Method = method;

                // Additional headers
                if (additionalHeaders != null)
                    foreach (var hdr in additionalHeaders.AllKeys)
                    {
                        if (hdr == "If-Modified-Since")
                            requestObj.IfModifiedSince = DateTime.Parse(additionalHeaders[hdr]);
                        else
                            requestObj.Headers.Add(hdr, additionalHeaders[hdr]);
                    }

#if PERFMON
                Stopwatch sw = new Stopwatch();
                sw.Start();
#endif
                // Get request object
               
                // Body was provided?
                try
                {

                    // Try assigned credentials
                    IBodySerializer serializer = null;
                    if (body != null)
                    {
                        // GET Stream, 
                        Stream requestStream = null;
                        try
                        {
                            var cancelTokenSource = new CancellationTokenSource();
                            CancellationToken ct = cancelTokenSource.Token;

                            // Get request object
                            var requestTask = Task.Run(requestObj.GetRequestStreamAsync, ct);

                            try
                            {
                                if (!requestTask.Wait(this.Description.Endpoint[0].Timeout))
                                {
                                    cancelTokenSource.Cancel();
                                    requestObj.Abort();
                                    throw new TimeoutException();
                                }

                                requestStream = requestTask.Result;
                            }
                            catch (AggregateException e)
                            {
                                requestObj.Abort();
                                throw e.InnerExceptions.First();
                            }
                            finally
                            {
                                try { requestTask.Dispose(); }
                                catch { }
                            }
                           

                            if (contentType == null && typeof(TResult) != typeof(Object))
                                throw new ArgumentNullException(nameof(contentType));

                            serializer = this.Description.Binding.ContentTypeMapper.GetSerializer(contentType, typeof(TBody));
                            // Serialize and compress with deflate
                            using (MemoryStream ms = new MemoryStream())
                            {
                                if (this.Description.Binding.Optimize)
                                {
                                    switch ((this.Description.Binding as ServiceClientBinding)?.OptimizationMethod)
                                    {
                                        case OptimizationMethod.Lzma:
                                            requestObj.Headers.Add("Content-Encoding", "lzma");
                                            using (var df = new LZipStream(new NonDisposingStream(requestStream), CompressionMode.Compress))
                                                serializer.Serialize(df, body);
                                            break;
                                        case OptimizationMethod.Bzip2:
                                            requestObj.Headers.Add("Content-Encoding", "bzip2");
                                            using (var df = new BZip2Stream(new NonDisposingStream(requestStream), CompressionMode.Compress, false))
                                                serializer.Serialize(df, body);
                                            break;
                                        case OptimizationMethod.Gzip:
                                            requestObj.Headers.Add("Content-Encoding", "gzip");
                                            using (var df = new GZipStream(new NonDisposingStream(requestStream), CompressionMode.Compress))
                                                serializer.Serialize(df, body);
                                            break;
                                        case OptimizationMethod.Deflate:
                                            requestObj.Headers.Add("Content-Encoding", "deflate");
                                            using (var df = new DeflateStream(new NonDisposingStream(requestStream), CompressionMode.Compress))
                                                serializer.Serialize(df, body);
                                            break;
                                        case OptimizationMethod.None:
                                        default:
                                            serializer.Serialize(ms, body);
                                            break;
                                    }
                                }
                                else
                                    serializer.Serialize(ms, body);

                                // Trace
                                if (this.Description.Trace)
                                    this.m_tracer.TraceVerbose("HTTP >> {0}", Convert.ToBase64String(ms.ToArray()));

                                using (var nms = new MemoryStream(ms.ToArray()))
                                    nms.CopyTo(requestStream);

                            }
                        }
                        finally
                        {
                            if (requestStream != null)
                                requestStream.Dispose();
                        }
                    }

                    // Response
                    HttpWebResponse response = null;
                    try
                    {

                        var cancelTokenSource = new CancellationTokenSource();
                        CancellationToken ct = cancelTokenSource.Token;

                        var responseTask = Task.Run(requestObj.GetResponseAsync, ct);
                        try
                        {
                            if (!responseTask.Wait(this.Description.Endpoint[0].Timeout))
                            {
                                requestObj.Abort();
                                cancelTokenSource.Cancel();
                                throw new TimeoutException();
                            }
                            response = (HttpWebResponse)responseTask.Result;
                        }
                        catch (AggregateException e)
                        {
                            try
                            {
                                requestObj.Abort();
                                cancelTokenSource.Cancel();
                            }
                            catch { }
                            throw e.InnerExceptions.First();
                        }
                        finally
                        {
                            try { responseTask.Dispose(); }
                            catch { }
                        }

#if PERFMON
                        sw.Stop();
                        ApplicationContext.Current.PerformanceLog(nameof(RestClient), "InvokeInternal", $"{nameof(TBody)}-RCV", sw.Elapsed);
                        sw.Reset();
                        sw.Start();
#endif

                        responseHeaders = response.Headers;
                        var validationResult = this.ValidateResponse(response);
                        if (validationResult != ServiceClientErrorType.Valid)
                        {
                            this.m_tracer.TraceError("Response failed validation : {0}", validationResult);
                            throw new WebException(Strings.err_response_failed_validation, null, WebExceptionStatus.Success, response);
                        }

                        // No content - does the result want a pointer maybe?
                        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.Continue)
                        {
                            return default(TResult);
                        }
                        else
                        {
                            // De-serialize
                            var responseContentType = response.ContentType;
                            if (String.IsNullOrEmpty(responseContentType))
                                return default(TResult);

                            if (responseContentType.Contains(";"))
                                responseContentType = responseContentType.Substring(0, responseContentType.IndexOf(";"));

                            if (response.StatusCode == HttpStatusCode.NotModified)
                                return default(TResult);

                            serializer = this.Description.Binding.ContentTypeMapper.GetSerializer(responseContentType, typeof(TResult));
                            
                            TResult retVal = default(TResult);
                            // Compression?
                            using (MemoryStream ms = new MemoryStream())
                            {
                                if (this.Description.Trace)
                                    this.m_tracer.TraceVerbose("Received response {0} : {1} bytes", response.ContentType, response.ContentLength);

                                response.GetResponseStream().CopyTo(ms);

#if PERFMON
                                sw.Stop();
                                ApplicationContext.Current.PerformanceLog(nameof(RestClient), "InvokeInternal", $"{nameof(TBody)}-INT", sw.Elapsed);
                                sw.Reset();
                                sw.Start();
#endif

                                ms.Seek(0, SeekOrigin.Begin);

                                // Trace
                                if (this.Description.Trace)
                                    this.m_tracer.TraceVerbose("HTTP << {0}", Convert.ToBase64String(ms.ToArray()));

                                switch (response.Headers[HttpResponseHeader.ContentEncoding])
                                {
                                    case "deflate":
                                        using (DeflateStream df = new DeflateStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                            retVal = (TResult)serializer.DeSerialize(df);
                                        break;
                                    case "gzip":
                                        using (GZipStream df = new GZipStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                            retVal = (TResult)serializer.DeSerialize(df);
                                        break;
                                    case "bzip2":
                                        using (var bzs = new BZip2Stream(new NonDisposingStream(ms), CompressionMode.Decompress, false))
                                            retVal = (TResult)serializer.DeSerialize(bzs);
                                        break;
                                    case "lzma":
                                        using (var lzmas = new LZipStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                            retVal = (TResult)serializer.DeSerialize(lzmas);
                                        break;
                                    default:
                                        retVal = (TResult)serializer.DeSerialize(ms);
                                        break;
                                }
                                //retVal = (TResult)serializer.DeSerialize(ms);
                            }

#if PERFMON
                            sw.Stop();
                            ApplicationContext.Current.PerformanceLog(nameof(RestClient), "InvokeInternal", $"{nameof(TBody)}-RET", sw.Elapsed);
                            sw.Reset();
                            sw.Start();
#endif

                            return retVal;
                        }
                    }
                    finally
                    {
                        if (response != null)
                        {
                            response.Close();
                            response.Dispose();
                        }
                        //responseTask.Dispose();
                    }
                }
                
                catch (TimeoutException e)
                {
                    this.m_tracer.TraceError("Request timed out:{0}", e.Message);
                    throw;
                }
                catch(WebException e) when (e.Response is HttpWebResponse errorResponse && errorResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    this.m_tracer.TraceInfo("Server indicates not modified {0} {1} : {2}", method, url, e.Message);
                    responseHeaders = errorResponse?.Headers;
                    return default(TResult);
                }
                catch(WebException e) when (e.Response is HttpWebResponse errorResponse && e.Status == WebExceptionStatus.ProtocolError)
                {
                    this.m_tracer.TraceError("Error executing {0} {1} : {2}", method, url, e.Message);
                    // Deserialize
                    object errorResult = default(ErrorResult);

                    var responseContentType = errorResponse.ContentType;
                    if (responseContentType.Contains(";"))
                        responseContentType = responseContentType.Substring(0, responseContentType.IndexOf(";"));

                    var ms = new MemoryStream(); // copy response to memory
                    errorResponse.GetResponseStream().CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);


                    try
                    {
                        var serializer = this.Description.Binding.ContentTypeMapper.GetSerializer(responseContentType, typeof(TResult));

                        switch (errorResponse.Headers[HttpResponseHeader.ContentEncoding])
                        {
                            case "deflate":
                                using (DeflateStream df = new DeflateStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                    errorResult = serializer.DeSerialize(df);
                                break;
                            case "gzip":
                                using (GZipStream df = new GZipStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                    errorResult = serializer.DeSerialize(df);
                                break;
                            case "bzip2":
                                using (var bzs = new BZip2Stream(new NonDisposingStream(ms), CompressionMode.Decompress, false))
                                    errorResult = serializer.DeSerialize(bzs);
                                break;
                            case "lzma":
                                using (var lzmas = new LZipStream(new NonDisposingStream(ms), CompressionMode.Decompress))
                                    errorResult = serializer.DeSerialize(lzmas);
                                break;
                            default:
                                errorResult = serializer.DeSerialize(ms);
                                break;
                        }
                    }
                    catch
                    {
                        errorResult = new RestServiceFault(e);
                    }

                    Exception exception = null;
                    if (errorResult is TResult)
                        exception = new RestClientException<TResult>((TResult)errorResult, e, e.Status, e.Response);
                    else
                        exception = new RestClientException<RestServiceFault>((RestServiceFault)errorResult, e, e.Status, e.Response);

                    switch (errorResponse.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized: // Validate the response
                            if (this.ValidateResponse(errorResponse) != ServiceClientErrorType.Valid)
                                throw exception;
                            break;
                        case HttpStatusCode.NotModified:
                            responseHeaders = errorResponse?.Headers;
                            return default(TResult);
                        case (HttpStatusCode)422:
                            throw exception;

                        default:
                            throw exception;
                    }
                }
                catch (WebException e) when (e.Status == WebExceptionStatus.Timeout)
                {
                    this.m_tracer.TraceError("Error executing {0} {1} : {2}", method, url, e.Message);
                    throw new TimeoutException($"Timeout executing REST operation {method} {url}", e);
                }
                catch(WebException e) when (e.Status == WebExceptionStatus.ConnectFailure)
                {
                    this.m_tracer.TraceError("Error executing {0} {1} : {2}", method, url, e.Message);
                    if ((e.InnerException as SocketException)?.SocketErrorCode == SocketError.TimedOut)
                        throw new TimeoutException();
                    else
                        throw;
                }
                catch(WebException e)
                {
                    this.m_tracer.TraceError("Error executing {0} {1} : {2}", method, url, e.Message);
                    throw;
                }
                catch (InvalidOperationException e)
                {
                    this.m_tracer.TraceError("Invalid Operation: {0}", e.Message);
                    throw;
                }

            }

            responseHeaders = new WebHeaderCollection();
            return default(TResult);
        }

        /// <summary>
        /// Gets or sets the client certificate
        /// </summary>
        /// <value>The client certificate.</value>
        public X509Certificate2Collection ClientCertificates
        {
            get;
            set;
        }
    }
}


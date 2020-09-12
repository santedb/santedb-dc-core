using Newtonsoft.Json;
using SanteDB.Core;
using SanteDB.Core.Api.Services;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;
using ZXing.CoreCompat.System.Drawing;
using ZXing.QrCode;

namespace SanteDB.DisconnectedClient.UI.Services
{
    /// <summary>
    /// Barcode generator service that generates a QR code
    /// </summary>
    public class QrBarcodeGenerator : IBarcodeProviderService
    {

        /// <summary>
        /// JWS format regex
        /// </summary>
        private readonly Regex m_jwsFormat = new Regex(@"^(.*?)\.(.*?)\.(.*?)$");

        /// <summary>
        /// Get the name of the service
        /// </summary>
        public string ServiceName => "QR Code Barcode Generator";

        /// <summary>
        /// Generate the specified barcode from the information provided
        /// </summary>
        public Stream Generate<TEntity>(IEnumerable<IdentifierBase<TEntity>> identifers) where TEntity : VersionedEntityData<TEntity>, new()
        {
            if (!identifers.Any())
                return null; // Cannot generate
            try
            {
                var signatureService = ApplicationServiceContext.Current.GetService<IDataSigningService>();
                var sourceKey = identifers.First().SourceEntityKey.Value.ToString();

                // Append the header to the token
                // Append authorities to identifiers
                var header = new
                {
                    alg = signatureService.GetSignatureAlgorithm(),
                    typ = $"x-santedb+{identifers.First().LoadProperty<TEntity>("SourceEntity")?.Type}",
                    key = sourceKey.Substring(0, 8)
                };

                // From RFC7515
                var hdrString= Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(header)).Base64UrlEncode();
                StringBuilder identityToken = new StringBuilder($"{hdrString}.");

                var domainList = new
                {
                    id = identifers.Select(o => new
                    {
                        value = o.Value,
                        ns = o.LoadProperty<AssigningAuthority>("Authority").DomainName
                    }).ToList()
                };

                // From RFC7515
                var bodyString = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(domainList)).Base64UrlEncode();
                identityToken.Append(bodyString);

                // Sign the data
                // From RFC7515
                var tokenData = Encoding.UTF8.GetBytes(identityToken.ToString());
                var signature = signatureService.SignData(tokenData, sourceKey);
                identityToken.AppendFormat(".{0}", signature.Base64UrlEncode());

                // Now generate the token
                var writer = new BarcodeWriter()
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions()
                    {
                        Width = 300,
                        Height = 300,
                        PureBarcode = true,
                        Margin = 1
                        
                    }
                };
                
                using (var bmp = writer.Write(identityToken.ToString()))
                {
                    var retVal = new MemoryStream();
                    bmp.Save(retVal, ImageFormat.Png);
                    retVal.Seek(0, SeekOrigin.Begin);
                    return retVal;
                }
            }
            catch(Exception e)
            {
                throw new Exception("Cannot generate QR code for specified identifier list", e);
            }
        }

        /// <summary>
        /// Let's resolve the specified resource
        /// </summary>
        public IdentifiedData ResolveResource(string data, bool validate = true)
        {
            try
            {
                var match = this.m_jwsFormat.Match(data);
                if (!match.Success)
                    throw new DetectedIssueException(new DetectedIssue(DetectedIssuePriorityType.Error, "jws.invalid", "Invalid Barcode Format", DetectedIssueKeys.InvalidDataIssue));

                // Get the parts of the header
                byte[] headerBytes = match.Groups[1].Value.ParseBase64UrlEncode(),
                    bodyBytes = match.Groups[2].Value.ParseBase64UrlEncode(),
                    signatureBytes = match.Groups[3].Value.ParseBase64UrlEncode();

                // Now lets parse the JSON objects
                dynamic header = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(headerBytes));
                dynamic body = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bodyBytes));

                // Now validate the payload
                if (!header.typ.ToString().StartsWith("x-santedb+"))
                    throw new DetectedIssueException(new DetectedIssue(DetectedIssuePriorityType.Error, "jws.invalid.type", "Invalid Barcode Type", DetectedIssueKeys.InvalidDataIssue));
                var type = new ModelSerializationBinder().BindToType(null, header.typ.ToString().Substring(10));
                var algorithm = header.alg.ToString();
                var keyId = header.key.ToString();

                // Validate the signature service can service the algorithm
                var signatureService = ApplicationServiceContext.Current.GetService<IDataSigningService>();
                if (signatureService.GetSignatureAlgorithm(keyId) != algorithm)
                    throw new DetectedIssueException(new DetectedIssue(DetectedIssuePriorityType.Error, "jws.algorithm", "Algorithm Not Supported", DetectedIssueKeys.SecurityIssue));

                // Attempt to locate the record
                var domainQuery = new NameValueCollection();
                foreach (var id in body.id)
                    domainQuery.Add($"identifier[{id.ns.ToString()}].value", id.value.ToString());

                IdentifiedData result = null;
                if (typeof(Entity).IsAssignableFrom(type)) {
                    var query = QueryExpressionParser.BuildLinqExpression<Entity>(domainQuery);
                    result = ApplicationServiceContext.Current.GetService<IRepositoryService<Entity>>().Find(query, 0, 2, out int tr).SingleOrDefault();
                }
                else if(typeof(Act).IsAssignableFrom(type))
                {
                    var query = QueryExpressionParser.BuildLinqExpression<Entity>(domainQuery);
                    result = ApplicationServiceContext.Current.GetService<IRepositoryService<Entity>>().Find(query, 0, 2, out int tr).SingleOrDefault();
                }

                // Validate the signature
                if (validate && !signatureService.Verify(Encoding.UTF8.GetBytes($"{match.Groups[1].Value}.{match.Groups[2].Value}"), signatureBytes, result.Key.Value.ToString()))
                    throw new DetectedIssueException(new DetectedIssue(DetectedIssuePriorityType.Error, "jws.verification", "Barcode Tampered", DetectedIssueKeys.SecurityIssue));

                // Return the result
                return result;

            }
            catch(DetectedIssueException) { throw; }
            catch (Exception e)
            {
                throw new Exception("Cannot resolve QR code", e);
            }
        }
    }
}

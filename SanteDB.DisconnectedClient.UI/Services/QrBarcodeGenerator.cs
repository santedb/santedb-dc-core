using Newtonsoft.Json;
using SanteDB.Core.Api.Services;
using SanteDB.Core.Model;
using SanteDB.Core.Model.DataTypes;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.CoreCompat.System.Drawing;
using ZXing.QrCode;

namespace SanteDB.DisconnectedClient.UI.Services
{
    /// <summary>
    /// Barcode generator service that generates a QR code
    /// </summary>
    public class QrBarcodeGenerator : IBarcodeGeneratorService
    {
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
                var sourceKey = identifers.First().SourceEntityKey.Value.ToByteArray();
                var dataSigner = new HMACSHA256(sourceKey);
                // Append the header to the token
                // Append authorities to identifiers
                var header = new
                {
                    alg = "HS256",
                    typ = $"x-santedb+{identifers.First().LoadProperty<TEntity>("SourceEntity")?.Type}",
                    key = sourceKey.Take(4).ToArray().ToHexString()
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
                var signature = dataSigner.ComputeHash(tokenData);
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
    }
}

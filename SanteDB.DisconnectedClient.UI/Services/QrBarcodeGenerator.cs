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
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace SanteDB.DisconnectedClient.UI.Services
{
    /// <summary>
    /// Barcode generator service that generates a QR code
    /// </summary>
    [ServiceProvider("QR Code Barcode Generator", Dependencies = new Type[] { typeof(IResourcePointerService) })]
    public class QrBarcodeGenerator : IBarcodeProviderService
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

                var pointerService = ApplicationServiceContext.Current.GetService<IResourcePointerService>();
                if (pointerService == null)
                    throw new InvalidOperationException("Cannot find resource pointer generator");

                // Generate the pointer
                var identityToken = pointerService.GeneratePointer(identifers);
                return this.Generate(identityToken.ToString());
            }
            catch(Exception e)
            {
                throw new Exception("Cannot generate QR code for specified identifier list", e);
            }
        }

        /// <summary>
        /// Generate a barcode from raw data
        /// </summary>
        public Stream Generate(string rawData)
        {

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

            using (var bmp = writer.Write(rawData))
            {
                var retVal = new MemoryStream();
                bmp.Save(retVal, ImageFormat.Png);
                retVal.Seek(0, SeekOrigin.Begin);
                return retVal;
            }
        }
    }
}

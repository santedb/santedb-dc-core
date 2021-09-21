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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SharpCompress.Compressors.Deflate;
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace SanteDB.DisconnectedClient.PeerToPeer
{

    /// <summary>
    /// Represents the peer transfer payload
    /// </summary>
    /// <remarks>
    /// <para>The P2P services in SanteDB are usually sent via limited bandwidth services like IrDA, Bluetooth or NFC. This necessitates the use of a binary based protocol</para>
    /// <para>The protocol for the transfer is as follows:</para>
    /// <list type="bullet">
    ///     <item>A 5 byte header = SDP2P</item>
    ///     <item>A 1 byte version</item>
    ///     <item>Encoding flags which indicate the compression of the stream</item>
    ///     <item>A 4 byte usigned integer with length of the payload</item>
    ///     <item>The JOSE payload data itself</item>
    /// </list>
    /// </remarks>
    public class PeerTransferPayload
    {
        /// <summary>
        /// JWS format regex
        /// </summary>
        private static readonly Regex s_jwsFormat = new Regex(@"^(.*?)\.(.*?)\.(.*?)$");

        /// <summary>
        /// Header indicates the magic header for the payload
        /// </summary>
        public static readonly byte[] MAGIC_HEADER = new byte[] { (byte)'S', (byte)'D', (byte)'P', (byte)'2', (byte)'P' };

        /// <summary>
        /// Version identifier
        /// </summary>
        public const byte VERSION_ID = 1;

        /// <summary>
        /// Creates a new peer transfer payload
        /// </summary>
        /// <param name="payload">The object being sent</param>
        /// <param name="encoding">The payload transfer flags</param>
        public PeerTransferPayload(IdentifiedData payload, PeerTransferEncodingFlags encoding)
        {
            this.Encoding = encoding;
            this.Payload = payload;
        }

        private PeerTransferPayload()
        {
        }

        /// <summary>
        /// Encrypt the payload
        /// </summary>
        public PeerTransferEncodingFlags Encoding { get; private set; }

        /// <summary>
        /// Gets the payload of the object
        /// </summary>
        public IdentifiedData Payload { get; private set; }

        /// <summary>
        /// Write this payload to the specified stream
        /// </summary>
        public void Write(Stream s, IDataSigningService signingProvider)
        {

            // Header
            byte[] header = new byte[7];
            Array.Copy(MAGIC_HEADER, header, 5);
            header[5] = VERSION_ID;
            header[6] = (byte)this.Encoding;
            s.Write(header, 0, 7);

            var jwsHeader = new
            {
                alg = signingProvider.GetSignatureAlgorithm(),
                typ = $"x-santedb+{this.Payload.GetType().GetSerializationName()}",
                key = "p2pdefault"
            };

            var hdrString = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(header)).Base64UrlEncode();
            StringBuilder payloadToken = new StringBuilder($"{hdrString}.");

            // Now we serialize the payload
            var payloadData = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this.Payload)).Base64UrlEncode();
            payloadToken.Append(payloadData);
            var tokenData = System.Text.Encoding.UTF8.GetBytes(payloadToken.ToString());
            var signature = signingProvider.SignData(tokenData, "p2pdefault");
            payloadToken.AppendFormat(".{0}", signature.Base64UrlEncode());

            // Now we write to the stream
            if (this.Encoding.HasFlag(PeerTransferEncodingFlags.Compressed))
                s = new GZipStream(s, SharpCompress.Compressors.CompressionMode.Compress);
            using (var sw = new StreamWriter(s))
                sw.Write(payloadData.ToString());
        }


        /// <summary>
        /// Read from the stream
        /// </summary>
        public static PeerTransferPayload Read(Stream s, IDataSigningService signingProvider, bool validateSignature)
        {
            byte[] hdr = new byte[7];
            s.Read(hdr, 0, 7);
            if (!hdr.Take(5).SequenceEqual(MAGIC_HEADER))
                throw new FormatException("Invalid payload");
            else if (hdr[5] >= VERSION_ID)
                throw new InvalidOperationException($"Payload version {hdr[5]} is greater than supported version of {VERSION_ID}");

            var retVal = new PeerTransferPayload();
            retVal.Encoding = (PeerTransferEncodingFlags)hdr[6];

            // Read the rest of the stream 
            if (retVal.Encoding.HasFlag(PeerTransferEncodingFlags.Compressed))
                s = new GZipStream(s, SharpCompress.Compressors.CompressionMode.Decompress);

            using (var sr = new StreamReader(s))
            {
                var data = sr.ReadToEnd();
                // Read the JWS header
                var match = s_jwsFormat.Match(data);
                if (!match.Success)
                    throw new FormatException("Payload must be in JWS format");

                // Get the parts of the header
                byte[] headerBytes = match.Groups[1].Value.ParseBase64UrlEncode(),
                    bodyBytes = match.Groups[2].Value.ParseBase64UrlEncode(),
                    signatureBytes = match.Groups[3].Value.ParseBase64UrlEncode();

                // Now lets parse the JSON objects
                dynamic header = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(headerBytes));
                dynamic body = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(bodyBytes));

                // Now validate the payload
                if (!header.typ.ToString().StartsWith("x-santedb+"))
                    throw new InvalidOperationException("Cannot determine type of data");

                var type = new ModelSerializationBinder().BindToType(null, header.typ.ToString().Substring(10));
                var algorithm = header.alg.ToString();
                String keyId = header.key.ToString();

                // Validate the signature if we have the key
                if (validateSignature)
                {

                    // We have the key?
                    if (!signingProvider.GetKeys().Any(k => k == keyId))
                    {
                        throw new InvalidOperationException("Cannot find appropriate validation key");
                    }

                    if (signingProvider.GetSignatureAlgorithm(keyId) != algorithm)
                        throw new InvalidOperationException("Invalid signature algorithm");

                    var payload = System.Text.Encoding.UTF8.GetBytes($"{match.Groups[1].Value}.{match.Groups[2].Value}");

                    if (!signingProvider.Verify(payload, signatureBytes, keyId))
                        throw new SecurityException("Cannot verify authenticity of the specified data payload");
                }

                retVal.Payload = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(bodyBytes), type);
                // Return the result
                return retVal;
            }
        }
    }
}

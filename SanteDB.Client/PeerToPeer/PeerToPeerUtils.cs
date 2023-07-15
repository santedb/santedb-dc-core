using DocumentFormat.OpenXml.Wordprocessing;
using Hl7.Fhir.Utility;
using SanteDB.Core;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Security.Signing;
using SharpCompress;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZXing;

namespace SanteDB.Client.PeerToPeer
{
    /// <summary>
    /// Represents the peer transfer payload formatter
    /// </summary>
    /// <remarks>
    /// <para>The P2P services in SanteDB are usually sent via limited bandwidth services like IrDA, Bluetooth or NFC. This necessitates the use of a binary based protocol</para>
    /// <para>The protocol for the transfer is as follows:</para>
    /// <list type="bullet">
    ///     <item>A 5 byte header = SDP2P</item>
    ///     <item>A 1 byte version</item>
    ///     <item>Encoding flags which indicate the compression of the stream</item>
    ///     <item>16-byte message UUID</item>
    ///     <item>16-byte origin peer ID</item>
    ///     <item>16-byte destination peer ID</item>
    ///     <item>8-byte origin timestamp in UNIX format</item>

    ///     <item>4-byte usigned integer with structure identifer length</item>
    ///     <item>structure identifier</item>
    ///     <item>4-byte usigned integer with length of the trigger-event name</item>
    ///     <item>The trigger event name</item>
    ///     <item>4-byte unsigned integer with length of the payload</item>
    ///     <item>Payload Data</item>
    ///     <item>4-byte unsigned size of signature</item>
    ///     <item>The signature</item>
    /// </list>
    /// </remarks>
    public static class PeerToPeerUtils
    {

        private static readonly Dictionary<string, Type> m_payloadHandlers = new Dictionary<string, Type>();
        private static readonly IDataSigningService m_dataSigningService = ApplicationServiceContext.Current.GetService<IDataSigningService>();
        private static readonly Dictionary<string, IPeerToPeerTriggerHandler> m_triggerHandlers = new Dictionary<string, IPeerToPeerTriggerHandler>();
        /// <summary>
        /// Static ctor
        /// </summary>
        static PeerToPeerUtils()
        {
            m_payloadHandlers = AppDomain.CurrentDomain.GetAllTypes()
                .Where(o => typeof(IPeerToPeerMessagePayload).IsAssignableFrom(o) && !o.IsAbstract && !o.IsInterface)
                .Select(o => o.CreateInjected() as IPeerToPeerMessagePayload)
                .ToDictionaryIgnoringDuplicates(o => o.StructureIdentifier, o => o.GetType());

            AppDomain.CurrentDomain.GetAllTypes()
                .Where(o => typeof(IPeerToPeerTriggerHandler).IsAssignableFrom(o) && !o.IsAbstract && !o.IsInterface)
                .Select(o => o.CreateInjected() as IPeerToPeerTriggerHandler)
                .ForEach(tg => {
                    tg.Triggers.ForEach(t =>
                    {
                        if (!m_triggerHandlers.ContainsKey(t))
                        {
                            m_triggerHandlers.Add(t, tg);
                        }
                    });
                });
        }

        /// <summary>
        /// Header indicates the magic header for the payload
        /// </summary>
        public static readonly byte[] MAGIC_HEADER = new byte[] { (byte)'S', (byte)'D', (byte)'P', (byte)'2', (byte)'P' };

        /// <summary>
        /// Version identifier
        /// </summary>
        public const byte VERSION_ID = 3;

        /// <summary>
        /// Read data in C format
        /// </summary>
        private static string ReadString(this Stream stream)
        {
            byte[] tBuffer = new byte[sizeof(int)];
            stream.Read(tBuffer, 0, sizeof(int));
            var szString = BitConverter.ToInt32(tBuffer, 0);
            tBuffer = new byte[szString];
            stream.Read(tBuffer, 0, szString);
            return Encoding.UTF8.GetString(tBuffer);
        }

        /// <summary>
        /// Read a date
        /// </summary>
        private static DateTimeOffset ReadDate(this Stream stream)
        {
            byte[] tBuffer = new byte[sizeof(long)];
            stream.Read(tBuffer, 0, sizeof(long));
            return DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt64(tBuffer, 0)).ToLocalTime();
        }

        /// <summary>
        /// Read a guid
        /// </summary>
        private static Guid ReadGuid(this Stream stream)
        {
            byte[] tBuffer = new byte[16];
            stream.Read(tBuffer, 0, 16);
            return new Guid(tBuffer);
        }

        /// <summary>
        /// Read a dynamically sized array
        /// </summary>
        private static byte[] ReadArray(this Stream stream)
        {
            byte[] tBuffer = new byte[sizeof(int)];
            stream.Read(tBuffer, 0, sizeof(int));
            var szArray = BitConverter.ToInt32(tBuffer, 0);
            tBuffer = new byte[szArray];
            stream.Read(tBuffer, 0, szArray);
            return tBuffer;
        }

        /// <summary>
        /// Write data in C format
        /// </summary>
        private static void Write<TData>(this Stream stream, TData dataToWrite)
        {
            switch (dataToWrite)
            {
                case Guid guidToWrite:
                    stream.Write(guidToWrite.ToByteArray(), 0, 16);
                    break;
                case string stringToWrite:
                    var byteData = Encoding.UTF8.GetBytes(stringToWrite);
                    stream.Write(BitConverter.GetBytes(byteData.Length), 0, sizeof(int));
                    stream.Write(byteData, 0, byteData.Length);
                    break;
                case DateTimeOffset dtoToWrite:
                    stream.Write(BitConverter.GetBytes(dtoToWrite.ToUniversalTime().ToUnixTimeSeconds()), 0, sizeof(long));
                    break;
                case byte[] arrayToWrite:
                    stream.Write(BitConverter.GetBytes(arrayToWrite.Length), 0, sizeof(int));
                    stream.Write(arrayToWrite, 0, arrayToWrite.Length);
                    break;
            }
        }

        /// <summary>
        /// Encode the message onto the stream
        /// </summary>
        /// <param name="message">The message to be written</param>
        /// <param name="stream">The target stream</param>
        /// <param name="encodingOptions">Encoding options</param>
        /// <param name="oneTimeKey">The one-time key to use for encrypting the payload (note: the recipient and sender should have established an incremental key derivation algorithm when they paired)</param>
        public static void WriteMessage(IPeerToPeerMessage message,
            Stream stream,
            PeerTransferEncodingFlags encodingOptions,
            byte[] oneTimeKey)
        {

            byte[] header = new byte[7];
            Array.Copy(MAGIC_HEADER, header, MAGIC_HEADER.Length);
            header[5] = VERSION_ID;
            header[6] = (byte)encodingOptions;
            
            stream.Write(header, 0, header.Length);

            if (encodingOptions.HasFlag(PeerTransferEncodingFlags.Compressed))
            {
                stream = new GZipStream(NonDisposingStream.Create(stream), SharpCompress.Compressors.CompressionMode.Compress);
            }

            stream.Write(message.Uuid);
            stream.Write(message.OriginNode);
            stream.Write(message.DestinationNode);
            stream.Write(message.OriginationTime);
            stream.Write(message.TriggerEvent);
            stream.Write(message.Payload.StructureIdentifier);
            var payload = message.Payload.Serialize();
            stream.Write(payload);
            var signature = m_dataSigningService.SignData(payload, SignatureSettings.HS256(oneTimeKey));
            stream.Write(signature);

            // Finalize 
            if(stream is GZipStream)
            {
                stream.Dispose();
            }

        }

        /// <summary>
        /// Read the payload from an input stream
        /// </summary>
        /// <param name="inputStream">The input stream to read from</param>
        /// <param name="validateSignature">True if the signature should be validated</param>
        /// <param name="expectedOneTimeKey">The one-time key used for validing the signature</param>
        /// <returns>The parsed peer to peer message</returns>
        public static TPeerMessage ReadMessage<TPeerMessage>(Stream inputStream, byte[] expectedOneTimeKey, bool validateSignature) where TPeerMessage : IPeerToPeerMessage, new()
        {
            // Read the header and asert the magic
            var header = new byte[7];
            inputStream.Read(header, 0, 7);
            if (!header.Take(5).SequenceEqual(MAGIC_HEADER))
            {
                throw new System.FormatException();
            }
            else if (header[5] > VERSION_ID)
            {
                throw new NotSupportedException(string.Format(ErrorMessages.ASSERTION_MISMATCH, VERSION_ID, header[5]));
            }

            if (header[6] == (byte)PeerTransferEncodingFlags.Compressed)
            {
                inputStream = new GZipStream(NonDisposingStream.Create(inputStream), SharpCompress.Compressors.CompressionMode.Decompress);
            }

            TPeerMessage peerMessage = new TPeerMessage();
            peerMessage.Uuid = inputStream.ReadGuid();
            peerMessage.OriginNode = inputStream.ReadGuid();
            peerMessage.DestinationNode = inputStream.ReadGuid();
            peerMessage.OriginationTime = inputStream.ReadDate();
            peerMessage.TriggerEvent = inputStream.ReadString();
            var structureIdentifier = inputStream.ReadString();
            var payloadData = inputStream.ReadArray();
            var signature = inputStream.ReadArray();

            // Validate the signature
            if (validateSignature && !m_dataSigningService.Verify(payloadData, signature, SignatureSettings.HS256(expectedOneTimeKey)))
            {
                throw new InvalidDataException(ErrorMessages.SIGNATURE_VALIDATION_ERROR);
            }

            // Find the content-type handler
            if (!m_payloadHandlers.TryGetValue(structureIdentifier, out var handlerType))
            {
                throw new NotSupportedException(string.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, structureIdentifier));
            }
            peerMessage.Payload = Activator.CreateInstance(handlerType) as IPeerToPeerMessagePayload;
            peerMessage.Payload.Populate(payloadData);

            if(inputStream is GZipStream)
            {
                inputStream.Dispose();
            }

            return peerMessage;
        }

        /// <summary>
        /// Execute a trigger for the specified message
        /// </summary>
        /// <param name="request">The request message</param>
        /// <returns>The response or result of the message</returns>
        public static IPeerToPeerMessage ExecuteTrigger(IPeerToPeerMessage request)
        {
            if(m_triggerHandlers.TryGetValue(request.TriggerEvent, out var handler))
            {
                return handler.Execute(request);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}

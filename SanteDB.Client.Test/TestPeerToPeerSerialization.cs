using NUnit.Framework;
using SanteDB.Client.Bluetooth.PeerToPeer;
using SanteDB.Client.PeerToPeer;
using SanteDB.Client.PeerToPeer.Messages;
using SanteDB.Core.TestFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Test
{
    [TestFixture]
    public class TestPeerToPeerSerialization
    {

        [OneTimeSetUp]
        public void Initialize()
        {
            // Force load of the DLL
            TestApplicationContext.TestAssembly = typeof(TestPeerToPeerSerialization).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);

        }

        /// <summary>
        /// Test that the formatting of a Peer-To-Peer binary payload can be parsed and read
        /// </summary>
        [Test]
        public void TestCanSerializeAndParse()
        {

            var sourceMessage = new BluetoothPeerToPeerMessage()
            {
                DestinationNode = Guid.NewGuid(),
                OriginationTime = DateTimeOffset.Now,
                OriginNode = Guid.NewGuid(),
                Payload = new PeerAcknowledgmentPayload(PeerToPeerAcknowledgementCode.Ok, Core.BusinessRules.DetectedIssuePriorityType.Information, "This is a test!!!"),
                TriggerEvent = PeerToPeerConstants.AckTriggerEvent,
                Uuid = Guid.NewGuid()
            };

            using (var ms = new MemoryStream()) {
                PeerToPeerUtils.WriteMessage(sourceMessage, ms, PeerTransferEncodingFlags.Compressed, new byte[] { 2, 4, 6, 8, 10 });
                Assert.Greater(ms.Length, 0);

                // Read the message
                ms.Seek(0, SeekOrigin.Begin);
                var parsed = PeerToPeerUtils.ReadMessage<BluetoothPeerToPeerMessage>(ms, new byte[] { 2, 4, 6, 8, 10 }, true);
                Assert.AreEqual(sourceMessage.TriggerEvent, parsed.TriggerEvent);
                Assert.AreEqual(sourceMessage.OriginNode, parsed.OriginNode);
                Assert.AreEqual(sourceMessage.Uuid, parsed.Uuid);
                Assert.AreEqual(sourceMessage.DestinationNode, parsed.DestinationNode);
                Assert.IsInstanceOf<PeerAcknowledgmentPayload>(sourceMessage.Payload);
            }
        }

    }
}

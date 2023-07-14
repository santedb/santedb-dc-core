using Newtonsoft.Json;
using SanteDB.Core.BusinessRules;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SanteDB.Client.PeerToPeer.Messages
{

    /// <summary>
    /// Peer to peer ack code
    /// </summary>
    public enum PeerToPeerAcknowledgementCode
    {
        Ok = 0x200,
        Forbidden = 0x401,
        Error = 0x500
    }

    /// <summary>
    /// Represents a peer acknowledgement
    /// </summary>
    public class PeerAcknowledgmentPayload : IPeerToPeerMessagePayload
    {

        public PeerAcknowledgmentPayload()
        {
            
        }
        /// <summary>
        /// Acknowledgement
        /// </summary>
        public PeerAcknowledgmentPayload(PeerToPeerAcknowledgementCode outcome, DetectedIssuePriorityType messagePriority, String message )
        {
            this.OutcomeStatus = outcome;
            this.Details = new List<DetectedIssue>()
            {
                new DetectedIssue(messagePriority, "p2p.ack", message, Guid.Empty)
            };
        }

        /// <summary>
        /// Gets the content type
        /// </summary>
        [JsonIgnore]
        public string ContentType => "application/json";

        /// <summary>
        /// Gets the structure identifier
        /// </summary>
        [JsonIgnore]
        public string StructureIdentifier => PeerToPeerConstants.AckMessageStructureId;

        /// <summary>
        /// Gets or sets the outcome
        /// </summary>
        [JsonProperty("outcome")]
        public PeerToPeerAcknowledgementCode OutcomeStatus { get; set; }

        /// <summary>
        /// Gets the details
        /// </summary>
        [JsonProperty("details")]
        public List<DetectedIssue> Details { get; set; }

        /// <inheritdoc/>
        public void Populate(byte[] payloadData) => JsonConvert.PopulateObject(Encoding.UTF8.GetString(payloadData), this);

        /// <inheritdoc/>
        public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
    }
}

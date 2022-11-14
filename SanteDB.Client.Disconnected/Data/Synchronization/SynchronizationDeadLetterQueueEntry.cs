using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    internal class SynchronizationDeadLetterQueueEntry : ISynchronizationDeadLetterQueueEntry
    {
        public string OriginalQueue { get; set; }
        public byte[] TagData { get; set; }
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string Type { get; set; }
        public string DataFileKey { get; set; }
        public IdentifiedData Data { get; set; }
        public SynchronizationQueueEntryOperation Operation { get; set; }
        public bool IsRetry { get; set; }
    }
}

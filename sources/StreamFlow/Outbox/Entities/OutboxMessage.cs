using System;

namespace StreamFlow.Outbox.Entities
{
    public class OutboxMessage
    {
        public Guid OutboxMessageId { get; set; }

        public string TargetAddress { get; set; }

        public byte[] Body { get; set; }
        public byte[]? Options { get; set; }

        public DateTime Created { get; set; }
        public DateTime Scheduled { get; set; }
        public DateTime? Published { get; set; }
    }
}

using System;
using Jasper.Messaging.Runtime;

namespace Jasper.Messaging.Tracking
{
    public class EnvelopeRecord
    {
        public Envelope Envelope { get; }
        public long SessionTime { get; }
        public Exception Exception { get; }
        public EventType EventType { get; }

        public EnvelopeRecord(EventType eventType, Envelope envelope, long sessionTime, Exception exception)
        {
            Envelope = envelope;
            SessionTime = sessionTime;
            Exception = exception;
            EventType = eventType;
        }

        public bool IsComplete { get; internal set; }
        public string ServiceName { get; set; }

        public override string ToString()
        {
            return $"Id: {Envelope.Id}, {nameof(SessionTime)}: {SessionTime}, {nameof(EventType)}: {EventType}, MessageType: {Envelope.MessageType}";
        }


    }
}

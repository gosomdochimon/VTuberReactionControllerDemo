using System;
using System.Collections.Generic;

namespace VTuberReactionControllerDemo.Controller
{
    [Serializable]
    public sealed class ReactionEvent
    {
        public string Type;
        public string User;
        public int Amount;
        public string Message;
    }

    internal sealed class ReactionInputGuard
    {
        private readonly object syncRoot = new object();
        private readonly Queue<DateTime> acceptedTimestamps = new Queue<DateTime>();
        private readonly int maxPayloadBytes;
        private readonly int maxPendingMessages;
        private readonly int maxMessagesPerSecond;
        private int pendingMessageCount;

        public ReactionInputGuard(int maxPayloadBytes, int maxPendingMessages, int maxMessagesPerSecond)
        {
            this.maxPayloadBytes = Math.Max(1, maxPayloadBytes);
            this.maxPendingMessages = Math.Max(1, maxPendingMessages);
            this.maxMessagesPerSecond = Math.Max(1, maxMessagesPerSecond);
        }

        public bool TryAccept(int payloadBytes, out string failureReason)
        {
            DateTime now = DateTime.UtcNow;

            if (payloadBytes > maxPayloadBytes)
            {
                failureReason = $"payload exceeds {maxPayloadBytes} bytes";
                return false;
            }

            lock (syncRoot)
            {
                while (acceptedTimestamps.Count > 0
                    && now - acceptedTimestamps.Peek() >= TimeSpan.FromSeconds(1))
                {
                    acceptedTimestamps.Dequeue();
                }

                if (acceptedTimestamps.Count >= maxMessagesPerSecond)
                {
                    failureReason = $"rate limit exceeded ({maxMessagesPerSecond} messages/second)";
                    return false;
                }

                if (pendingMessageCount >= maxPendingMessages)
                {
                    failureReason = $"pending queue limit reached ({maxPendingMessages} messages)";
                    return false;
                }

                acceptedTimestamps.Enqueue(now);
                pendingMessageCount++;
                failureReason = null;
                return true;
            }
        }

        public void MarkDequeued()
        {
            lock (syncRoot)
            {
                if (pendingMessageCount > 0)
                {
                    pendingMessageCount--;
                }
            }
        }

        public void Reset()
        {
            lock (syncRoot)
            {
                acceptedTimestamps.Clear();
                pendingMessageCount = 0;
            }
        }
    }
}

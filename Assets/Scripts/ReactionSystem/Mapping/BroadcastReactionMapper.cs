using System;

namespace VTuberReactionControllerDemo.Controller
{
    public static class BroadcastReactionMapper
    {
        private const int GiftAmountThreshold = 1000;
        private const int SparkAmountThreshold = 10000;

        public static bool LooksLikeBroadcastEvent(ExternalBroadcastEvent broadcastEvent)
        {
            if (broadcastEvent == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(FirstNonEmpty(
                    broadcastEvent.Source,
                    broadcastEvent.source,
                    broadcastEvent.Platform,
                    broadcastEvent.platform,
                    broadcastEvent.EventKind,
                    broadcastEvent.eventKind,
                    broadcastEvent.Kind,
                    broadcastEvent.kind,
                    broadcastEvent.BroadcastType,
                    broadcastEvent.broadcastType,
                    broadcastEvent.BroadcastEventType,
                    broadcastEvent.broadcastEventType,
                    broadcastEvent.ReactionType,
                    broadcastEvent.reactionType,
                    broadcastEvent.SelectedReaction,
                    broadcastEvent.selectedReaction,
                    broadcastEvent.Reaction,
                    broadcastEvent.reaction,
                    broadcastEvent.ReactionName,
                    broadcastEvent.reactionName)))
            {
                return true;
            }

            return IsBroadcastKind(GetKind(broadcastEvent));
        }

        public static bool TryMap(ExternalBroadcastEvent broadcastEvent, out ReactionEvent reactionEvent, out string failureReason)
        {
            reactionEvent = null;
            failureReason = null;

            if (broadcastEvent == null)
            {
                failureReason = "broadcast payload could not be parsed";
                return false;
            }

            string kind = GetKind(broadcastEvent);
            string selectedReaction = GetSelectedReaction(broadcastEvent);
            string user = GetUser(broadcastEvent);
            int amount = GetAmount(broadcastEvent);
            string message = GetMessage(broadcastEvent);
            string reactionType = ResolveReactionType(selectedReaction, kind, amount, message);

            if (string.IsNullOrWhiteSpace(reactionType))
            {
                failureReason = $"no broadcast mapping rule matched selectedReaction='{selectedReaction}', kind='{kind}', amount={amount}, message='{message}'";
                return false;
            }

            reactionEvent = new ReactionEvent
            {
                Type = reactionType,
                User = user,
                Amount = amount,
                Message = string.IsNullOrWhiteSpace(message)
                    ? $"Broadcast {kind} event mapped to {reactionType}."
                    : message.Trim()
            };

            return true;
        }

        private static string ResolveReactionType(string selectedReaction, string kind, int amount, string message)
        {
            if (TryNormalizeReactionType(selectedReaction, out string explicitReactionType))
            {
                return explicitReactionType;
            }

            if (TryNormalizeReactionType(kind, out string kindReactionType))
            {
                return kindReactionType;
            }

            string normalizedMessage = message ?? string.Empty;

            if (ContainsOrdinalIgnoreCase(normalizedMessage, "!throw"))
            {
                return "throw";
            }

            if (IsDonationKind(kind))
            {
                if (amount >= SparkAmountThreshold)
                {
                    return "spark";
                }

                if (amount >= GiftAmountThreshold)
                {
                    return "gift";
                }
            }

            if (ContainsOrdinalIgnoreCase(normalizedMessage, "ㅋㅋ"))
            {
                return "cheer";
            }

            if (ContainsOrdinalIgnoreCase(normalizedMessage, "놀람")
                || ContainsOrdinalIgnoreCase(normalizedMessage, "!"))
            {
                return "shock";
            }

            return string.Empty;
        }

        private static string GetKind(ExternalBroadcastEvent broadcastEvent)
        {
            return FirstNonEmpty(
                broadcastEvent.EventKind,
                broadcastEvent.eventKind,
                broadcastEvent.Kind,
                broadcastEvent.kind,
                broadcastEvent.BroadcastType,
                broadcastEvent.broadcastType,
                broadcastEvent.BroadcastEventType,
                broadcastEvent.broadcastEventType,
                broadcastEvent.EventType,
                broadcastEvent.eventType,
                broadcastEvent.Type,
                broadcastEvent.type);
        }

        private static string GetSelectedReaction(ExternalBroadcastEvent broadcastEvent)
        {
            return FirstNonEmpty(
                broadcastEvent.ReactionType,
                broadcastEvent.reactionType,
                broadcastEvent.SelectedReaction,
                broadcastEvent.selectedReaction,
                broadcastEvent.Reaction,
                broadcastEvent.reaction,
                broadcastEvent.ReactionName,
                broadcastEvent.reactionName,
                broadcastEvent.Type,
                broadcastEvent.type);
        }

        private static string GetUser(ExternalBroadcastEvent broadcastEvent)
        {
            return FirstNonEmpty(
                broadcastEvent.User,
                broadcastEvent.user,
                broadcastEvent.DisplayName,
                broadcastEvent.displayName,
                broadcastEvent.Author,
                broadcastEvent.author,
                broadcastEvent.Sender,
                broadcastEvent.sender,
                broadcastEvent.Nickname,
                broadcastEvent.nickname);
        }

        private static int GetAmount(ExternalBroadcastEvent broadcastEvent)
        {
            if (broadcastEvent.Amount != 0)
            {
                return broadcastEvent.Amount;
            }

            if (broadcastEvent.amount != 0)
            {
                return broadcastEvent.amount;
            }

            if (broadcastEvent.DonationAmount != 0)
            {
                return broadcastEvent.DonationAmount;
            }

            if (broadcastEvent.donationAmount != 0)
            {
                return broadcastEvent.donationAmount;
            }

            if (broadcastEvent.Value != 0)
            {
                return broadcastEvent.Value;
            }

            return broadcastEvent.value;
        }

        private static string GetMessage(ExternalBroadcastEvent broadcastEvent)
        {
            return FirstNonEmpty(
                broadcastEvent.Message,
                broadcastEvent.message,
                broadcastEvent.Comment,
                broadcastEvent.comment,
                broadcastEvent.Text,
                broadcastEvent.text,
                broadcastEvent.Content,
                broadcastEvent.content);
        }

        private static bool IsBroadcastKind(string kind)
        {
            string normalized = Normalize(kind);
            return string.Equals(normalized, "chat", StringComparison.Ordinal)
                || string.Equals(normalized, "message", StringComparison.Ordinal)
                || string.Equals(normalized, "donation", StringComparison.Ordinal)
                || string.Equals(normalized, "tip", StringComparison.Ordinal)
                || string.Equals(normalized, "superchat", StringComparison.Ordinal)
                || string.Equals(normalized, "support", StringComparison.Ordinal)
                || string.Equals(normalized, "command", StringComparison.Ordinal)
                || string.Equals(normalized, "follow", StringComparison.Ordinal)
                || string.Equals(normalized, "subscription", StringComparison.Ordinal)
                || string.Equals(normalized, "subscribe", StringComparison.Ordinal);
        }

        private static bool IsDonationKind(string kind)
        {
            string normalized = Normalize(kind);
            return string.Equals(normalized, "donation", StringComparison.Ordinal)
                || string.Equals(normalized, "tip", StringComparison.Ordinal)
                || string.Equals(normalized, "superchat", StringComparison.Ordinal)
                || string.Equals(normalized, "support", StringComparison.Ordinal)
                || string.Equals(normalized, "gift", StringComparison.Ordinal);
        }

        private static bool TryNormalizeReactionType(string rawValue, out string reactionType)
        {
            string normalized = Normalize(rawValue);
            switch (normalized)
            {
                case "cheer":
                case "응원":
                case "환호":
                    reactionType = "cheer";
                    return true;

                case "shock":
                case "놀람":
                case "깜짝":
                    reactionType = "shock";
                    return true;

                case "gift":
                case "선물":
                    reactionType = "gift";
                    return true;

                case "throw":
                case "던지기":
                case "투척":
                    reactionType = "throw";
                    return true;

                case "spark":
                case "스파크":
                    reactionType = "spark";
                    return true;

                case "bonk":
                    reactionType = "bonk";
                    return true;

                default:
                    reactionType = string.Empty;
                    return false;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

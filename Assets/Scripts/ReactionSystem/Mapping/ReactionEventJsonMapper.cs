using System;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    /// <summary>
    /// 외부 JSON payload를 프로젝트 내부 표준 입력인 ReactionEvent로 변환합니다.
    /// UDP와 WebSocket이 같은 변환 규칙을 쓰도록 이 클래스로 책임을 분리합니다.
    /// </summary>
    public static class ReactionEventJsonMapper
    {
        public static bool TryFromJson(string json, out ReactionEvent reactionEvent, out string failureReason)
        {
            reactionEvent = null;
            failureReason = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                failureReason = "payload is empty";
                return false;
            }

            try
            {
                ReactionEventPayload payload = JsonUtility.FromJson<ReactionEventPayload>(json);
                ExternalBroadcastEvent broadcastEvent = JsonUtility.FromJson<ExternalBroadcastEvent>(json);
                if (payload == null && broadcastEvent == null)
                {
                    failureReason = "payload could not be parsed";
                    return false;
                }

                if (BroadcastReactionMapper.LooksLikeBroadcastEvent(broadcastEvent))
                {
                    if (BroadcastReactionMapper.TryMap(broadcastEvent, out reactionEvent, out failureReason))
                    {
                        return true;
                    }

                    failureReason = $"broadcast payload has no matching reaction: {failureReason}";
                    return false;
                }

                if (TryCreateStandardReaction(payload, out reactionEvent, out failureReason))
                {
                    return true;
                }

                if (BroadcastReactionMapper.TryMap(broadcastEvent, out reactionEvent, out failureReason))
                {
                    return true;
                }

                failureReason = $"payload has no reaction type and no broadcast mapping: {failureReason}";
                return false;
            }
            catch (ArgumentException exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static bool TryCreateStandardReaction(
            ReactionEventPayload payload,
            out ReactionEvent reactionEvent,
            out string failureReason)
        {
            reactionEvent = null;
            failureReason = null;

            if (payload == null)
            {
                failureReason = "payload could not be parsed";
                return false;
            }

            string type = FirstNonEmpty(
                payload.Type,
                payload.type,
                payload.EventType,
                payload.eventType,
                payload.ReactionType,
                payload.reactionType);

            if (string.IsNullOrWhiteSpace(type))
            {
                failureReason = "payload has no reaction type";
                return false;
            }

            reactionEvent = new ReactionEvent
            {
                Type = type.Trim(),
                User = FirstNonEmpty(payload.User, payload.user, payload.DisplayName, payload.displayName),
                Amount = payload.Amount != 0 ? payload.Amount : payload.amount,
                Message = FirstNonEmpty(payload.Message, payload.message, payload.Comment, payload.comment)
            };

            return true;
        }

        [Serializable]
        private sealed class ReactionEventPayload
        {
            public string Type;
            public string type;
            public string EventType;
            public string eventType;
            public string ReactionType;
            public string reactionType;

            public string User;
            public string user;
            public string DisplayName;
            public string displayName;

            public int Amount;
            public int amount;

            public string Message;
            public string message;
            public string Comment;
            public string comment;
        }
    }
}

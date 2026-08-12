using System;

namespace VTuberReactionControllerDemo.Controller
{
    [Serializable]
    public sealed class ExternalBroadcastEvent
    {
        public string Source;
        public string source;
        public string Platform;
        public string platform;

        public string Type;
        public string type;
        public string EventType;
        public string eventType;
        public string EventKind;
        public string eventKind;
        public string Kind;
        public string kind;
        public string BroadcastType;
        public string broadcastType;
        public string BroadcastEventType;
        public string broadcastEventType;
        public string ReactionType;
        public string reactionType;
        public string SelectedReaction;
        public string selectedReaction;
        public string Reaction;
        public string reaction;
        public string ReactionName;
        public string reactionName;

        public string User;
        public string user;
        public string DisplayName;
        public string displayName;
        public string Author;
        public string author;
        public string Sender;
        public string sender;
        public string Nickname;
        public string nickname;

        public int Amount;
        public int amount;
        public int DonationAmount;
        public int donationAmount;
        public int Value;
        public int value;

        public string Message;
        public string message;
        public string Comment;
        public string comment;
        public string Text;
        public string text;
        public string Content;
        public string content;
    }
}

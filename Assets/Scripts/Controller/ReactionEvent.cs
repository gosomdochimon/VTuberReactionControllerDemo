using System;

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
}

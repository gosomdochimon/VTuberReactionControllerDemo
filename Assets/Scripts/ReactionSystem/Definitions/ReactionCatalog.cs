using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionCatalog : MonoBehaviour
    {
        [SerializeField] private ReactionMenuItem[] items = CreateDefaultItems();

        public event Action<IReadOnlyList<ReactionMenuItem>> ItemsChanged;

        public IReadOnlyList<ReactionMenuItem> Items => GetCurrentItems();

        public IReadOnlyList<ReactionMenuItem> GetCurrentItems()
        {
            return items ?? Array.Empty<ReactionMenuItem>();
        }

        public void SetItems(IReadOnlyList<ReactionMenuItem> newItems)
        {
            if (newItems == null || newItems.Count == 0)
            {
                items = Array.Empty<ReactionMenuItem>();
                NotifyItemsChanged();
                return;
            }

            items = new ReactionMenuItem[newItems.Count];
            for (int i = 0; i < newItems.Count; i++)
            {
                items[i] = newItems[i];
            }

            NotifyItemsChanged();
        }

        public void NotifyItemsChanged()
        {
            ItemsChanged?.Invoke(GetCurrentItems());
        }

        private void Start()
        {
            NotifyItemsChanged();
        }

        private static ReactionMenuItem[] CreateDefaultItems()
        {
            return new[]
            {
                new ReactionMenuItem(
                    "1 Cheer",
                    Key.Digit1,
                    "cheer",
                    "Visitor00",
                    1000,
                    "Visitor00 donated 1,000 won: cheer reaction."),
                new ReactionMenuItem(
                    "2 Shock",
                    Key.Digit2,
                    "shock",
                    "Visitor01",
                    3000,
                    "Visitor01 donated 3,000 won: shock reaction."),
                new ReactionMenuItem(
                    "3 Gift",
                    Key.Digit3,
                    "gift",
                    "Visitor02",
                    5000,
                    "Visitor02 donated 5,000 won: gift reaction."),
                new ReactionMenuItem(
                    "4 Throw",
                    Key.Digit4,
                    "throw",
                    "Visitor03",
                    10000,
                    "Visitor03 donated 10,000 won: throw reaction."),
                new ReactionMenuItem(
                    "5 Spark",
                    Key.Digit5,
                    "spark",
                    "Visitor04",
                    1500,
                    "Visitor04 donated 1,500 won: spark reaction."),
                new ReactionMenuItem(
                    "6 Bonk",
                    Key.Digit6,
                    "bonk",
                    "Visitor05",
                    3000,
                    "Visitor05 triggered bonk motion reaction.")
            };
        }

        [Serializable]
        public sealed class ReactionMenuItem
        {
            [SerializeField] private string displayName;
            [SerializeField] private Key key;
            [SerializeField] private ReactionEvent reactionEvent;

            public ReactionMenuItem(string displayName, Key key, string type, string user, int amount, string message)
            {
                this.displayName = displayName;
                this.key = key;
                reactionEvent = new ReactionEvent
                {
                    Type = type,
                    User = user,
                    Amount = amount,
                    Message = message
                };
            }

            public string DisplayName => displayName;
            public Key Key => key;
            public string ReactionType => reactionEvent != null ? reactionEvent.Type : string.Empty;
            public int Amount => reactionEvent != null ? reactionEvent.Amount : 0;
            public bool IsValid => key != Key.None
                && reactionEvent != null
                && !string.IsNullOrWhiteSpace(reactionEvent.Type);

            public ReactionEvent CreateReactionEvent()
            {
                if (reactionEvent == null)
                {
                    return null;
                }

                return new ReactionEvent
                {
                    Type = reactionEvent.Type,
                    User = reactionEvent.User,
                    Amount = reactionEvent.Amount,
                    Message = reactionEvent.Message
                };
            }
        }
    }
}

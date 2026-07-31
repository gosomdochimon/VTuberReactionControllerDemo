using UnityEngine;
using UnityEngine.InputSystem;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class KeyboardReactionSimulator : MonoBehaviour
    {
        [SerializeField] private ReactionController controller;

        private KeyBinding[] bindings;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            PollKeyboard();
        }

        private void PollKeyboard()
        {
            EnsureInitialized();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || controller == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                KeyBinding binding = bindings[i];
                if (keyboard[binding.Key].wasPressedThisFrame)
                {
                    controller.Enqueue(binding.ReactionEvent);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (controller == null)
            {
                controller = GetComponent<ReactionController>();
            }

            if (bindings != null)
            {
                return;
            }

            bindings = new[]
            {
                new KeyBinding(
                    Key.Digit1,
                    new ReactionEvent
                    {
                        Type = "cheer",
                        User = "ViewerA",
                        Amount = 1000,
                        Message = "Key 1 pressed: cheer reaction test."
                    }),
                new KeyBinding(
                    Key.Digit2,
                    new ReactionEvent
                    {
                        Type = "shock",
                        User = "ViewerB",
                        Amount = 0,
                        Message = "Key 2 pressed: shock reaction test."
                    }),
                new KeyBinding(
                    Key.Digit3,
                    new ReactionEvent
                    {
                        Type = "gift",
                        User = "ViewerC",
                        Amount = 5000,
                        Message = "Key 3 pressed: gift reaction test."
                    })
            };
        }

        private readonly struct KeyBinding
        {
            public KeyBinding(Key key, ReactionEvent reactionEvent)
            {
                Key = key;
                ReactionEvent = reactionEvent;
            }

            public Key Key { get; }
            public ReactionEvent ReactionEvent { get; }
        }
    }
}

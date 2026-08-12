using UnityEngine;
using UnityEngine.InputSystem;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class KeyboardReactionSimulator : MonoBehaviour
    {
        [SerializeField] private ReactionController controller;
        [SerializeField] private ReactionCatalog catalog;

        private void Update()
        {
            PollKeyboard();
        }

        public void Configure(ReactionController targetController, ReactionCatalog targetCatalog)
        {
            if (targetController != null)
            {
                controller = targetController;
            }

            if (targetCatalog != null)
            {
                catalog = targetCatalog;
            }
        }

        private void PollKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || controller == null || catalog == null)
            {
                return;
            }

            var items = catalog.GetCurrentItems();
            for (int i = 0; i < items.Count; i++)
            {
                ReactionCatalog.ReactionMenuItem item = items[i];
                if (item == null || !item.IsValid)
                {
                    continue;
                }

                if (keyboard[item.Key].wasPressedThisFrame)
                {
                    controller.Enqueue(item.CreateReactionEvent());
                }
            }
        }
    }
}

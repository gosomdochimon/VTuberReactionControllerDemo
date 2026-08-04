using UnityEngine;
using UnityEngine.InputSystem;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class KeyboardReactionSimulator : MonoBehaviour
    {
        [SerializeField] private ReactionController controller;
        [SerializeField] private ReactionCatalog catalog;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            controller = GetComponent<ReactionController>();
            catalog = GetComponent<ReactionCatalog>();
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

        private void EnsureInitialized()
        {
            if (controller == null)
            {
                controller = GetComponent<ReactionController>();
            }

            if (catalog == null)
            {
                catalog = GetComponent<ReactionCatalog>();
            }
        }
    }
}

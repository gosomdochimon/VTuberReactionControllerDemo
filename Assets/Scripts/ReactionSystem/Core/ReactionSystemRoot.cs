using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionSystemRoot : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private ReactionController controller;
        [SerializeField] private ReactionCatalog catalog;

        [Header("Input Adapters")]
        [SerializeField] private KeyboardReactionSimulator keyboardSimulator;
        [SerializeField] private UdpJsonEventReceiver udpReceiver;
        [SerializeField] private WebSocketJsonEventReceiver webSocketReceiver;
        [SerializeField] private OscReactionReceiver oscReceiver;

        [Header("HUD")]
        [SerializeField] private ReactionListPresenter listPresenter;
        [SerializeField] private ReactionLogPresenter logPresenter;
        [SerializeField] private ReactionHudPresenter hudPresenter;

        [Header("Startup")]
        [SerializeField] private bool configureOnAwake = true;
        [SerializeField] private bool refreshHudOnStart = true;
        [SerializeField] private bool logConfigurationStatus;

        private void Awake()
        {
            if (configureOnAwake)
            {
                ConfigureSystem();
            }
        }

        private void Start()
        {
            if (refreshHudOnStart)
            {
                RefreshHud();
            }
        }

        public void ConfigureSystem()
        {
            if (keyboardSimulator != null)
            {
                keyboardSimulator.Configure(controller, catalog);
            }

            if (udpReceiver != null)
            {
                udpReceiver.Configure(controller);
            }

            if (webSocketReceiver != null)
            {
                webSocketReceiver.Configure(controller);
            }

            if (oscReceiver != null)
            {
                oscReceiver.Configure(controller);
            }

            if (listPresenter != null)
            {
                listPresenter.Configure(catalog);
            }

            if (logPresenter != null)
            {
                logPresenter.Configure(controller);
            }

            if (hudPresenter != null)
            {
                hudPresenter.Configure(listPresenter, logPresenter);
            }

            if (logConfigurationStatus)
            {
                Debug.Log(CreateConfigurationStatus(), this);
            }
        }

        public void RefreshHud()
        {
            if (catalog != null)
            {
                catalog.NotifyItemsChanged();
            }

            if (hudPresenter != null)
            {
                hudPresenter.Refresh();
            }
            else if (listPresenter != null)
            {
                listPresenter.RefreshFromCurrentReactionList();
            }
        }

        private string CreateConfigurationStatus()
        {
            return $"ReactionSystemRoot configured: "
                + $"controller={Format(controller)}, "
                + $"catalog={Format(catalog)}, "
                + $"keyboard={Format(keyboardSimulator)}, "
                + $"udp={Format(udpReceiver)}, "
                + $"webSocket={Format(webSocketReceiver)}, "
                + $"osc={Format(oscReceiver)}, "
                + $"list={Format(listPresenter)}, "
                + $"log={Format(logPresenter)}, "
                + $"hud={Format(hudPresenter)}";
        }

        private static string Format(Object value)
        {
            return value != null ? value.name : "missing";
        }
    }
}

using System;
using System.Collections.Generic;
using extOSC;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    [DisallowMultipleComponent]
    public sealed class OscReactionReceiver : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private ReactionController controller;

        [Header("extOSC")]
        [SerializeField] private OSCReceiver receiver;
        [SerializeField] private string addressMask = "/reaction/*";
        [SerializeField, Min(1)] private int localPort = 9002;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField, Min(1)] private int maxMessagesPerFrame = 16;

        [Header("Defaults")]
        [SerializeField] private string defaultUser = "OSCViewer";
        [SerializeField] private int defaultAmount;

        [Header("Debug")]
        [SerializeField] private bool logReceivedPackets = true;
        [SerializeField] private bool logEnqueuedReactions = true;

        private readonly Queue<ReactionEvent> pendingReactions = new Queue<ReactionEvent>();
        private readonly object pendingReactionsLock = new object();
        private readonly List<OSCBind> activeBinds = new List<OSCBind>();

        public int PendingMessageCount
        {
            get
            {
                lock (pendingReactionsLock)
                {
                    return pendingReactions.Count;
                }
            }
        }

        public int LocalPort => localPort;
        public string AddressMask => addressMask;

        public void Configure(ReactionController targetController)
        {
            if (targetController != null)
            {
                controller = targetController;
            }
        }

        private void Awake()
        {
            EnsureReceiver();
            ConfigureReceiver();
            RebindReceiver();
        }

        private void Reset()
        {
            receiver = GetComponent<OSCReceiver>();
            controller = GetComponent<ReactionController>();
        }

        private void OnEnable()
        {
            EnsureReceiver();
            ConfigureReceiver();
            RebindReceiver();

            if (startOnEnable)
            {
                StartReceiver();
            }
        }

        private void Update()
        {
            FlushPendingMessages();
        }

        private void OnDisable()
        {
            StopReceiver();
            ClearPendingMessages();
        }

        public void StartReceiver()
        {
            EnsureReceiver();
            ConfigureReceiver();
            RebindReceiver();

            if (receiver == null)
            {
                Debug.LogWarning("OSC reaction receiver has no extOSC OSCReceiver component.");
                return;
            }

            if (!receiver.IsStarted)
            {
                receiver.Connect();
            }

            Debug.Log($"OSC reaction receiver started on 0.0.0.0:{localPort} with address mask {addressMask}.");
        }

        public void StopReceiver()
        {
            if (receiver != null && receiver.IsStarted)
            {
                receiver.Close();
            }
        }

        private void EnsureReceiver()
        {
            if (receiver == null)
            {
                receiver = GetComponent<OSCReceiver>();
            }

            if (receiver == null)
            {
                receiver = gameObject.AddComponent<OSCReceiver>();
            }
        }

        private void ConfigureReceiver()
        {
            if (receiver == null)
            {
                return;
            }

            receiver.AutoConnect = false;
            receiver.LocalPort = localPort;
        }

        private void RebindReceiver()
        {
            if (receiver == null)
            {
                return;
            }

            for (int i = 0; i < activeBinds.Count; i++)
            {
                receiver.Unbind(activeBinds[i]);
            }

            activeBinds.Clear();

            string normalizedAddressMask = NormalizeAddressMask(addressMask);
            if (string.IsNullOrWhiteSpace(normalizedAddressMask))
            {
                Debug.LogWarning("OSC reaction receiver has no address mask.");
                return;
            }

            activeBinds.Add(receiver.Bind(normalizedAddressMask, HandleMessage));
        }

        private void HandleMessage(OSCMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (!TryCreateReactionEvent(message, out ReactionEvent reactionEvent, out string failureReason))
            {
                Debug.LogWarning($"OSC reaction ignored: {failureReason} / {message}");
                return;
            }

            if (logReceivedPackets)
            {
                Debug.Log($"OSC reaction received: {message.Address} -> {reactionEvent.Type}");
            }

            lock (pendingReactionsLock)
            {
                pendingReactions.Enqueue(reactionEvent);
            }
        }

        private void FlushPendingMessages()
        {
            for (int i = 0; i < maxMessagesPerFrame; i++)
            {
                if (!TryDequeueReaction(out ReactionEvent reactionEvent))
                {
                    return;
                }

                EnqueueReaction(reactionEvent);
            }
        }

        private bool TryDequeueReaction(out ReactionEvent reactionEvent)
        {
            lock (pendingReactionsLock)
            {
                if (pendingReactions.Count == 0)
                {
                    reactionEvent = null;
                    return false;
                }

                reactionEvent = pendingReactions.Dequeue();
                return true;
            }
        }

        private void EnqueueReaction(ReactionEvent reactionEvent)
        {
            if (controller == null)
            {
                Debug.LogWarning("OSC reaction receiver has no ReactionController target.");
                return;
            }

            if (logEnqueuedReactions)
            {
                Debug.Log($"OSC reaction enqueued: {reactionEvent.Type} / {reactionEvent.User} / {reactionEvent.Message}");
            }

            controller.Enqueue(reactionEvent);
        }

        private bool TryCreateReactionEvent(OSCMessage message, out ReactionEvent reactionEvent, out string failureReason)
        {
            reactionEvent = null;
            failureReason = null;

            if (!TryExtractReactionType(message.Address, out string reactionType))
            {
                failureReason = $"address does not match {addressMask}";
                return false;
            }

            reactionEvent = new ReactionEvent
            {
                Type = reactionType,
                User = GetStringValue(message, 0, defaultUser),
                Amount = GetAmountValue(message, defaultAmount),
                Message = GetStringValue(message, 1, $"OSC {message.Address} received.")
            };

            return true;
        }

        private bool TryExtractReactionType(string address, out string reactionType)
        {
            reactionType = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string normalizedAddress = NormalizeAddress(address);
            string normalizedMask = NormalizeAddressMask(addressMask);
            int wildcardIndex = normalizedMask.IndexOf('*');
            if (wildcardIndex < 0)
            {
                reactionType = normalizedAddress.Trim('/');
                return !string.IsNullOrWhiteSpace(reactionType);
            }

            string prefix = normalizedMask.Substring(0, wildcardIndex);
            if (!normalizedAddress.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string remainder = normalizedAddress.Substring(prefix.Length).Trim('/');
            if (string.IsNullOrWhiteSpace(remainder) || remainder.IndexOf('/') >= 0)
            {
                return false;
            }

            reactionType = remainder;
            return true;
        }

        private static string GetStringValue(OSCMessage message, int stringIndex, string fallback)
        {
            int currentStringIndex = 0;

            for (int i = 0; i < message.Values.Count; i++)
            {
                OSCValue value = message.Values[i];
                if (value.Type != OSCValueType.String)
                {
                    continue;
                }

                if (currentStringIndex == stringIndex)
                {
                    return string.IsNullOrWhiteSpace(value.StringValue) ? fallback : value.StringValue;
                }

                currentStringIndex++;
            }

            return fallback;
        }

        private static int GetAmountValue(OSCMessage message, int fallback)
        {
            for (int i = 0; i < message.Values.Count; i++)
            {
                OSCValue value = message.Values[i];
                switch (value.Type)
                {
                    case OSCValueType.Int:
                        return value.IntValue;
                    case OSCValueType.Long:
                        return ClampLongToInt(value.LongValue);
                    case OSCValueType.Float:
                        return Mathf.RoundToInt(value.FloatValue);
                    case OSCValueType.Double:
                        return Mathf.RoundToInt((float)value.DoubleValue);
                }
            }

            return fallback;
        }

        private static int ClampLongToInt(long value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value;
        }

        private void ClearPendingMessages()
        {
            lock (pendingReactionsLock)
            {
                pendingReactions.Clear();
            }
        }

        private static string NormalizeAddressMask(string rawAddressMask)
        {
            if (string.IsNullOrWhiteSpace(rawAddressMask))
            {
                return string.Empty;
            }

            return NormalizeAddress(rawAddressMask);
        }

        private static string NormalizeAddress(string rawAddress)
        {
            string trimmed = rawAddress.Trim();
            return trimmed.StartsWith("/", StringComparison.Ordinal)
                ? trimmed
                : "/" + trimmed;
        }
    }
}

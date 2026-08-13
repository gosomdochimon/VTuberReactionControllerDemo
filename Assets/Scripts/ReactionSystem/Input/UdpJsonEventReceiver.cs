using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    [DisallowMultipleComponent]
    public sealed class UdpJsonEventReceiver : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private ReactionController controller;

        [Header("UDP")]
        [SerializeField] private string bindAddress = "127.0.0.1";
        [SerializeField, Min(1)] private int port = 9000;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField, Min(1)] private int maxMessagesPerFrame = 16;
        [SerializeField, Min(256)] private int maxPayloadBytes = 8192;
        [SerializeField, Min(1)] private int maxPendingMessages = 128;
        [SerializeField, Min(1)] private int maxMessagesPerSecond = 60;

        [Header("Debug")]
        [SerializeField] private bool logReceivedPackets = true;
        [SerializeField] private bool logEnqueuedReactions = true;

        private readonly Queue<string> pendingJsonMessages = new Queue<string>();
        private readonly Queue<string> pendingLogMessages = new Queue<string>();
        private readonly object pendingJsonLock = new object();
        private readonly object pendingLogLock = new object();

        private UdpClient client;
        private Thread receiveThread;
        private volatile bool isRunning;
        private ReactionInputGuard inputGuard;

        public int PendingMessageCount
        {
            get
            {
                lock (pendingJsonLock)
                {
                    return pendingJsonMessages.Count;
                }
            }
        }

        public void Configure(ReactionController targetController)
        {
            if (targetController != null)
            {
                controller = targetController;
            }
        }

        private void OnEnable()
        {
            EnsureInputGuard();

            if (startOnEnable)
            {
                StartReceiver();
            }
        }

        private void Update()
        {
            FlushPendingLogs();
            FlushPendingMessages();
        }

        private void OnDisable()
        {
            StopReceiver();
            ClearPendingMessages();
        }

        private void OnDestroy()
        {
            StopReceiver();
        }

        public void StartReceiver()
        {
            if (isRunning)
            {
                return;
            }

            try
            {
                IPAddress address = ResolveBindAddress();
                IPEndPoint localEndPoint = new IPEndPoint(address, port);
                client = new UdpClient(AddressFamily.InterNetwork);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(localEndPoint);

                isRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = $"{nameof(UdpJsonEventReceiver)}:{port}"
                };
                receiveThread.Start();

                Debug.Log($"UDP JSON receiver started on {localEndPoint}.");
            }
            catch (Exception exception)
            {
                isRunning = false;
                DisposeClient();
                Debug.LogError($"UDP JSON receiver failed to start on port {port}: {exception.Message}");
            }
        }

        public void StopReceiver()
        {
            isRunning = false;
            DisposeClient();

            if (receiveThread != null)
            {
                receiveThread.Join(100);
                receiveThread = null;
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                try
                {
                    UdpClient activeClient = client;
                    if (activeClient == null)
                    {
                        break;
                    }

                    byte[] data = activeClient.Receive(ref remoteEndPoint);
                    if (!TryAcceptInput(data.Length, out string failureReason))
                    {
                        EnqueueLog($"UDP JSON packet dropped: {failureReason}.");
                        continue;
                    }

                    string json = Encoding.UTF8.GetString(data);
                    if (!EnqueueJson(json))
                    {
                        inputGuard.MarkDequeued();
                    }

                    EnqueueLog($"UDP JSON received ({data.Length} bytes).");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException exception)
                {
                    if (!isRunning)
                    {
                        break;
                    }

                    EnqueueLog($"UDP JSON receive socket error: {exception.SocketErrorCode} / {exception.Message}");
                }
                catch (Exception exception)
                {
                    if (!isRunning)
                    {
                        break;
                    }

                    EnqueueLog($"UDP JSON receive error: {exception.Message}");
                }
            }
        }

        private bool EnqueueJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            lock (pendingJsonLock)
            {
                pendingJsonMessages.Enqueue(json);
            }

            return true;
        }

        private void FlushPendingMessages()
        {
            for (int i = 0; i < maxMessagesPerFrame; i++)
            {
                if (!TryDequeueJson(out string json))
                {
                    return;
                }

                inputGuard.MarkDequeued();
                EnqueueReaction(json);
            }
        }

        private bool TryDequeueJson(out string json)
        {
            lock (pendingJsonLock)
            {
                if (pendingJsonMessages.Count == 0)
                {
                    json = null;
                    return false;
                }

                json = pendingJsonMessages.Dequeue();
                return true;
            }
        }

        private void EnqueueReaction(string json)
        {
            if (controller == null)
            {
                Debug.LogWarning("UDP JSON receiver has no ReactionController target.");
                return;
            }

            if (!ReactionEventJsonMapper.TryFromJson(json, out ReactionEvent reactionEvent, out string failureReason))
            {
                Debug.LogWarning($"UDP JSON ignored invalid reaction payload: {failureReason}.");
                return;
            }

            if (logEnqueuedReactions)
            {
                Debug.Log($"UDP JSON enqueued reaction: {reactionEvent.Type}.");
            }

            controller.Enqueue(reactionEvent);
        }

        private IPAddress ResolveBindAddress()
        {
            if (string.IsNullOrWhiteSpace(bindAddress))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(bindAddress, out IPAddress address))
            {
                return address;
            }

            Debug.LogWarning($"Invalid UDP bind address '{bindAddress}'. Falling back to 127.0.0.1.");
            return IPAddress.Loopback;
        }

        private void EnqueueLog(string message)
        {
            if (!logReceivedPackets)
            {
                return;
            }

            lock (pendingLogLock)
            {
                pendingLogMessages.Enqueue(message);
            }
        }

        private void FlushPendingLogs()
        {
            while (TryDequeueLog(out string message))
            {
                Debug.Log(message);
            }
        }

        private bool TryDequeueLog(out string message)
        {
            lock (pendingLogLock)
            {
                if (pendingLogMessages.Count == 0)
                {
                    message = null;
                    return false;
                }

                message = pendingLogMessages.Dequeue();
                return true;
            }
        }

        private void ClearPendingMessages()
        {
            lock (pendingJsonLock)
            {
                pendingJsonMessages.Clear();
            }

            lock (pendingLogLock)
            {
                pendingLogMessages.Clear();
            }

            inputGuard?.Reset();
        }

        private void EnsureInputGuard()
        {
            if (inputGuard == null)
            {
                inputGuard = new ReactionInputGuard(
                    maxPayloadBytes,
                    maxPendingMessages,
                    maxMessagesPerSecond);
            }
        }

        private bool TryAcceptInput(int payloadBytes, out string failureReason)
        {
            EnsureInputGuard();
            return inputGuard.TryAccept(payloadBytes, out failureReason);
        }

        private void DisposeClient()
        {
            if (client == null)
            {
                return;
            }

            client.Close();
            client.Dispose();
            client = null;
        }
    }
}

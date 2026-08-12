using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    /// <summary>
    /// 로컬 WebSocket으로 JSON 문자열을 받아 ReactionController에 넘기는 입력 어댑터입니다.
    /// 이 컴포넌트는 리액션을 직접 실행하지 않고, 외부 도구가 보낸 JSON을 ReactionEvent로 바꿔 큐에 넣는 역할만 맡습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WebSocketJsonEventReceiver : MonoBehaviour
    {
        // WebSocket 표준(RFC 6455)에서 클라이언트가 보낸 Sec-WebSocket-Key를 검증 응답으로 바꿀 때 쓰는 고정 문자열입니다.
        // 비밀번호나 프로젝트 전용 값이 아니라, 모든 WebSocket 서버가 같은 값을 사용합니다.
        private const string WebSocketMagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        [Header("Target")]
        // 최종적으로 ReactionEvent를 처리할 컨트롤러입니다. ReactionSystemRoot가 실행 시 Configure로 연결해줍니다.
        [SerializeField] private ReactionController controller;

        [Header("WebSocket")]
        // 127.0.0.1은 "이 컴퓨터 안에서만 접속 가능"하다는 뜻입니다. 외부 PC에서도 받으려면 0.0.0.0 같은 바인딩을 사용합니다.
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField, Min(1)] private int port = 9001;
        // ws://127.0.0.1:9001/reaction/ 에서 마지막 /reaction/ 부분입니다.
        [SerializeField] private string path = "/reaction/";
        [SerializeField] private bool startOnEnable = true;
        // 한 프레임에 너무 많은 JSON을 처리하면 Unity 화면이 끊길 수 있으므로 프레임당 처리량을 제한합니다.
        [SerializeField, Min(1)] private int maxMessagesPerFrame = 16;

        [Header("Debug")]
        [SerializeField] private bool logReceivedPackets = true;
        [SerializeField] private bool logEnqueuedReactions = true;
        [SerializeField] private bool sendAcknowledgement = true;

        // 네트워크 수신은 별도 스레드에서 일어나지만, Unity 오브젝트 접근은 메인 스레드에서 해야 합니다.
        // 그래서 수신 스레드는 문자열만 큐에 넣고, Update에서 꺼내 ReactionController에 전달합니다.
        private readonly Queue<string> pendingJsonMessages = new Queue<string>();
        private readonly Queue<string> pendingLogMessages = new Queue<string>();
        private readonly object pendingJsonLock = new object();
        private readonly object pendingLogLock = new object();
        private readonly List<TcpClient> activeClients = new List<TcpClient>();
        private readonly object activeClientsLock = new object();

        private TcpListener listener;
        private Thread receiveThread;
        private volatile bool isRunning;

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

        public string WebSocketUrl => $"ws://{host}:{port}{NormalizePath(path)}";

        public void Configure(ReactionController targetController)
        {
            if (targetController != null)
            {
                controller = targetController;
            }
        }

        private void OnEnable()
        {
            if (startOnEnable)
            {
                StartReceiver();
            }
        }

        private void Update()
        {
            // Debug.Log와 ReactionController.Enqueue는 Unity 메인 스레드에서 실행되도록 여기에서 처리합니다.
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
                // TcpListener는 지정한 host/port에서 들어오는 TCP 연결을 기다리는 로컬 서버 역할을 합니다.
                // 여기서는 WebSocket 패키지를 쓰지 않고, 필요한 handshake와 frame 처리만 직접 구현합니다.
                IPAddress address = ResolveBindAddress();
                listener = new TcpListener(address, port);
                listener.Start();

                isRunning = true;
                // AcceptTcpClient와 NetworkStream.Read는 대기 시간이 길 수 있으므로 Unity 메인 스레드가 아닌 백그라운드 스레드에서 돌립니다.
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = $"{nameof(WebSocketJsonEventReceiver)}:{port}"
                };
                receiveThread.Start();

                Debug.Log($"WebSocket JSON receiver started on {WebSocketUrl}.");
            }
            catch (Exception exception)
            {
                isRunning = false;
                DisposeListener();
                Debug.LogError($"WebSocket JSON receiver failed to start on {WebSocketUrl}: {exception.Message}");
            }
        }

        public void StopReceiver()
        {
            isRunning = false;
            DisposeListener();
            DisposeActiveClients();

            if (receiveThread != null)
            {
                receiveThread.Join(100);
                receiveThread = null;
            }
        }

        private void ReceiveLoop()
        {
            // 연결을 기다리는 루프입니다. 테스트 HTML이나 Python 송신기가 접속하면 HandleClient가 실제 메시지를 읽습니다.
            while (isRunning)
            {
                try
                {
                    TcpListener activeListener = listener;
                    if (activeListener == null)
                    {
                        break;
                    }

                    TcpClient client = activeListener.AcceptTcpClient();
                    EndPoint remoteEndPoint = client.Client.RemoteEndPoint;
                    TrackClient(client);

                    Thread clientThread = new Thread(() => HandleClientSafely(client, remoteEndPoint))
                    {
                        IsBackground = true,
                        Name = $"{nameof(WebSocketJsonEventReceiver)}Client:{port}"
                    };
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    if (!isRunning)
                    {
                        break;
                    }

                    EnqueueLog("WebSocket JSON listener socket error.");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (!isRunning)
                    {
                        break;
                    }

                    EnqueueLog($"WebSocket JSON receive error: {exception.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            client.NoDelay = true;

            using (NetworkStream stream = client.GetStream())
            {
                // WebSocket은 처음부터 JSON을 보내지 않습니다.
                // 먼저 HTTP GET + Upgrade 요청을 받고, 서버가 101 Switching Protocols로 승인해야 진짜 WebSocket 통신이 시작됩니다.
                if (!TryAcceptHandshake(stream, client.Client.RemoteEndPoint))
                {
                    return;
                }

                while (isRunning && client.Connected)
                {
                    WebSocketFrame frame = ReadFrame(stream);
                    if (frame.Opcode == WebSocketOpcode.Close)
                    {
                        SendCloseFrame(stream);
                        return;
                    }

                    if (frame.Opcode == WebSocketOpcode.Ping)
                    {
                        // 브라우저/테스트 도구가 연결 상태 확인용 Ping을 보내면 Pong으로 응답해야 연결이 유지됩니다.
                        SendFrame(stream, WebSocketOpcode.Pong, frame.Payload);
                        continue;
                    }

                    if (frame.Opcode != WebSocketOpcode.Text)
                    {
                        continue;
                    }

                    string message = Encoding.UTF8.GetString(frame.Payload);
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    EnqueueJson(message);
                    EnqueueLog($"WebSocket JSON received: {message}");

                    if (sendAcknowledgement)
                    {
                        // 테스트 도구에서 "Unity가 메시지를 받았는지" 확인할 수 있도록 간단한 수신 확인을 되돌려줍니다.
                        SendTextFrame(stream, "{\"Status\":\"received\"}");
                    }
                }
            }
        }

        private void HandleClientSafely(TcpClient client, EndPoint remoteEndPoint)
        {
            try
            {
                HandleClient(client);
            }
            catch (IOException)
            {
                if (isRunning)
                {
                    EnqueueLog($"WebSocket JSON client disconnected: {remoteEndPoint}");
                }
            }
            catch (SocketException)
            {
                if (isRunning)
                {
                    EnqueueLog($"WebSocket JSON client socket closed: {remoteEndPoint}");
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                if (isRunning)
                {
                    EnqueueLog($"WebSocket JSON client error from {remoteEndPoint}: {exception.Message}");
                }
            }
            finally
            {
                UntrackClient(client);
            }
        }

        private bool TryAcceptHandshake(NetworkStream stream, EndPoint remoteEndPoint)
        {
            // 브라우저 주소창으로 ws://를 열면 화면이 보이지 않는 이유가 여기 있습니다.
            // WebSocket 주소는 페이지가 아니라, HTTP 요청을 WebSocket 연결로 "업그레이드"하기 위한 엔드포인트입니다.
            string request = ReadHttpRequest(stream);
            if (string.IsNullOrWhiteSpace(request))
            {
                return false;
            }

            string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0 || !TryParseRequestPath(lines[0], out string requestPath))
            {
                SendBadRequest(stream, "Invalid WebSocket request.");
                return false;
            }

            // 올바른 경로(/reaction/)와 WebSocket 필수 헤더가 있는 요청만 허용합니다.
            // 일반 HTTP 요청이면 400을 돌려 "여기는 WebSocket 요청만 받는다"는 메시지를 보냅니다.
            Dictionary<string, string> headers = ParseHeaders(lines);
            if (!IsExpectedPath(requestPath)
                || !HeaderContains(headers, "Upgrade", "websocket")
                || !HeaderContains(headers, "Connection", "Upgrade")
                || !headers.TryGetValue("Sec-WebSocket-Key", out string key)
                || string.IsNullOrWhiteSpace(key))
            {
                SendBadRequest(stream, "This endpoint expects a WebSocket request.");
                return false;
            }

            // 클라이언트가 보낸 Sec-WebSocket-Key를 표준 방식으로 변환해 Sec-WebSocket-Accept로 돌려주면 handshake가 완료됩니다.
            string acceptKey = CreateAcceptKey(key.Trim());
            string response = "HTTP/1.1 101 Switching Protocols\r\n"
                + "Upgrade: websocket\r\n"
                + "Connection: Upgrade\r\n"
                + $"Sec-WebSocket-Accept: {acceptKey}\r\n"
                + "\r\n";
            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            EnqueueLog($"WebSocket JSON client connected: {remoteEndPoint}");
            return true;
        }

        private static string ReadHttpRequest(NetworkStream stream)
        {
            // WebSocket handshake는 HTTP 헤더 형식이므로 빈 줄(\r\n\r\n)이 나올 때까지 읽으면 요청 헤더 하나가 완성됩니다.
            using (MemoryStream buffer = new MemoryStream())
            {
                int current;

                while ((current = stream.ReadByte()) != -1)
                {
                    buffer.WriteByte((byte)current);

                    if (EndsWithHttpHeaderTerminator(buffer) || buffer.Length > 8192)
                    {
                        break;
                    }
                }

                return Encoding.ASCII.GetString(buffer.ToArray());
            }
        }

        private static bool EndsWithHttpHeaderTerminator(MemoryStream buffer)
        {
            if (buffer.Length < 4)
            {
                return false;
            }

            byte[] data = buffer.GetBuffer();
            long offset = buffer.Length - 4;
            return data[offset] == '\r'
                && data[offset + 1] == '\n'
                && data[offset + 2] == '\r'
                && data[offset + 3] == '\n';
        }

        private static bool TryParseRequestPath(string requestLine, out string requestPath)
        {
            string[] parts = requestLine.Split(' ');
            if (parts.Length >= 2 && string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
            {
                requestPath = parts[1];
                return true;
            }

            requestPath = null;
            return false;
        }

        private static Dictionary<string, string> ParseHeaders(string[] lines)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                headers[key] = value;
            }

            return headers;
        }

        private bool IsExpectedPath(string requestPath)
        {
            string normalizedRequestPath = NormalizePath(requestPath.Split('?')[0]);
            return string.Equals(normalizedRequestPath, NormalizePath(path), StringComparison.Ordinal);
        }

        private static bool HeaderContains(Dictionary<string, string> headers, string key, string expectedValue)
        {
            if (!headers.TryGetValue(key, out string value))
            {
                return false;
            }

            return value.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CreateAcceptKey(string key)
        {
            // 공식 WebSocket handshake 규칙: Base64(SHA1(clientKey + magicGuid)).
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + WebSocketMagicGuid));
                return Convert.ToBase64String(hash);
            }
        }

        private static WebSocketFrame ReadFrame(NetworkStream stream)
        {
            // handshake 이후 실제 데이터는 "프레임" 단위로 옵니다.
            // 첫 바이트는 메시지 종류(Text/Close/Ping 등), 두 번째 바이트는 마스크 여부와 payload 길이를 담습니다.
            int firstByte = stream.ReadByte();
            int secondByte = stream.ReadByte();
            if (firstByte < 0 || secondByte < 0)
            {
                return new WebSocketFrame(WebSocketOpcode.Close, Array.Empty<byte>());
            }

            WebSocketOpcode opcode = (WebSocketOpcode)(firstByte & 0x0F);
            bool masked = (secondByte & 0x80) != 0;
            ulong payloadLength = (ulong)(secondByte & 0x7F);

            if (payloadLength == 126)
            {
                byte[] extended = ReadExact(stream, 2);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(extended);
                }

                payloadLength = BitConverter.ToUInt16(extended, 0);
            }
            else if (payloadLength == 127)
            {
                byte[] extended = ReadExact(stream, 8);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(extended);
                }

                payloadLength = BitConverter.ToUInt64(extended, 0);
            }

            byte[] mask = masked ? ReadExact(stream, 4) : null;
            byte[] payload = ReadExact(stream, checked((int)payloadLength));

            if (masked && mask != null)
            {
                // WebSocket 규칙상 클라이언트가 서버로 보내는 payload는 마스킹되어 있습니다.
                // 서버는 4바이트 mask key로 XOR을 풀어야 원래 JSON 문자열을 얻습니다.
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(payload[i] ^ mask[i % 4]);
                }
            }

            return new WebSocketFrame(opcode, payload);
        }

        private static byte[] ReadExact(NetworkStream stream, int length)
        {
            byte[] data = new byte[length];
            int offset = 0;

            while (offset < length)
            {
                int read = stream.Read(data, offset, length - offset);
                if (read <= 0)
                {
                    throw new IOException("WebSocket connection closed while reading.");
                }

                offset += read;
            }

            return data;
        }

        private static void SendTextFrame(NetworkStream stream, string message)
        {
            SendFrame(stream, WebSocketOpcode.Text, Encoding.UTF8.GetBytes(message));
        }

        private static void SendCloseFrame(NetworkStream stream)
        {
            SendFrame(stream, WebSocketOpcode.Close, Array.Empty<byte>());
        }

        private static void SendFrame(NetworkStream stream, WebSocketOpcode opcode, byte[] payload)
        {
            // 서버가 클라이언트로 보내는 WebSocket 프레임은 마스킹하지 않습니다.
            // payload 길이가 125를 넘으면 표준 형식에 맞춰 2바이트 또는 8바이트 확장 길이를 붙입니다.
            using (MemoryStream frame = new MemoryStream())
            {
                frame.WriteByte((byte)(0x80 | (byte)opcode));

                if (payload.Length <= 125)
                {
                    frame.WriteByte((byte)payload.Length);
                }
                else if (payload.Length <= 65535)
                {
                    frame.WriteByte(126);
                    byte[] lengthBytes = BitConverter.GetBytes((ushort)payload.Length);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBytes);
                    }

                    frame.Write(lengthBytes, 0, lengthBytes.Length);
                }
                else
                {
                    frame.WriteByte(127);
                    byte[] lengthBytes = BitConverter.GetBytes((ulong)payload.Length);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBytes);
                    }

                    frame.Write(lengthBytes, 0, lengthBytes.Length);
                }

                frame.Write(payload, 0, payload.Length);
                byte[] frameBytes = frame.ToArray();
                stream.Write(frameBytes, 0, frameBytes.Length);
            }
        }

        private static void SendBadRequest(NetworkStream stream, string message)
        {
            byte[] body = Encoding.UTF8.GetBytes(message);
            string response = "HTTP/1.1 400 Bad Request\r\n"
                + "Content-Type: text/plain; charset=utf-8\r\n"
                + string.Format(CultureInfo.InvariantCulture, "Content-Length: {0}\r\n", body.Length)
                + "Connection: close\r\n"
                + "\r\n";
            byte[] header = Encoding.ASCII.GetBytes(response);
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
        }

        private void EnqueueJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            // 수신 스레드와 Unity 메인 스레드가 같은 큐를 만지므로 lock으로 동시에 수정되는 상황을 막습니다.
            lock (pendingJsonLock)
            {
                pendingJsonMessages.Enqueue(json);
            }
        }

        private void FlushPendingMessages()
        {
            // 네트워크에서 들어온 JSON을 Unity 프레임 업데이트 안에서 ReactionEvent로 바꿔 처리합니다.
            for (int i = 0; i < maxMessagesPerFrame; i++)
            {
                if (!TryDequeueJson(out string json))
                {
                    return;
                }

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
                Debug.LogWarning("WebSocket JSON receiver has no ReactionController target.");
                return;
            }

            if (!ReactionEventJsonMapper.TryFromJson(json, out ReactionEvent reactionEvent, out string failureReason))
            {
                Debug.LogWarning($"WebSocket JSON ignored invalid reaction payload: {failureReason} / {json}");
                return;
            }

            if (logEnqueuedReactions)
            {
                Debug.Log($"WebSocket JSON enqueued reaction: {reactionEvent.Type} / {reactionEvent.User} / {reactionEvent.Message}");
            }

            controller.Enqueue(reactionEvent);
        }

        private IPAddress ResolveBindAddress()
        {
            // TcpListener는 문자열 host가 아니라 IPAddress를 필요로 하므로 인스펙터 값을 IPAddress로 변환합니다.
            if (string.IsNullOrWhiteSpace(host))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(host, out IPAddress address))
            {
                return address;
            }

            Debug.LogWarning($"Invalid WebSocket host '{host}'. Falling back to 127.0.0.1.");
            return IPAddress.Loopback;
        }

        private void EnqueueLog(string message)
        {
            if (!logReceivedPackets)
            {
                return;
            }

            // Debug.Log도 Unity 메인 스레드에서 찍도록 로그 메시지만 큐에 쌓습니다.
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
        }

        private static string NormalizePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return "/";
            }

            string normalized = rawPath.StartsWith("/", StringComparison.Ordinal)
                ? rawPath
                : "/" + rawPath;

            return normalized.EndsWith("/", StringComparison.Ordinal)
                ? normalized
                : normalized + "/";
        }

        private void DisposeListener()
        {
            if (listener == null)
            {
                return;
            }

            listener.Stop();
            listener = null;
        }

        private void TrackClient(TcpClient client)
        {
            lock (activeClientsLock)
            {
                activeClients.Add(client);
            }
        }

        private void UntrackClient(TcpClient client)
        {
            lock (activeClientsLock)
            {
                activeClients.Remove(client);
            }

            client.Close();
        }

        private void DisposeActiveClients()
        {
            lock (activeClientsLock)
            {
                for (int i = 0; i < activeClients.Count; i++)
                {
                    activeClients[i].Close();
                }

                activeClients.Clear();
            }
        }

        private enum WebSocketOpcode
        {
            Text = 1,
            Close = 8,
            Ping = 9,
            Pong = 10
        }

        private readonly struct WebSocketFrame
        {
            public WebSocketFrame(WebSocketOpcode opcode, byte[] payload)
            {
                Opcode = opcode;
                Payload = payload;
            }

            public WebSocketOpcode Opcode { get; }
            public byte[] Payload { get; }
        }
    }
}

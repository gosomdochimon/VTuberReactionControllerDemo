#!/usr/bin/env python3
"""Send a VTuber reaction event to Unity over local WebSocket JSON."""

from __future__ import annotations

import argparse
import base64
import json
import os
import socket
import struct
import time

DEFAULT_AMOUNT = 1000
DEFAULT_MESSAGE = "event from websocket python"
DEFAULT_BROADCAST_SOURCE = "soop-scon"

BROADCAST_REACTION_PAYLOADS = {
    "cheer": ("support", "cheer", 1000, "ㅋㅋ SOOP/SCON websocket cheer reaction test"),
    "shock": ("support", "shock", 1000, "놀람! SOOP/SCON websocket shock reaction test"),
    "gift": ("support", "gift", 5000, "SOOP/SCON websocket gift reaction test"),
    "throw": ("support", "throw", 3000, "!throw SOOP/SCON websocket throw reaction test"),
    "spark": ("support", "spark", 10000, "SOOP/SCON websocket spark reaction test"),
    "bonk": ("support", "bonk", 3000, "SOOP/SCON websocket bonk motion reaction test"),
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Send a ReactionEvent JSON message to the Unity WebSocket demo."
    )
    parser.add_argument(
        "reaction_type",
        nargs="?",
        default="cheer",
        help="Reaction type matched by Unity, such as cheer, shock, gift, throw, or spark.",
    )
    parser.add_argument("--user", default="LocalTester", help="Event sender name.")
    parser.add_argument("--amount", type=int, default=DEFAULT_AMOUNT, help="Numeric event value.")
    parser.add_argument(
        "--message",
        default=DEFAULT_MESSAGE,
        help="Message shown by the Unity reaction log.",
    )
    parser.add_argument(
        "--source",
        default=DEFAULT_BROADCAST_SOURCE,
        help="Broadcast source name for broadcast payloads.",
    )
    parser.add_argument(
        "--payload-style",
        choices=("standard", "external", "broadcast"),
        default="standard",
        help="JSON field naming style to send. broadcast uses source/eventKind/selectedReaction/displayName/amount/message.",
    )
    parser.add_argument("--host", default="127.0.0.1", help="Unity WebSocket receiver host.")
    parser.add_argument("--port", type=int, default=9001, help="Unity WebSocket receiver port.")
    parser.add_argument("--path", default="/reaction/", help="Unity WebSocket receiver path.")
    parser.add_argument("--count", type=int, default=1, help="Number of messages to send.")
    parser.add_argument(
        "--delay",
        type=float,
        default=0.25,
        help="Delay between messages when count is greater than 1.",
    )
    return parser


def create_payload(args: argparse.Namespace) -> dict[str, object]:
    if args.payload_style == "broadcast":
        reaction_key = args.reaction_type.lower()
        event_kind, selected_reaction, default_amount, default_message = BROADCAST_REACTION_PAYLOADS.get(
            reaction_key,
            ("support", args.reaction_type, args.amount, args.message),
        )
        amount = default_amount if args.amount == DEFAULT_AMOUNT else args.amount
        message = default_message if args.message == DEFAULT_MESSAGE else args.message
        return {
            "source": args.source,
            "eventKind": event_kind,
            "selectedReaction": selected_reaction,
            "displayName": args.user,
            "amount": amount,
            "message": message,
        }

    if args.payload_style == "external":
        return {
            "type": args.reaction_type,
            "displayName": args.user,
            "amount": args.amount,
            "comment": args.message,
        }

    return {
        "Type": args.reaction_type,
        "User": args.user,
        "Amount": args.amount,
        "Message": args.message,
    }


def normalize_path(path: str) -> str:
    if not path:
        return "/"

    normalized = path if path.startswith("/") else f"/{path}"
    return normalized if normalized.endswith("/") else f"{normalized}/"


def connect_websocket(host: str, port: int, path: str) -> socket.socket:
    tcp_socket = socket.create_connection((host, port), timeout=5)
    websocket_key = base64.b64encode(os.urandom(16)).decode("ascii")
    request = (
        f"GET {normalize_path(path)} HTTP/1.1\r\n"
        f"Host: {host}:{port}\r\n"
        "Connection: Upgrade\r\n"
        "Pragma: no-cache\r\n"
        "Cache-Control: no-cache\r\n"
        "User-Agent: VTuberReactionControllerDemo-WebSocketSender/1.0\r\n"
        "Upgrade: websocket\r\n"
        f"Origin: http://{host}:{port}\r\n"
        f"Sec-WebSocket-Key: {websocket_key}\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        "\r\n"
    )

    tcp_socket.sendall(request.encode("ascii"))
    response = tcp_socket.recv(4096).decode("iso-8859-1", errors="replace")
    if " 101 " not in response.split("\r\n", 1)[0]:
        tcp_socket.close()
        raise RuntimeError(f"WebSocket handshake failed: {response.strip()}")

    return tcp_socket


def encode_text_frame(message: str) -> bytes:
    payload = message.encode("utf-8")
    mask_key = os.urandom(4)
    first_byte = 0x81

    if len(payload) <= 125:
        header = struct.pack("!BB", first_byte, 0x80 | len(payload))
    elif len(payload) <= 65535:
        header = struct.pack("!BBH", first_byte, 0x80 | 126, len(payload))
    else:
        header = struct.pack("!BBQ", first_byte, 0x80 | 127, len(payload))

    masked_payload = bytes(
        value ^ mask_key[index % 4]
        for index, value in enumerate(payload)
    )
    return header + mask_key + masked_payload


def send_close_frame(tcp_socket: socket.socket) -> None:
    try:
        tcp_socket.sendall(b"\x88\x80" + b"\x00\x00\x00\x00")
    except OSError:
        pass


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.count < 1:
        parser.error("--count must be at least 1")

    payload = create_payload(args)
    encoded_payload = json.dumps(payload, ensure_ascii=False)

    with connect_websocket(args.host, args.port, args.path) as websocket:
        for index in range(args.count):
            websocket.sendall(encode_text_frame(encoded_payload))
            print(
                f"sent {index + 1}/{args.count} to "
                f"ws://{args.host}:{args.port}{normalize_path(args.path)}: {encoded_payload}"
            )

            if index + 1 < args.count:
                time.sleep(args.delay)

        send_close_frame(websocket)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

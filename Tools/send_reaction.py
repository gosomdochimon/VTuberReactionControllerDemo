#!/usr/bin/env python3
"""Send a VTuber reaction event to Unity over UDP JSON."""

from __future__ import annotations

import argparse
import json
import socket
import time


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Send a ReactionEvent JSON packet to the Unity demo."
    )
    parser.add_argument(
        "reaction_type",
        nargs="?",
        default="cheer",
        help="Reaction type matched by Unity, such as cheer, shock, or gift.",
    )
    parser.add_argument("--user", default="LocalTester", help="Event sender name.")
    parser.add_argument("--amount", type=int, default=1000, help="Numeric event value.")
    parser.add_argument(
        "--message",
        default="event from python",
        help="Message shown by the Unity reaction log.",
    )
    parser.add_argument("--host", default="127.0.0.1", help="Unity receiver host.")
    parser.add_argument("--port", type=int, default=9000, help="Unity receiver UDP port.")
    parser.add_argument("--count", type=int, default=1, help="Number of packets to send.")
    parser.add_argument(
        "--delay",
        type=float,
        default=0.25,
        help="Delay between packets when count is greater than 1.",
    )
    return parser


def create_payload(args: argparse.Namespace) -> dict[str, object]:
    return {
        "Type": args.reaction_type,
        "User": args.user,
        "Amount": args.amount,
        "Message": args.message,
    }


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.count < 1:
        parser.error("--count must be at least 1")

    payload = create_payload(args)
    encoded_payload = json.dumps(payload, ensure_ascii=False).encode("utf-8")

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp_socket:
        for index in range(args.count):
            udp_socket.sendto(encoded_payload, (args.host, args.port))
            print(
                f"sent {index + 1}/{args.count} to {args.host}:{args.port}: "
                f"{encoded_payload.decode('utf-8')}"
            )

            if index + 1 < args.count:
                time.sleep(args.delay)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

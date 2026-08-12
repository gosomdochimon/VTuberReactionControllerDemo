#!/usr/bin/env python3
import argparse
import socket
import struct


REACTION_DEFAULTS = {
    "cheer": {"user": "OSCViewerA", "amount": 1000, "message": "OSC cheer reaction test."},
    "shock": {"user": "OSCViewerB", "amount": 0, "message": "OSC shock reaction test."},
    "gift": {"user": "OSCViewerC", "amount": 5000, "message": "OSC gift reaction test."},
    "throw": {"user": "OSCViewerD", "amount": 1, "message": "OSC throw reaction test."},
    "spark": {"user": "OSCViewerE", "amount": 10000, "message": "OSC spark reaction test."},
    "bonk": {"user": "OSCViewerF", "amount": 3000, "message": "OSC bonk motion reaction test."},
}


def pad_osc_string(value):
    data = value.encode("utf-8") + b"\0"
    padding = (-len(data)) % 4
    return data + (b"\0" * padding)


def pack_message(address, user, amount, message):
    return b"".join(
        [
            pad_osc_string(address),
            pad_osc_string(",sis"),
            pad_osc_string(user),
            struct.pack(">i", amount),
            pad_osc_string(message),
        ]
    )


def parse_args():
    parser = argparse.ArgumentParser(description="Send a VTuber reaction OSC message to Unity.")
    parser.add_argument("reaction", choices=sorted(REACTION_DEFAULTS.keys()))
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=9002)
    parser.add_argument("--user")
    parser.add_argument("--amount", type=int)
    parser.add_argument("--message")
    return parser.parse_args()


def main():
    args = parse_args()
    defaults = REACTION_DEFAULTS[args.reaction]
    user = args.user if args.user is not None else defaults["user"]
    amount = args.amount if args.amount is not None else defaults["amount"]
    message = args.message if args.message is not None else defaults["message"]
    address = f"/reaction/{args.reaction}"
    packet = pack_message(address, user, amount, message)

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client:
        client.sendto(packet, (args.host, args.port))

    print(f"Sent OSC {address} to {args.host}:{args.port} user={user} amount={amount} message={message}")


if __name__ == "__main__":
    main()

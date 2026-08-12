"""Capture a repeatable Unity Editor QA video with the browser tester and six reactions."""

from __future__ import annotations

import argparse
import ctypes
import time
from pathlib import Path

import cv2
import numpy as np
from PIL import ImageGrab


USER32 = ctypes.windll.user32
MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
MOUSEEVENTF_WHEEL = 0x0800
HWND_TOP = ctypes.c_void_p(0)
HWND_TOPMOST = ctypes.c_void_p(-1)
HWND_NOTOPMOST = ctypes.c_void_p(-2)
SWP_SHOWWINDOW = 0x0040
SW_RESTORE = 9

USER32.FindWindowW.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p]
USER32.FindWindowW.restype = ctypes.c_void_p
USER32.SetWindowPos.argtypes = [
    ctypes.c_void_p,
    ctypes.c_void_p,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_int,
    ctypes.c_uint,
]
USER32.ShowWindow.argtypes = [ctypes.c_void_p, ctypes.c_int]
USER32.SetForegroundWindow.argtypes = [ctypes.c_void_p]


def click(x: int, y: int) -> None:
    USER32.SetCursorPos(x, y)
    USER32.mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    USER32.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)


def scroll_down(x: int, y: int, notches: int) -> None:
    USER32.SetCursorPos(x, y)
    USER32.mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120 * notches, 0)


def scroll_up(x: int, y: int, notches: int) -> None:
    USER32.SetCursorPos(x, y)
    USER32.mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120 * notches, 0)


def place_windows() -> None:
    browser = USER32.FindWindowW(None, "WebSocket Reaction Tester - Chrome")
    unity = USER32.FindWindowW(
        None,
        "VTuberReactionControllerDemo - SampleScene - Windows, Mac, Linux - Unity 6.5 (6000.5.6f1) <DX12>",
    )
    if not browser or not unity:
        raise RuntimeError("Could not find the Chrome tester and Unity Editor windows.")

    USER32.ShowWindow(browser, SW_RESTORE)
    USER32.ShowWindow(unity, SW_RESTORE)
    USER32.SetWindowPos(browser, HWND_TOP, 0, 0, 900, 1040, SWP_SHOWWINDOW)
    USER32.SetWindowPos(unity, HWND_TOP, 900, 0, 1020, 1040, SWP_SHOWWINDOW)
    # Force the browser above the shell/editor that launched this script, then
    # remove topmost so the Unity Editor remains a normal peer window.
    USER32.SetWindowPos(browser, HWND_TOPMOST, 0, 0, 900, 1040, SWP_SHOWWINDOW)
    USER32.SetWindowPos(browser, HWND_NOTOPMOST, 0, 0, 900, 1040, SWP_SHOWWINDOW)
    USER32.SetForegroundWindow(browser)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="QA/VTuber_Demo_UnityEditor_QA.mp4",
        help="Output MP4 path relative to the project root.",
    )
    parser.add_argument("--duration", type=float, default=32.0)
    parser.add_argument("--fps", type=float, default=15.0)
    args = parser.parse_args()

    project_root = Path(__file__).resolve().parents[1]
    output_path = project_root / args.output
    output_path.parent.mkdir(parents=True, exist_ok=True)

    capture_width, capture_height = 1920, 1080
    output_width, output_height = 1280, 720
    writer = cv2.VideoWriter(
        str(output_path),
        cv2.VideoWriter_fourcc(*"mp4v"),
        args.fps,
        (output_width, output_height),
    )
    if not writer.isOpened():
        raise RuntimeError(f"Could not open video writer: {output_path}")

    # The browser is positioned on the left half of the primary monitor.
    # These controls are stable at the current 900x1040 browser layout.
    connect_button = (95, 321)
    reaction_buttons = [
        (88, 1017),   # cheer
        (170, 1017),  # shock
        (246, 1017),  # gift
        (320, 1017),  # throw
        (404, 1017),  # spark
        (484, 1017),  # bonk
    ]
    # Leave enough time for the reaction animation and final log review.
    event_times = [3.0, 7.0, 11.0, 15.0, 19.0, 23.0]
    next_event = 0
    started = time.perf_counter()
    frame_period = 1.0 / args.fps

    place_windows()
    time.sleep(0.5)
    scroll_up(500, 500, 20)
    time.sleep(0.5)
    # Connecting is harmless if the button is disabled because the page is
    # already connected; otherwise it establishes the visible WebSocket QA log.
    click(*connect_button)
    time.sleep(1.0)
    USER32.SetCursorPos(1770, 100)

    try:
        while True:
            elapsed = time.perf_counter() - started
            if elapsed >= args.duration:
                break

            if next_event < len(event_times) and elapsed >= event_times[next_event]:
                click(*reaction_buttons[next_event])
                next_event += 1
                USER32.SetCursorPos(1770, 100)

            if elapsed >= 27.0 and elapsed < 27.0 + frame_period:
                # Show the accumulated tester log after all six reactions.
                scroll_down(500, 800, 7)
                USER32.SetCursorPos(1770, 100)

            screenshot = ImageGrab.grab(bbox=(0, 0, capture_width, capture_height))
            frame = cv2.cvtColor(cv2.resize(
                cv2.cvtColor(np.array(screenshot), cv2.COLOR_RGB2BGR),
                (output_width, output_height),
                interpolation=cv2.INTER_AREA,
            ), cv2.COLOR_BGR2RGB)
            writer.write(cv2.cvtColor(frame, cv2.COLOR_RGB2BGR))

            sleep_for = frame_period - (time.perf_counter() - started - elapsed)
            if sleep_for > 0:
                time.sleep(sleep_for)
    finally:
        writer.release()

    print(f"Captured {output_path} ({output_path.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()

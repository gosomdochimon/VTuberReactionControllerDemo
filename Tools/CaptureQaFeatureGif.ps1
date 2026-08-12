param(
    [string]$OutputPath = "Docs/VTuberReactionControllerDemo_QA_FeatureDemo.gif"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $projectRoot $OutputPath
$frameDirectory = Join-Path $env:TEMP "vtuber-qa-frames-20260812"
$drawingAssembly = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll"

if (Test-Path -LiteralPath $frameDirectory)
{
    Remove-Item -LiteralPath $frameDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $frameDirectory | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null

$captureSource = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class WindowCaptureQaRun
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr windowHandle, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr windowHandle, ref Point point);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr windowHandle);

    public static void Capture(IntPtr windowHandle, string path, int outputWidth)
    {
        Rect rect;
        GetClientRect(windowHandle, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        Point origin = new Point();
        ClientToScreen(windowHandle, ref origin);

        using (Bitmap source = new Bitmap(width, height, PixelFormat.Format24bppRgb))
        using (Graphics graphics = Graphics.FromImage(source))
        {
            graphics.CopyFromScreen(
                origin.X,
                origin.Y,
                0,
                0,
                new Size(width, height),
                CopyPixelOperation.SourceCopy);

            int outputHeight = (int)Math.Round((double)height * outputWidth / width);
            using (Bitmap scaled = new Bitmap(outputWidth, outputHeight, PixelFormat.Format24bppRgb))
            using (Graphics scaledGraphics = Graphics.FromImage(scaled))
            {
                scaledGraphics.DrawImage(source, 0, 0, outputWidth, outputHeight);
                scaled.Save(path, ImageFormat.Png);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $captureSource -ReferencedAssemblies $drawingAssembly

$game = Get-Process -Name VTuberReactionControllerDemo | Select-Object -First 1
if ($null -eq $game)
{
    throw "VTuberReactionControllerDemo is not running."
}

[WindowCaptureQaRun]::SetForegroundWindow($game.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500

$events = @(
    @("cheer", 1),
    @("shock", 3),
    @("gift", 5),
    @("throw", 7),
    @("spark", 9),
    @("bonk", 11)
)

$startTime = Get-Date
$eventIndex = 0
$frameIndex = 0

while (((Get-Date) - $startTime).TotalSeconds -lt 14)
{
    $elapsedSeconds = ((Get-Date) - $startTime).TotalSeconds
    if ($eventIndex -lt $events.Count -and $elapsedSeconds -ge $events[$eventIndex][1])
    {
        $reactionType = $events[$eventIndex][0]
        & python (Join-Path $projectRoot "Tools/send_reaction.py") $reactionType | Out-Null
        Write-Output "Sent $reactionType at $([math]::Round($elapsedSeconds, 1))s"
        $eventIndex++
    }

    $framePath = Join-Path $frameDirectory ("frame_{0:D3}.png" -f $frameIndex)
    [WindowCaptureQaRun]::Capture($game.MainWindowHandle, $framePath, 640)
    $frameIndex++
    Start-Sleep -Milliseconds 200
}

Add-Type -AssemblyName WindowsBase, PresentationCore

$encoderSource = @'
using System;
using System.IO;
using System.Windows.Media.Imaging;

public static class GifEncoderQa
{
    public static void Encode(string[] framePaths, string outputPath)
    {
        GifBitmapEncoder encoder = new GifBitmapEncoder();
        foreach (string framePath in framePaths)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(framePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            encoder.Frames.Add(BitmapFrame.Create(image));
        }

        using (FileStream stream = File.Create(outputPath))
        {
            encoder.Save(stream);
        }
    }
}
'@

Add-Type -TypeDefinition $encoderSource -ReferencedAssemblies @(
    "WindowsBase",
    "PresentationCore",
    "System.Xaml"
)

$frames = Get-ChildItem -LiteralPath $frameDirectory -Filter "*.png" | Sort-Object Name
[GifEncoderQa]::Encode($frames.FullName, $outputPath)

Write-Output "Captured $($frames.Count) frames"
Get-Item -LiteralPath $outputPath | Select-Object FullName, Length, LastWriteTime

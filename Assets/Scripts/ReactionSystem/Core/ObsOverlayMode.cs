using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ObsOverlayMode : MonoBehaviour
    {
        private const string OverlayArgument = "--obs-overlay";
        private const string KeepHudArgument = "--obs-overlay-show-hud";
        private const string BlueArgument = "--obs-overlay-blue";
        private const string ColorArgumentPrefix = "--obs-overlay-color=";

        private static readonly Color DefaultChromaKeyColor = new Color(0f, 1f, 0f, 1f);
        private static readonly Color BlueChromaKeyColor = new Color(0f, 0f, 1f, 1f);

        [Header("Camera")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Color chromaKeyColor = DefaultChromaKeyColor;
        [SerializeField] private bool disablePostProcessing = true;

        [Header("HUD")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private bool hideHud = true;

        [Header("Diagnostics")]
        [SerializeField] private bool logStatus = true;

        private bool hasApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!HasArgument(args, OverlayArgument))
            {
                return;
            }

            if (FindAnyObjectByType<ObsOverlayMode>() != null)
            {
                return;
            }

            GameObject overlayObject = new GameObject(nameof(ObsOverlayMode));
            ObsOverlayMode overlayMode = overlayObject.AddComponent<ObsOverlayMode>();
            overlayMode.ConfigureFromCommandLine(args);
            overlayMode.Apply();
        }

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (hasApplied)
            {
                return;
            }

            hasApplied = true;

            Camera cameraToConfigure = ResolveTargetCamera();
            if (cameraToConfigure != null)
            {
                ApplyCameraSettings(cameraToConfigure);
            }

            Canvas canvasToConfigure = ResolveHudCanvas();
            if (hideHud && canvasToConfigure != null)
            {
                canvasToConfigure.gameObject.SetActive(false);
            }

            if (logStatus)
            {
                Debug.Log(
                    $"OBS overlay mode enabled. "
                    + $"camera={Format(cameraToConfigure)}, "
                    + $"color=#{ColorUtility.ToHtmlStringRGB(chromaKeyColor)}, "
                    + $"hideHud={hideHud}, "
                    + $"hud={Format(canvasToConfigure)}",
                    this);
            }
        }

        private void ConfigureFromCommandLine(string[] args)
        {
            hideHud = !HasArgument(args, KeepHudArgument);
            chromaKeyColor = HasArgument(args, BlueArgument)
                ? BlueChromaKeyColor
                : ReadColorArgument(args, DefaultChromaKeyColor);
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main != null
                ? Camera.main
                : FindAnyObjectByType<Camera>();

            return targetCamera;
        }

        private Canvas ResolveHudCanvas()
        {
            if (hudCanvas != null)
            {
                return hudCanvas;
            }

            GameObject hudObject = GameObject.Find("ReactionHudCanvas");
            if (hudObject != null)
            {
                hudCanvas = hudObject.GetComponent<Canvas>();
            }

            return hudCanvas;
        }

        private void ApplyCameraSettings(Camera cameraToConfigure)
        {
            cameraToConfigure.clearFlags = CameraClearFlags.SolidColor;
            cameraToConfigure.backgroundColor = chromaKeyColor;

            if (disablePostProcessing)
            {
                TryDisablePostProcessing(cameraToConfigure);
            }
        }

        private static bool HasArgument(string[] args, string argument)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Color ReadColorArgument(string[] args, Color fallbackColor)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith(ColorArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string colorText = arg.Substring(ColorArgumentPrefix.Length).Trim();
                if (TryParseHexColor(colorText, out Color parsedColor))
                {
                    return parsedColor;
                }

                Debug.LogWarning(
                    $"Invalid OBS overlay color '{colorText}'. Use a hex value such as #00FF00.");
            }

            return fallbackColor;
        }

        private static bool TryParseHexColor(string colorText, out Color color)
        {
            color = default;

            string normalized = colorText.StartsWith("#", StringComparison.Ordinal)
                ? colorText.Substring(1)
                : colorText;

            if (normalized.Length != 6)
            {
                return false;
            }

            if (!int.TryParse(
                    normalized.Substring(0, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int red)
                || !int.TryParse(
                    normalized.Substring(2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int green)
                || !int.TryParse(
                    normalized.Substring(4, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int blue))
            {
                return false;
            }

            color = new Color(red / 255f, green / 255f, blue / 255f, 1f);
            return true;
        }

        private static void TryDisablePostProcessing(Camera cameraToConfigure)
        {
            Component[] cameraComponents = cameraToConfigure.GetComponents<Component>();
            for (int i = 0; i < cameraComponents.Length; i++)
            {
                Component component = cameraComponents[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (!string.Equals(type.Name, "UniversalAdditionalCameraData", StringComparison.Ordinal))
                {
                    continue;
                }

                PropertyInfo renderPostProcessing = type.GetProperty(
                    "renderPostProcessing",
                    BindingFlags.Instance | BindingFlags.Public);

                if (renderPostProcessing != null && renderPostProcessing.CanWrite)
                {
                    renderPostProcessing.SetValue(component, false);
                }

                return;
            }
        }

        private static string Format(UnityEngine.Object value)
        {
            return value != null ? value.name : "missing";
        }
    }
}

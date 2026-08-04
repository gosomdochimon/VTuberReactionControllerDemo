using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionLogPresenter : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private ReactionController controller;

        [Header("UI")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Text latestText;
        [SerializeField] private Text historyText;

        [Header("Log")]
        [SerializeField, Min(1)] private int maxEntries = 6;
        [SerializeField] private string currencySuffix = "원";

        private readonly Queue<string> entries = new Queue<string>();
        private bool missingUiWarningLogged;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            controller = GetComponent<ReactionController>();
        }

        private void OnEnable()
        {
            EnsureInitialized();

            if (controller != null)
            {
                controller.ReactionStarted += AddReaction;
            }

            Render();
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.ReactionStarted -= AddReaction;
            }
        }

        private void EnsureInitialized()
        {
            if (controller == null)
            {
                controller = GetComponent<ReactionController>();
            }

            if ((targetCanvas == null || latestText == null || historyText == null) && !missingUiWarningLogged)
            {
                Debug.LogWarning(
                    $"{nameof(ReactionLogPresenter)} needs Editor-authored UI references. Assign ReactionHudCanvas, LatestReactionText, and ReactionHistoryText in the Inspector.",
                    this);
                missingUiWarningLogged = true;
            }
        }

        private void AddReaction(ReactionEvent reactionEvent)
        {
            if (reactionEvent == null)
            {
                return;
            }

            string text = FormatReaction(reactionEvent);
            entries.Enqueue(text);

            while (entries.Count > maxEntries)
            {
                entries.Dequeue();
            }

            Render();
        }

        private void Render()
        {
            if (latestText != null)
            {
                latestText.text = entries.Count > 0
                    ? GetLatestEntry()
                    : "리액션 대기 중";
            }

            if (historyText != null)
            {
                historyText.text = entries.Count > 0
                    ? string.Join("\n", entries)
                    : "아직 표시할 후원 로그가 없습니다.";
            }
        }

        private string GetLatestEntry()
        {
            string latest = string.Empty;
            foreach (string entry in entries)
            {
                latest = entry;
            }

            return latest;
        }

        private string FormatReaction(ReactionEvent reactionEvent)
        {
            string user = string.IsNullOrWhiteSpace(reactionEvent.User)
                ? "Visitor"
                : reactionEvent.User;
            string type = string.IsNullOrWhiteSpace(reactionEvent.Type)
                ? "reaction"
                : reactionEvent.Type;

            if (reactionEvent.Amount > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}님이 {1:N0}{2}을 후원했습니다. ({3})",
                    user,
                    reactionEvent.Amount,
                    currencySuffix,
                    type);
            }

            return $"{user}님이 {type} 리액션을 보냈습니다.";
        }
    }
}

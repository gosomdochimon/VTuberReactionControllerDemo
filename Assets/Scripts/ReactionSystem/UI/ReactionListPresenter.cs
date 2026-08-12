using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionListPresenter : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private ReactionCatalog catalog;

        [Header("UI")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Text listText;

        [Header("Display")]
        [SerializeField] private string currencySuffix = "원";

        private bool missingUiWarningLogged;
        private ReactionCatalog subscribedCatalog;

        private void Awake()
        {
            ValidateUiReferences();
        }

        private void OnEnable()
        {
            ValidateUiReferences();
            SubscribeToCatalog();
            RefreshFromCurrentReactionList();
        }

        private void Start()
        {
            RefreshFromCurrentReactionList();
        }

        private void OnDisable()
        {
            UnsubscribeFromCatalog();
        }

        public void RefreshFromCurrentReactionList()
        {
            ValidateUiReferences();
            SubscribeToCatalog();
            Render(GetCurrentReactionItems());
        }

        public void Configure(ReactionCatalog sourceCatalog)
        {
            if (sourceCatalog == null)
            {
                return;
            }

            if (catalog == sourceCatalog)
            {
                RefreshFromCurrentReactionList();
                return;
            }

            UnsubscribeFromCatalog();
            catalog = sourceCatalog;
            SubscribeToCatalog();
            RefreshFromCurrentReactionList();
        }

        private void ValidateUiReferences()
        {
            if ((targetCanvas == null || listText == null) && !missingUiWarningLogged)
            {
                Debug.LogWarning(
                    $"{nameof(ReactionListPresenter)} needs Editor-authored UI references. Assign ReactionHudCanvas and ReactionListText in the Inspector.",
                    this);
                missingUiWarningLogged = true;
            }
        }

        private IReadOnlyList<ReactionCatalog.ReactionMenuItem> GetCurrentReactionItems()
        {
            return catalog != null
                ? catalog.GetCurrentItems()
                : Array.Empty<ReactionCatalog.ReactionMenuItem>();
        }

        private void SubscribeToCatalog()
        {
            if (subscribedCatalog == catalog)
            {
                return;
            }

            UnsubscribeFromCatalog();

            if (catalog == null)
            {
                return;
            }

            subscribedCatalog = catalog;
            subscribedCatalog.ItemsChanged += HandleItemsChanged;
        }

        private void UnsubscribeFromCatalog()
        {
            if (subscribedCatalog == null)
            {
                return;
            }

            subscribedCatalog.ItemsChanged -= HandleItemsChanged;
            subscribedCatalog = null;
        }

        private void HandleItemsChanged(IReadOnlyList<ReactionCatalog.ReactionMenuItem> currentItems)
        {
            Render(currentItems);
        }

        private void Render(IReadOnlyList<ReactionCatalog.ReactionMenuItem> currentItems)
        {
            if (listText == null)
            {
                return;
            }

            if (currentItems == null || currentItems.Count == 0)
            {
                listText.text = "표시할 리액션이 없습니다.";
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < currentItems.Count; i++)
            {
                ReactionCatalog.ReactionMenuItem item = currentItems[i];
                if (item == null || !item.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "[{0}] {1}  {2:N0}{3}",
                    FormatKey(item.Key),
                    FormatDisplayName(item),
                    item.Amount,
                    currencySuffix);
            }

            listText.text = builder.Length > 0
                ? builder.ToString()
                : "표시할 리액션이 없습니다.";
        }

        private static string FormatKey(Key key)
        {
            return key switch
            {
                Key.Digit0 => "0",
                Key.Digit1 => "1",
                Key.Digit2 => "2",
                Key.Digit3 => "3",
                Key.Digit4 => "4",
                Key.Digit5 => "5",
                Key.Digit6 => "6",
                Key.Digit7 => "7",
                Key.Digit8 => "8",
                Key.Digit9 => "9",
                _ => key.ToString()
            };
        }

        private static string FormatDisplayName(ReactionCatalog.ReactionMenuItem binding)
        {
            string keyName = FormatKey(binding.Key);
            string displayName = string.IsNullOrWhiteSpace(binding.DisplayName)
                ? binding.ReactionType
                : binding.DisplayName.Trim();

            string keyPrefix = keyName + " ";
            if (displayName.StartsWith(keyPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                displayName = displayName.Substring(keyPrefix.Length).TrimStart();
            }

            return string.IsNullOrWhiteSpace(displayName)
                ? binding.ReactionType
                : displayName;
        }
    }
}

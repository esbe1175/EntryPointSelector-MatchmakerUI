using BepInEx.Configuration;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using JsonType;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using archon.EntryPointSelector.MatchmakerUI.Data;

namespace archon.EntryPointSelector.MatchmakerUI.UI
{
    internal sealed class MatchmakerLocationSelectorPanel : MonoBehaviour
    {
        private const string HighlightColorHex = "#D9D3B8";
        private const string RootName = "ArchonLocationSelector";
        private const string OptionsName = "ArchonLocationOptions";

        private static readonly System.Reflection.FieldInfo ConditionsField =
            AccessTools.Field(typeof(MatchMakerSelectionLocationScreen), "_conditions");

        private static readonly System.Reflection.FieldInfo SideField =
            AccessTools.Field(typeof(MatchMakerSelectionLocationScreen), "esideType_0");

        private static readonly System.Reflection.FieldInfo LocationButtonTemplateField =
            AccessTools.Field(typeof(MatchMakerSelectionLocationScreen), "_locationButtonTemplate");

        private MatchMakerSelectionLocationScreen _screen;
        private string _locationId;
        private RectTransform _root;
        private RectTransform _optionsRoot;
        private RectTransform _valueBox;
        private RectTransform _overlayParent;
        private Image _valueBackground;
        private Image _valueBorder;
        private TextMeshProUGUI _labelText;
        private TextMeshProUGUI _valueText;
        private TextMeshProUGUI _infoText;
        private TextMeshProUGUI _arrowText;
        private GameObject _warningIcon;
        private string _stateSignature;
        private bool _expanded;

        public static void EnsureFor(MatchMakerSelectionLocationScreen screen, LocationSettingsClass.Location location)
        {
            if (screen == null)
            {
                return;
            }

            MatchmakerLocationSelectorPanel controller = screen.GetComponent<MatchmakerLocationSelectorPanel>();
            if (controller == null)
            {
                controller = screen.gameObject.AddComponent<MatchmakerLocationSelectorPanel>();
            }

            controller.Bind(screen, location);
        }

        private void Bind(MatchMakerSelectionLocationScreen screen, LocationSettingsClass.Location location)
        {
            _screen = screen;
            _locationId = OriginalPluginAccessor.NormalizeLocationId(location?.Id);
            EnsureUi();
            Refresh(force: true);
        }

        private void LateUpdate()
        {
            Refresh(force: false);
        }

        private void OnDisable()
        {
            SetExpanded(false);
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            SetExpanded(false);
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        private void EnsureUi()
        {
            if (_screen == null)
            {
                return;
            }

            LocationConditionsPanel conditionsPanel = GetConditionsPanel();
            RectTransform conditionsRect = conditionsPanel != null ? conditionsPanel.transform as RectTransform : null;
            RectTransform tilesRect = conditionsRect != null ? FindChildRecursive(conditionsRect, "Tiles") as RectTransform : null;
            TextMeshProUGUI phaseCaptionTemplate = conditionsRect != null ? FindChildRecursive(conditionsRect, "Phase Caption")?.GetComponent<TextMeshProUGUI>() : null;
            if (conditionsRect == null || tilesRect == null || phaseCaptionTemplate == null)
            {
                return;
            }

            if (_root != null)
            {
                if (_root.parent != conditionsRect)
                {
                    Destroy(_root.gameObject);
                    _root = null;
                }
                else
                {
                    return;
                }
            }

            GameObject rootObject = new GameObject(RootName, typeof(RectTransform));
            rootObject.transform.SetParent(conditionsRect, false);
            _root = rootObject.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 0f);
            _root.anchorMax = new Vector2(1f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.offsetMin = new Vector2(0f, 100f);
            _root.offsetMax = new Vector2(0f, 138f);
            _root.SetAsLastSibling();

            Image tilesImage = tilesRect.GetComponent<Image>();

            GameObject backingObject = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            backingObject.transform.SetParent(_root, false);
            Image backing = backingObject.GetComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.8f);
            backing.raycastTarget = false;
            Stretch(backing.rectTransform);

            GameObject tileOverlayObject = new GameObject("TileOverlay", typeof(RectTransform), typeof(Image));
            tileOverlayObject.transform.SetParent(_root, false);
            Image tileOverlay = tileOverlayObject.GetComponent<Image>();
            tileOverlay.sprite = tilesImage.sprite;
            tileOverlay.material = tilesImage.material;
            tileOverlay.type = tilesImage.type;
            tileOverlay.color = new Color(1f, 1f, 1f, 0.28f);
            tileOverlay.raycastTarget = false;
            Stretch(tileOverlay.rectTransform);

            _labelText = CreateStyledText(phaseCaptionTemplate, _root, "SelectorLabel");
            RectTransform labelRect = _labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(14f, 0f);
            labelRect.sizeDelta = new Vector2(150f, 0f);
            _labelText.text = "SELECT INSERTION POINT:";
            _labelText.fontSize = Mathf.Max(phaseCaptionTemplate.fontSize - 0.5f, 10f);
            _labelText.alignment = TextAlignmentOptions.MidlineLeft;

            _infoText = CreateStyledText(phaseCaptionTemplate, _root, "InfoText");
            RectTransform infoRect = _infoText.rectTransform;
            infoRect.anchorMin = new Vector2(0f, 0f);
            infoRect.anchorMax = new Vector2(1f, 1f);
            infoRect.offsetMin = new Vector2(14f, 0f);
            infoRect.offsetMax = new Vector2(-14f, 0f);
            _infoText.fontSize = Mathf.Max(phaseCaptionTemplate.fontSize - 0.25f, 10f);
            _infoText.alignment = TextAlignmentOptions.MidlineLeft;
            _infoText.enableWordWrapping = true;
            _infoText.overflowMode = TextOverflowModes.Ellipsis;
            _infoText.color = new Color(0.784f, 0.78f, 0.729f, 1f);
            _infoText.gameObject.SetActive(false);

            GameObject valueBoxObject = new GameObject("SelectorButton", typeof(RectTransform), typeof(Image), typeof(Button));
            valueBoxObject.transform.SetParent(_root, false);
            _valueBox = valueBoxObject.GetComponent<RectTransform>();
            _valueBox.anchorMin = new Vector2(0f, 0.5f);
            _valueBox.anchorMax = new Vector2(1f, 0.5f);
            _valueBox.pivot = new Vector2(0f, 0.5f);
            _valueBox.offsetMin = new Vector2(156f, -13f);
            _valueBox.offsetMax = new Vector2(-12f, 13f);

            _valueBackground = valueBoxObject.GetComponent<Image>();
            _valueBackground.color = new Color(0f, 0f, 0f, 1f);

            Button valueButton = valueBoxObject.GetComponent<Button>();
            valueButton.transition = Selectable.Transition.None;
            valueButton.onClick.AddListener(ToggleExpanded);

            GameObject borderObject = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObject.transform.SetParent(_valueBox, false);
            _valueBorder = borderObject.GetComponent<Image>();
            _valueBorder.color = new Color(0.784f, 0.78f, 0.729f, 0.8f);
            _valueBorder.raycastTarget = false;
            Stretch(_valueBorder.rectTransform);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(_valueBox, false);
            Image fill = fillObject.GetComponent<Image>();
            fill.color = new Color(0f, 0f, 0f, 1f);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(1f, 1f);
            fill.rectTransform.offsetMax = new Vector2(-1f, -1f);

            _valueText = CreateStyledText(phaseCaptionTemplate, _valueBox, "SizeLabel");
            RectTransform valueTextRect = _valueText.rectTransform;
            valueTextRect.anchorMin = Vector2.zero;
            valueTextRect.anchorMax = Vector2.one;
            valueTextRect.offsetMin = new Vector2(12f, 0f);
            valueTextRect.offsetMax = new Vector2(-58f, 0f);
            _valueText.fontSize = phaseCaptionTemplate.fontSize + 3f;
            _valueText.alignment = TextAlignmentOptions.Midline;
            _valueText.color = new Color(0.784f, 0.78f, 0.729f, 1f);

            GameObject arrowBoxObject = new GameObject("ArrowBox", typeof(RectTransform), typeof(Image));
            arrowBoxObject.transform.SetParent(_valueBox, false);
            Image arrowBox = arrowBoxObject.GetComponent<Image>();
            arrowBox.color = new Color(1f, 1f, 1f, 0.2f);
            arrowBox.raycastTarget = false;
            RectTransform arrowBoxRect = arrowBox.rectTransform;
            arrowBoxRect.anchorMin = new Vector2(1f, 0.5f);
            arrowBoxRect.anchorMax = new Vector2(1f, 0.5f);
            arrowBoxRect.pivot = new Vector2(1f, 0.5f);
            arrowBoxRect.anchoredPosition = new Vector2(-5f, 0f);
            arrowBoxRect.sizeDelta = new Vector2(18f, 18f);

            _arrowText = CreateStyledText(phaseCaptionTemplate, arrowBoxRect, "ExpandArrow");
            Stretch(_arrowText.rectTransform);
            _arrowText.fontSize = phaseCaptionTemplate.fontSize + 1f;
            _arrowText.alignment = TextAlignmentOptions.Midline;
            _arrowText.text = "v";

            _warningIcon = CreateWarningIcon(_valueBox);

            GameObject optionsObject = new GameObject(OptionsName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            optionsObject.transform.SetParent(_root, false);
            _optionsRoot = optionsObject.GetComponent<RectTransform>();
            _optionsRoot.anchorMin = new Vector2(0f, 1f);
            _optionsRoot.anchorMax = new Vector2(1f, 1f);
            _optionsRoot.pivot = new Vector2(0.5f, 0f);
            _optionsRoot.offsetMin = new Vector2(156f, 6f);
            _optionsRoot.offsetMax = new Vector2(-12f, 6f);

            Canvas optionsCanvas = optionsObject.GetComponent<Canvas>();
            optionsCanvas.overrideSorting = true;
            optionsCanvas.sortingOrder = 500;

            Image optionsBackground = optionsObject.GetComponent<Image>();
            optionsBackground.color = new Color(0f, 0f, 0f, 0.8f);

            VerticalLayoutGroup layout = optionsObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 2f;

            ContentSizeFitter fitter = optionsObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _optionsRoot.gameObject.SetActive(false);
        }

        private void Refresh(bool force)
        {
            EnsureUi();
            if (_root == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_locationId))
            {
                _root.gameObject.SetActive(false);
                SetExpanded(false);
                return;
            }

            bool isScav = IsScavSelected();
            ConfigEntry<string> entry = OriginalPluginAccessor.GetExfilConfigEntry(_locationId, isScav);
            List<string> choices = OriginalPluginAccessor.GetExfilChoices(_locationId, isScav);
            string selectedPreference = entry != null ? entry.Value : null;
            bool hasSavedPosition = OriginalPluginAccessor.TryGetSavedPosition(_locationId, out Vector3 savedPosition);
            RuntimeExtractMatch nearestExtract = hasSavedPosition
                ? RuntimeExtractCatalogStore.FindNearestExtract(_locationId, savedPosition, isScav)
                : null;
            bool useLastExfil = OriginalPluginAccessor.UseLastExfilEnabled && !OriginalPluginAccessor.IsHomeComfortsInstalled;
            bool chooseInfil = OriginalPluginAccessor.ChooseInfilEnabled;
            bool useStandardInsertion = !chooseInfil && !useLastExfil;

            bool canShow = IsConditionsVisible() &&
                           !string.IsNullOrWhiteSpace(_locationId) &&
                           (chooseInfil
                               ? entry != null && choices != null && choices.Count > 0
                               : true);

            _root.gameObject.SetActive(canShow);
            if (!canShow)
            {
                SetExpanded(false);
                return;
            }

            string effectiveSelection = selectedPreference;
            string signature = string.Join("|",
                _locationId,
                isScav ? "scav" : "pmc",
                selectedPreference ?? string.Empty,
                nearestExtract?.DisplayName ?? string.Empty,
                hasSavedPosition,
                useLastExfil,
                chooseInfil,
                useStandardInsertion,
                choices.Count);

            if (!force && signature == _stateSignature)
            {
                return;
            }

            _stateSignature = signature;
            _valueText.text = effectiveSelection ?? string.Empty;
            _valueText.color = new Color(0.784f, 0.78f, 0.729f, 1f);
            if (_warningIcon != null)
            {
                _warningIcon.SetActive(useLastExfil && hasSavedPosition);
            }

            bool showSelector = chooseInfil;
            bool showLastExfilInfo = !chooseInfil && useLastExfil;
            bool showStandardInfo = !chooseInfil && !useLastExfil;

            _labelText.gameObject.SetActive(showSelector);
            _valueBox.gameObject.SetActive(showSelector);
            _infoText.gameObject.SetActive(showLastExfilInfo || showStandardInfo);
            _root.offsetMin = new Vector2(0f, 100f);
            _root.offsetMax = (showLastExfilInfo || showStandardInfo) ? new Vector2(0f, 150f) : new Vector2(0f, 138f);

            if (showLastExfilInfo)
            {
                _infoText.text = BuildLastExfilInfoText(hasSavedPosition, nearestExtract);
                SetExpanded(false);
            }
            else if (showStandardInfo)
            {
                _infoText.text = BuildStandardInsertionInfoText();
                SetExpanded(false);
            }
            else
            {
                _infoText.text = string.Empty;
            }

            if (showSelector)
            {
                RebuildOptions(choices, selectedPreference, entry);
            }
            else
            {
                SetExpanded(false);
                ClearOptions();
            }
        }

        private void RebuildOptions(List<string> choices, string selectedPreference, ConfigEntry<string> entry)
        {
            if (_optionsRoot == null)
            {
                return;
            }

            ClearOptions();

            LocationConditionsPanel conditionsPanel = GetConditionsPanel();
            TextMeshProUGUI phaseCaptionTemplate = conditionsPanel != null
                ? FindChildRecursive(conditionsPanel.transform, "Phase Caption")?.GetComponent<TextMeshProUGUI>()
                : null;
            if (phaseCaptionTemplate == null || choices == null)
            {
                return;
            }

            foreach (string choice in choices)
            {
                GameObject optionObject = new GameObject("Option", typeof(RectTransform), typeof(Image), typeof(Button));
                optionObject.transform.SetParent(_optionsRoot, false);
                RectTransform optionRect = optionObject.GetComponent<RectTransform>();
                optionRect.sizeDelta = new Vector2(0f, 26f);

                Image optionBackground = optionObject.GetComponent<Image>();
                bool isSelected = string.Equals(choice, selectedPreference, StringComparison.OrdinalIgnoreCase);
                optionBackground.color = isSelected
                    ? new Color(1f, 1f, 1f, 0.14f)
                    : new Color(0f, 0f, 0f, 0f);

                Button optionButton = optionObject.GetComponent<Button>();
                optionButton.transition = Selectable.Transition.None;
                OptionHoverState hoverState = optionObject.AddComponent<OptionHoverState>();
                hoverState.Initialize(optionBackground, isSelected);

                TextMeshProUGUI optionText = CreateStyledText(phaseCaptionTemplate, optionRect, "OptionLabel");
                Stretch(optionText.rectTransform);
                optionText.rectTransform.offsetMin = new Vector2(10f, 0f);
                optionText.rectTransform.offsetMax = new Vector2(-10f, 0f);
                optionText.text = choice;
                optionText.fontSize = phaseCaptionTemplate.fontSize + 1.5f;
                optionText.alignment = TextAlignmentOptions.Midline;
                optionText.color = isSelected
                    ? new Color(0.784f, 0.78f, 0.729f, 1f)
                    : new Color(0.784f, 0.78f, 0.729f, 0.8f);
                optionText.raycastTarget = false;

                optionButton.onClick.AddListener(() =>
                {
                    entry.Value = choice;
                    SetExpanded(false);
                    Refresh(force: true);
                });
            }
        }

        private void ToggleExpanded()
        {
            if (!OriginalPluginAccessor.ChooseInfilEnabled)
            {
                return;
            }

            SetExpanded(!_expanded);
        }

        private void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_optionsRoot != null)
            {
                MoveOptionsToOverlay(expanded);
                _optionsRoot.gameObject.SetActive(expanded);
                if (expanded)
                {
                    _optionsRoot.SetAsLastSibling();
                }
            }

            if (_arrowText != null)
            {
                _arrowText.text = expanded ? "^" : "v";
            }
        }

        private void MoveOptionsToOverlay(bool expanded)
        {
            if (_optionsRoot == null || _valueBox == null || _root == null || _screen == null)
            {
                return;
            }

            if (expanded)
            {
                RectTransform overlayParent = GetOverlayParent();
                if (overlayParent == null)
                {
                    return;
                }

                Vector3[] worldCorners = new Vector3[4];
                _valueBox.GetWorldCorners(worldCorners);

                _optionsRoot.SetParent(overlayParent, true);
                _optionsRoot.anchorMin = new Vector2(0f, 1f);
                _optionsRoot.anchorMax = new Vector2(0f, 1f);
                _optionsRoot.pivot = new Vector2(0f, 0f);
                _optionsRoot.position = worldCorners[1] + new Vector3(0f, 6f, 0f);

                float parentScaleX = Mathf.Approximately(overlayParent.lossyScale.x, 0f) ? 1f : overlayParent.lossyScale.x;
                float width = (_valueBox.rect.width * _valueBox.lossyScale.x) / parentScaleX;
                _optionsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
            else if (_optionsRoot.parent != _root)
            {
                _optionsRoot.SetParent(_root, false);
                _optionsRoot.anchorMin = new Vector2(0f, 1f);
                _optionsRoot.anchorMax = new Vector2(1f, 1f);
                _optionsRoot.pivot = new Vector2(0.5f, 0f);
                _optionsRoot.offsetMin = new Vector2(156f, 6f);
                _optionsRoot.offsetMax = new Vector2(-12f, 6f);
            }
        }

        private RectTransform GetOverlayParent()
        {
            if (_overlayParent != null)
            {
                return _overlayParent;
            }

            _overlayParent = _screen.transform as RectTransform;
            Transform parent = _screen.transform.parent;
            if (parent is RectTransform parentRect)
            {
                _overlayParent = parentRect;
            }

            return _overlayParent;
        }

        private LocationConditionsPanel GetConditionsPanel()
        {
            return ConditionsField?.GetValue(_screen) as LocationConditionsPanel;
        }

        private bool IsConditionsVisible()
        {
            LocationConditionsPanel conditionsPanel = GetConditionsPanel();
            return conditionsPanel != null &&
                   conditionsPanel.gameObject.activeInHierarchy &&
                   conditionsPanel.gameObject.activeSelf;
        }

        private bool IsScavSelected()
        {
            object sideValue = SideField?.GetValue(_screen);
            return string.Equals(sideValue?.ToString(), "Savage", StringComparison.OrdinalIgnoreCase);
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform nestedMatch = FindChildRecursive(child, childName);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }

        private static TextMeshProUGUI CreateStyledText(TextMeshProUGUI template, Transform parent, string name)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.fontSize = template.fontSize;
            text.color = template.color;
            text.characterSpacing = template.characterSpacing;
            text.wordSpacing = template.wordSpacing;
            text.lineSpacing = template.lineSpacing;
            text.paragraphSpacing = template.paragraphSpacing;
            text.enableKerning = template.enableKerning;
            text.extraPadding = template.extraPadding;
            text.richText = template.richText;
            text.isOverlay = template.isOverlay;
            text.alignment = template.alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ClearOptions()
        {
            if (_optionsRoot == null)
            {
                return;
            }

            for (int i = _optionsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_optionsRoot.GetChild(i).gameObject);
            }
        }

        private string BuildLastExfilInfoText(bool hasSavedPosition, RuntimeExtractMatch nearestExtract)
        {
            string highlightedLocation = Highlight(GetLocationDisplayName());
            if (!hasSavedPosition)
            {
                return $"You will deploy near your last extraction point on {highlightedLocation}.";
            }

            if (nearestExtract == null || string.IsNullOrWhiteSpace(nearestExtract.DisplayName))
            {
                return $"You will deploy near your last extraction point on {highlightedLocation}.";
            }

            string highlightedExtract = Highlight(nearestExtract.DisplayName);
            return $"You will deploy near your last extraction point. Your last extraction point on {highlightedLocation} was near {highlightedExtract}.";
        }

        private string BuildStandardInsertionInfoText()
        {
            return $"You will deploy at a standard insertion point on {Highlight(GetLocationDisplayName())}.";
        }

        private static string Highlight(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return $"<color={HighlightColorHex}>{value}</color>";
        }

        private string GetLocationDisplayName()
        {
            if (string.IsNullOrWhiteSpace(_locationId))
            {
                return "this map";
            }

            switch (_locationId)
            {
                case "bigmap":
                    return "Customs";
                case "factory4_day":
                case "factory4_night":
                    return "Factory";
                case "rezervbase":
                    return "Reserve";
                case "tarkovstreets":
                    return "Streets of Tarkov";
                case "sandbox":
                case "sandbox_high":
                    return "Ground Zero";
                case "laboratory":
                    return "Labs";
                default:
                    return char.ToUpperInvariant(_locationId[0]) + _locationId.Substring(1);
            }
        }

        private GameObject CreateWarningIcon(Transform parent)
        {
            Sprite warningSprite = ResolveWarningSprite();
            if (warningSprite == null)
            {
                return null;
            }

            GameObject icon = new GameObject("ModeIndicator", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(parent, false);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.sprite = warningSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform iconRect = icon.transform as RectTransform;
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-30f, 0f);
            iconRect.sizeDelta = new Vector2(14f, 14f);
            icon.SetActive(false);
            return icon;
        }

        private Sprite ResolveWarningSprite()
        {
            LocationButton template = LocationButtonTemplateField?.GetValue(_screen) as LocationButton;
            if (template == null)
            {
                return null;
            }

            Transform iconTransform = FindChildRecursive(template.transform, "New Icon");
            if (iconTransform == null)
            {
                iconTransform = FindChildRecursive(template.transform, "_newIcon");
            }

            Image image = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private sealed class OptionHoverState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private static readonly Color SelectedColor = new Color(1f, 1f, 1f, 0.14f);
            private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.08f);
            private static readonly Color TransparentColor = new Color(0f, 0f, 0f, 0f);

            private Image _background;
            private bool _selected;

            public void Initialize(Image background, bool selected)
            {
                _background = background;
                _selected = selected;
                ApplyDefault();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (_background != null)
                {
                    _background.color = HoverColor;
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                ApplyDefault();
            }

            private void ApplyDefault()
            {
                if (_background != null)
                {
                    _background.color = _selected ? SelectedColor : TransparentColor;
                }
            }
        }
    }
}

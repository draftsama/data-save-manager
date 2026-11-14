#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OperatorConsole
{
    /// <summary>Builds a Runtime Panel's whole UI in code, so a Panel Definition needs no prefab.</summary>
    internal static class OCPanelFactory
    {
        private static readonly OCPanelLayout Defaults = new();
        private const float SliderInputWidth = 100f;

        /// <summary>
        /// Builds a Runtime Panel under <paramref name="parent"/>, which must be inactive so no component
        /// wakes before the panel has its Panel Definition. Returns null, after logging an error, when
        /// TextMeshPro has no default font.
        /// </summary>
        public static OCRuntimePanel? Create(Transform parent)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                Debug.LogError("OC: TMP_Settings.defaultFontAsset is null, so no Runtime Panel was created. Import TextMeshPro's " +
                               "\"TMP Essential Resources\" (Window > TextMeshPro > Import TMP Essential Resources).");
                return null;
            }

            var rootGo = new GameObject("OCRuntimePanel", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var canvas = rootGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = rootGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            rootGo.AddComponent<GraphicRaycaster>();

            // "Panel" is the visible surface (_root): toggled active/inactive by OCRuntimePanel.Show/Hide.
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(rootGo.transform, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = Defaults.PanelSize;
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = Defaults.BackgroundColor;

            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 16, 16);
            panelLayout.spacing = 12f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            BuildHeader(panel, font);
            var (tabBar, settingsTab, commandsTab) = BuildTabBar(panel, font);
            var container = BuildScrollView(panel, "SettingsView");
            var commandContainer = BuildScrollView(panel, "CommandsView");
            var settingsView = ViewOf(container);
            var commandsView = ViewOf(commandContainer);
            var (saveButton, resetButton, closeButton) = BuildFooter(panel, font);
            var (confirmDialog, confirmCancelButton, confirmResetButton, confirmMessage, confirmButtonLabel) = BuildConfirmDialog(panel, font);
            tabBar.SetActive(false);
            commandsView.SetActive(false);
            var lockScreen = BuildLockScreen(rootGo, font);
            var templates = BuildTemplates(rootGo.transform, font);

            var panelComponent = rootGo.AddComponent<OCRuntimePanel>();
            OCFieldWiring.Set(panelComponent, "_templates", templates);
            OCFieldWiring.Set(panelComponent, "_root", panel);
            OCFieldWiring.Set(panelComponent, "_container", container);
            OCFieldWiring.Set(panelComponent, "_saveButton", saveButton);
            OCFieldWiring.Set(panelComponent, "_resetButton", resetButton);
            OCFieldWiring.Set(panelComponent, "_closeButton", closeButton);
            OCFieldWiring.Set(panelComponent, "_confirmDialog", confirmDialog);
            OCFieldWiring.Set(panelComponent, "_confirmCancelButton", confirmCancelButton);
            OCFieldWiring.Set(panelComponent, "_confirmResetButton", confirmResetButton);
            OCFieldWiring.Set(panelComponent, "_tabBar", tabBar);
            OCFieldWiring.Set(panelComponent, "_settingsTab", settingsTab);
            OCFieldWiring.Set(panelComponent, "_commandsTab", commandsTab);
            OCFieldWiring.Set(panelComponent, "_settingsView", settingsView);
            OCFieldWiring.Set(panelComponent, "_commandsView", commandsView);
            OCFieldWiring.Set(panelComponent, "_commandContainer", commandContainer);
            OCFieldWiring.Set(panelComponent, "_confirmMessage", confirmMessage);
            OCFieldWiring.Set(panelComponent, "_confirmButtonLabel", confirmButtonLabel);
            OCFieldWiring.Set(panelComponent, "_lockScreen", lockScreen);
            return panelComponent;
        }

        /// <summary>Builds the row templates under an inactive holder: the templates never wake, and their clones start active.</summary>
        private static OCWidgetTemplates BuildTemplates(Transform root, TMP_FontAsset font)
        {
            var holder = new GameObject("Templates", typeof(RectTransform));
            holder.transform.SetParent(root, false);
            holder.SetActive(false);
            var parent = holder.transform;

            return new OCWidgetTemplates
            {
                BoolWidget = BuildBoolWidget(parent, font),
                IntWidget = BuildIntWidget(parent, font),
                FloatWidget = BuildFloatWidget(parent, font),
                StringWidget = BuildStringWidget(parent, font),
                Vector2Widget = BuildVector2Widget(parent, font),
                Vector3Widget = BuildVector3Widget(parent, font),
                ColorWidget = BuildColorWidget(parent, font),
                IntSliderWidget = BuildIntSliderWidget(parent, font),
                FloatSliderWidget = BuildFloatSliderWidget(parent, font),
                DropdownWidget = BuildDropdownWidget(parent, font),
                HeaderRow = BuildHeaderRow(parent, font),
                CommandRow = BuildCommandRow(parent, font),
                EntryActionBar = BuildEntryActionBar(parent, font),
                EntryActionError = BuildEntryActionError(parent, font),
            };
        }

        // ---- Row templates ----

        private static BoolWidget BuildBoolWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("BoolWidget", parent);
            var label = CreateRowLabel(row, font);
            var toggle = CreateRowToggle(row);

            var widget = row.AddComponent<BoolWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_toggle", toggle);

            return row.GetComponent<BoolWidget>();
        }

        private static IntWidget BuildIntWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("IntWidget", parent);
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.IntegerNumber);

            var widget = row.AddComponent<IntWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_input", input);

            return row.GetComponent<IntWidget>();
        }

        private static FloatWidget BuildFloatWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("FloatWidget", parent);
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<FloatWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_input", input);

            return row.GetComponent<FloatWidget>();
        }

        private static StringWidget BuildStringWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("StringWidget", parent);
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.Standard);

            var widget = row.AddComponent<StringWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_input", input);

            return row.GetComponent<StringWidget>();
        }

        private static Vector2Widget BuildVector2Widget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("Vector2Widget", parent);
            var label = CreateRowLabel(row, font);
            CreateAxisLabel(row, font, "x:");
            var xInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            CreateAxisLabel(row, font, "y:");
            var yInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<Vector2Widget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_xInput", xInput);
            OCFieldWiring.Set(widget, "_yInput", yInput);

            return row.GetComponent<Vector2Widget>();
        }

        private static Vector3Widget BuildVector3Widget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("Vector3Widget", parent);
            var label = CreateRowLabel(row, font);
            CreateAxisLabel(row, font, "x:");
            var xInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            CreateAxisLabel(row, font, "y:");
            var yInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            CreateAxisLabel(row, font, "z:");
            var zInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<Vector3Widget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_xInput", xInput);
            OCFieldWiring.Set(widget, "_yInput", yInput);
            OCFieldWiring.Set(widget, "_zInput", zInput);

            return row.GetComponent<Vector3Widget>();
        }

        private static ColorWidget BuildColorWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("ColorWidget", parent);
            var label = CreateRowLabel(row, font);
            var hexInput = CreateRowInput(row, font, TMP_InputField.ContentType.Standard);
            hexInput.characterLimit = 9;
            if (hexInput.placeholder is TextMeshProUGUI placeholder) placeholder.text = "#RRGGBBAA";
            var swatch = CreateRowSwatch(row);

            var widget = row.AddComponent<ColorWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_hexInput", hexInput);
            OCFieldWiring.Set(widget, "_swatch", swatch);

            return row.GetComponent<ColorWidget>();
        }

        private static IntSliderWidget BuildIntSliderWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("IntSliderWidget", parent);
            var label = CreateRowLabel(row, font);
            var slider = CreateRowSlider(row);
            var input = CreateSliderValueInput(row, font, TMP_InputField.ContentType.IntegerNumber);

            var widget = row.AddComponent<IntSliderWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_slider", slider);
            OCFieldWiring.Set(widget, "_input", input);

            return row.GetComponent<IntSliderWidget>();
        }

        private static FloatSliderWidget BuildFloatSliderWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("FloatSliderWidget", parent);
            var label = CreateRowLabel(row, font);
            var slider = CreateRowSlider(row);
            var input = CreateSliderValueInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<FloatSliderWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_slider", slider);
            OCFieldWiring.Set(widget, "_input", input);

            return row.GetComponent<FloatSliderWidget>();
        }

        private static DropdownWidget BuildDropdownWidget(Transform parent, TMP_FontAsset font)
        {
            var row = CreateRowRoot("DropdownWidget", parent);
            var label = CreateRowLabel(row, font);

            var go = TMP_DefaultControls.CreateDropdown(OCUiSprites.TmpUiResources());
            go.transform.SetParent(row.transform, false);
            go.name = "Dropdown";

            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            if (dropdown.captionText != null)
            {
                dropdown.captionText.font = font;
                dropdown.captionText.fontSize = Defaults.FontSize;
            }
            if (dropdown.itemText != null)
            {
                dropdown.itemText.font = font;
                dropdown.itemText.fontSize = Defaults.FontSize;
                var item = dropdown.itemText.transform.parent as RectTransform;
                if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, Defaults.RowHeight - 4f);
            }
            if (dropdown.template != null)
                dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 240f);

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            var widget = row.AddComponent<DropdownWidget>();
            OCFieldWiring.Set(widget, "_label", label);
            OCFieldWiring.Set(widget, "_dropdown", dropdown);

            return row.GetComponent<DropdownWidget>();
        }

        private static OCHeaderRow BuildHeaderRow(Transform parent, TMP_FontAsset font)
        {
            var row = new GameObject("HeaderRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var group = row.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(4, 4, Mathf.RoundToInt(Defaults.HeaderSpacing), 0);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(row.transform, false);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Header";
            text.font = font;
            text.fontSize = Defaults.HeaderFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.75f, 0.85f, 1f, 1f);
            text.raycastTarget = false;

            var widget = row.AddComponent<OCHeaderRow>();
            OCFieldWiring.Set(widget, "_text", text);

            return row.GetComponent<OCHeaderRow>();
        }

        private static OCCommandRow BuildCommandRow(Transform parent, TMP_FontAsset font)
        {
            var row = new GameObject("CommandRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(4, 4, 8, 8);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            row.AddComponent<LayoutElement>().minHeight = Defaults.RowHeight;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(row.transform, false);
            var textLayout = textGo.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2f;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            textGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var title = CreateCommandText(textGo, "Title", font, Defaults.FontSize, Color.white);
            var description = CreateCommandText(textGo, "Description", font, Defaults.CommandDescriptionFontSize, new Color(0.6f, 0.63f, 0.69f, 1f));
            var error = CreateCommandText(textGo, "Error", font, Defaults.CommandDescriptionFontSize, new Color(0.89f, 0.34f, 0.30f, 1f));
            error.gameObject.SetActive(false);

            var statusGo = new GameObject("StatusDot", typeof(RectTransform));
            statusGo.transform.SetParent(row.transform, false);
            var statusDot = statusGo.AddComponent<Image>();
            statusDot.sprite = OCUiSprites.Knob;
            statusDot.preserveAspect = true;
            statusDot.raycastTarget = false;
            statusDot.enabled = false;
            var statusLayout = statusGo.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 14f;
            statusLayout.preferredHeight = 14f;

            var buttonGo = TMP_DefaultControls.CreateButton(OCUiSprites.TmpUiResources());
            buttonGo.transform.SetParent(row.transform, false);
            buttonGo.name = "RunButton";
            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = Defaults.CommandButtonWidth;
            buttonLayout.preferredHeight = Defaults.RowHeight;
            var runLabel = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            runLabel.text = "Run";
            runLabel.font = font;
            runLabel.fontSize = Defaults.FontSize;

            var widget = row.AddComponent<OCCommandRow>();
            OCFieldWiring.Set(widget, "_title", title);
            OCFieldWiring.Set(widget, "_description", description);
            OCFieldWiring.Set(widget, "_statusDot", statusDot);
            OCFieldWiring.Set(widget, "_error", error);
            OCFieldWiring.Set(widget, "_runButton", buttonGo.GetComponent<Button>());
            OCFieldWiring.Set(widget, "_runLabel", runLabel);

            return row.GetComponent<OCCommandRow>();
        }

        private static OCEntryActionBar BuildEntryActionBar(Transform parent, TMP_FontAsset font)
        {
            var bar = new GameObject("EntryActionBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var barLayout = bar.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 8f;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = true;
            barLayout.childAlignment = TextAnchor.MiddleRight;
            bar.AddComponent<LayoutElement>().flexibleWidth = 0f;

            var buttonGo = TMP_DefaultControls.CreateButton(OCUiSprites.TmpUiResources());
            buttonGo.transform.SetParent(bar.transform, false);
            buttonGo.name = "ButtonTemplate";
            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = Defaults.EntryActionButtonWidth;
            buttonLayout.flexibleWidth = 0f;
            var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            label.text = "Action";
            label.font = font;
            label.fontSize = Defaults.FontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            buttonGo.SetActive(false);

            var component = bar.AddComponent<OCEntryActionBar>();
            OCFieldWiring.Set(component, "_buttonTemplate", buttonGo.GetComponent<Button>());

            return bar.GetComponent<OCEntryActionBar>();
        }

        private static OCEntryActionError BuildEntryActionError(Transform parent, TMP_FontAsset font)
        {
            var row = new GameObject("EntryActionError", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(Mathf.RoundToInt(Defaults.LabelWidth) + 12, 4, 0, 4);
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var text = CreateCommandText(row, "Text", font, Defaults.EntryActionErrorFontSize, new Color(0.89f, 0.34f, 0.30f, 1f));

            var component = row.AddComponent<OCEntryActionError>();
            OCFieldWiring.Set(component, "_text", text);
            row.SetActive(false);

            return row.GetComponent<OCEntryActionError>();
        }

        private static TextMeshProUGUI CreateCommandText(GameObject parent, string goName, TMP_FontAsset font, float fontSize, Color color)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = goName;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Builds the full-screen Lock Screen as a sibling after the Panel, so it covers the screen whatever the panel size; left inactive.</summary>
        private static OCLockScreen BuildLockScreen(GameObject canvasRoot, TMP_FontAsset font)
        {
            var go = new GameObject("LockScreen", typeof(RectTransform));
            go.transform.SetParent(canvasRoot.transform, false);
            Stretch(go.GetComponent<RectTransform>());
            var backdrop = go.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.035f, 0.047f, Defaults.LockBackgroundAlpha);
            backdrop.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0.5f, 0.5f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 28f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childAlignment = TextAnchor.MiddleCenter;
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateCommandText(content, "Title", font, 34f, new Color(0.95f, 0.96f, 0.97f, 1f));
            title.text = "Enter Passcode";
            title.alignment = TextAlignmentOptions.Center;

            var dotSlot = new GameObject("DotSlot", typeof(RectTransform));
            dotSlot.transform.SetParent(content.transform, false);
            var dotSlotLayout = dotSlot.AddComponent<LayoutElement>();
            dotSlotLayout.preferredHeight = Defaults.LockDotSize;

            var dotRow = new GameObject("Dots", typeof(RectTransform));
            dotRow.transform.SetParent(dotSlot.transform, false);
            var dotRowRect = dotRow.GetComponent<RectTransform>();
            dotRowRect.anchorMin = dotRowRect.anchorMax = dotRowRect.pivot = new Vector2(0.5f, 0.5f);
            var dotRowLayout = dotRow.AddComponent<HorizontalLayoutGroup>();
            dotRowLayout.spacing = Defaults.LockDotSize * 1.1f;
            dotRowLayout.childControlWidth = true;
            dotRowLayout.childControlHeight = true;
            dotRowLayout.childForceExpandWidth = false;
            dotRowLayout.childForceExpandHeight = false;
            dotRowLayout.childAlignment = TextAnchor.MiddleCenter;
            var dotRowFitter = dotRow.AddComponent<ContentSizeFitter>();
            dotRowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            dotRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dots = new Image[OCPasscode.MaxLength];
            for (var i = 0; i < dots.Length; i++)
            {
                var dot = new GameObject($"Dot{i + 1}", typeof(RectTransform));
                dot.transform.SetParent(dotRow.transform, false);
                var image = dot.AddComponent<Image>();
                image.raycastTarget = false;
                var element = dot.AddComponent<LayoutElement>();
                element.preferredWidth = Defaults.LockDotSize;
                element.preferredHeight = Defaults.LockDotSize;
                dots[i] = image;
            }

            var error = CreateCommandText(content, "Error", font, 22f, new Color(1f, 0.42f, 0.42f, 1f));
            error.text = string.Empty;
            error.alignment = TextAlignmentOptions.Center;
            var errorLayout = error.gameObject.AddComponent<LayoutElement>();
            errorLayout.preferredHeight = 26f;
            errorLayout.preferredWidth = 400f;

            var keypad = new GameObject("Keypad", typeof(RectTransform));
            keypad.transform.SetParent(content.transform, false);
            var grid = keypad.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Defaults.LockKeySize, Defaults.LockKeySize);
            grid.spacing = new Vector2(Defaults.LockKeySpacing, Defaults.LockKeySpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var digitKeys = new Button[10];
            for (var digit = 1; digit <= 9; digit++)
                digitKeys[digit] = CreateLockKey(keypad, digit.ToString(), font, Defaults.LockKeyFontSize, true);
            var cancel = CreateLockKey(keypad, "Cancel", font, Defaults.LockKeyFontSize * 0.5f, false);
            digitKeys[0] = CreateLockKey(keypad, "0", font, Defaults.LockKeyFontSize, true);
            var backspace = CreateLockKey(keypad, "Delete", font, Defaults.LockKeyFontSize * 0.5f, false);

            var lockScreen = go.AddComponent<OCLockScreen>();
            OCFieldWiring.Set(lockScreen, "_backdrop", backdrop);
            OCFieldWiring.Set(lockScreen, "_digitKeys", digitKeys);
            OCFieldWiring.Set(lockScreen, "_cancelKey", cancel);
            OCFieldWiring.Set(lockScreen, "_backspaceKey", backspace);
            OCFieldWiring.Set(lockScreen, "_keypad", grid);
            OCFieldWiring.Set(lockScreen, "_dots", dots);
            OCFieldWiring.Set(lockScreen, "_dotRow", dotRowRect);
            OCFieldWiring.Set(lockScreen, "_dotRowSlot", dotSlotLayout);
            OCFieldWiring.Set(lockScreen, "_error", error);

            go.SetActive(false);
            return lockScreen;
        }

        private static Button CreateLockKey(GameObject keypad, string text, TMP_FontAsset font, float fontSize, bool filled)
        {
            var go = new GameObject(text == "Delete" ? "DeleteKey" : $"Key{text}", typeof(RectTransform));
            go.transform.SetParent(keypad.transform, false);
            var image = go.AddComponent<Image>();
            image.color = Color.white;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, filled ? 0.14f : 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, filled ? 0.2f : 0.06f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(1f, 1f, 1f, filled ? 0.4f : 0.12f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var label = CreateCommandText(go, "Label", font, fontSize, Color.white);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            Stretch(label.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildHeader(GameObject panel, TMP_FontAsset font)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(panel.transform, false);

            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(header.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Settings";
            title.font = font;
            title.fontSize = 28f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Left;

            var spacerGo = new GameObject("Spacer", typeof(RectTransform));
            spacerGo.transform.SetParent(header.transform, false);
            var spacerLayout = spacerGo.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
        }

        /// <summary>Builds the Settings / Commands tab bar under the title: two tabs on one line, then a 1px rule.</summary>
        private static (GameObject bar, OCPanelTab settings, OCPanelTab commands) BuildTabBar(GameObject panel, TMP_FontAsset font)
        {
            var bar = new GameObject("TabBar", typeof(RectTransform));
            bar.transform.SetParent(panel.transform, false);
            var barLayout = bar.AddComponent<VerticalLayoutGroup>();
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = false;

            var tabs = new GameObject("Tabs", typeof(RectTransform));
            tabs.transform.SetParent(bar.transform, false);
            var tabsLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 4f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = false;
            tabsLayout.childAlignment = TextAnchor.LowerLeft;

            var settings = CreateTab(tabs, "SettingsTab", "Settings", font, true);
            var commands = CreateTab(tabs, "CommandsTab", "Commands", font, false);

            var rule = new GameObject("Rule", typeof(RectTransform));
            rule.transform.SetParent(bar.transform, false);
            var ruleImage = rule.AddComponent<Image>();
            ruleImage.color = new Color(1f, 1f, 1f, 0.12f);
            ruleImage.raycastTarget = false;
            var ruleLayout = rule.AddComponent<LayoutElement>();
            ruleLayout.preferredHeight = 1f;
            ruleLayout.minHeight = 1f;

            return (bar, settings, commands);
        }

        private static OCPanelTab CreateTab(GameObject tabs, string goName, string text, TMP_FontAsset font, bool selected)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(tabs.transform, false);

            // Transparent, but it is what the Button raycasts against.
            var hitArea = go.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = hitArea;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 180f;
            layout.preferredHeight = Defaults.TabHeight;
            layout.minHeight = Defaults.TabHeight;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.fontSize = Defaults.FontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var underline = new GameObject("Underline", typeof(RectTransform));
            underline.transform.SetParent(go.transform, false);
            var underlineRect = underline.GetComponent<RectTransform>();
            underlineRect.anchorMin = new Vector2(0f, 0f);
            underlineRect.anchorMax = new Vector2(1f, 0f);
            underlineRect.pivot = new Vector2(0.5f, 0f);
            underlineRect.sizeDelta = new Vector2(0f, 3f);
            underlineRect.anchoredPosition = Vector2.zero;
            var underlineImage = underline.AddComponent<Image>();
            underlineImage.color = new Color(0.43f, 0.64f, 0.96f, 1f);
            underlineImage.raycastTarget = false;
            underline.SetActive(selected);

            var tab = go.AddComponent<OCPanelTab>();
            OCFieldWiring.Set(tab, "_button", button);
            OCFieldWiring.Set(tab, "_label", label);
            OCFieldWiring.Set(tab, "_underline", underline);
            return tab;
        }

        /// <summary>The scroll view GameObject that holds a container built by <see cref="BuildScrollView"/> (Content → Viewport → view).</summary>
        private static GameObject ViewOf(Transform container) => container.parent.parent.gameObject;

        /// <summary>Builds a scroll view and returns its Content transform (a row container).</summary>
        private static Transform BuildScrollView(GameObject panel, string name)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);

            var scrollLayoutElement = scrollGo.AddComponent<LayoutElement>();
            scrollLayoutElement.flexibleHeight = 1f;

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportGo.AddComponent<RectMask2D>();
            // Without a raycast target here, drags on empty space between rows hit the Panel behind the ScrollRect and never scroll.
            var viewportHitArea = viewportGo.AddComponent<Image>();
            viewportHitArea.color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = Defaults.RowSpacing;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            var scrollbar = CreateVerticalScrollbar(scrollGo);
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 6f;
            scrollRect.scrollSensitivity = Defaults.ScrollSensitivity;

            return contentGo.transform;
        }

        private static Scrollbar CreateVerticalScrollbar(GameObject scrollView)
        {
            var go = DefaultControls.CreateScrollbar(OCUiSprites.UiResources());
            go.transform.SetParent(scrollView.transform, false);
            go.name = "Scrollbar Vertical";

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(Defaults.ScrollbarWidth, 0f);
            rect.anchoredPosition = Vector2.zero;

            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            go.transform.Find("Sliding Area/Handle").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);

            var scrollbar = go.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private static (Button save, Button reset, Button close) BuildFooter(GameObject panel, TMP_FontAsset font)
        {
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(panel.transform, false);

            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;
            footerLayout.childAlignment = TextAnchor.MiddleRight;

            var save = CreateFooterButton(footer, "SaveButton", "Save", font);
            var reset = CreateFooterButton(footer, "ResetButton", "Reset to Defaults", font);
            var close = CreateFooterButton(footer, "CloseButton", "Close", font);

            return (save, reset, close);
        }

        /// <summary>Builds the modal confirm overlay as the last child of the panel; left inactive.</summary>
        private static (GameObject dialog, Button cancel, Button confirm, TMP_Text message, TMP_Text confirmLabel) BuildConfirmDialog(GameObject panel, TMP_FontAsset font)
        {
            var dialog = new GameObject("ConfirmDialog", typeof(RectTransform));
            dialog.transform.SetParent(panel.transform, false);

            // Panel has a VerticalLayoutGroup; the overlay must not take part in it.
            var ignore = dialog.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = Vector2.zero;
            dialogRect.anchorMax = Vector2.one;
            dialogRect.offsetMin = Vector2.zero;
            dialogRect.offsetMax = Vector2.zero;

            var dim = dialog.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(dialog.transform, false);

            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(560f, 220f);
            boxRect.anchoredPosition = Vector2.zero;

            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.14f, 0.14f, 0.18f, 1f);

            var boxLayout = box.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(24, 24, 24, 24);
            boxLayout.spacing = 20f;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;

            var messageGo = new GameObject("Message", typeof(RectTransform));
            messageGo.transform.SetParent(box.transform, false);
            var message = messageGo.AddComponent<TextMeshProUGUI>();
            message.text = "Reset all settings on this panel to their defaults?\nThis cannot be undone.";
            message.font = font;
            message.fontSize = Defaults.FontSize;
            message.alignment = TextAlignmentOptions.Center;
            message.textWrappingMode = TextWrappingModes.Normal;
            var messageLayout = messageGo.AddComponent<LayoutElement>();
            messageLayout.flexibleHeight = 1f;

            var buttonRow = new GameObject("Buttons", typeof(RectTransform));
            buttonRow.transform.SetParent(box.transform, false);
            var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;

            var cancel = CreateFooterButton(buttonRow, "CancelButton", "Cancel", font);
            var confirm = CreateFooterButton(buttonRow, "ConfirmResetButton", "Reset", font);

            dialog.SetActive(false);
            return (dialog, cancel, confirm, message, confirm.GetComponentInChildren<TMP_Text>());
        }

        private static Button CreateFooterButton(GameObject footer, string goName, string text, TMP_FontAsset font)
        {
            var go = TMP_DefaultControls.CreateButton(OCUiSprites.TmpUiResources());
            go.transform.SetParent(footer.transform, false);
            go.name = goName;

            var button = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
                label.font = font;
            }

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 160f;
            layout.preferredHeight = 44f;

            return button;
        }

        // ---- Row building blocks shared by the widget templates ----

        private static GameObject CreateRowRoot(string name, Transform parent)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowLayoutGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowLayoutGroup.spacing = 8f;
            rowLayoutGroup.padding = new RectOffset(4, 4, 4, 4);

            rowLayoutGroup.childControlWidth = true;
            rowLayoutGroup.childControlHeight = true;
            rowLayoutGroup.childForceExpandWidth = false;
            rowLayoutGroup.childForceExpandHeight = true;


            rowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;

            var rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = Defaults.RowHeight;

            return row;
        }

        private static TextMeshProUGUI CreateRowLabel(GameObject row, TMP_FontAsset font)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = Defaults.FontSize;
            label.alignment = TextAlignmentOptions.Left;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = Defaults.LabelWidth;

            return label;
        }

        private static TextMeshProUGUI CreateAxisLabel(GameObject row, TMP_FontAsset font, string text)
        {
            var go = new GameObject("AxisLabel", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.fontSize = Defaults.FontSize;
            label.alignment = TextAlignmentOptions.Right;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 28f;
            layout.flexibleWidth = 0f;

            return label;
        }

        private static TMP_InputField CreateRowInput(GameObject row, TMP_FontAsset font, TMP_InputField.ContentType contentType)
        {
            var go = TMP_DefaultControls.CreateInputField(OCUiSprites.TmpUiResources());
            go.transform.SetParent(row.transform, false);

            var input = go.GetComponent<TMP_InputField>();
            input.contentType = contentType;
            input.fontAsset = font;

            if (input.textComponent is TextMeshProUGUI text)
            {
                text.font = font;
                text.fontSize = Defaults.FontSize;
            }
            if (input.placeholder is TextMeshProUGUI placeholder)
                placeholder.font = font;

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            return input;
        }

        private static Slider CreateRowSlider(GameObject row)
        {
            var go = DefaultControls.CreateSlider(OCUiSprites.UiResources());
            go.transform.SetParent(row.transform, false);
            go.name = "Slider";

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            return go.GetComponent<Slider>();
        }

        private static TMP_InputField CreateSliderValueInput(GameObject row, TMP_FontAsset font, TMP_InputField.ContentType contentType)
        {
            var input = CreateRowInput(row, font, contentType);

            var layout = input.GetComponent<LayoutElement>();
            layout.preferredWidth = SliderInputWidth;
            layout.flexibleWidth = 0f;

            return input;
        }

        private static Toggle CreateRowToggle(GameObject row)
        {
            var go = DefaultControls.CreateToggle(OCUiSprites.UiResources());
            go.transform.SetParent(row.transform, false);
            go.name = "Toggle";

            var toggle = go.GetComponent<Toggle>();

            // The row already has its own label (OCWidget<T>._label) — drop the toggle's built-in one.
            var builtInLabel = go.transform.Find("Label");
            if (builtInLabel != null) Object.DestroyImmediate(builtInLabel.gameObject);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = Defaults.RowHeight;
            layout.preferredHeight = Defaults.RowHeight;

            return toggle;
        }

        private static Image CreateRowSwatch(GameObject row)
        {
            var go = new GameObject("Swatch", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = Defaults.RowHeight;
            layout.preferredHeight = Defaults.RowHeight;

            return image;
        }
    }
}

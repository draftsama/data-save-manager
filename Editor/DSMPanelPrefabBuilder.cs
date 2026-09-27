#nullable enable

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DataSaveManager.Editor
{
    /// <summary>
    /// Generates the Runtime Panel's widget row prefabs, the DSMWidgetConfig asset, and the DSMRuntimePanel
    /// prefab itself. Prefabs are agent-unsafe to hand-author (serialized YAML), so this menu command builds
    /// them from code instead.
    /// </summary>
    internal static class DSMPanelPrefabBuilder
    {
        private const float RowHeight = 40f;
        private const float LabelWidth = 220f;
        private const float FontSize = 20f;

        [MenuItem("DSM/Build Runtime Panel Prefabs")]
        internal static void Build()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                Debug.LogError("DSM: TMP_Settings.defaultFontAsset is null. Import TextMeshPro's \"TMP Essential Resources\" " +
                                "(Window > TextMeshPro > Import TMP Essential Resources) and run this command again.");
                return;
            }

            var prefabDir = ResolvePrefabFolder();
            if (prefabDir == null)
            {
                Debug.LogError("DSM: could not resolve this package's Prefab folder from DSMPanelPrefabBuilder's own script path.");
                return;
            }

            var boolWidget = BuildBoolWidgetPrefab(Path.Combine(prefabDir, "BoolWidget.prefab").Replace('\\', '/'), font);
            var intWidget = BuildIntWidgetPrefab(Path.Combine(prefabDir, "IntWidget.prefab").Replace('\\', '/'), font);
            var floatWidget = BuildFloatWidgetPrefab(Path.Combine(prefabDir, "FloatWidget.prefab").Replace('\\', '/'), font);
            var stringWidget = BuildStringWidgetPrefab(Path.Combine(prefabDir, "StringWidget.prefab").Replace('\\', '/'), font);
            var vector2Widget = BuildVector2WidgetPrefab(Path.Combine(prefabDir, "Vector2Widget.prefab").Replace('\\', '/'), font);
            var vector3Widget = BuildVector3WidgetPrefab(Path.Combine(prefabDir, "Vector3Widget.prefab").Replace('\\', '/'), font);
            var colorWidget = BuildColorWidgetPrefab(Path.Combine(prefabDir, "ColorWidget.prefab").Replace('\\', '/'), font);

            var configPath = Path.Combine(prefabDir, "DSMWidgetConfig.asset").Replace('\\', '/');
            BuildWidgetConfig(configPath, boolWidget, intWidget, floatWidget, stringWidget, vector2Widget, vector3Widget, colorWidget);
            var widgetConfig = AssetDatabase.LoadAssetAtPath<DSMWidgetConfig>(configPath);

            var panelPath = Path.Combine(prefabDir, "DSMRuntimePanel.prefab").Replace('\\', '/');
            BuildRuntimePanelPrefab(panelPath, widgetConfig, font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("DSM: built runtime panel prefabs:\n" +
                       $"  {Path.Combine(prefabDir, "BoolWidget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "IntWidget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "FloatWidget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "StringWidget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "Vector2Widget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "Vector3Widget.prefab")}\n" +
                       $"  {Path.Combine(prefabDir, "ColorWidget.prefab")}\n" +
                       $"  {configPath}\n" +
                       $"  {panelPath}");
        }

        /// <summary>Package root is this script's grandparent folder (script -> Editor -> package root), so this works wherever the package is installed.</summary>
        private static string? ResolvePrefabFolder()
        {
            var guids = AssetDatabase.FindAssets("t:Script DSMPanelPrefabBuilder");
            if (guids.Length == 0) return null;

            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var editorDir = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(editorDir)) return null;

            var packageRoot = Path.GetDirectoryName(editorDir);
            if (string.IsNullOrEmpty(packageRoot)) return null;

            return Path.Combine(packageRoot, "Prefab").Replace('\\', '/');
        }

        // ---- Widget prefabs ----

        private static BoolWidget BuildBoolWidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("BoolWidget");
            var label = CreateRowLabel(row, font);
            var toggle = CreateRowToggle(row);

            var widget = row.AddComponent<BoolWidget>();
            SetField(widget, "_label", label);
            SetField(widget, "_toggle", toggle);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<BoolWidget>();
        }

        private static IntWidget BuildIntWidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("IntWidget");
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.IntegerNumber);

            var widget = row.AddComponent<IntWidget>();
            SetField(widget, "_label", label);
            SetField(widget, "_input", input);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<IntWidget>();
        }

        private static FloatWidget BuildFloatWidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("FloatWidget");
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<FloatWidget>();
            SetField(widget, "_label", label);
            SetField(widget, "_input", input);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<FloatWidget>();
        }

        private static StringWidget BuildStringWidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("StringWidget");
            var label = CreateRowLabel(row, font);
            var input = CreateRowInput(row, font, TMP_InputField.ContentType.Standard);

            var widget = row.AddComponent<StringWidget>();
            SetField(widget, "_label", label);
            SetField(widget, "_input", input);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<StringWidget>();
        }

        private static Vector2Widget BuildVector2WidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("Vector2Widget");
            var label = CreateRowLabel(row, font);
            var xInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var yInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<Vector2Widget>();
            SetField(widget, "_label", label);
            SetField(widget, "_xInput", xInput);
            SetField(widget, "_yInput", yInput);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<Vector2Widget>();
        }

        private static Vector3Widget BuildVector3WidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("Vector3Widget");
            var label = CreateRowLabel(row, font);
            var xInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var yInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var zInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);

            var widget = row.AddComponent<Vector3Widget>();
            SetField(widget, "_label", label);
            SetField(widget, "_xInput", xInput);
            SetField(widget, "_yInput", yInput);
            SetField(widget, "_zInput", zInput);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<Vector3Widget>();
        }

        private static ColorWidget BuildColorWidgetPrefab(string path, TMP_FontAsset font)
        {
            var row = CreateRowRoot("ColorWidget");
            var label = CreateRowLabel(row, font);
            var rInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var gInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var bInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var aInput = CreateRowInput(row, font, TMP_InputField.ContentType.DecimalNumber);
            var swatch = CreateRowSwatch(row);

            var widget = row.AddComponent<ColorWidget>();
            SetField(widget, "_label", label);
            SetField(widget, "_rInput", rInput);
            SetField(widget, "_gInput", gInput);
            SetField(widget, "_bInput", bInput);
            SetField(widget, "_aInput", aInput);
            SetField(widget, "_swatch", swatch);

            var saved = SaveAndDestroy(row, path);
            return saved.GetComponent<ColorWidget>();
        }

        private static void BuildWidgetConfig(
            string path,
            BoolWidget boolWidget, IntWidget intWidget, FloatWidget floatWidget, StringWidget stringWidget,
            Vector2Widget vector2Widget, Vector3Widget vector3Widget, ColorWidget colorWidget)
        {
            var config = AssetDatabase.LoadAssetAtPath<DSMWidgetConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DSMWidgetConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            SetField(config, "_boolWidget", boolWidget);
            SetField(config, "_intWidget", intWidget);
            SetField(config, "_floatWidget", floatWidget);
            SetField(config, "_stringWidget", stringWidget);
            SetField(config, "_vector2Widget", vector2Widget);
            SetField(config, "_vector3Widget", vector3Widget);
            SetField(config, "_colorWidget", colorWidget);

            EditorUtility.SetDirty(config);
        }

        private static void BuildRuntimePanelPrefab(string path, DSMWidgetConfig? widgetConfig, TMP_FontAsset font)
        {
            var rootGo = new GameObject("DSMRuntimePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // "Panel" is the visible surface (_root): toggled active/inactive by DSMRuntimePanel.Show/Hide.
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(rootGo.transform, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900f, 800f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 16, 16);
            panelLayout.spacing = 12f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            BuildHeader(panel, font);
            var container = BuildScrollView(panel);
            var (saveButton, resetAllButton, closeButton) = BuildFooter(panel, font);

            var panelComponent = rootGo.AddComponent<DSMRuntimePanel>();
            SetField(panelComponent, "_widgetConfig", widgetConfig);
            SetField(panelComponent, "_root", panel);
            SetField(panelComponent, "_container", container);
            SetField(panelComponent, "_saveButton", saveButton);
            SetField(panelComponent, "_resetAllButton", resetAllButton);
            SetField(panelComponent, "_closeButton", closeButton);
            // _startVisible / _toggleKey keep their C# field defaults (false / KeyCode.F1).

            SaveAndDestroy(rootGo, path);
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

        /// <summary>Builds the scroll view and returns its Content transform (the panel's row container).</summary>
        private static Transform BuildScrollView(GameObject panel)
        {
            var scrollGo = new GameObject("Scroll View", typeof(RectTransform));
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

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return contentGo.transform;
        }

        private static (Button save, Button resetAll, Button close) BuildFooter(GameObject panel, TMP_FontAsset font)
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
            var resetAll = CreateFooterButton(footer, "ResetAllButton", "Reset All", font);
            var close = CreateFooterButton(footer, "CloseButton", "Close", font);

            return (save, resetAll, close);
        }

        private static Button CreateFooterButton(GameObject footer, string goName, string text, TMP_FontAsset font)
        {
            var go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
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

        // ---- Row building blocks shared by the 7 widget prefabs ----

        private static GameObject CreateRowRoot(string name)
        {
            var row = new GameObject(name, typeof(RectTransform));

            var rowLayoutGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowLayoutGroup.spacing = 8f;
            rowLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
            rowLayoutGroup.childControlWidth = true;
            rowLayoutGroup.childControlHeight = true;
            rowLayoutGroup.childForceExpandWidth = true;
            rowLayoutGroup.childForceExpandHeight = false;
            rowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;

            var rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = RowHeight;

            return row;
        }

        private static TextMeshProUGUI CreateRowLabel(GameObject row, TMP_FontAsset font)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = FontSize;
            label.alignment = TextAlignmentOptions.Left;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = LabelWidth;

            return label;
        }

        private static TMP_InputField CreateRowInput(GameObject row, TMP_FontAsset font, TMP_InputField.ContentType contentType)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.transform.SetParent(row.transform, false);

            var input = go.GetComponent<TMP_InputField>();
            input.contentType = contentType;
            input.fontAsset = font;

            if (input.textComponent is TextMeshProUGUI text)
            {
                text.font = font;
                text.fontSize = FontSize;
            }
            if (input.placeholder is TextMeshProUGUI placeholder)
                placeholder.font = font;

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            return input;
        }

        private static Toggle CreateRowToggle(GameObject row)
        {
            var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
            go.transform.SetParent(row.transform, false);
            go.name = "Toggle";

            var toggle = go.GetComponent<Toggle>();

            // The row already has its own label (DSMWidget<T>._label) — drop the toggle's built-in one.
            var builtInLabel = go.transform.Find("Label");
            if (builtInLabel != null) Object.DestroyImmediate(builtInLabel.gameObject);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = RowHeight;
            layout.preferredHeight = RowHeight;

            return toggle;
        }

        private static Image CreateRowSwatch(GameObject row)
        {
            var go = new GameObject("Swatch", typeof(RectTransform));
            go.transform.SetParent(row.transform, false);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = RowHeight;
            layout.preferredHeight = RowHeight;

            return image;
        }

        // ---- Small helpers ----

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>Assigns a private [SerializeField] reference by name — these are project MonoBehaviours/ScriptableObjects with no public setters.</summary>
        private static void SetField(Object target, string fieldName, Object? value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"DSM: serialized property '{fieldName}' not found on '{target.GetType().Name}'.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

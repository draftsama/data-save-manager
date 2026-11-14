#nullable enable

using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Settings tab: the Save File and the Config's Audiences.</summary>
    internal sealed class OCSettingsTab : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly TextField _path;
        private readonly VisualElement _audiences = new();
        private readonly Button _addAudience;
        private int _shownAudiences = -1;

        public OCSettingsTab(OCManagerContext context)
        {
            _context = context;
            AddToClassList("oc-tab");
            var scroll = new ScrollView();
            scroll.AddToClassList("oc-scroll");
            Add(scroll);

            var so = context.So;
            var saveCard = Section(scroll, "Save File", null);
            saveCard.Add(Bound(so, "_autoSave", "AutoSave"));
            saveCard.Add(Bound(so, "_autoSaveDebounce", "AutoSave Debounce"));
            saveCard.Add(Bound(so, "_saveDirectory", "Save Directory"));
            saveCard.Add(Bound(so, "_fileName", "File Name"));
            saveCard.Add(Bound(so, "_logCapacity", "Log Capacity"));

            _path = new TextField("Resolved Path") { isReadOnly = true };
            _path.AddToClassList("oc-mono-field");
            saveCard.Add(_path);

            var buttons = new VisualElement();
            buttons.AddToClassList("oc-row");
            buttons.AddToClassList("oc-actions");
            buttons.Add(new Button(OpenFolder) { text = "Open Folder" });
            var delete = new Button(DeleteSaveFile) { text = "Delete Save File" };
            delete.AddToClassList("oc-button--danger");
            buttons.Add(delete);
            saveCard.Add(buttons);

            var audienceCard = Section(scroll, "Audiences", "Kinds of operator an Exposed Entry is meant for. Each Runtime Panel picks the Audiences it serves.");
            audienceCard.Add(_audiences);
            _addAudience = new Button(AddAudience) { text = "+ Add Audience" };
            _addAudience.AddToClassList("oc-align-start");
            audienceCard.Add(_addAudience);

            Refresh();
        }

        public void Refresh()
        {
            _path.SetValueWithoutNotify(OCPaths.ResolveSaveFilePath(_context.Config));
            var audiencesProp = _context.So.FindProperty("_audiences");
            if (audiencesProp.arraySize != _shownAudiences) RebuildAudiences(audiencesProp);
        }

        private void RebuildAudiences(SerializedProperty audiencesProp)
        {
            _audiences.Clear();
            _shownAudiences = audiencesProp.arraySize;
            _addAudience.SetEnabled(audiencesProp.arraySize < OCAudienceEditing.MaxAudiences);
            if (audiencesProp.arraySize == 0)
            {
                var none = new Label("No Audiences: every Runtime Panel shows every Exposed Entry.");
                none.AddToClassList("oc-muted");
                _audiences.Add(none);
            }

            for (var i = 0; i < audiencesProp.arraySize; i++)
            {
                var index = i;
                var audienceProp = audiencesProp.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.AddToClassList("oc-row");
                row.AddToClassList("oc-detail-row");
                var name = new TextField { isDelayed = true };
                name.BindProperty(audienceProp.FindPropertyRelative("_name"));
                name.AddToClassList("oc-grow");
                var remove = new Button(() => TryRemoveAudience(index)) { text = "✕", tooltip = "Remove Audience" };
                remove.AddToClassList("oc-icon-button");
                row.Add(name);
                row.Add(remove);
                _audiences.Add(row);
            }
        }

        private void AddAudience()
        {
            var audiencesProp = _context.So.FindProperty("_audiences");
            var index = audiencesProp.arraySize;
            audiencesProp.InsertArrayElementAtIndex(index);
            var added = audiencesProp.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("_id").stringValue = OCAudience.NewId();
            added.FindPropertyRelative("_name").stringValue = $"Audience {index + 1}";
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private void TryRemoveAudience(int index)
        {
            var audiencesProp = _context.So.FindProperty("_audiences");
            var audienceProp = audiencesProp.GetArrayElementAtIndex(index);
            var id = audienceProp.FindPropertyRelative("_id").stringValue;
            var name = audienceProp.FindPropertyRelative("_name").stringValue;

            var users = OCAudienceEditing.FindUsers(_context.Config, id);
            if (users.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Audience in use",
                    $"'{name}' can't be removed while it is still named by:\n\n- {string.Join("\n- ", users)}\n\nRemove it from these first.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Audience", $"Remove Audience '{name}'?", "Remove", "Cancel")) return;
            audiencesProp.DeleteArrayElementAtIndex(index);
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private void OpenFolder()
        {
            var dir = Path.GetDirectoryName(OCPaths.ResolveSaveFilePath(_context.Config));
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private void DeleteSaveFile()
        {
            var path = OCPaths.ResolveSaveFilePath(_context.Config);
            if (!EditorUtility.DisplayDialog("Delete Save File", $"Delete '{path}'? This cannot be undone.", "Delete", "Cancel")) return;
            if (File.Exists(path)) File.Delete(path);
            OC.Load();
        }

        private static PropertyField Bound(SerializedObject so, string path, string label)
        {
            var field = new PropertyField(so.FindProperty(path), label);
            field.BindProperty(so.FindProperty(path));
            return field;
        }

        internal static VisualElement Section(VisualElement parent, string title, string? description)
        {
            var heading = new Label(title);
            heading.AddToClassList("oc-section-title");
            parent.Add(heading);
            if (description != null)
            {
                var text = new Label(description);
                text.AddToClassList("oc-muted");
                text.AddToClassList("oc-wrap");
                text.AddToClassList("oc-section-description");
                parent.Add(text);
            }
            var card = new VisualElement();
            card.AddToClassList("oc-card");
            card.AddToClassList("oc-form");
            parent.Add(card);
            return card;
        }
    }
}

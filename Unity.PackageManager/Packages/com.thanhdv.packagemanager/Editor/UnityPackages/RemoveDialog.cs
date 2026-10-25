using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.PackageManager
{
    /// <summary>
    /// Confirms which files Remove deletes, laid out like Unity's import dialog: indented by folder, a folder's
    /// checkbox switches everything under it and shows "mixed" when only part of it is selected.
    /// </summary>
    internal class RemoveDialog : EditorWindow
    {
        private string _packageId;
        private List<string> _paths;
        private HashSet<string> _selected;
        private ListView _list;
        private Button _confirm;

        public static void Show(ImportedPackage record)
        {
            var data = ProjectData.Instance;
            var paths = PackageRemover.RemovablePaths(data, record.PackageId);
            if (paths.Count == 0)
            {
                if (EditorUtility.DisplayDialog("Remove package",
                        $"None of {record.DisplayName}'s files are left in the project. Remove {record.DisplayName} from the imported list?",
                        "Remove from list", "Cancel"))
                    PackageRemover.Remove(data, record.PackageId, paths);
                return;
            }

            var window = CreateInstance<RemoveDialog>();
            window.titleContent = new GUIContent($"Remove {record.DisplayName}");
            window._packageId = record.PackageId;
            window._paths = paths;
            window._selected = new HashSet<string>(paths);
            window.minSize = new Vector2(480, 360);
            window.Build(record, PackageRemover.SharedCount(data, record.PackageId));
            window.ShowUtility();
        }

        private void Build(ImportedPackage record, int sharedCount)
        {
            var root = rootVisualElement;
            Theme.Apply(root);
            var body = new VisualElement();
            body.AddToClassList("remove");
            root.Add(body);

            body.Add(new HelpBox($"The files below from {record.DisplayName} {record.Version} will be moved to the trash. " +
                                 "Scenes, prefabs or scripts that use them will lose those references.", HelpBoxMessageType.Warning));
            if (sharedCount > 0)
            {
                var note = new Label($"{sharedCount} asset(s) shared with other imported packages are kept.");
                note.AddToClassList("t");
                note.AddToClassList("small");
                note.AddToClassList("dim");
                note.AddToClassList("remove__note");
                body.Add(note);
            }

            _list = new ListView(_paths, 20, MakeRow, BindRow) { selectionType = SelectionType.None };
            _list.AddToClassList("remove__tree");
            if (sharedCount == 0) _list.style.marginTop = 6;
            body.Add(_list);

            var buttons = new VisualElement();
            buttons.AddToClassList("remove__buttons");
            buttons.Add(DialogButton("All", () => SetAll(true)));
            buttons.Add(DialogButton("None", () => SetAll(false)));
            buttons.Add(new VisualElement { style = { flexGrow = 1 } });
            buttons.Add(DialogButton("Cancel", Close));
            _confirm = DialogButton(ConfirmLabel(), Confirm);
            _confirm.AddToClassList("tdv-button--primary");
            _confirm.AddToClassList("remove__confirm");
            buttons.Add(_confirm);
            body.Add(buttons);
        }

        private static Button DialogButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("tdv-button");
            return button;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("file-row");
            var toggle = new Toggle();
            toggle.AddToClassList("file-row__toggle");
            toggle.RegisterValueChangedCallback(e =>
            {
                if (row.userData is string path) SetNode(path, e.newValue);
            });
            row.Add(toggle);
            var icon = new Image();
            icon.AddToClassList("file-row__icon");
            icon.style.width = icon.style.height = 16;
            icon.style.flexShrink = 0;
            row.Add(icon);
            var name = new Label();
            name.AddToClassList("t");
            name.AddToClassList("nowrap");
            name.AddToClassList("file-row__name");
            row.Add(name);
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            string path = _paths[index];
            row.userData = path;
            row.style.paddingLeft = 4 + 14 * (Depth(path) - Depth(_paths[0]));
            var (on, off) = Count(path);
            var toggle = row.Q<Toggle>();
            toggle.SetValueWithoutNotify(off == 0);
            toggle.showMixedValue = on > 0 && off > 0;
            row.EnableInClassList("file-row--off", on == 0);
            row.Q<Image>().image = AssetDatabase.GetCachedIcon(path);
            row.Q<Label>().text = path.Substring(path.LastIndexOf('/') + 1);
            row.tooltip = path;
        }

        /// <summary>Selected / unselected entries among <paramref name="path"/> and everything under it.</summary>
        private (int On, int Off) Count(string path)
        {
            int on = 0, off = 0;
            foreach (string p in Subtree(path))
                if (_selected.Contains(p)) on++; else off++;
            return (on, off);
        }

        private IEnumerable<string> Subtree(string path) =>
            _paths.Where(p => p == path || p.StartsWith(path + "/", System.StringComparison.Ordinal));

        private void SetNode(string path, bool on)
        {
            foreach (string p in Subtree(path).ToList())
                if (on) _selected.Add(p); else _selected.Remove(p);
            // A listed parent folder stays selected while anything under it is.
            for (int cut = path.LastIndexOf('/'); cut > 0; cut = path.LastIndexOf('/', cut - 1))
            {
                string parent = path.Substring(0, cut);
                if (!_paths.Contains(parent)) continue;
                if (Subtree(parent).Any(p => p != parent && _selected.Contains(p))) _selected.Add(parent);
                else _selected.Remove(parent);
            }
            Refresh();
        }

        private void SetAll(bool on)
        {
            _selected = on ? new HashSet<string>(_paths) : new HashSet<string>();
            Refresh();
        }

        private void Refresh()
        {
            _list.RefreshItems();
            _confirm.text = ConfirmLabel();
        }

        private static int Depth(string path) => path.Count(c => c == '/');

        /// <summary>Counts files: a folder only goes once it is empty, so it is not a removal of its own.</summary>
        private string ConfirmLabel() => $"Remove ({_selected.Count(p => !AssetDatabase.IsValidFolder(p))})";

        private void Confirm()
        {
            var failed = PackageRemover.Remove(ProjectData.Instance, _packageId, _paths.Where(_selected.Contains).ToList());
            if (failed.Count > 0)
                Debug.LogWarning("[ThanhDV Package Manager] Could not remove:\n" + string.Join("\n", failed));
            Close();
        }

        private void OnEnable()
        {
            // After a domain reload the fields are gone; there is nothing sensible to show.
            // (Checked later: on CreateInstance, OnEnable runs before Show() fills them in.)
            EditorApplication.delayCall += () =>
            {
                if (this != null && _paths == null) Close();
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PackageManager.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ThanhDV.PackageManager
{
    /// <summary>Right-hand pane: header, main image, status line, version picker + actions and tabs for the selected package.</summary>
    internal class DetailsView : VisualElement
    {
        private const string TabDescription = "Description";
        private const string TabVersions = "Versions";
        private const string TabDependencies = "Dependencies";

        private enum State
        {
            Neutral,
            Installed,
            Warning,
            Dim,
        }

        private readonly Func<IReadOnlyDictionary<string, PackageInfo>> _installed;
        private readonly Action<string, bool, float?> _setStatus;
        // A cached null means the server no longer has the package.
        private readonly Dictionary<string, PackageDetailDto> _packages = new Dictionary<string, PackageDetailDto>();
        private readonly Dictionary<string, UpmPackageDetailDto> _upm = new Dictionary<string, UpmPackageDetailDto>();
        private readonly HashSet<string> _fetching = new HashSet<string>();

        private PackageItem _item;
        private string _tab = TabDescription;
        private string _version;
        private string _loadError;
        // Downloading, installing or removing: every action is off until it ends.
        private bool _busy;

        /// <summary>Raised after a UPM install / remove so the window re-reads the installed list.</summary>
        public event Action UpmChanged;

        public DetailsView(Func<IReadOnlyDictionary<string, PackageInfo>> installed, Action<string, bool, float?> setStatus)
        {
            _installed = installed;
            _setStatus = setStatus;
            AddToClassList("details");
        }

        public void ClearCache()
        {
            _packages.Clear();
            _upm.Clear();
        }

        public async void Show(PackageItem item)
        {
            if (_item?.Key != item?.Key)
            {
                _tab = TabDescription;
                _version = null;
            }
            _item = item;
            _loadError = null;
            Render();
            if (item == null || IsCached(item)) return;

            // The window re-shows the selection on every list rebuild; fetch each package once at a time.
            if (!_fetching.Add(item.Key)) return;
            try
            {
                if (item.IsUpm) _upm[item.Id] = await ServerClient.GetUpm(item.Id);
                else _packages[item.Id] = await ServerClient.GetPackage(item.Id);
            }
            catch (Exception e)
            {
                if (_item?.Key == item.Key) _loadError = e.Message;
            }
            finally
            {
                _fetching.Remove(item.Key);
            }
            if (_item?.Key == item.Key) Render();
        }

        private bool IsCached(PackageItem item) => item.IsUpm ? _upm.ContainsKey(item.Id) : _packages.ContainsKey(item.Id);

        private void Render()
        {
            Clear();
            if (_item == null)
            {
                Add(Text("Select a package to see its details.", "details-empty"));
                return;
            }
            if (_item.IsUpm) RenderUpm();
            else RenderUnityPackage();
        }

        // ---------- .unitypackage ----------

        private void RenderUnityPackage()
        {
            bool loaded = _packages.TryGetValue(_item.Id, out var detail);
            bool gone = loaded && detail == null;
            var record = ProjectData.Instance.Find(_item.Id);
            bool importing = ImportTracker.ImportingPackageId != null;

            Header(detail?.DisplayName ?? _item.DisplayName, detail?.Publisher ?? _item.Subtitle, _item.Id, detail?.Category,
                string.IsNullOrWhiteSpace(detail?.UnityVersion) ? null : $"Unity {detail.UnityVersion.TrimEnd('+')}+");
            if (!string.IsNullOrWhiteSpace(detail?.MainImageUrl)) Add(MainImage(detail.MainImageUrl));

            if (ImportTracker.ImportingPackageId == _item.Id) StatusLine("Importing… (finish in Unity's Import dialog)", State.Neutral);
            else if (gone) StatusLine("The server no longer has this package.", State.Dim);
            else if (record == null) StatusLine("Not imported", State.Neutral);
            else if (PackageRemover.IsMissing(record)) StatusLine($"Imported {record.Version}, but its files are no longer in the project", State.Warning);
            else StatusLine($"✓ Imported {record.Version} · {Date(record.ImportedAt)}", State.Installed);

            if (!loaded)
            {
                Add(Text(_loadError ?? "Loading details…", "details__loading"));
                // Without the server, what's already in the project can still be removed.
                if (_loadError != null && record != null) Actions(row => row.Add(RemoveButton(record)));
                return;
            }
            if (gone)
            {
                if (record != null) Actions(row => row.Add(RemoveButton(record)));
                return;
            }

            var versions = detail.Versions;
            var selected = versions.FirstOrDefault(v => v.Version == _version) ?? versions.FirstOrDefault();
            Actions(row =>
            {
                if (selected != null)
                {
                    row.Add(VersionPicker(versions.Select(v => v.Version).ToList(), selected.Version, versions[0].Version));
                    string label = (DownloadCache.Has(detail.PackageId, selected) ? "Import " : "Download & Import ") + selected.Version;
                    // While Unity's Import dialog is open only the primary action waits; the rest stays usable.
                    row.Add(ActionButton(label, () => Import(detail, selected), !importing, primary: true));
                }
                if (record != null) row.Add(RemoveButton(record));
                row.Add(ActionButton("Open on Web", () => Application.OpenURL(ServerClient.PackagePageUrl(detail.PackageId))));
            });

            Tabs(TabDescription, TabVersions);
            var content = TabContent();
            if (_tab == TabVersions)
            {
                content.AddToClassList("tab-content--versions");
                if (versions.Count == 0) content.Add(Text("No versions yet.", "dim"));
                foreach (var v in versions)
                {
                    var meta = new List<string> { Size(v.Size) };
                    if (!string.IsNullOrWhiteSpace(v.UnityVersion)) meta.Add("Unity " + v.UnityVersion);
                    meta.Add(Date(v.UploadedAt));
                    content.Add(VersionRow(v.Version, v.IsLatest, record?.Version == v.Version, string.Join(" · ", meta), v.ReleaseNotes));
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(detail.DescriptionHtml)) Rich(content, detail.DescriptionHtml, true);
                else if (!string.IsNullOrWhiteSpace(detail.ShortDescription)) content.Add(Plain(detail.ShortDescription, true));
                else content.Add(Text("No description.", "dim"));
            }
        }

        private async void Import(PackageDetailDto detail, PackageVersionDto version)
        {
            SetBusy(true);
            string name = $"{detail.DisplayName} {version.Version}";
            try
            {
                string path = await DownloadCache.Get(detail.PackageId, version, (p, bytes) =>
                    _setStatus($"Downloading {name}… {(int)(p * 100)}% ({Progress(bytes, version.Size)})", false, p));
                _setStatus($"Importing {name}…", false, null);
                ImportTracker.Import(detail.PackageId, detail.DisplayName, version.Version, path);
            }
            catch (Exception e)
            {
                _setStatus(e.Message, true, null);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private Button RemoveButton(ImportedPackage record) => ActionButton("Remove", () => RemoveDialog.Show(record));

        // ---------- UPM ----------

        private void RenderUpm()
        {
            bool loaded = _upm.TryGetValue(_item.Id, out var detail);
            bool gone = loaded && detail == null;
            _installed().TryGetValue(_item.Id, out var info);

            Header(detail?.DisplayName ?? _item.DisplayName, detail?.Author ?? _item.Subtitle, _item.Id,
                detail?.License == null ? null : "License " + detail.License);

            if (gone) StatusLine("The registry no longer has this package.", State.Dim);
            else if (info == null) StatusLine("Not installed", State.Neutral);
            else if (info.isDirectDependency) StatusLine($"✓ Installed {info.version}", State.Installed);
            else StatusLine($"✓ Installed {info.version} (as a dependency of another package)", State.Installed);

            if (!loaded)
            {
                Add(Text(_loadError ?? "Loading details…", "details__loading"));
                if (_loadError != null && info != null) Actions(row => UpmInstalledButtons(row, info));
                return;
            }
            if (gone)
            {
                if (info != null) Actions(row => UpmInstalledButtons(row, info));
                return;
            }

            var versions = detail.Versions;
            var selected = versions.FirstOrDefault(v => v.Version == _version) ?? versions.FirstOrDefault();
            Actions(row =>
            {
                if (selected != null)
                {
                    row.Add(VersionPicker(versions.Select(v => v.Version).ToList(), selected.Version, detail.LatestVersion));
                    bool current = info?.version == selected.Version;
                    string label = current ? "Installed" : (info != null ? "Switch to " : "Install ") + selected.Version;
                    row.Add(ActionButton(label, () => Install(detail, selected.Version), !current, primary: true));
                }
                if (info != null) UpmInstalledButtons(row, info);
                row.Add(ActionButton("Open on Web", () => Application.OpenURL(ServerClient.UpmPageUrl(detail.Name))));
                if (!string.IsNullOrWhiteSpace(detail.RepositoryUrl))
                    row.Add(ActionButton("Repository", () => Application.OpenURL(detail.RepositoryUrl)));
            });

            Tabs(TabDescription, TabVersions, TabDependencies);
            var content = TabContent();
            if (_tab == TabVersions)
            {
                content.AddToClassList("tab-content--versions");
                foreach (var v in versions)
                {
                    var meta = new List<string>();
                    if (!string.IsNullOrWhiteSpace(v.Unity)) meta.Add("Unity " + v.Unity);
                    if (v.PublishedAt is { } published) meta.Add(Date(published));
                    content.Add(VersionRow(v.Version, v.Version == detail.LatestVersion, info?.version == v.Version, string.Join(" · ", meta), null));
                }
            }
            else if (_tab == TabDependencies)
            {
                content.Add(Text($"For version {selected?.Version}; Unity installs these automatically.", "small", "dim", "wrap", "deps-note"));
                if (selected == null || selected.Dependencies.Count == 0) content.Add(Text("None."));
                else
                    foreach (var dep in selected.Dependencies.OrderBy(d => d.Key, StringComparer.Ordinal))
                    {
                        var row = new VisualElement();
                        row.AddToClassList("dep-row");
                        row.Add(Text(dep.Key, "nowrap"));
                        row.Add(Text(dep.Value, "dim"));
                        content.Add(row);
                    }
            }
            else
            {
                bool hasDescription = !string.IsNullOrWhiteSpace(detail.Description);
                bool hasReadme = !string.IsNullOrWhiteSpace(detail.ReadmeHtml);
                if (hasDescription) content.Add(Plain(detail.Description, true));
                if (hasDescription && hasReadme) content.Add(Rule());
                if (hasReadme) Rich(content, detail.ReadmeHtml, true);
                if (!hasDescription && !hasReadme) content.Add(Text("No description.", "dim"));
            }
        }

        private void UpmInstalledButtons(VisualElement row, PackageInfo info)
        {
            var remove = ActionButton("Remove", () => RemoveUpm(info.name), info.isDirectDependency);
            if (!info.isDirectDependency) remove.tooltip = "Installed as a dependency — remove the package that needs it first.";
            row.Add(remove);
            row.Add(ActionButton("Open in Package Manager", () => UnityEditor.PackageManager.UI.Window.Open(info.name)));
        }

        private async void Install(UpmPackageDetailDto detail, string version)
        {
            SetBusy(true);
            _setStatus($"Installing {detail.Name} {version}…", false, null);
            try
            {
                await UpmService.Install(detail, version);
                _setStatus($"Installed {detail.Name} {version}.", false, null);
            }
            catch (Exception e)
            {
                _setStatus(e.Message, true, null);
            }
            finally
            {
                SetBusy(false);
                UpmChanged?.Invoke();
            }
        }

        private async void RemoveUpm(string name)
        {
            if (!EditorUtility.DisplayDialog("Remove package", $"Remove {name} from the project?", "Remove", "Cancel")) return;
            SetBusy(true);
            _setStatus($"Removing {name}…", false, null);
            try
            {
                await UpmService.Remove(name);
                _setStatus($"Removed {name}.", false, null);
            }
            catch (Exception e)
            {
                _setStatus(e.Message, true, null);
            }
            finally
            {
                SetBusy(false);
                UpmChanged?.Invoke();
            }
        }

        // ---------- building blocks ----------

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Render();
        }

        private void Header(string title, params string[] subtitleParts)
        {
            Add(Text(title, "details__title"));
            var sub = Text(string.Join(" · ", subtitleParts.Where(s => !string.IsNullOrWhiteSpace(s))), "details__subtitle");
            sub.selection.isSelectable = true;
            Add(sub);
        }

        private static VisualElement MainImage(string url)
        {
            var box = new VisualElement();
            box.AddToClassList("details__image");
            var placeholder = Theme.IconElement("Texture Icon", 32, "details__image-placeholder");
            box.Add(placeholder);
            LoadImage(box, placeholder, url);
            return box;
        }

        private static async void LoadImage(VisualElement box, VisualElement placeholder, string url)
        {
            try
            {
                var texture = await ServerClient.GetImage(url);
                box.style.backgroundImage = Background.FromTexture2D(texture);
                placeholder.RemoveFromHierarchy();
            }
            catch (ServerException)
            {
                // Keep the placeholder: the image is decoration, the rest of the pane still works.
            }
        }

        private void StatusLine(string text, State state)
        {
            var line = new VisualElement();
            line.AddToClassList("details__state");
            line.AddToClassList(state switch
            {
                State.Installed => "details__state--installed",
                State.Warning => "details__state--warning",
                State.Dim => "details__state--dim",
                _ => "details__state--neutral",
            });
            if (state == State.Warning) line.Add(Theme.IconElement("console.warnicon.sml", 16, "details__state-icon"));
            line.Add(Text(text, "details__state-text"));
            Add(line);
        }

        private void Actions(Action<VisualElement> fill)
        {
            var row = new VisualElement();
            row.AddToClassList("details__actions");
            fill(row);
            Add(row);
        }

        private Button ActionButton(string text, Action onClick, bool enabled = true, bool primary = false)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("tdv-button");
            if (primary) button.AddToClassList("tdv-button--primary");
            button.SetEnabled(enabled && !_busy);
            return button;
        }

        private DropdownField VersionPicker(List<string> versions, string selected, string latest)
        {
            var labels = versions.Select(v => v == latest ? v + " (latest)" : v).ToList();
            var field = new DropdownField(labels, Math.Max(0, versions.IndexOf(selected)));
            field.AddToClassList("details__version");
            field.SetEnabled(!_busy);
            field.RegisterValueChangedCallback(e =>
            {
                // field.index is not updated yet while the change event runs; map the new label instead.
                int i = labels.IndexOf(e.newValue);
                if (i < 0) return;
                _version = versions[i];
                Render();
            });
            return field;
        }

        private void Tabs(params string[] names)
        {
            if (!names.Contains(_tab)) _tab = names[0];
            var row = new VisualElement();
            row.AddToClassList("tabs");
            foreach (string name in names)
            {
                var tab = new Button(() =>
                {
                    _tab = name;
                    Render();
                }) { text = name };
                tab.AddToClassList("tab");
                tab.EnableInClassList("tab--selected", name == _tab);
                row.Add(tab);
            }
            Add(row);
        }

        private VisualElement TabContent()
        {
            var content = new VisualElement();
            content.AddToClassList("tab-content");
            Add(content);
            return content;
        }

        private static VisualElement VersionRow(string version, bool latest, bool inUse, string meta, string notes)
        {
            var row = new VisualElement();
            row.AddToClassList("version-row");
            var head = new VisualElement();
            head.AddToClassList("version-row__head");
            head.Add(Text(version, "version-row__number"));
            if (latest) head.Add(Text("latest", "badge", "badge--latest"));
            if (inUse) head.Add(Text("in use", "badge", "badge--in-use"));
            head.Add(Text(meta, "small", "dim", "version-row__meta"));
            row.Add(head);
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var notesLabel = Text(notes, "version-row__notes");
                notesLabel.enableRichText = false;
                row.Add(notesLabel);
            }
            return row;
        }

        /// <summary>One Label per block: a single huge Label can exceed UI Toolkit's mesh limits, and each kind has its own spacing.</summary>
        private static void Rich(VisualElement parent, string html, bool first)
        {
            foreach (var block in HtmlToRichText.Convert(html, Theme.CodeColor, Theme.LinkColor))
            {
                if (block.Kind == RichKind.Rule)
                {
                    parent.Add(Rule());
                    first = true;
                    continue;
                }
                var label = Text(block.Text, "rich", block.Kind switch
                {
                    RichKind.Heading1 => "rich--h1",
                    RichKind.Heading2 => "rich--h2",
                    RichKind.Heading3 => "rich--h3",
                    RichKind.List => "rich--list",
                    RichKind.Code => "rich--code",
                    RichKind.Table => "rich--table",
                    _ => "rich--p",
                });
                if (first) label.AddToClassList("rich--first");
                label.selection.isSelectable = true;
                parent.Add(label);
                first = false;
            }
        }

        private static VisualElement Rule()
        {
            var rule = new VisualElement();
            rule.AddToClassList("rich--rule");
            return rule;
        }

        private static Label Plain(string text, bool first)
        {
            var label = Text(text, "rich", "rich--p");
            if (first) label.AddToClassList("rich--first");
            label.enableRichText = false;
            label.selection.isSelectable = true;
            return label;
        }

        private static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            label.AddToClassList("t");
            foreach (string c in classes) label.AddToClassList(c);
            return label;
        }

        private static string Date(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd");

        /// <summary>"21.6 MB / 48.0 MB"; both in KB when the file is under 1 MB.</summary>
        private static string Progress(ulong done, long total)
        {
            bool kb = total < 1024 * 1024;
            double unit = kb ? 1024.0 : 1024.0 * 1024.0;
            string suffix = kb ? "KB" : "MB";
            return $"{done / unit:0.0} {suffix} / {total / unit:0.0} {suffix}";
        }

        private static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            string[] units = { "KB", "MB", "GB" };
            double value = bytes;
            int unit = -1;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return $"{value:0.0} {units[unit]}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PackageManager.Shared;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ThanhDV.PackageManager
{
    /// <summary>Lists the server's .unitypackage and UPM packages next to what this project already has, like Unity's Package Manager.</summary>
    internal class PackageManagerWindow : EditorWindow
    {
        private const float NarrowWidth = 860;
        // An action's outcome waiting to be shown, kept across domain reloads ("!" prefix = error).
        private const string OutcomeKey = "ThanhDV.PackageManager.Outcome";

        private static readonly (SourceView View, string Label)[] Sources =
        {
            (SourceView.InProject, "In Project"),
            (SourceView.UnityPackages, "Unity Packages"),
            (SourceView.Upm, "UPM"),
        };

        // Kept across domain reloads (importing scripts or installing a UPM package triggers one).
        [SerializeField] private SourceView _view = SourceView.UnityPackages;
        [SerializeField] private string _selectedKey;

        private List<PackageSummaryDto> _packages = new List<PackageSummaryDto>();
        private string _packagesError;
        private UpmListDto _upm;
        private string _upmError;
        private Dictionary<string, PackageInfo> _installed = new Dictionary<string, PackageInfo>();
        private bool _loading;
        private string _loadedUrl;
        // "Imported …" / "Installed …" that arrived while the window was being rebuilt or loading; shown once loading ends.
        private string _outcome;
        private bool _outcomeIsError;

        private readonly List<PackageItem> _items = new List<PackageItem>();
        private VisualElement _sidebar;
        private ToolbarSearchField _search;
        private ListView _list;
        private VisualElement _listMessage;
        private Label _status;
        private ProgressBar _progress;
        private DetailsView _details;

        [MenuItem("Tools/ThanhDV/Package Manager")]
        public static void Open()
        {
            var window = GetWindow<PackageManagerWindow>();
            window.titleContent = new GUIContent("ThanhDV Package Manager");
            window.minSize = new Vector2(720, 400);
        }

        private void OnEnable()
        {
            ProjectData.Changed += OnLocalChange;
            ImportTracker.Changed += OnLocalChange;
            ImportTracker.Finished += OnActionFinished;
            PackageRemover.Removed += OnActionFinished;
            DependencyImporter.Status += OnActionFinished;
            UnityEditor.PackageManager.Events.registeredPackages += OnRegisteredPackages;
        }

        private void OnDisable()
        {
            ProjectData.Changed -= OnLocalChange;
            ImportTracker.Changed -= OnLocalChange;
            ImportTracker.Finished -= OnActionFinished;
            PackageRemover.Removed -= OnActionFinished;
            DependencyImporter.Status -= OnActionFinished;
            UnityEditor.PackageManager.Events.registeredPackages -= OnRegisteredPackages;
            // Importing scripts or installing a UPM package reloads the domain right after the action ends;
            // carry its outcome over to the rebuilt window.
            if (_outcome == null && _status != null && IsOutcome(_status.text))
            {
                _outcome = _status.text;
                _outcomeIsError = _status.ClassListContains("footer__status--error");
            }
            if (_outcome != null) SessionState.SetString(OutcomeKey, (_outcomeIsError ? "!" : " ") + _outcome);
        }

        // Only when the user closes the window (not on a domain reload): a reopened window starts fresh.
        private void OnDestroy() => SessionState.EraseString(OutcomeKey);

        private void CreateGUI()
        {
            var root = rootVisualElement;
            Theme.Apply(root);
            Theme.Layout("PackageManagerWindow.uxml").CloneTree(root);

            var settings = root.Q<ToolbarButton>("settings");
            settings.Add(Theme.IconElement("SettingsIcon", 16));
            settings.clicked += () => SettingsService.OpenProjectSettings(PackageManagerSettingsProvider.Path);
            var refresh = root.Q<Button>("refresh");
            refresh.Add(Theme.IconElement("Refresh", 16));
            refresh.clicked += () => _ = Reload(true);
            _search = root.Q<ToolbarSearchField>("search");
            _search.RegisterValueChangedCallback(_ => RebuildItems());

            _sidebar = root.Q("sidebar");
            foreach (var (view, label) in Sources)
            {
                var item = new VisualElement { userData = view };
                item.AddToClassList("sidebar-item");
                item.Add(Text(label, "t"));
                item.Add(Text("", "t", "small", "dim"));
                item.RegisterCallback<ClickEvent>(_ => SelectView(view));
                _sidebar.Add(item);
            }

            _listMessage = root.Q("list-message");
            _status = root.Q<Label>("status");
            _progress = root.Q<ProgressBar>("progress");
            _progress.style.display = DisplayStyle.None;
            _list = root.Q<ListView>("list");
            _list.itemsSource = _items;
            _list.makeItem = MakeRow;
            _list.bindItem = (row, i) => BindRow(row, _items[i]);
            _list.selectionChanged += selection =>
            {
                var item = selection.OfType<PackageItem>().FirstOrDefault();
                _selectedKey = item?.Key;
                _details.Show(item);
            };

            _details = new DetailsView(() => _installed, SetStatus);
            _details.UpmChanged += () => _ = RefreshInstalled();
            root.Q<ScrollView>("details-scroll").Add(_details);

            // Narrow windows give the details pane room: the list drops to 200 px, and back to 300 when wide again.
            // Only on crossing the threshold, so a width the user dragged to is kept otherwise.
            var split = root.Q<TwoPaneSplitView>("split");
            bool? narrow = null;
            root.RegisterCallback<GeometryChangedEvent>(e =>
            {
                bool now = e.newRect.width < NarrowWidth;
                if (now == narrow) return;
                narrow = now;
                // TwoPaneSplitView's own setter is internal; move the pane and its drag line together.
                float width = now ? 200 : 300;
                split.fixedPane.style.width = width;
                var anchor = split.Q(className: "unity-two-pane-split-view__dragline-anchor");
                if (anchor != null) anchor.style.left = width;
            });

            string carried = SessionState.GetString(OutcomeKey, "");
            SessionState.EraseString(OutcomeKey);
            if (_outcome == null && carried.Length > 1)
            {
                _outcome = carried.Substring(1);
                _outcomeIsError = carried[0] == '!';
            }
            SelectView(_view);
            _ = Reload(false);
        }

        private void SelectView(SourceView view)
        {
            _view = view;
            foreach (var item in _sidebar.Children())
                item.EnableInClassList("sidebar-item--selected", (SourceView)item.userData == view);
            RebuildItems();
        }

        private async Task Reload(bool rereadProjectData)
        {
            if (_loading) return;
            _loading = true;
            if (rereadProjectData) ProjectData.Reload();
            _loadedUrl = ProjectData.Instance.ServerUrl;
            _details.ClearCache();
            SetStatus("Loading packages…", false);
            RebuildItems();

            var packages = Try(ServerClient.ListPackages());
            var upm = Try(ServerClient.ListUpm());
            var installed = Try(UpmService.ListInstalled());

            (_packages, _packagesError) = await packages;
            _packages ??= new List<PackageSummaryDto>();
            (_upm, _upmError) = await upm;
            if (_upm != null && !_upm.Available) _upmError = "Verdaccio is not responding.";
            var (installedResult, installedError) = await installed;
            _installed = installedResult ?? new Dictionary<string, PackageInfo>();

            _loading = false;
            string error = _packagesError ?? _upmError ?? installedError;
            string warning = _upm != null && _upm.Available ? UpmService.SharedScopesWarning(_upm.Registry) : null;
            // Not configured yet is a normal first state (the list explains it), not something to log.
            if (!ServerClient.IsConfigured) SetStatus("", false);
            else if (error == null && _outcome != null) SetStatus(_outcome, _outcomeIsError);
            else if (error == null && warning != null) SetStatus(warning, true);
            else SetStatus(error ?? $"Loaded {_packages.Count} Unity package(s) and {_upm?.Packages.Count ?? 0} UPM package(s).", error != null);
            _outcome = null;
            RebuildItems();
        }

        /// <summary>A finished action's message, not routine loading text or something still in progress ("…").</summary>
        private static bool IsOutcome(string message) =>
            !string.IsNullOrEmpty(message) && !message.StartsWith("Loading") && !message.StartsWith("Loaded") && !message.EndsWith("…");

        private async Task RefreshInstalled()
        {
            var (installed, error) = await Try(UpmService.ListInstalled());
            if (error != null) SetStatus(error, true);
            else _installed = installed;
            RebuildItems();
        }

        private static async Task<(T Result, string Error)> Try<T>(Task<T> task)
        {
            try { return (await task, null); }
            catch (Exception e) { return (default, e.Message); }
        }

        private void OnLocalChange()
        {
            if (_list == null) return;
            if (ProjectData.Instance.ServerUrl != _loadedUrl) _ = Reload(false);
            else RebuildItems();
        }

        /// <summary>Import and Remove outcomes, reported by ImportTracker / PackageRemover / DependencyImporter.</summary>
        private void OnActionFinished(string message, bool isError) => SetStatus(message, isError);

        private void OnRegisteredPackages(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            if (_list != null) _ = RefreshInstalled();
        }

        // ---------- items ----------

        private List<PackageItem> UnityPackageItems()
        {
            var data = ProjectData.Instance;
            return _packages.Select(p =>
            {
                var record = data.Find(p.PackageId);
                return new PackageItem
                {
                    Id = p.PackageId,
                    DisplayName = p.DisplayName,
                    Subtitle = p.Publisher,
                    LatestVersion = p.LatestVersion,
                    InstalledVersion = record?.Version,
                    Missing = record != null && PackageRemover.IsMissing(record),
                    Description = p.ShortDescription,
                };
            }).ToList();
        }

        private List<PackageItem> UpmItems()
        {
            if (_upm == null) return new List<PackageItem>();
            return _upm.Packages.Select(p => new PackageItem
            {
                IsUpm = true,
                Id = p.Name,
                DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName,
                Subtitle = p.Author,
                LatestVersion = p.Version,
                InstalledVersion = _installed.TryGetValue(p.Name, out var info) ? info.version : null,
                Description = p.Description,
            }).ToList();
        }

        /// <summary>Everything this project has from the server, including packages the server / registry no longer lists.</summary>
        private List<PackageItem> InProjectItems(List<PackageItem> unityPackages, List<PackageItem> upm)
        {
            var items = unityPackages.Where(i => i.InstalledVersion != null).ToList();
            var listed = new HashSet<string>(_packages.Select(p => p.PackageId));
            // "Gone" only once the server's list actually came back; without it (no URL, loading, error) we don't know.
            bool serverListed = ServerClient.IsConfigured && !_loading && _packagesError == null;
            items.AddRange(ProjectData.Instance.Imported.Where(r => !listed.Contains(r.PackageId)).Select(r => new PackageItem
            {
                Id = r.PackageId,
                DisplayName = r.DisplayName,
                InstalledVersion = r.Version,
                Missing = PackageRemover.IsMissing(r),
                Gone = serverListed,
            }));

            items.AddRange(upm.Where(i => i.InstalledVersion != null));
            if (_upm != null && _upm.Available && !_loading)
            {
                var names = new HashSet<string>(_upm.Packages.Select(p => p.Name));
                items.AddRange(_installed.Values
                    .Where(p => !names.Contains(p.name) && p.registry != null && ManifestRegistry.SameUrl(p.registry.url, _upm.Registry))
                    .Select(p => new PackageItem
                    {
                        IsUpm = true,
                        Id = p.name,
                        DisplayName = string.IsNullOrWhiteSpace(p.displayName) ? p.name : p.displayName,
                        Subtitle = p.author?.name,
                        InstalledVersion = p.version,
                        Gone = true,
                    }));
            }
            return items;
        }

        private void RebuildItems()
        {
            if (_list == null) return;
            var unityPackages = UnityPackageItems();
            var upm = UpmItems();
            var inProject = InProjectItems(unityPackages, upm);
            UpdateCounts(inProject.Count, unityPackages.Count, upm.Count);

            var items = _view switch
            {
                SourceView.InProject => inProject,
                SourceView.UnityPackages => unityPackages,
                _ => upm,
            };
            string query = _search.value?.Trim();
            if (!string.IsNullOrEmpty(query)) items = items.Where(i => i.Matches(query)).ToList();

            _items.Clear();
            _items.AddRange(items.OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase));
            _list.Rebuild();

            int index = _items.FindIndex(i => i.Key == _selectedKey);
            // Without notify: _selectedKey survives switching to a view that doesn't list the package.
            _list.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
            _details.Show(index >= 0 ? _items[index] : null);
            ShowListMessage(query);
        }

        /// <summary>Counts stay blank while they mean nothing: loading, no server URL, or that source failed.</summary>
        private void UpdateCounts(int inProject, int unityPackages, int upm)
        {
            bool known = !_loading && ServerClient.IsConfigured;
            var counts = new[]
            {
                known && _packagesError == null ? inProject.ToString() : "",
                known && _packagesError == null ? unityPackages.ToString() : "",
                known && _upmError == null ? upm.ToString() : "",
            };
            int i = 0;
            foreach (var item in _sidebar.Children())
                ((Label)item[1]).text = counts[i++];
        }

        private void ShowListMessage(string query)
        {
            _listMessage.Clear();
            var (message, isError) = EmptyMessage(query);
            _listMessage.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
            // An empty ListView adds its own "List is empty" label; the message replaces it.
            _list.style.display = message == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (message == null) return;
            if (isError) _listMessage.Add(Theme.IconElement("console.erroricon.sml", 16, "list-message__icon"));
            _listMessage.Add(Text(message, "t", "list-message__text"));
        }

        private (string Message, bool IsError) EmptyMessage(string query)
        {
            if (_items.Count > 0) return (null, false);
            if (_loading) return ("Loading…", false);
            if (!ServerClient.IsConfigured) return ("No server URL set. Click Settings and enter the portal URL.", false);
            string error = _view switch
            {
                SourceView.UnityPackages => _packagesError,
                SourceView.Upm => _upmError,
                _ => _packagesError ?? _upmError,
            };
            if (error != null) return (error, true);
            if (!string.IsNullOrEmpty(query)) return ($"No packages match \"{query}\".", false);
            return (_view == SourceView.InProject ? "This project uses no packages from the server yet." : "The server has no packages yet.", false);
        }

        // ---------- rows ----------

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("package-row");
            var text = new VisualElement();
            text.AddToClassList("package-row__text");
            text.Add(Text("", "t", "nowrap", "package-row__name"));
            text.Add(Text("", "t", "small", "dim", "nowrap", "package-row__sub"));
            row.Add(text);
            row.Add(Text("", "t", "small", "dim", "package-row__version"));
            var mark = new VisualElement();
            mark.AddToClassList("package-row__mark");
            mark.Add(Text("✓", "t", "package-row__check"));
            mark.Add(Theme.IconElement("console.warnicon.sml", 14));
            row.Add(mark);
            return row;
        }

        private static void BindRow(VisualElement row, PackageItem item)
        {
            var labels = row.Query<Label>().ToList();
            labels[0].text = item.DisplayName;
            string kind = item.IsUpm ? "UPM" : "Unity Package";
            labels[1].text = string.IsNullOrWhiteSpace(item.Subtitle) ? kind : $"{kind} · {item.Subtitle}";
            labels[2].text = item.InstalledVersion ?? item.LatestVersion ?? "";
            var mark = row[row.childCount - 1];
            // A missing package shows the warning in the ✓ cell, so the version column stays aligned.
            mark[0].style.display = item.InstalledVersion != null && !item.Missing ? DisplayStyle.Flex : DisplayStyle.None;
            mark[1].style.display = item.Missing ? DisplayStyle.Flex : DisplayStyle.None;
            row.EnableInClassList("package-row--gone", item.Gone);
            row.tooltip = item.Tooltip;
        }

        private static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (string c in classes) label.AddToClassList(c);
            return label;
        }

        // ---------- status bar ----------

        /// <param name="progress">0–1 shows the download bar; null hides it.</param>
        private void SetStatus(string message, bool isError, float? progress = null)
        {
            // Still being rebuilt, or about to show "Loaded …": hold an outcome until loading ends.
            if ((_status == null || _loading) && !progress.HasValue && IsOutcome(message))
            {
                _outcome = message;
                _outcomeIsError = isError;
            }
            if (_status == null) return;
            _status.text = message ?? "";
            _status.EnableInClassList("footer__status--error", isError);
            _progress.style.display = progress.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (progress.HasValue) _progress.value = progress.Value;
            if (isError && !string.IsNullOrEmpty(message)) Debug.LogWarning("[ThanhDV Package Manager] " + message);
        }
    }
}

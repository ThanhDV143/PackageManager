using UnityEditor;
using UnityEngine.UIElements;

namespace ThanhDV.PackageManager
{
    /// <summary>Project Settings > ThanhDV Package Manager: the portal URL, saved in ProjectSettings/ so the team shares it.</summary>
    internal static class PackageManagerSettingsProvider
    {
        public const string Path = "Project/ThanhDV Package Manager";

        [SettingsProvider]
        public static SettingsProvider Create() => new SettingsProvider(Path, SettingsScope.Project)
        {
            label = "ThanhDV Package Manager",
            keywords = new[] { "package", "server", "unitypackage", "upm" },
            activateHandler = (_, root) =>
            {
                Theme.Apply(root);
                var page = new VisualElement();
                page.AddToClassList("settings");
                root.Add(page);

                var title = new Label("ThanhDV Package Manager");
                title.AddToClassList("t");
                title.AddToClassList("settings__title");
                page.Add(title);

                // Delayed: save on Enter / focus out, not on every keystroke.
                var url = new TextField("Server URL") { value = ProjectData.Instance.ServerUrl, isDelayed = true };
                url.AddToClassList("settings__url");
                url.RegisterValueChangedCallback(e =>
                {
                    ProjectData.Instance.ServerUrl = e.newValue.Trim();
                    ProjectData.Instance.Save();
                });
                page.Add(url);

                var help = new HelpBox(
                    "URL of the package portal (the web app), e.g. https://packages.example.com — not the Verdaccio URL. " +
                    $"Saved in {ProjectData.FilePath}, so the whole team shares it once committed.",
                    HelpBoxMessageType.Info);
                help.AddToClassList("settings__help");
                page.Add(help);
            },
        };
    }
}

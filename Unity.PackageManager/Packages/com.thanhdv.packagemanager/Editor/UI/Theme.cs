using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.PackageManager
{
    /// <summary>
    /// Styles shared by every window of the tool: the main USS, the four accent colors for the editor skin
    /// (--tdv-green / amber / red / blue), and editor icons for that skin.
    /// </summary>
    internal static class Theme
    {
        private const string UiPath = "Packages/com.thanhdv.packagemanager/Editor/UI/";

        public static bool IsDark => EditorGUIUtility.isProSkin;

        // Rich text can't read USS variables, so descriptions get the accents as hex (same values as the theme USS).
        public static string CodeColor => IsDark ? "#F4BC02" : "#6E4700";
        public static string LinkColor => IsDark ? "#7BAEFA" : "#0645AD";

        public static void Apply(VisualElement root)
        {
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UiPath + "PackageManager.uss"));
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UiPath + (IsDark ? "ThemeDark.uss" : "ThemeLight.uss")));
            root.AddToClassList("tdv");
        }

        public static VisualTreeAsset Layout(string name) => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + name);

        public static Texture Icon(string name)
        {
            var content = IsDark ? EditorGUIUtility.IconContent("d_" + name) : null;
            return content?.image != null ? content.image : EditorGUIUtility.IconContent(name).image;
        }

        /// <summary>An icon as a fixed-size element (tinted by USS through -unity-background-image-tint-color).</summary>
        public static VisualElement IconElement(string name, int size, string className = null)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = icon.style.height = size;
            icon.style.flexShrink = 0;
            icon.style.backgroundImage = Background.FromTexture2D(Icon(name) as Texture2D);
            if (className != null) icon.AddToClassList(className);
            return icon;
        }
    }
}

using System;
using System.IO;
using System.Text;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Helpers
{
    public static class BigBoxRommStarIconsInjector
    {
        private const string TargetConditionFind = "<Condition Binding=\"{Binding ElementName=Installed, Path=HasBadge}\" Value=\"False\"/>";
        private const string TargetConditionInsert = "<Condition Binding=\"{Binding ElementName=RommStarSynced, Path=HasBadge}\" Value=\"False\"/>";
        private const string TargetConditionInsert2 = "<Condition Binding=\"{Binding ElementName=InstallingFromRomm, Path=HasBadge}\" Value=\"False\"/>";


        private const string TargetBadgeFind = "<coverFlow:FlowBadge x:Name=\"Installed\" DataContext=\"{Binding SelectedGame}\" BadgeName=\"Installed\" Style=\"{StaticResource FlowBadgeVisibilityStyle}\"/>";
        private const string TargetBadgeInsert = "<coverFlow:FlowBadge x:Name=\"RommStarSynced\" DataContext=\"{Binding SelectedGame}\" BadgeName=\"rommStarSynced\" Style=\"{StaticResource FlowBadgeVisibilityStyle}\"/>";
        private const string TargetBadgeInsert2 = "<coverFlow:FlowBadge x:Name=\"InstallingFromRomm\" DataContext=\"{Binding SelectedGame}\" BadgeName=\"installingFromRomm\" Style=\"{StaticResource FlowBadgeVisibilityStyle}\"/>";

        /// <summary>
        /// Automatically patches the active theme's GameDetailsView.xaml to include the RommStarSynced badge and condition.
        /// Call this once when your plugin initializes.
        /// </summary>
        public static void PatchThemeXaml(string themeName)
        {
            if (string.IsNullOrEmpty(themeName)) return;

            try
            {
                // 1. Locate the active Big Box theme directory via PluginHelper or standard LaunchBox paths
                string themePath = GetActiveThemeGameDetailsPath(themeName);

                if (string.IsNullOrEmpty(themePath) || !File.Exists(themePath))
                {
                    // Fallback: search common themes directory if active theme path resolution varies
                    return;
                }

                string content = File.ReadAllText(themePath, Encoding.UTF8);
                bool modified = false;

                // 2. Check and inject the MultiDataTrigger condition if missing
                if (content.Contains(TargetConditionFind) && !content.Contains(TargetConditionInsert))
                {
                    // Insert right after the SaveStates condition line
                    string replacementBlock = TargetConditionFind + Environment.NewLine + "                                            " 
                        + TargetConditionInsert + Environment.NewLine + "                                            " 
                        + TargetConditionInsert2;

                    content = content.Replace(TargetConditionFind, replacementBlock);
                    modified = true;
                }

                // 3. Check and inject the FlowBadge element if missing
                if (content.Contains(TargetBadgeFind) && !content.Contains(TargetBadgeInsert))
                {
                    // Insert right after the SaveStates badge line
                    string replacementBlock = TargetBadgeFind + Environment.NewLine + "                            " 
                        + TargetBadgeInsert + Environment.NewLine + "                            "
                        + TargetBadgeInsert2;

                    content = content.Replace(TargetBadgeFind, replacementBlock);
                    modified = true;
                }

                // 4. Save back to disk only if changes were made
                if (modified)
                {
                    File.WriteAllText(themePath, content, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RommStar] Failed to patch GameDetailsView.xaml: {ex.Message}");
            }
        }

        private static string GetActiveThemeGameDetailsPath(string themeName)
        {
            try
            {
                // LaunchBox exposes the core installation directory or theme paths through PluginHelper/StateManager
                string appDir = Path.GetDirectoryName(typeof(PluginHelper).Assembly.Location);

                if (string.IsNullOrEmpty(appDir)) return null;

                // Path structure: [LaunchBoxFolder]/Themes/[ActiveThemeName]/Views/GameDetailsView.xaml
                // Since Big Box active theme name can vary, we can check the standard "Big Box" theme folder or current applied theme.
                // Alternatively, search inside Themes directory for any GameDetailsView.xaml file.
                string viewsDir = Path.Combine(Constants.LaunchboxRootDir, $"Themes\\{themeName}");
                if (!Directory.Exists(viewsDir)) return null;

                // Search recursively for GameDetailsView.xaml across installed themes
                var matches = Directory.GetFiles(viewsDir, "Views/GameDetailsView.xaml", SearchOption.AllDirectories);
                foreach (var match in matches)
                {
                    // Check if it's currently in use or return the first valid match (or target the user's active theme)
                    // If you want to be precise, you can check which theme folder is currently set in BigBox settings, 
                    // or patch all installed themes so it works regardless of what theme the user switches to.
                    return match;
                }
            }
            catch { }

            return null;
        }
    }
}
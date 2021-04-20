using System.IO;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.Build.Editor;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

namespace Unity.Platforms.Android.Build
{
    static class AndroidMenuItem
    {
        const string k_CreateBuildConfigurationAssetClassic = BuildConfigurationMenuItem.k_BuildConfigurationMenu + "Android Classic Build Configuration";

        [MenuItem(k_CreateBuildConfigurationAssetClassic, true)]
        static bool CreateBuildConfigurationAssetClassicValidation()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(k_CreateBuildConfigurationAssetClassic)]
        static void CreateBuildConfigurationAssetClassic()
        {
            Selection.activeObject = BuildConfigurationMenuItem.CreateAssetInActiveDirectory(
                "AndroidClassic",
                new GeneralSettings(),
                new SceneList(),
                new ClassicBuildProfile { Platform = new AndroidPlatform() });
        }
    }
}

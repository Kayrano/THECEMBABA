using Unity.Build;
using Unity.Properties;

namespace Unity.Platforms.Android.Build
{
    enum AndroidBuildSystem
    {
        Gradle,
        VisualStudio
    }

    enum AndroidTargetType
    {
        AndroidPackage,
        AndroidAppBundle
    }

    class AndroidExportSettings : IBuildComponent
    {
        [CreateProperty] public AndroidTargetType TargetType { set; get; } = AndroidTargetType.AndroidPackage;
        [CreateProperty] public bool ExportProject { set; get; } = false;
        [CreateProperty] public AndroidBuildSystem BuildSystem { set; get; } = AndroidBuildSystem.Gradle;
    }
}

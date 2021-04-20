using Unity.Build;
using Unity.Properties;

namespace Unity.Platforms.Android.Build
{
    enum AspectRatioMode
    {
        LegacyWideScreen,
        SuperWideScreen,
        Custom
    }

    class AndroidAspectRatio : IBuildComponent
    {
        [CreateProperty]
        public AspectRatioMode AspectRatioMode { set; get; } = AspectRatioMode.SuperWideScreen;

        [CreateProperty]
        public float CustomAspectRatio { set; get; } = 2.1f;
    }
}

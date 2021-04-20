using Unity.Build;
using Unity.Properties;

namespace Unity.Platforms.Android.Build
{
    sealed class ApplicationIdentifier : IBuildComponent
    {
        string m_PackageName;

        [CreateProperty]
        public string PackageName
        {
            get => !string.IsNullOrEmpty(m_PackageName) ? m_PackageName : "com.unity.DefaultPackage";
            set => m_PackageName = value;
        }
    }
}

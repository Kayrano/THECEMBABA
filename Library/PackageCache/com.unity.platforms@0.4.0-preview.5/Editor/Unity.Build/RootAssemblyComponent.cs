using Unity.Properties;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Build
{
    /// <summary>
    /// A <see cref="RootAssemblyComponent"/> allows to specify an Assembly Definition Asset for a build.
    /// </summary>
    public class RootAssemblyComponent : IBuildComponent
    {
        /// <summary>
        /// Gets or sets the root assembly for a build. This root
        /// assembly determines what other assemblies will be pulled in for the build.
        /// </summary>
        [CreateProperty]
#if UNITY_2020_1_OR_NEWER
        public LazyLoadReference<AssemblyDefinitionAsset> RootAssembly
        {
            get => m_RootAssembly;
            set
            {
                m_RootAssembly = value;
            }
        }
        LazyLoadReference<AssemblyDefinitionAsset> m_RootAssembly;
#else
        public AssemblyDefinitionAsset RootAssembly
        {
            get => m_RootAssembly;
            set
            {
                m_RootAssembly = value;
            }
        }
        AssemblyDefinitionAsset m_RootAssembly;
#endif
    }
}

using System.IO;
using Unity.Build;

namespace Unity.Platforms.Android.Build
{
    sealed class AndroidProjectArtifact : IBuildArtifact
    {
        public DirectoryInfo ProjectDirectory;
    }
}

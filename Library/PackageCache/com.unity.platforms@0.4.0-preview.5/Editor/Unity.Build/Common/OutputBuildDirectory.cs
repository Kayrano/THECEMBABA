using System;
using System.ComponentModel;
using Unity.Serialization;

namespace Unity.Build.Common
{
    /// <summary>
    /// Overrides the default output directory of Builds/BuildConfiguration.name to an arbitrary location.
    /// </summary>
    [FormerName("Unity.Build.Common.OutputBuildDirectory, Unity.Build.Common")]
    public class OutputBuildDirectory : IBuildComponent
    {
        public string OutputDirectory;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Remove usage. (RemovedAfter 2020-07-01)", true)]
    [FormerName("Unity.Build.Common.BuildStepExtensions, Unity.Build.Common")]
    public static class BuildStepExtensions
    {
        public static string GetOutputBuildDirectory(this BuildStep step, BuildContext context) => throw null;
    }
}

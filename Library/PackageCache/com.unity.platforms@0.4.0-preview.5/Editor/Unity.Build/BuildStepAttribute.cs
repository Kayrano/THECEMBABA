using System;
using System.ComponentModel;

namespace Unity.Build
{
    /// <summary>
    /// Attribute to configure various properties of a <see cref="BuildStepBase"/> derived type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BuildStepAttribute : Attribute
    {
        /// <summary>
        /// Display description of the build step.
        /// </summary>
        public string Description { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Remove usage, build steps no longer have names. (RemovedAfter 2020-07-01)", true)]
        public string Name
        {
            get => throw null;
            set => throw null;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Remove usage, build steps no longer have categories. (RemovedAfter 2020-07-01)", true)]
        public string Category
        {
            get => throw null;
            set => throw null;
        }
    }
}

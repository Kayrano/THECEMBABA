#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;
using UnityEditor;

namespace Unity.Platforms.Android.Build
{
    class GraphCopyGradleResources : BuildStepBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(GeneralSettings),
            typeof(AndroidAPILevels)
        };

        public override BuildResult Run(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var androidContext = context.GetValue<AndroidBuildContext>();
            var targetAPILevel = context.GetComponentOrDefault<AndroidAPILevels>().ResolvedTargetAPILevel;
            var srcAPKResources = classicContext.PlayerPackageDirectory.Combine("Apk");

            CopyStylesXML(androidContext, srcAPKResources, targetAPILevel);
            CopyStrinsgXML(androidContext, srcAPKResources, context.GetComponentOrDefault<GeneralSettings>().ProductName);
            CopyIcons(androidContext, srcAPKResources);

            return context.Success();
        }

        /// <summary>
        /// Returns which API the target file is intended for
        /// For ex, values-v28 is for API 28
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private int GetPathTargetAPI(NPath path)
        {
            int index = path.Parent.FileName.IndexOf("-v");
            if (index == -1)
                return 0;

            var targetAPIString = path.Parent.FileName.Substring(index + 2);
            int targetAPI;
            if (int.TryParse(targetAPIString, out targetAPI))
                return targetAPI;
            throw new Exception($"Failed to resolve target API for {path.Parent.FileName}");
        }

        private void CopyStylesXML(AndroidBuildContext androidContext, NPath srcAPKResources, AndroidSdkVersions targetAPILevel)
        {
            var destinations = new[] { androidContext.LauncherSrcMainDirectory, androidContext.LibrarySrcMainDirectory };

            foreach (var dstAPKResources in destinations)
            {
                int endIndex = srcAPKResources.ToString().Length;
                foreach (var file in srcAPKResources.Files("styles.xml", true))
                {
                    int pathTargetAPI = GetPathTargetAPI(file);
                    if (pathTargetAPI > (int)targetAPILevel)
                        continue;
                    var endPart = file.ToString().Substring(endIndex + 1);
                    var target = dstAPKResources.Combine(endPart);
                    CopyTool.Instance().Setup(target, file);
                    androidContext.AddGradleProjectFile(target);
                }
            }
        }

        private void CopyStrinsgXML(AndroidBuildContext androidContext, NPath srcAPKResources, string productName)
        {
            var xmlDocument = new AndroidXmlDocument(srcAPKResources.Combine("res", "values", "strings.xml"));
            xmlDocument.PatchStringRes("string", "app_name", productName);
            var target = androidContext.LauncherSrcMainDirectory.Combine("res", "values", "strings.xml");
            Backend.Current.AddWriteTextAction(target, xmlDocument.GetContents());
            androidContext.AddGradleProjectFile(target);
        }

        private void CopyIcons(AndroidBuildContext androidContext, NPath srcAPKResources)
        {
            // TODO: Round Icons, Adaptive Icons
            var iconFolders = new[] { "mipmap-anydpi-v26", "mipmap-mdpi" };
            foreach (var iconFolder in iconFolders)
            {
                foreach (var file in srcAPKResources.Combine("res", iconFolder).Files())
                {
                    var target = androidContext.LauncherSrcMainDirectory.Combine("res", iconFolder, file.FileName);
                    CopyTool.Instance().Setup(target, file);
                    androidContext.AddGradleProjectFile(target);
                }
            }
        }
    }
}
#endif

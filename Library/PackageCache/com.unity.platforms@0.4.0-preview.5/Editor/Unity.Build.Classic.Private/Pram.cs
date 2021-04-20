#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Tools;
using NiceIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.BuildTools;
using UnityEngine;

namespace Unity.Build.Classic.Private
{
	internal class Pram : RunTargetProviderBase
    {
        // Local pram development setup - set to machine local directory to quickly iterate on pram features.
        private static readonly NPath LocalPramDevelopmentRepository = null;
        // Enable tracing
        public static bool Trace { get; set; } = false;

        private IReadOnlyDictionary<string, NPath> PlatformAssemblyLoadPath { get; }
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Environment { get; }

        public Pram()
        {
            var platformAssemblyLoadPath = new Dictionary<string, NPath>();
            var environment = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            var plugins = TypeCacheHelper.ConstructTypesDerivedFrom<PramPlatformPlugin>();

            foreach (var plugin in plugins)
                foreach (var provider in plugin.Providers)
                {
                    platformAssemblyLoadPath[provider] = plugin.PlatformAssemblyLoadPath;
                    environment[provider] = plugin.Environment;
                }

            PlatformAssemblyLoadPath = platformAssemblyLoadPath;
            Environment = environment;
        }

        public override RunTargetBase[] Discover()
        {
            return Discover();
        }

        public RunTargetBase[] Discover(params string[] providers)
        {
            var detected = Execute(providers, "env-detect");
            var regex = new Regex(@"\s*(\S*)\s*\|\s*(\S*)");
            var devices = new List<PramRunTarget>();
            foreach (Match result in detected.Split('\n').Select(s => regex.Match(s)))
            {
                if (result.Success)
                {
                    var provider = result.Groups[1].Captures[0].Value;
                    var envId = result.Groups[2].Captures[0].Value;
                    var deviceData = new PramRunTarget(this, $"{provider} - {envId}", provider, envId);
                    devices.Add(deviceData);
                }
            }
            return devices.OrderBy(x => x.DisplayName).ToArray();
        }

        public void Deploy(string provider, string environment, string applicationId, NPath path) =>
            Execute(new[] { provider }, "app-deploy", provider, environment, applicationId, path.InQuotes());

        public void Start(string provider, string environment, string applicationId) =>
            Execute(new[] { provider }, "app-start", provider, environment, applicationId);

        public void ForceStop(string provider, string environment, string applicationId) =>
            Execute(new[] { provider }, "app-kill", provider, environment, applicationId);

        private string Execute(string[] providers, params string[] args)
        {
            var providersSet = (providers != null && providers.Any()) ? new HashSet<string>(providers) : null;

            var assemblyLoadPaths = PlatformAssemblyLoadPath
                .Where(x => providersSet?.Contains(x.Key) ?? true)
                .Select(x => x.Value)
                .ToArray();
            var environment = Environment
                .Where(x => providersSet?.Contains(x.Key) ?? true)
                .SelectMany(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);

            NPath platformsPackagePath = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.platforms").resolvedPath;

            var pramExecutable = (LocalPramDevelopmentRepository != null)
                ? LocalPramDevelopmentRepository.Combine("artifacts/PramDistribution/pram.exe")
                : platformsPackagePath.Combine("Editor/Unity.Build.Classic.Private/pram~/pram.exe");
            var platformAssembliesPaths = string.Join(" ", assemblyLoadPaths.Select(x => $"--assembly-load-path {x.InQuotes()}"));

            var trace = Trace ? "--trace --very-verbose" : "";
            var result = Shell.Execute(new Shell.ExecuteArgs
            {
                Executable = Paths.MonoBleedingEdgeCLI,
                Arguments = $"{pramExecutable.InQuotes()} {trace} {platformAssembliesPaths} {args.InQuotes().SeparateWithSpace()}",
                EnvVars = environment
            });

            if (Trace)
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", result.StdErr);

            if (!result.Success)
                throw new Exception($"Failed {result}\n{result.StdErr}");
            return result.StdOut;
        }
    }
}
#endif
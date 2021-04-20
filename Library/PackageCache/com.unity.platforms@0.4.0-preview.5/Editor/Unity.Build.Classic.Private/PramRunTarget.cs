#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
namespace Unity.Build.Classic.Private
{
    internal class PramRunTarget : RunTargetBase
    {
        public override string UniqueId => $"{ProviderName}-{EnvironmentId}";

        private Pram Pram { get; }
        private string ProviderName { get; }
        private string EnvironmentId { get; }

        public PramRunTarget(Pram pram, string displayName, string providerName, string environmentId)
        : base(displayName)
        {
            Pram = pram;
            ProviderName = providerName;
            EnvironmentId = environmentId;
        }

        public override void Deploy(string applicationId, string path)
        {
            Pram.Deploy(ProviderName, EnvironmentId, applicationId, path);
        }

        public override void Start(string applicationId)
        {
            Pram.Start(ProviderName, EnvironmentId, applicationId);
        }

        public override void ForceStop(string applicationId)
        {
            Pram.ForceStop(ProviderName, EnvironmentId, applicationId);
        }
    }
}
#endif
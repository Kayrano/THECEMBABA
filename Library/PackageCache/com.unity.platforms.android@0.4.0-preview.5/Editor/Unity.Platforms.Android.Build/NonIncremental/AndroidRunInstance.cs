using Unity.Build;

namespace Unity.Platforms.Android.Build
{
    sealed class AndroidRunInstance : IRunInstance
    {
        public bool IsRunning => true;

        public AndroidRunInstance()
        {
        }

        public void Dispose()
        {
        }
    }
}

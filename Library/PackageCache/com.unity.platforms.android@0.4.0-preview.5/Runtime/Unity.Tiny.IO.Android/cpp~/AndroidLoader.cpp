#if (UNITY_ANDROID && !STATIC_LINKING)
#include <dlfcn.h>
#include <jni.h>

class AssetLoader
{
    typedef void* (*fp_loadAsset)(const char* path, int *size, void* (*alloc)(size_t));
    void *m_libandroid;
    fp_loadAsset m_loadAsset;

public:
    AssetLoader()
    {
        m_libandroid = dlopen("lib_unity_tiny_android.so", RTLD_NOW | RTLD_LOCAL);
        if (m_libandroid != NULL)
        {
            m_loadAsset = reinterpret_cast<fp_loadAsset>(dlsym(m_libandroid, "loadAsset"));
        }
    }
    ~AssetLoader()
    {
        if (m_libandroid != NULL)
        {
            dlclose(m_libandroid);
        }
    }

    void* loadAsset(const char *path, int *size, void* (*alloc)(size_t))
    {
        return m_loadAsset(path, size, alloc);
    }
};

AssetLoader sAssetLoader;

extern "C"
JNIEXPORT void* loadAsset(const char *path, int *size, void* (*alloc)(size_t))
{
    return sAssetLoader.loadAsset(path, size, alloc);
}
#endif

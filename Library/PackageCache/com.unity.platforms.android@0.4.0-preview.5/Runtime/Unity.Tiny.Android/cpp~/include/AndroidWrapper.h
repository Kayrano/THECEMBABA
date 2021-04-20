#include <jni.h>
#include <android/log.h>

extern "C" {
    jobject get_activity();
    JavaVM* get_javavm();
}

class JavaVMThreadScope
{
public:
    JavaVMThreadScope()
    {
        m_env = 0;
        m_detached = get_javavm()->GetEnv((void**)&m_env, JNI_VERSION_1_2) == JNI_EDETACHED;
        if (m_detached)
        {
            get_javavm()->AttachCurrentThread(&m_env, NULL);
        }
        CheckException();
    }

    ~JavaVMThreadScope()
    {
        CheckException();
        if (m_detached)
        {
            get_javavm()->DetachCurrentThread();
        }
    }

    JNIEnv* GetEnv()
    {
        return m_env;
    }

private:
    JNIEnv* m_env;
    bool m_detached;

#if defined(DEBUG)
    void CheckException()
    {
        if (!m_env->ExceptionCheck())
            return;

        __android_log_print(ANDROID_LOG_INFO, "AndroidWrapper", "Java exception detected");
        m_env->ExceptionDescribe();
        m_env->ExceptionClear();
    }
#else
    void CheckException() {}
#endif
};


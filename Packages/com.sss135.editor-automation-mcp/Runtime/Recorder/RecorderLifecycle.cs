using UnityEngine;

namespace com.SSS135.EditorAutomation.Recorder
{
    /// <summary>
    /// Binds the recorder to the play-mode lifecycle: a session opens before the
    /// first scene loads and closes on application quit (which, in the Editor,
    /// fires on play-mode exit). All hooks are inert when recording is disabled.
    /// </summary>
    static class RecorderLifecycle
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnPlay()
        {
            Recorder.BeginSession();
            // Idempotent: drop any stale handler before re-subscribing so a second
            // play run in the same domain doesn't end the session twice.
            Application.quitting -= Recorder.EndSession;
            Application.quitting += Recorder.EndSession;
        }
    }
}

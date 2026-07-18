#nullable enable
using Extensions.Unity.PlayerPrefsEx;

namespace com.SSS135.EditorAutomation.Editor.API.TestRunner
{
    /// <summary>
    /// Stores the most recent TestRunnerApi run guid returned by
    /// <see cref="UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.Execute"/>.
    /// Backed by <see cref="PlayerPrefsString"/> so the value survives the domain reload
    /// that PlayMode entry triggers — mirrors the
    /// <see cref="TestResultCollector.TestCallRequestID"/> persistence pattern.
    ///
    /// Consumed by <c>tests-cancel</c> to look up the active run when the caller hands
    /// in a job ticket id. Cleared by <see cref="TestResultCollector.RunFinished"/> so a
    /// stale guid never targets a finished run.
    /// </summary>
    public static class RunGuidRegistry
    {
        public static PlayerPrefsString RunGuid = new PlayerPrefsString("EditorAutomation_TestRunner_RunGuid");
    }
}

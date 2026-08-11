using System;

namespace AppLauncher.Services
{
    /// <summary>
    /// Reports multi-step operation progress to the UI (1-based step index).
    /// </summary>
    public readonly record struct ProgressStep(int Step, int Total, string Message)
    {
        public static ProgressStep Of(int step, int total, string message) => new(step, total, message);
    }

    public static class ProgressReport
    {
        public static void Report(IProgress<ProgressStep>? progress, int step, int total, string message)
        {
            progress?.Report(ProgressStep.Of(step, total, message));
        }
    }
}

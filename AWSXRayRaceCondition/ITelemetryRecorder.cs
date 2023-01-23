using System.Runtime.CompilerServices;

namespace AWSXRayRaceCondition
{
    public interface ITelemetryRecorder
    {
        void Trace(Action method, string subsegmentName, [CallerMemberName] string methodName = null, string telemetryNamespace = null);

        TResult Trace<TResult>(Func<TResult> method, string subsegmentName, [CallerMemberName] string methodName = null, string telemetryNamespace = null);

        Task TraceAsync(Func<Task> method, string subsegmentName, [CallerMemberName] string methodName = null, string telemetryNamespace = null);

        Task<TResult> TraceAsync<TResult>(Func<Task<TResult>> method, string subsegmentName, [CallerMemberName] string methodName = null, string telemetryNamespace = null);
    }
}

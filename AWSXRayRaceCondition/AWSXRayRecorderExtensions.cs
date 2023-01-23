using Amazon.XRay.Recorder.Core;

namespace AWSXRayRaceCondition
{
    public static class AwsXRayRecorderExtensions
    {
        public static void WithMethodName(this AWSXRayRecorder recorder, string methodName)
        {
            ArgumentNullException.ThrowIfNull(recorder);

            if (!string.IsNullOrWhiteSpace(methodName))
            {
                recorder.AddAnnotation("Method", methodName);
            }
        }

        public static void WithNamespace(this AWSXRayRecorder recorder, string telemetryNamespace)
        {
            ArgumentNullException.ThrowIfNull(recorder);

            if (!string.IsNullOrWhiteSpace(telemetryNamespace))
            {
                recorder.SetNamespace(telemetryNamespace);
            }
        }
    }
}

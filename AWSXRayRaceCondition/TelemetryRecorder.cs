using Amazon.XRay.Recorder.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AWSXRayRaceCondition
{
    public class TelemetryRecorder : ITelemetryRecorder
    {
        internal const string SegmentName = "Telemetry";
        private readonly AWSXRayRecorder _recorder;

        public TelemetryRecorder(AWSXRayRecorder recorder)
        {
            _recorder = recorder;
        }

        public void Trace(Action method, string subsegmentName,
            [CallerMemberName] string methodName = null, string telemetryNamespace = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentException.ThrowIfNullOrEmpty(subsegmentName);

            if (_recorder?.XRayOptions.IsXRayTracingDisabled == false)
            {
                var cleanupSegment = BuildSegment();
                try
                {
                    var stopwatch = BeginTrace(subsegmentName, methodName, telemetryNamespace);
                    try
                    {
                        method();
                    }
                    catch (Exception ex)
                    {
                        AddException(ex);
                        throw;
                    }
                    finally
                    {
                        EndTrace(stopwatch);
                    }
                }
                finally
                {
                    CleanupSegment(cleanupSegment);
                }
            }
            else
            {
                method();
            }
        }

        public TResult Trace<TResult>(Func<TResult> method, string subsegmentName,
            [CallerMemberName] string methodName = null, string telemetryNamespace = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentException.ThrowIfNullOrEmpty(subsegmentName);

            if (_recorder?.XRayOptions.IsXRayTracingDisabled == false)
            {
                var cleanupSegment = BuildSegment();
                try
                {
                    var stopwatch = BeginTrace(subsegmentName, methodName, telemetryNamespace);
                    try
                    {
                        return method();
                    }
                    catch (Exception ex)
                    {
                        AddException(ex);
                        throw;
                    }
                    finally
                    {
                        EndTrace(stopwatch);
                    }
                }
                finally
                {
                    CleanupSegment(cleanupSegment);
                }
            }
            else
            {
                return method();
            }
        }

        public async Task TraceAsync(Func<Task> method, string subsegmentName,
            [CallerMemberName] string methodName = null, string telemetryNamespace = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentException.ThrowIfNullOrEmpty(subsegmentName);

            if (_recorder?.XRayOptions.IsXRayTracingDisabled == false)
            {
                var cleanupSegment = BuildSegment();
                try
                {
                    var stopwatch = BeginTrace(subsegmentName, methodName, telemetryNamespace);
                    try
                    {
                        await method();
                    }
                    catch (Exception ex)
                    {
                        AddException(ex);
                        throw;
                    }
                    finally
                    {
                        EndTrace(stopwatch);
                    }
                }
                finally
                {
                    CleanupSegment(cleanupSegment);
                }
            }
            else
            {
                await method();
            }
        }

        public async Task<TResult> TraceAsync<TResult>(Func<Task<TResult>> method, string subsegmentName,
            [CallerMemberName] string methodName = null, string telemetryNamespace = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentException.ThrowIfNullOrEmpty(subsegmentName);

            if (_recorder?.XRayOptions.IsXRayTracingDisabled == false)
            {
                var cleanupSegment = BuildSegment();
                try
                {
                    var stopwatch = BeginTrace(subsegmentName, methodName, telemetryNamespace);
                    try
                    {
                        return await method();
                    }
                    catch (Exception ex)
                    {
                        AddException(ex);
                        throw;
                    }
                    finally
                    {
                        EndTrace(stopwatch);
                    }
                }
                finally
                {
                    CleanupSegment(cleanupSegment);
                }
            }
            else
            {
                return await method();
            }
        }

        /// <summary>
        /// Builds a "root" segment if an entity is not currently constructed by XRay
        /// This can happen is calls are initiated from a HostedService (running on a background thread)
        /// rather than a HTTP Context (a WebApi call).
        /// </summary>
        /// <returns>A value indicating if a segment was added so that the caller
        /// knows whether or not it needs to be cleaned up.</returns>
        internal virtual bool BuildSegment()
        {
            var cleanupSegment = false;

            // Create a segment if the entity is missing or it has already completed (on the main thread).
            // Check the Enitiy's Progress status (false, if it's completed already and it has a non-zero EndTime).
            // Don't call GetEntity before verifying the Entity is present, otherwise
            // it throws 'Amazon.XRay.Recorder.Core.Exceptions.EntityNotAvailableException'
            if (_recorder.TraceContext == null || !_recorder.TraceContext.IsEntityPresent() ||
                (
                    _recorder.TraceContext.GetEntity()?.IsInProgress == false &&
                    _recorder.TraceContext.GetEntity()?.EndTime != default
                ))
            {
                _recorder.BeginSegment(SegmentName);
                cleanupSegment = true;
            }

            return cleanupSegment;
        }

        /// <summary>
        /// Cleansup the segment (if one was created) by calling EndSegment().
        /// </summary>
        /// <param name="cleanupSegment"></param>
        internal virtual void CleanupSegment(bool cleanupSegment)
        {
            if (cleanupSegment)
            {
                _recorder.EndSegment();
            }
        }

        internal virtual Stopwatch BeginTrace(string subsegmentName, string methodName, string telemetryNamespace)
        {
            _recorder.BeginSubsegment(subsegmentName);
            _recorder.WithNamespace(telemetryNamespace);
            _recorder.WithMethodName(methodName);

            return Stopwatch.StartNew();
        }

        internal virtual void EndTrace(Stopwatch stopwatch)
        {
            stopwatch.Stop();

            _recorder.AddMetadata("ElapsedTimeInMilliseconds", stopwatch.ElapsedMilliseconds);
            _recorder.EndSubsegment();
        }

        internal virtual void AddException(Exception exception) => _recorder.AddException(exception);
    }
}
using Amazon.XRay.Recorder.Core;
using AWSXRayRaceCondition;
using Moq;
using System.Diagnostics;

namespace AWSXRayRaceConditionTests
{
    public class TelemetryRecorderTests : IDisposable
    {
        private const string SubsegmentName = "subSegment";
        private const string NamespaceName = "telemetryNamespace";
        private const string MethodName = "SomeMethod";

        private readonly AWSXRayRecorder _recorder;
        private readonly Mock<TelemetryRecorder> _mockSut;

        // Use default ctor for each test's setup
        public TelemetryRecorderTests()
        {
            _recorder = new AWSXRayRecorder();

            // ***********************************************************************************
            //  NOTE: Normally, you do not mock the SUT, you mock its dependencies.
            //  However, because of the way the AWS XRay is implemented in the AWSXRayRecorder nuget,
            //  our ability to properly mock it is extremely limited at best. So unfortunately
            //  we have to  mock the SUT to record the calls to its 'internal' methods to
            //  be able to unit test TelemetryRecorder to any reasonable level of coverage.
            // ***********************************************************************************
            _mockSut = new Mock<TelemetryRecorder>(MockBehavior.Strict, _recorder);
        }

        // Use IDisposable pattern for each test's cleanup
        public void Dispose()
        {
            _recorder.ClearEntity();
            _recorder.Dispose();

            // dispose the XRay singleton
            AWSXRayRecorder.Instance.Dispose();
        }

        #region Trace tests

        [Fact]
        public void Trace_ThrowsExceptions_ForParamValidation()
        {
            //arrange
            static void action() => TestClass.MethodVoid();

            //act/assert
            Assert.Throws<ArgumentNullException>(() => _mockSut.Object.Trace(null, SubsegmentName));
            Assert.Throws<ArgumentNullException>(() => _mockSut.Object.Trace(action, null));
            Assert.Throws<ArgumentException>(() => _mockSut.Object.Trace(action, string.Empty));
        }

        [Fact]
        public void Trace_CallsActionButDoesNotRecord_WhenTracingIsDisabled()
        {
            //arrange
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Action>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() => wasCalled = true);

            //disable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = true;

            //act
            _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Never);
            _mockSut.Verify(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockSut.Verify(sut => sut.EndTrace(It.IsAny<Stopwatch>()), Times.Never);
            _mockSut.Verify(sut => sut.CleanupSegment(It.IsAny<bool>()), Times.Never);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void Trace_CallsActionAndRecords_WhenTracingIsEnabled()
        {
            //arrange
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Action>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() => wasCalled = true);
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void Trace_CallsActionAndRecordsWithException_WhenTracingIsEnabledAndTraceMethodThrowsException()
        {
            //arrange
            var wasCalled = false;
            var expectedException = new Exception("uh oh");
            var mockMethodToTrace = new Mock<Action>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() =>
                {
                    wasCalled = true;
                    throw expectedException;
                });
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.AddException(expectedException)).Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            var actualException = Assert.Throws<Exception>(() => _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName));

            //assert
            Assert.True(wasCalled);
            Assert.Same(expectedException, actualException);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(expectedException), Times.Once);
        }
        #endregion Trace tests

        #region Trace TResult tests

        [Fact]
        public void TraceOfTResult_ThrowsExceptions_ForParamValidation()
        {
            //arrange
            static int func() => TestClass.Method();

            //act/assert
            Assert.Throws<ArgumentNullException>(() => _mockSut.Object.Trace((Func<int>?)null, SubsegmentName));
            Assert.Throws<ArgumentNullException>(() => _mockSut.Object.Trace(func, null));
            Assert.Throws<ArgumentException>(() => _mockSut.Object.Trace(func, string.Empty));
        }

        [Fact]
        public void TraceOfTResult_CallsFuncButDoesNotRecord_WhenTracingIsDisabled()
        {
            //arrange
            const int expectedResult = 42;
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<int>>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() =>
                {
                    wasCalled = true;
                })
                .Returns(expectedResult);

            //disable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = true;

            //act
            var actual = _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.Equal(expectedResult, actual);
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Never);
            _mockSut.Verify(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockSut.Verify(sut => sut.EndTrace(It.IsAny<Stopwatch>()), Times.Never);
            _mockSut.Verify(sut => sut.CleanupSegment(It.IsAny<bool>()), Times.Never);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void TraceOfTResult_CallsFuncAndRecords_WhenTracingIsEnabled()
        {
            //arrange
            const int expectedResult = 42;
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<int>>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() =>
                {
                    wasCalled = true;
                })
                .Returns(expectedResult);
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            var actual = _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.Equal(expectedResult, actual);
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void TraceOfTResult_CallsFuncAndRecordsWithException_WhenTracingIsEnabledAndTraceMethodThrowsException()
        {
            //arrange
            var wasCalled = false;
            var expectedException = new Exception("uh oh");
            var mockMethodToTrace = new Mock<Func<int>>();
            mockMethodToTrace.Setup(_ => _())
                .Callback(() =>
                {
                    wasCalled = true;
                    throw expectedException;
                });
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.AddException(expectedException)).Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            var actualException = Assert.Throws<Exception>(() => _mockSut.Object.Trace(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName));

            //assert
            Assert.True(wasCalled);
            Assert.Same(expectedException, actualException);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(expectedException), Times.Once);
        }

        #endregion Trace TResult tests

        #region TraceAsync tests

        [Fact]
        public async Task TraceAsync_ThrowsExceptions_ForParamValidation()
        {
            //arrange
            static Task action() => TestClass.MethodVoidAsync();

            //act/assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mockSut.Object.TraceAsync(null, SubsegmentName));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mockSut.Object.TraceAsync(action, null));
            await Assert.ThrowsAsync<ArgumentException>(() => _mockSut.Object.TraceAsync(action, string.Empty));
        }

        [Fact]
        public async Task TraceAsync_CallsActionButDoesNotRecord_WhenTracingIsDisabled()
        {
            //arrange
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<Task>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() => wasCalled = true)
                .Returns(Task.CompletedTask);

            //disable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = true;

            //act
            await _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Never);
            _mockSut.Verify(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockSut.Verify(sut => sut.EndTrace(It.IsAny<Stopwatch>()), Times.Never);
            _mockSut.Verify(sut => sut.CleanupSegment(It.IsAny<bool>()), Times.Never);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task TraceAsync_CallsActionAndRecords_WhenTracingIsEnabled()
        {
            //arrange
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<Task>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() => wasCalled = true)
                .Returns(Task.CompletedTask);
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            await _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task TraceAsync_CallsActionAndRecordsWithException_WhenTracingIsEnabledAndTraceMethodThrowsException()
        {
            //arrange
            var wasCalled = false;
            var expectedException = new Exception("uh oh");
            var mockMethodToTrace = new Mock<Func<Task>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() =>
                {
                    wasCalled = true;
                    throw expectedException;
                })
                .Returns(Task.CompletedTask);
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.AddException(expectedException)).Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            var actualException = await Assert.ThrowsAsync<Exception>(() =>
                _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName));

            //assert
            Assert.True(wasCalled);
            Assert.Same(expectedException, actualException);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(expectedException), Times.Once);
        }

        #endregion TraceAsync tests

        #region TraceAsync TResult tests

        [Fact]
        public async Task TraceAsyncOfTResult_ThrowsExceptions_ForParamValidation()
        {
            //arrange
            static Task<int> func() => TestClass.MethodAsync();

            //act/assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mockSut.Object.TraceAsync((Func<Task<int>>?)null, SubsegmentName));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mockSut.Object.TraceAsync(func, null));
            await Assert.ThrowsAsync<ArgumentException>(() => _mockSut.Object.TraceAsync(func, string.Empty));
        }

        [Fact]
        public async Task TraceAsyncOfTResult_CallsFuncButDoesNotRecord_WhenTracingIsDisabled()
        {
            //arrange
            const int expectedResult = 42;
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<Task<int>>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() => wasCalled = true)
                .Returns(Task.FromResult(expectedResult));

            //disable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = true;

            //act
            await _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Never);
            _mockSut.Verify(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockSut.Verify(sut => sut.EndTrace(It.IsAny<Stopwatch>()), Times.Never);
            _mockSut.Verify(sut => sut.CleanupSegment(It.IsAny<bool>()), Times.Never);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task TraceAsyncOfTResult_CallsFuncAndRecords_WhenTracingIsEnabled()
        {
            //arrange
            const int expectedResult = 42;
            var wasCalled = false;
            var mockMethodToTrace = new Mock<Func<Task<int>>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() => wasCalled = true)
                .Returns(Task.FromResult(expectedResult));
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            await _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName);

            //assert
            Assert.True(wasCalled);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public async Task TraceAsyncOfTResult_CallsFuncAndRecordsWithException_WhenTracingIsEnabledAndTraceMethodThrowsException()
        {
            //arrange
            const int expectedResult = 42;
            var wasCalled = false;
            var expectedException = new Exception("uh oh");
            var mockMethodToTrace = new Mock<Func<Task<int>>>();
            mockMethodToTrace.Setup(m => m())
                .Callback(() =>
                {
                    wasCalled = true;
                    throw expectedException;
                })
                .Returns(Task.FromResult(expectedResult));
            var mockStopwatch = new Mock<Stopwatch>();

            //enable tracing
            _recorder.XRayOptions.IsXRayTracingDisabled = false;
            _mockSut.Setup(sut => sut.BuildSegment())
                .Returns(true)
                .Verifiable();
            _mockSut.Setup(sut => sut.BeginTrace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockStopwatch.Object)
                .Verifiable();
            _mockSut.Setup(sut => sut.AddException(expectedException)).Verifiable();
            _mockSut.Setup(sut => sut.EndTrace(It.IsAny<Stopwatch>())).Verifiable();
            _mockSut.Setup(sut => sut.CleanupSegment(true)).Verifiable();

            //act
            var actualException = await Assert.ThrowsAsync<Exception>(() =>
                _mockSut.Object.TraceAsync(mockMethodToTrace.Object, SubsegmentName, MethodName, NamespaceName));

            //assert
            Assert.True(wasCalled);
            Assert.Same(expectedException, actualException);
            _mockSut.Verify(sut => sut.BuildSegment(), Times.Once);
            _mockSut.Verify(sut => sut.BeginTrace(SubsegmentName, MethodName, NamespaceName), Times.Once);
            _mockSut.Verify(sut => sut.EndTrace(mockStopwatch.Object), Times.Once);
            _mockSut.Verify(sut => sut.CleanupSegment(true), Times.Once);
            _mockSut.Verify(sut => sut.AddException(expectedException), Times.Once);
        }

        #endregion TraceAsync TResult tests

        internal static class TestClass
        {
            internal static Task MethodVoidAsync() => Task.CompletedTask;

            internal static Task<int> MethodAsync() => Task.FromResult(42);

            internal static void MethodVoid() { }

            internal static int Method() => 42;
        }
    }
}
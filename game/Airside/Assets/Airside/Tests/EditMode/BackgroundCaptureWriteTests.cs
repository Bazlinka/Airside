using System;
using System.IO;
using System.Threading;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class BackgroundCaptureWriteTests
    {
        [Test]
        public void SlowEncoderDoesNotBlockCallerAndWritesCopiedPayload()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            using var started = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var callerThread = Thread.CurrentThread.ManagedThreadId;
            var encoderThread = callerThread;
            var write = BackgroundCaptureWrite.Start(path, () =>
            {
                encoderThread = Thread.CurrentThread.ManagedThreadId;
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                return new byte[] { 1, 2, 3, 4 };
            });
            try
            {
                Assert.That(started.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(write.IsCompleted, Is.False, "blocked encoder must not hold the frame caller");
                Assert.That(encoderThread, Is.Not.EqualTo(callerThread));
                Assert.That(File.Exists(path), Is.False);
                release.Set();
                Assert.That(write.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            }
            finally
            {
                release.Set();
                write.Wait(TimeSpan.FromSeconds(5));
                File.Delete(path);
            }
        }

        [Test]
        public void FailedWriteIsObservableInsteadOfReportingCaptureSuccess()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing", "capture.png");
            var write = BackgroundCaptureWrite.Start(path, () => new byte[] { 1 });
            Assert.Throws<AggregateException>(() => write.Wait(TimeSpan.FromSeconds(3)));
            Assert.That(write.IsFaulted, Is.True);
            Assert.That(write.Exception.GetBaseException(), Is.TypeOf<DirectoryNotFoundException>());
        }
    }
}

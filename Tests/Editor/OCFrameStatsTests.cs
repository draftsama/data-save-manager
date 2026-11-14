#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCFrameStatsTests
    {
        [Test]
        public void AverageFps_UsesTotalTimeOfKeptFrames()
        {
            var stats = new OCFrameStats();
            stats.Push(10f, float.NaN);
            stats.Push(30f, float.NaN);
            Assert.AreEqual(50f, stats.AverageFps(), 0.001f);
        }

        [Test]
        public void OnePercentLow_IsSlowestFrameOutsideTopPercent()
        {
            var stats = new OCFrameStats();
            for (var i = 0; i < 297; i++) stats.Push(10f, float.NaN);
            stats.Push(100f, float.NaN);
            stats.Push(50f, float.NaN);
            stats.Push(40f, float.NaN);

            Assert.AreEqual(1000f / 40f, stats.OnePercentLowFps(), 0.001f);
        }

        [Test]
        public void Worst_SurvivesWrapUntilReset()
        {
            var stats = new OCFrameStats();
            stats.Push(80f, float.NaN);
            for (var i = 0; i < OCFrameStats.Capacity; i++) stats.Push(10f, float.NaN);
            Assert.AreEqual(80f, stats.WorstMs);

            stats.ResetWorst();
            stats.Push(12f, float.NaN);
            Assert.AreEqual(12f, stats.WorstMs);
        }

        [Test]
        public void FrameAt_IsOldestFirstAfterWrap()
        {
            var stats = new OCFrameStats();
            for (var i = 0; i < OCFrameStats.Capacity + 5; i++) stats.Push(i, i * 0.5f);

            Assert.AreEqual(OCFrameStats.Capacity, stats.Count);
            Assert.AreEqual(5f, stats.FrameAt(0));
            Assert.AreEqual(OCFrameStats.Capacity + 4f, stats.FrameAt(stats.Count - 1));
            Assert.AreEqual((OCFrameStats.Capacity + 4f) * 0.5f, stats.LatestCpuMs);
        }
    }
}

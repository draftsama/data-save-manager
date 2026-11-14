#nullable enable

using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace OperatorConsole
{
    /// <summary>
    /// Live measurements of the running app for the Runtime Panel's Performance tab. Times are in milliseconds,
    /// sizes in bytes. A Metric the running build can't measure reads <see cref="float.NaN"/> or -1.
    /// Main thread only.
    /// </summary>
    public sealed class OCPerformance
    {
        private const float NanosecondsPerMillisecond = 1_000_000f;

        private readonly OCFrameStats _frames = new();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _gcAllocated;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private float _gpuMs = float.NaN;

        /// <summary>True while frames are being measured.</summary>
        public bool IsMeasuring { get; private set; }

        internal OCFrameStats Frames => _frames;

        /// <summary>True when the build reports GPU and main-thread times through <see cref="FrameTimingManager"/>; needs Frame Timing Stats in Player Settings outside the Editor.</summary>
        public bool FrameTimingAvailable => FrameTimingManager.IsFeatureEnabled();

        public float LatestFrameMs => _frames.LatestMs;

        /// <summary>Frame rate over the last few frames.</summary>
        public float CurrentFps => _frames.RecentFps(10);

        /// <summary>Frame rate over the kept history (<see cref="OCFrameStats.Capacity"/> frames).</summary>
        public float AverageFps => _frames.AverageFps();

        public float OnePercentLowFps => _frames.OnePercentLowFps();

        /// <summary>Slowest frame since measuring started or the last <see cref="ResetWorstFrame"/>.</summary>
        public float WorstFrameMs => _frames.WorstMs;

        public float CpuMainThreadMs => _frames.LatestCpuMs;

        public float GpuMs => _gpuMs;

        public long GcAllocatedInFrame => Read(_gcAllocated);

        public long Batches => Read(_batches);

        public long SetPassCalls => Read(_setPassCalls);

        public long Triangles => Read(_triangles);

        public long TotalAllocatedMemory => Profiler.GetTotalAllocatedMemoryLong();

        public long TotalReservedMemory => Profiler.GetTotalReservedMemoryLong();

        public long MonoUsedMemory => Profiler.GetMonoUsedSizeLong();

        public long MonoHeapMemory => Profiler.GetMonoHeapSizeLong();

        public void ResetWorstFrame() => _frames.ResetWorst();

        internal void StartMeasuring()
        {
            if (IsMeasuring) return;
            IsMeasuring = true;
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        internal void StopMeasuring()
        {
            if (!IsMeasuring) return;
            IsMeasuring = false;
            _mainThread.Dispose();
            _gcAllocated.Dispose();
            _batches.Dispose();
            _setPassCalls.Dispose();
            _triangles.Dispose();
        }

        /// <summary>Records the frame that just ended. Called once per frame, late in the player loop.</summary>
        internal void Sample()
        {
            if (!IsMeasuring) return;

            var cpuMs = float.NaN;
            _gpuMs = float.NaN;
            if (FrameTimingManager.IsFeatureEnabled())
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                {
                    cpuMs = (float)_timings[0].cpuMainThreadFrameTime;
                    var gpu = (float)_timings[0].gpuFrameTime;
                    if (gpu > 0f) _gpuMs = gpu;
                }
            }
            if (float.IsNaN(cpuMs) && _mainThread.Valid && _mainThread.LastValue > 0)
                cpuMs = _mainThread.LastValue / NanosecondsPerMillisecond;

            _frames.Push(Time.unscaledDeltaTime * 1000f, cpuMs);
        }

        private static long Read(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;
    }
}

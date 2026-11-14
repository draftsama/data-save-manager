#nullable enable

using System;

namespace OperatorConsole
{
    /// <summary>The last <see cref="Capacity"/> frame times, with the averages Performance shows. Times are in milliseconds.</summary>
    internal sealed class OCFrameStats
    {
        public const int Capacity = 300;

        private readonly float[] _frameMs = new float[Capacity];
        private readonly float[] _cpuMs = new float[Capacity];
        private readonly float[] _sorted = new float[Capacity];
        private int _head;
        private int _count;

        public int Count => _count;

        /// <summary>The slowest frame since the start or the last <see cref="ResetWorst"/>.</summary>
        public float WorstMs { get; private set; }

        public float LatestMs => _count == 0 ? 0f : _frameMs[(_head + Capacity - 1) % Capacity];

        public float LatestCpuMs => _count == 0 ? float.NaN : _cpuMs[(_head + Capacity - 1) % Capacity];

        /// <param name="cpuMs">Main-thread time of the frame, or NaN when unknown.</param>
        public void Push(float frameMs, float cpuMs)
        {
            _frameMs[_head] = frameMs;
            _cpuMs[_head] = cpuMs;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
            if (frameMs > WorstMs) WorstMs = frameMs;
        }

        public void ResetWorst() => WorstMs = 0f;

        /// <summary>Frame time <paramref name="index"/> frames after the oldest kept frame.</summary>
        public float FrameAt(int index) => _frameMs[Slot(index)];

        public float CpuAt(int index) => _cpuMs[Slot(index)];

        public float AverageFps()
        {
            if (_count == 0) return 0f;
            var total = 0f;
            for (var i = 0; i < _count; i++) total += _frameMs[i];
            return total > 0f ? _count * 1000f / total : 0f;
        }

        /// <summary>Frame rate of the frame slower than 99% of the kept frames.</summary>
        public float OnePercentLowFps()
        {
            if (_count == 0) return 0f;
            Array.Copy(_frameMs, _sorted, Capacity);
            Array.Sort(_sorted, 0, _count);
            var slow = _sorted[_count - 1 - (_count - 1) / 100];
            return slow > 0f ? 1000f / slow : 0f;
        }

        /// <summary>Frame rate over the last <paramref name="frames"/> frames, for a reading that doesn't flicker.</summary>
        public float RecentFps(int frames)
        {
            frames = Math.Min(frames, _count);
            if (frames == 0) return 0f;
            var total = 0f;
            for (var i = 0; i < frames; i++) total += _frameMs[(_head + Capacity - 1 - i) % Capacity];
            return total > 0f ? frames * 1000f / total : 0f;
        }

        private int Slot(int index) => (_head - _count + index + Capacity) % Capacity;
    }
}

#nullable enable

using System;
using UnityEngine;

namespace DataSaveManager
{
    public enum DSMScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>The Corner Tap Open Trigger: a set number of presses on one screen corner, each soon after the last.</summary>
    [Serializable]
    public sealed class DSMCornerTap
    {
        private const double DuplicateWindow = 0.05;

        [SerializeField] private bool _enabled;

        [SerializeField] private DSMScreenCorner _corner = DSMScreenCorner.TopRight;

        [SerializeField, Range(1f, 50f), Tooltip("Side of the square tap zone, as a percent of the screen's shorter side.")]
        private float _sizePercent = 10f;

        [SerializeField, Range(2, 10)] private int _tapCount = 3;

        [SerializeField, Range(0.1f, 3f), Tooltip("Longest gap between two taps, in seconds; a longer gap starts the count again.")]
        private float _maxInterval = 0.5f;

        [NonSerialized] private int _taps;
        [NonSerialized] private double _lastTapTime;

        public DSMCornerTap() { }

        public DSMCornerTap(DSMScreenCorner corner, float sizePercent = 10f, int tapCount = 3, float maxInterval = 0.5f)
        {
            _enabled = true;
            _corner = corner;
            _sizePercent = sizePercent;
            _tapCount = tapCount;
            _maxInterval = maxInterval;
        }

        public bool Enabled => _enabled;
        public DSMScreenCorner Corner => _corner;
        public int TapCount => Mathf.Max(1, _tapCount);
        public float MaxInterval => _maxInterval;

        /// <summary>Screen-space rectangle of the tap zone, in pixels with the origin at the bottom left.</summary>
        public Rect Zone(Vector2 screenSize)
        {
            var side = Mathf.Min(screenSize.x, screenSize.y) * Mathf.Clamp(_sizePercent, 0f, 100f) / 100f;
            var x = _corner is DSMScreenCorner.TopLeft or DSMScreenCorner.BottomLeft ? 0f : screenSize.x - side;
            var y = _corner is DSMScreenCorner.BottomLeft or DSMScreenCorner.BottomRight ? 0f : screenSize.y - side;
            return new Rect(x, y, side, side);
        }

        /// <summary>Counts one press. Returns true on the press that completes the Corner Tap, which also starts the count again.</summary>
        /// <param name="time">Seconds on a clock that keeps running while the game is paused.</param>
        public bool RegisterPress(Vector2 position, Vector2 screenSize, double time)
        {
            if (!Zone(screenSize).Contains(position))
            {
                _taps = 0;
                return false;
            }

            // Windows can report one touch twice, as a touch and as a promoted mouse click a frame apart.
            if (_taps > 0 && time - _lastTapTime < DuplicateWindow) return false;
            if (_taps > 0 && time - _lastTapTime > _maxInterval) _taps = 0;
            _taps++;
            _lastTapTime = time;

            if (_taps < TapCount) return false;
            _taps = 0;
            return true;
        }

        public void ResetCount() => _taps = 0;
    }
}

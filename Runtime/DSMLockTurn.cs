#nullable enable

namespace DataSaveManager
{
    internal enum DSMLockTurnResult
    {
        Show,
        Yield,
        TakeOver
    }

    /// <summary>Decides which Runtime Panel shows its Lock Screen when one Open Trigger opens several panels with a Panel Lock: at most one at a time, the first in the Config's order.</summary>
    internal sealed class DSMLockTurn
    {
        private object? _holder;
        private int _holderOrder;
        private int _holderFrame;
        private int _endedFrame = int.MinValue;

        /// <param name="order">The panel's position in the Config's Panel Definitions.</param>
        /// <param name="displaced">On <see cref="DSMLockTurnResult.TakeOver"/>, the panel whose Lock Screen must close.</param>
        public DSMLockTurnResult Claim(object panel, int order, int frame, out object? displaced)
        {
            displaced = null;
            if (_holder == null)
            {
                // A Lock Screen that closed this frame was closed by this same trigger use; opening another would chain prompts.
                if (_endedFrame == frame) return DSMLockTurnResult.Yield;
                Take(panel, order, frame);
                return DSMLockTurnResult.Show;
            }

            if (ReferenceEquals(_holder, panel)) return DSMLockTurnResult.Show;
            if (_holderFrame != frame || _holderOrder <= order) return DSMLockTurnResult.Yield;

            displaced = _holder;
            Take(panel, order, frame);
            return DSMLockTurnResult.TakeOver;
        }

        public void Release(object panel, int frame)
        {
            if (!ReferenceEquals(_holder, panel)) return;
            _holder = null;
            _endedFrame = frame;
        }

        public void Clear()
        {
            _holder = null;
            _endedFrame = int.MinValue;
        }

        private void Take(object panel, int order, int frame)
        {
            _holder = panel;
            _holderOrder = order;
            _holderFrame = frame;
        }
    }
}

#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCLockTurnTests
    {
        private readonly object _first = new();
        private readonly object _second = new();

        [Test]
        public void SameTriggerUse_LaterPanelFirst_FirstPanelTakesOver()
        {
            var turn = new OCLockTurn();
            Assert.AreEqual(OCLockTurnResult.Show, turn.Claim(_second, 1, 10, out _));
            Assert.AreEqual(OCLockTurnResult.TakeOver, turn.Claim(_first, 0, 10, out var displaced));
            Assert.AreSame(_second, displaced);
        }

        [Test]
        public void SameTriggerUse_FirstPanelFirst_LaterPanelYields()
        {
            var turn = new OCLockTurn();
            Assert.AreEqual(OCLockTurnResult.Show, turn.Claim(_first, 0, 10, out _));
            Assert.AreEqual(OCLockTurnResult.Yield, turn.Claim(_second, 1, 10, out _));
        }

        [Test]
        public void LockScreenAlreadyUp_EarlierPanelYields()
        {
            var turn = new OCLockTurn();
            turn.Claim(_second, 1, 10, out _);
            Assert.AreEqual(OCLockTurnResult.Yield, turn.Claim(_first, 0, 11, out _));
        }

        [Test]
        public void CancelledThisFrame_OtherPanelYields()
        {
            var turn = new OCLockTurn();
            turn.Claim(_first, 0, 10, out _);
            turn.Release(_first, 20);
            Assert.AreEqual(OCLockTurnResult.Yield, turn.Claim(_second, 1, 20, out _));
            Assert.AreEqual(OCLockTurnResult.Show, turn.Claim(_second, 1, 21, out _));
        }
    }
}

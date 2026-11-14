#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMCornerTapTests
    {
        private static readonly Vector2 Screen = new(1920f, 1080f);
        private static readonly Vector2 TopRight = new(1900f, 1060f);

        [Test]
        public void ThirdQuickTapInZone_Completes()
        {
            var tap = new DSMCornerTap(DSMScreenCorner.TopRight);
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 0.0));
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 0.3));
            Assert.IsTrue(tap.RegisterPress(TopRight, Screen, 0.6));
        }

        [Test]
        public void GapLongerThanMaxInterval_StartsCountAgain()
        {
            var tap = new DSMCornerTap(DSMScreenCorner.TopRight);
            tap.RegisterPress(TopRight, Screen, 0.0);
            tap.RegisterPress(TopRight, Screen, 0.3);
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 1.0));
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 1.3));
            Assert.IsTrue(tap.RegisterPress(TopRight, Screen, 1.6));
        }

        [Test]
        public void PressOutsideZone_StartsCountAgain()
        {
            var tap = new DSMCornerTap(DSMScreenCorner.TopRight);
            tap.RegisterPress(TopRight, Screen, 0.0);
            tap.RegisterPress(TopRight, Screen, 0.2);
            Assert.IsFalse(tap.RegisterPress(new Vector2(960f, 540f), Screen, 0.4));
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 0.6));
        }

        [Test]
        public void DuplicatePressWithinOneFrame_CountsOnce()
        {
            var tap = new DSMCornerTap(DSMScreenCorner.TopRight);
            tap.RegisterPress(TopRight, Screen, 0.0);
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 0.016));
            Assert.IsFalse(tap.RegisterPress(TopRight, Screen, 0.3));
            Assert.IsTrue(tap.RegisterPress(TopRight, Screen, 0.6));
        }

        [TestCase(DSMScreenCorner.TopLeft, 10f, 1070f)]
        [TestCase(DSMScreenCorner.TopRight, 1910f, 1070f)]
        [TestCase(DSMScreenCorner.BottomLeft, 10f, 10f)]
        [TestCase(DSMScreenCorner.BottomRight, 1910f, 10f)]
        public void Zone_IsTenPercentOfShortSideAtChosenCorner(DSMScreenCorner corner, float x, float y)
        {
            var zone = new DSMCornerTap(corner).Zone(Screen);
            Assert.AreEqual(108f, zone.width, 0.001f);
            Assert.AreEqual(108f, zone.height, 0.001f);
            Assert.IsTrue(zone.Contains(new Vector2(x, y)));
        }
    }
}

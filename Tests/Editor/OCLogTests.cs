#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCLogTests
    {
        private static readonly DateTime T0 = new(2026, 11, 14, 9, 41, 2);

        [Test]
        public void SameMessageBackToBack_CountsOnOneEntry()
        {
            var log = new OCLog();
            log.Add(OCLogType.Warning, "retrying", "at A", T0);
            log.Add(OCLogType.Warning, "retrying", "at A", T0);

            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(2, log.WarningCount);
            Assert.AreEqual(2, Entries(log)[0].Count);
        }

        [Test]
        public void SameMessageWithAnotherBetween_StaysSeparate()
        {
            var log = new OCLog();
            log.Add(OCLogType.Info, "a", null, T0);
            log.Add(OCLogType.Info, "b", null, T0);
            log.Add(OCLogType.Info, "a", null, T0);

            Assert.AreEqual(3, log.Count);
        }

        [Test]
        public void Full_DropsOldestAndItsCounts()
        {
            var log = new OCLog(2);
            log.Add(OCLogType.Error, "first", null, T0);
            log.Add(OCLogType.Error, "first", null, T0);
            log.Add(OCLogType.Info, "second", null, T0);
            log.Add(OCLogType.Info, "third", null, T0);

            var entries = Entries(log);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("second", entries[0].Message);
            Assert.AreEqual(0, log.ErrorCount);
            Assert.AreEqual(2, log.InfoCount);
        }

        [Test]
        public void Clear_EmptiesLogButKeepsErrorsLogged()
        {
            var log = new OCLog();
            log.Add(OCLogType.Error, "boom", null, T0);
            log.Clear();

            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(0, log.ErrorCount);
            Assert.AreEqual(1, log.ErrorsLogged);
        }

        [Test]
        public void UnseenErrors_CountsErrorsAfterMarkUpToWhatLogHolds()
        {
            var log = new OCLog();
            log.Add(OCLogType.Error, "a", null, T0);
            var seen = log.ErrorsLogged;
            log.Add(OCLogType.Error, "b", null, T0);
            log.Add(OCLogType.Error, "c", null, T0);
            Assert.AreEqual(2, log.UnseenErrors(seen));

            log.Clear();
            Assert.AreEqual(0, log.UnseenErrors(seen));
        }

        [Test]
        public void SetCapacity_KeepsNewest()
        {
            var log = new OCLog();
            log.Add(OCLogType.Info, "a", null, T0);
            log.Add(OCLogType.Info, "b", null, T0);
            log.Add(OCLogType.Info, "c", null, T0);
            log.SetCapacity(2);

            var entries = Entries(log);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("b", entries[0].Message);
            Assert.AreEqual(2, log.InfoCount);
        }

        [Test]
        public void Preview_IsFirstLine()
        {
            var log = new OCLog();
            log.Add(OCLogType.Error, "NullReferenceException\r\nmore detail", null, T0);
            Assert.AreEqual("NullReferenceException", Entries(log)[0].Preview);
        }

        private static List<OCLogEntry> Entries(OCLog log)
        {
            var list = new List<OCLogEntry>();
            log.CopyTo(list);
            return list;
        }
    }
}

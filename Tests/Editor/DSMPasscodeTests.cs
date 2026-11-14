#nullable enable

using System;
using NUnit.Framework;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMPasscodeTests
    {
        [Test]
        public void SameCode_Matches()
        {
            var passcode = DSMPasscode.Create("1234");
            Assert.IsTrue(passcode.Matches("1234"));
            Assert.AreEqual(4, passcode.Length);
        }

        [TestCase("1235")]
        [TestCase("123")]
        [TestCase("12345")]
        [TestCase("")]
        [TestCase(null)]
        public void OtherCode_DoesNotMatch(string? code)
        {
            Assert.IsFalse(DSMPasscode.Create("1234").Matches(code));
        }

        [TestCase("123")]
        [TestCase("123456789")]
        [TestCase("12a4")]
        [TestCase(" 1234")]
        public void InvalidCode_IsRejected(string code)
        {
            Assert.IsFalse(DSMPasscode.IsValid(code));
            Assert.Throws<ArgumentException>(() => DSMPasscode.Create(code));
        }

        [Test]
        public void NewPasscode_IsNotSetAndMatchesNothing()
        {
            var passcode = new DSMPasscode();
            Assert.IsFalse(passcode.IsSet);
            Assert.IsFalse(passcode.Matches("0000"));
        }

        [Test]
        public void Cleared_IsNotSet()
        {
            var passcode = DSMPasscode.Create("123456");
            passcode.Clear();
            Assert.IsFalse(passcode.IsSet);
            Assert.IsFalse(passcode.Matches("123456"));
        }

        [Test]
        public void SameCode_HashesDifferentlyEachTime()
        {
            Assert.AreNotEqual(DSMPasscode.Hash("1234", DSMPasscode.NewSalt()), DSMPasscode.Hash("1234", DSMPasscode.NewSalt()));
        }
    }
}

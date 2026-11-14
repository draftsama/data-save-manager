#nullable enable

using System;
using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCPasscodeTests
    {
        [Test]
        public void SameCode_Matches()
        {
            var passcode = OCPasscode.Create("1234");
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
            Assert.IsFalse(OCPasscode.Create("1234").Matches(code));
        }

        [TestCase("123")]
        [TestCase("123456789")]
        [TestCase("12a4")]
        [TestCase(" 1234")]
        public void InvalidCode_IsRejected(string code)
        {
            Assert.IsFalse(OCPasscode.IsValid(code));
            Assert.Throws<ArgumentException>(() => OCPasscode.Create(code));
        }

        [Test]
        public void NewPasscode_IsNotSetAndMatchesNothing()
        {
            var passcode = new OCPasscode();
            Assert.IsFalse(passcode.IsSet);
            Assert.IsFalse(passcode.Matches("0000"));
        }

        [Test]
        public void Cleared_IsNotSet()
        {
            var passcode = OCPasscode.Create("123456");
            passcode.Clear();
            Assert.IsFalse(passcode.IsSet);
            Assert.IsFalse(passcode.Matches("123456"));
        }

        [Test]
        public void SameCode_HashesDifferentlyEachTime()
        {
            Assert.AreNotEqual(OCPasscode.Hash("1234", OCPasscode.NewSalt()), OCPasscode.Hash("1234", OCPasscode.NewSalt()));
        }
    }
}

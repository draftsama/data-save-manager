#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DataSaveManager.Tests
{
    internal sealed class BindingTestComponent : MonoBehaviour
    {
        public float FloatProp { get; set; }
        public float FloatField = 0f;
        public readonly float ReadonlyField = 0f;
        public int IntProp { get; set; }

        public float LastSetValue;
        public void SetX(float value) => LastSetValue = value;

        public string Text { get; set; } = string.Empty;
        public string LastLabel = string.Empty;
        public void SetLabel(string value) => LastLabel = value;
    }

    [TestFixture]
    internal sealed class DSMBindingMembersTests
    {
        private GameObject? _go;
        private BindingTestComponent? _component;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(DSMBindingMembersTests));
            _component = _go.AddComponent<BindingTestComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Find_ReturnsOnlyValidFloatMembers()
        {
            var members = DSMBindingMembers.Find(typeof(BindingTestComponent), typeof(float));
            var ids = members.Select(m => m.id).ToList();

            Assert.That(ids, Has.Some.EqualTo("P:FloatProp"));
            Assert.That(ids, Has.Some.EqualTo("F:FloatField"));
            Assert.That(ids, Has.Some.EqualTo("M:SetX"));
            Assert.That(ids, Has.None.Contains("ReadonlyField"));
            Assert.That(ids, Has.None.Contains("IntProp"));
        }

        [Test]
        public void TryCreateSetter_PropertySetsValue()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "P:FloatProp", out var setter), Is.True);
            setter(3f);
            Assert.That(_component!.FloatProp, Is.EqualTo(3f));
        }

        [Test]
        public void TryCreateSetter_FieldSetsValue()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "F:FloatField", out var setter), Is.True);
            setter(4f);
            Assert.That(_component!.FloatField, Is.EqualTo(4f));
        }

        [Test]
        public void TryCreateSetter_MethodSetsValue()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "M:SetX", out var setter), Is.True);
            setter(5f);
            Assert.That(_component!.LastSetValue, Is.EqualTo(5f));
        }

        [Test]
        public void TryCreateSetter_UnknownId_ReturnsFalse()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "P:DoesNotExist", out _), Is.False);
        }

        [Test]
        public void Find_NonStringValue_AlsoOffersStringMembersAsFormatted()
        {
            var members = DSMBindingMembers.Find(typeof(BindingTestComponent), typeof(float));

            Assert.That(members.Where(m => !m.formatted).Select(m => m.id), Has.None.EqualTo("P:Text"));
            Assert.That(members.Where(m => m.formatted).Select(m => m.id),
                Is.SupersetOf(new[] { "P:Text", "M:SetLabel" }));
        }

        [Test]
        public void Find_StringValue_HasNoFormattedMembers()
        {
            var members = DSMBindingMembers.Find(typeof(BindingTestComponent), typeof(string));

            Assert.That(members.Any(m => m.formatted), Is.False);
            Assert.That(members.Select(m => m.id), Is.SupersetOf(new[] { "P:Text", "M:SetLabel" }));
            Assert.That(members.Select(m => m.id), Is.Unique);
        }

        [Test]
        public void FormatValue_UsesInvariantCulture()
        {
            Assert.That(DSMBindingMembers.FormatValue("Speed: {0:0.0}", 6f), Is.EqualTo("Speed: 6.0"));
        }

        [Test]
        public void FormatValue_EmptyFormat_FallsBackToPlainValue()
        {
            Assert.That(DSMBindingMembers.FormatValue(string.Empty, 7), Is.EqualTo("7"));
        }

        [Test]
        public void TryFormat_BadFormat_ReturnsFalseWithError()
        {
            Assert.That(DSMBindingMembers.TryFormat("{0", 1, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void Find_FormattedMembers_DisplayAsDsmSetters_AndSkipUnityBaseMembers()
        {
            var formatted = DSMBindingMembers.Find(typeof(BindingTestComponent), typeof(float)).Where(m => m.formatted).ToList();

            Assert.That(formatted.Single(m => m.id == "P:Text").display, Does.StartWith("DSM_SetText "));
            Assert.That(formatted.Single(m => m.id == "M:SetLabel").display, Does.StartWith("DSM_SetLabel "));
            Assert.That(formatted.Select(m => m.id), Has.None.EqualTo("P:name").And.None.EqualTo("P:tag"));
        }
    }
}

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

        public float LastBothFloat;
        public string LastBothText = string.Empty;
        public void SetBoth(float value) => LastBothFloat = value;
        public void SetBoth(string value) => LastBothText = value;
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
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "P:FloatProp", "{0}", null, out var setter), Is.True);
            setter(3f);
            Assert.That(_component!.FloatProp, Is.EqualTo(3f));
        }

        [Test]
        public void TryCreateSetter_FieldSetsValue()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "F:FloatField", "{0}", null, out var setter), Is.True);
            setter(4f);
            Assert.That(_component!.FloatField, Is.EqualTo(4f));
        }

        [Test]
        public void TryCreateSetter_MethodSetsValue()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "M:SetX", "{0}", null, out var setter), Is.True);
            setter(5f);
            Assert.That(_component!.LastSetValue, Is.EqualTo(5f));
        }

        [Test]
        public void TryCreateSetter_UnknownId_ReturnsFalse()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "P:DoesNotExist", "{0}", null, out _), Is.False);
        }

        [Test]
        public void Match_ExactOverloadWinsOverTextOverload()
        {
            Assert.That(DSMBindingMembers.Match(typeof(BindingTestComponent), "M:SetBoth", typeof(float)), Is.EqualTo(DSMMemberMatch.Exact));

            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "M:SetBoth", "{0}", null, out var setter), Is.True);
            setter(2f);
            Assert.That(_component!.LastBothFloat, Is.EqualTo(2f));
            Assert.That(_component.LastBothText, Is.Empty);
        }

        [Test]
        public void Match_TextOnlyMember_IsFormattedForNonTextValue()
        {
            Assert.That(DSMBindingMembers.Match(typeof(BindingTestComponent), "M:SetLabel", typeof(float)), Is.EqualTo(DSMMemberMatch.Formatted));
        }

        [Test]
        public void Match_MissingMemberOrUnknownPrefix_IsNone()
        {
            Assert.That(DSMBindingMembers.Match(typeof(BindingTestComponent), "P:DoesNotExist", typeof(float)), Is.EqualTo(DSMMemberMatch.None));
            Assert.That(DSMBindingMembers.Match(typeof(BindingTestComponent), "X:FloatProp", typeof(float)), Is.EqualTo(DSMMemberMatch.None));
        }

        [Test]
        public void TryCreateSetter_FormattedMember_AppliesFormat()
        {
            Assert.That(DSMBindingMembers.TryCreateSetter<float>(_component!, "P:Text", "Speed: {0:0.0}", null, out var setter), Is.True);
            setter(6f);
            Assert.That(_component!.Text, Is.EqualTo("Speed: 6.0"));
        }

        [Test]
        public void TryCreateSetter_BadFormat_WritesPlainValueAndReportsOnce()
        {
            var errors = 0;
            Assert.That(DSMBindingMembers.TryCreateSetter<int>(_component!, "M:SetLabel", "{0", _ => errors++, out var setter), Is.True);

            setter(1);
            Assert.That(_component!.LastLabel, Is.EqualTo("1"));
            setter(2);
            Assert.That(_component.LastLabel, Is.EqualTo("2"));
            Assert.That(errors, Is.EqualTo(1));
        }

        [Test]
        public void TryGetPreservedMember_MapsEachPrefix()
        {
            Assert.That(DSMBindingMembers.TryGetPreservedMember("P:Text", out var property, out var propertyIsField), Is.True);
            Assert.That((property, propertyIsField), Is.EqualTo(("set_Text", false)));
            Assert.That(DSMBindingMembers.TryGetPreservedMember("F:FloatField", out var field, out var fieldIsField), Is.True);
            Assert.That((field, fieldIsField), Is.EqualTo(("FloatField", true)));
            Assert.That(DSMBindingMembers.TryGetPreservedMember("M:SetX", out var method, out var methodIsField), Is.True);
            Assert.That((method, methodIsField), Is.EqualTo(("SetX", false)));
            Assert.That(DSMBindingMembers.TryGetPreservedMember("X:SetX", out _, out _), Is.False);
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

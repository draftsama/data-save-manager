#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class DSMSchemaValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_SchemaTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Strict_SetTypeMismatch_ThrowsSchemaViolation()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-set");

        Assert.Throws<DSMSchemaViolationException>(() => slot.Set("testKey", "not-an-int"));
    }

    [Test]
    public void Strict_GetTypeMismatch_ThrowsSchemaViolation()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-get");

        Assert.Throws<DSMSchemaViolationException>(() => slot.Get("testKey", "wrong-default-type"));
    }

    [Test]
    public void Strict_MatchingType_DoesNotThrow()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-match");

        Assert.DoesNotThrow(() => slot.Set("testKey", 5));
        Assert.DoesNotThrow(() => slot.Get("testKey", 0));
    }

    [Test]
    public void Strict_UnconstrainedKey_PassesThrough()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-unconstrained");

        Assert.DoesNotThrow(() => slot.Set("freeform", "anything"));
    }

    [Test]
    public void Lenient_Mismatch_CoercesToSchemaType()
    {
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-coerce");

        Assert.DoesNotThrow(() => slot.Set("testKey", "42"));
        Assert.That(slot.Get("testKey", 0), Is.EqualTo(42));
    }

    [Test]
    public void Lenient_UncoercibleMismatch_DoesNotThrow()
    {
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-uncoercible");

        Assert.DoesNotThrow(() => slot.Set("testKey", "not-a-number"));
    }

    [Test]
    public void Strict_ExceptionMessage_DoesNotLeakOffendingValue()
    {
        const string secretValue = "s3cr3t-value";
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("leak-check");

        var ex = Assert.Throws<DSMSchemaViolationException>(() => slot.Set("testKey", secretValue));
        Assert.That(ex!.Message, Does.Not.Contain(secretValue));
    }

    [Test]
    public void Lenient_CoercionFailure_Warning_DoesNotLeakOffendingValue()
    {
        const string offendingValue = "s3cr3t-not-a-number";
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-leak-check");

        var warnings = new List<string>();
        void Capture(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) warnings.Add(condition);
        }

        LogAssert.Expect(LogType.Warning, new Regex("could not coerce key 'testKey'"));
        Application.logMessageReceived += Capture;
        try
        {
            slot.Set("testKey", offendingValue);
        }
        finally
        {
            Application.logMessageReceived -= Capture;
        }

        var failureWarning = warnings.Find(w => w.Contains("could not coerce"));
        Assert.That(failureWarning, Is.Not.Null);
        Assert.That(failureWarning, Does.Contain("testKey"));
        Assert.That(failureWarning, Does.Contain("String"));
        Assert.That(failureWarning, Does.Contain("Int32"));
        Assert.That(failureWarning, Does.Not.Contain(offendingValue));
    }

    [Test]
    public async Task Coerced_Set_Reaches_SchemaTyped_WatchAsync_Subscriber()
    {
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-coerce-watch");

        var received = new List<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = cts.Token;
        UniTask.Void(async () =>
        {
            try
            {
                await foreach (var value in slot.WatchAsync<int>("testKey").WithCancellation(token))
                    received.Add(value);
            }
            catch (OperationCanceledException)
            {
            }
        });
        await UniTask.Yield();
        await UniTask.Yield();

        // The schema types testKey as int, so the string is coerced on the way in. The
        // subscriber is typed on the SCHEMA type — Notify must carry the coerced value or
        // the is-T filter drops it silently (WR-05).
        slot.Set("testKey", "42");
        slot.FlushWatchers();
        await UniTask.Yield();
        await UniTask.Yield();
        cts.Cancel();

        Assert.That(received, Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public void EmptySchema_NullConstantType_IsPassThroughNoOp()
    {
        var schema = DSMSchema.For(null);

        Assert.That(schema.Count, Is.EqualTo(0));
        Assert.That(schema.TryGetExpectedType("testKey", out _), Is.False);
    }
}

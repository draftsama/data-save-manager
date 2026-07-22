#nullable enable
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DSMConstantReflectionCache
{
    private static List<DSMDataEntry>? s_cache;

    static DSMConstantReflectionCache()
    {
        // An assembly reload is the only thing that invalidates the scan — reopening the
        // window must not re-enumerate every AppDomain assembly again (PERF-02).
        AssemblyReloadEvents.afterAssemblyReload += () => s_cache = null;
    }

    public static List<DSMDataEntry> GetDefaults()
    {
        s_cache ??= BuildFromReflection();
        // Callers edit their defaults list (type changes, runtime-key sync, add/remove),
        // so hand out a copy and keep the scanned result pristine for the next open.
        var copy = new List<DSMDataEntry>(s_cache.Count);
        foreach (var entry in s_cache)
            copy.Add(new DSMDataEntry
            {
                Key = entry.Key,
                Type = entry.Type,
                SerializedDefault = entry.SerializedDefault
            });
        return copy;
    }

    private static List<DSMDataEntry> BuildFromReflection()
    {
        var defaults = new List<DSMDataEntry>();

        Type? constantType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
                    { constantType = t; break; }
                }
            }
            catch (ReflectionTypeLoadException) { /* Expected — some assemblies cannot be fully reflected */ }
            catch (Exception ex) { Debug.LogWarning($"DSM: Unexpected exception scanning assembly for DSMConstant: {ex.Message}"); }
            if (constantType != null) break;
        }

        if (constantType == null) return defaults;

        foreach (var field in constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null);
            if (value == null) continue;
            defaults.Add(new DSMDataEntry
            {
                Key = field.Name,
                Type = FieldToType(field.FieldType),
                SerializedDefault = ValueToSerialized(value)
            });
        }

        return defaults;
    }

    public static DSMDataType FieldToType(Type t)
    {
        if (t == typeof(int))     return DSMDataType.Int;
        if (t == typeof(float))   return DSMDataType.Float;
        if (t == typeof(double))  return DSMDataType.Double;
        if (t == typeof(long))    return DSMDataType.Long;
        if (t == typeof(bool))    return DSMDataType.Bool;
        if (t == typeof(string))  return DSMDataType.String;
        if (t == typeof(Vector2)) return DSMDataType.Vector2;
        if (t == typeof(Vector3)) return DSMDataType.Vector3;
        if (t == typeof(Vector4)) return DSMDataType.Vector4;
        if (t == typeof(Color))   return DSMDataType.Color;
        return DSMDataType.String;
    }

    public static string ValueToSerialized(object value) => value switch
    {
        Vector2 v => $"{Fs(v.x)},{Fs(v.y)}",
        Vector3 v => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)}",
        Vector4 v => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)},{Fs(v.w)}",
        Color c   => $"{Fs(c.r)},{Fs(c.g)},{Fs(c.b)},{Fs(c.a)}",
        float f   => f.ToString("G", CultureInfo.InvariantCulture),
        double d  => d.ToString("G", CultureInfo.InvariantCulture),
        _         => value.ToString() ?? string.Empty
    };

    public static string GetTypeDefault(DSMDataType type) => type switch
    {
        DSMDataType.Bool => "False", DSMDataType.Int => "0", DSMDataType.Float => "0",
        DSMDataType.Double => "0",   DSMDataType.Long => "0", DSMDataType.String => string.Empty,
        DSMDataType.Vector2 => "0,0", DSMDataType.Vector3 => "0,0,0",
        DSMDataType.Vector4 => "0,0,0,0", DSMDataType.Color => "1,1,1,1",
        _ => string.Empty
    };

    private static string Fs(float v) => v.ToString("G", CultureInfo.InvariantCulture);
}

#endif

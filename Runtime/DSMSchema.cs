#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class DSMSchema
{
    private static readonly object s_cacheLock = new();
    private static readonly Dictionary<Type, DSMSchema> s_cache = new();
    private static readonly DSMSchema s_empty = new(new Dictionary<string, Type>());

    private readonly IReadOnlyDictionary<string, Type> _map;

    private DSMSchema(IReadOnlyDictionary<string, Type> map)
    {
        _map = map;
    }

    public int Count => _map.Count;

    public bool TryGetExpectedType(string key, out Type expectedType) =>
        _map.TryGetValue(key, out expectedType!);

    public static DSMSchema For(Type? constantType)
    {
        if (constantType == null) return s_empty;

        lock (s_cacheLock)
        {
            if (s_cache.TryGetValue(constantType, out var cached)) return cached;
            var built = Build(constantType);
            s_cache[constantType] = built;
            return built;
        }
    }

    private static DSMSchema Build(Type constantType)
    {
        try
        {
            var map = new Dictionary<string, Type>();
            foreach (var field in constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
                map[field.Name] = field.FieldType;
            return new DSMSchema(map);
        }
        catch (ReflectionTypeLoadException ex)
        {
            Debug.LogWarning($"DSM: could not build schema for '{constantType.Name}': {ex.Message}");
            return s_empty;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DSM: could not build schema for '{constantType.Name}': {ex.Message}");
            return s_empty;
        }
    }
}

public sealed class DSMSchemaViolationException : Exception
{
    public DSMSchemaViolationException(string key, Type expected, Type actual)
        : base($"DSM: schema violation for key '{key}' — expected '{expected.Name}', got '{actual.Name}'.")
    {
    }
}

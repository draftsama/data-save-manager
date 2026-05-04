#nullable enable

using System;
using UnityEngine;

[Serializable]
public struct DSMTransformData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public static readonly DSMTransformData Identity = new()
    {
        Position = Vector3.zero,
        Rotation = Quaternion.identity,
        Scale = Vector3.one,
    };
}

public static class DSMTransformExtensions
{
    public static DSMTransformData Capture(this Transform transform) => new()
    {
        Position = transform.position,
        Rotation = transform.rotation,
        Scale = transform.localScale,
    };

    public static void Restore(this Transform transform, DSMTransformData data)
    {
        transform.position = data.Position;
        transform.rotation = data.Rotation;
        transform.localScale = data.Scale;
    }
}

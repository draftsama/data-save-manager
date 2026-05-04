#nullable enable

using System;
using UnityEngine;

[Serializable]
public sealed class DSMDataEntry
{
    [SerializeField] public string Key = string.Empty;
    [SerializeField] public DSMDataType Type = DSMDataType.String;
    // Stored as comma-separated for vectors/color: "x,y" "x,y,z" "x,y,z,w" "r,g,b,a"
    [SerializeField] public string SerializedDefault = string.Empty;
}

#nullable enable

using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class DSMDataEntry
{
    [SerializeField, FormerlySerializedAs("Key")]
    private string _key = string.Empty;
    public string Key { get => _key; set => _key = value; }

    [SerializeField, FormerlySerializedAs("Type")]
    private DSMDataType _type = DSMDataType.String;
    public DSMDataType Type { get => _type; set => _type = value; }

    // Stored as comma-separated for vectors/color: "x,y" "x,y,z" "x,y,z,w" "r,g,b,a"
    [SerializeField, FormerlySerializedAs("SerializedDefault")]
    private string _serializedDefault = string.Empty;
    public string SerializedDefault { get => _serializedDefault; set => _serializedDefault = value; }
}

#nullable enable
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class DSMSlotVersionPanel
{
    private readonly List<Reading> _readings = new();
    private bool _expanded = true;
    private int _currentVersion = DSMSaveEnvelope.LegacyVersion;

    private static GUIStyle? s_okStyle;
    private static GUIStyle? s_warnStyle;

    internal IEnumerable<string> InspectedSlots
    {
        get
        {
            foreach (var reading in _readings)
                yield return reading.Slot;
        }
    }

    // Readings are taken on refresh, never per OnGUI frame: each one is a file read plus a
    // decrypt for encrypted slots.
    public void Refresh(DSMConfig? config, IEnumerable<string> slotNames, Func<string, string> resolveSlotPath,
        int? currentVersion = null)
    {
        _readings.Clear();
        if (config == null) return;

        // Callers that already know the target version pass it in; reading DSM.CurrentSaveVersion
        // builds the global manager, which tests must not do.
        _currentVersion = currentVersion ?? DSM.CurrentSaveVersion;
        foreach (var slot in slotNames)
        {
            var known = DSMSaveInspector.TryReadOnDiskVersion(resolveSlotPath(slot), config, out var version);
            _readings.Add(new Reading(slot, known, version));
        }
    }

    public void Draw()
    {
        _expanded = EditorGUILayout.BeginFoldoutHeaderGroup(_expanded, "Save Versions");
        if (_expanded)
        {
            if (_readings.Count == 0)
            {
                EditorGUILayout.HelpBox("No slots to inspect.", MessageType.Info);
            }
            else
            {
                foreach (var reading in _readings)
                {
                    using var row = new EditorGUILayout.HorizontalScope();
                    GUILayout.Label(reading.Slot, EditorStyles.miniLabel, GUILayout.Width(140));
                    GUILayout.Label(reading.Known ? $"v{reading.Version}" : "v?", EditorStyles.miniLabel, GUILayout.Width(34));
                    GUILayout.Label(StatusText(reading), IsClean(reading) ? OkStyle : WarnStyle);
                    GUILayout.FlexibleSpace();
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private bool IsClean(Reading reading) => reading.Known && reading.Version == _currentVersion;

    private string StatusText(Reading reading)
    {
        if (!reading.Known) return "Unknown — unreadable or not saved yet";
        if (reading.Version == _currentVersion) return $"Up to date (v{_currentVersion})";
        if (reading.Version < _currentVersion) return $"Needs migration: v{reading.Version} → v{_currentVersion}";
        return $"Newer than this build: v{reading.Version} > v{_currentVersion}";
    }

    private static GUIStyle OkStyle =>
        s_okStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.45f, 0.8f, 0.45f) } };

    private static GUIStyle WarnStyle =>
        s_warnStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.75f, 0.2f) } };

    private readonly struct Reading
    {
        public readonly string Slot;
        public readonly bool Known;
        public readonly int Version;

        public Reading(string slot, bool known, int version)
        {
            Slot = slot;
            Known = known;
            Version = version;
        }
    }
}

#endif

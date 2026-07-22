#nullable enable
#if UNITY_EDITOR

using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

internal sealed class DSMKeyRotationPanel
{
    private readonly Action? _repaint;
    private string _newKey = string.Empty;
    private bool _rotating;
    private bool _lastFailed;
    private string _lastResult = string.Empty;
    private bool _expanded;

    public DSMKeyRotationPanel(Action? repaint = null) => _repaint = repaint;

    public void Draw()
    {
        _expanded = EditorGUILayout.BeginFoldoutHeaderGroup(_expanded, "Encryption Key Rotation");
        if (_expanded)
        {
            _newKey = EditorGUILayout.PasswordField("New Key", _newKey);

            using (new EditorGUI.DisabledScope(_rotating || string.IsNullOrWhiteSpace(_newKey)))
            {
                if (GUILayout.Button(_rotating ? "Rotating…" : "Rotate Encryption Key", GUILayout.Height(24)))
                    Rotate();
            }

            if (!string.IsNullOrEmpty(_lastResult))
                EditorGUILayout.HelpBox(_lastResult, _lastFailed ? MessageType.Error : MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void Rotate()
    {
        if (!EditorUtility.DisplayDialog(
                "Rotate encryption key?",
                "This re-encrypts EVERY slot with the new key and cannot be undone.",
                "Rotate", "Cancel"))
            return;

        // Rotation re-encrypts every slot as one atomic operation, so the guard must hold
        // until the awaited call finishes — a second invoke mid-flight would compete with it.
        _rotating = true;
        _lastResult = string.Empty;
        RotateAsync().Forget();
    }

    private async UniTaskVoid RotateAsync()
    {
        try
        {
            await DSM.RotateEncryptionKeyAsync(_newKey);
            _lastFailed = false;
            _lastResult = "Encryption key rotated — every encrypted slot was re-encrypted.";
            _newKey = string.Empty;
        }
        catch (Exception ex)
        {
            _lastFailed = true;
            _lastResult = $"Key rotation failed: {ex.GetType().Name}. See the Console for details.";
            Debug.LogError($"DSM key rotation failed: {ex.GetType().Name} — {ex.Message}");
        }
        finally
        {
            _rotating = false;
            _repaint?.Invoke();
        }
    }
}

#endif

using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
public partial class AdminHarness {
    private void OpenMenu()
    {
        visible = true;
        AnyOpen = true;
        buffers.Clear();
        Refresh();
        BlockUIInput();
        if (freezeGameWhenOpen) AcquireFreeze();
    }
    private void CloseMenu()
    {
        visible = false;
        AnyOpen = false;
        foreach (var raycaster in blockedRaycasters) if (raycaster != null) raycaster.enabled = true;
        blockedRaycasters.Clear();
        ReleaseFreeze();
    }
    private void BlockUIInput()
    {
        foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (raycaster != null && raycaster.enabled) { raycaster.enabled = false; blockedRaycasters.Add(raycaster); }
    }
    private void AcquireFreeze()
    {
        if (ownsPause || GameplayPause.IsPaused) return;
        GameplayPause.Pause();
        ownsPause = true;
    }
    private void ReleaseFreeze()
    {
        if (!ownsPause) return;
        ownsPause = false;
        GameplayPause.Resume();
    }
    private static bool TryParseFloat(string s, out float result)
    {
        return float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) && !float.IsInfinity(result);
    }
    private string EditText(string key, string current)
    {
        GUI.SetNextControlName(key);
        bool focused = GUI.GetNameOfFocusedControl() == key;
        string shown = focused && buffers.TryGetValue(key, out string b) ? b : current;
        EditorGUI.BeginChangeCheck();
        string edited = GUILayout.TextField(shown, GUILayout.MinWidth(70));
        lastTextEdited = EditorGUI.EndChangeCheck();
        if (focused) buffers[key] = edited; else buffers.Remove(key);
        return edited;
    }
    private int IntRow(string label, string key, int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        string e = EditText(key, value.ToString());
        GUILayout.EndHorizontal();
        return lastTextEdited && int.TryParse(e, out int r) ? r : value;
    }
    private float FloatRow(string label, string key, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        string e = EditText(key, FormatFloat(value));
        GUILayout.EndHorizontal();
        return lastTextEdited && TryParseFloat(e, out float r) ? r : value;
    }
    private bool VecComponent(string key, ref float value)
    {
        string cur = FormatFloat(value);
        string e = EditText(key, cur);
        if (lastTextEdited && TryParseFloat(e, out float r)) { value = r; return true; }
        return false;
    }
    private static string FormatFloat(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

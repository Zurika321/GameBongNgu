using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyCombatConfig))]
public sealed class EnemyCombatConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var config = (EnemyCombatConfig)target;
        if (config.needsHealth && config.maxHealth <= 0f)
            EditorGUILayout.HelpBox("Excel chưa có HP: gán Max Health. Chiêu bị khóa khi HP chưa cấu hình.", MessageType.Warning);
        if (config.needsAttack && config.attack <= 0f)
            EditorGUILayout.HelpBox("Excel chưa có ATK: gán Attack. Chiêu gây sát thương chờ chỉ số này; bản triệu hồi có thể nhận ATK từ chủ.", MessageType.Warning);
        EditorGUILayout.HelpBox("Prefab → CharacterAnimation: kéo Unity Json Asset của đúng quái. Skill cast/end nằm trong EnemySkillConfig → Default Context → Effect Animation.", MessageType.Info);
        DrawDefaultInspector();
        if (config.phases != null)
            foreach (var phase in config.phases)
                if (phase != null && phase.manualOnly) EditorGUILayout.HelpBox("Phase " + phase.phase + ": " + phase.notes, MessageType.Info);
        if (config.skills != null)
            foreach (var slot in config.skills)
                if (slot != null && slot.needsInspectorSetup) EditorGUILayout.HelpBox(slot.skillId + ": " + slot.setupNotes, MessageType.Warning);
    }
}

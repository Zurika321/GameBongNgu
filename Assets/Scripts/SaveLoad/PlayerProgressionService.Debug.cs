#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerProgressionService
{
    public bool DebugSetLevel(int level)
    {
        if (Data == null) return false;
        level = Mathf.Clamp(level, 1, 20);
        LevelData entry = FindLevel(GetLevelTable(), level);
        if (entry == null) return false;
        Data.level = level;
        Data.exp = entry.expRequired;
        for (int current = 1; current <= level; current++) UnlockSkillsForLevel(current, false);
        SyncPlayerSkillControllers();
        if (player == null) FindPlayer();
        Data.hp = GetMaxHealth();
        if (playerHealth != null) playerHealth.ConfigureHealth(Data.hp, Data.hp);
        StatsChanged?.Invoke();
        Save();
        return true;
    }

    public bool DebugSetSkillUnlocked(string skill, bool unlocked)
    {
        if (Data == null) return false;
        SkillConfigEntry entry = ResolveSkillEntry(skill);
        if (entry == null) return false;
        ChangeDebugSkill(entry.skillId, unlocked);
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public void DebugSetAllSkillsUnlocked(bool unlocked)
    {
        if (Data == null) return;
        if (unlocked)
        {
            foreach (SkillConfigEntry entry in ResolveSkillConfig().Skills)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId)) ChangeDebugSkill(entry.skillId, true);
        }
        else
        {
            Data.unlockedSkills?.Clear();
            Data.equippedSkills?.Clear();
            skillStateVersion++;
        }
        SyncPlayerSkillControllers();
        Save();
    }

    private void ChangeDebugSkill(string skill, bool unlocked)
    {
        Data.unlockedSkills ??= new List<string>();
        if (unlocked)
        {
            if (HasUnlockedSkill(skill)) return;
            Data.unlockedSkills.Add(skill);
            SkillUnlocked?.Invoke(skill);
        }
        else
        {
            Data.unlockedSkills.RemoveAll(id => string.Equals(ResolveSkillId(id), skill, StringComparison.OrdinalIgnoreCase));
            Data.equippedSkills?.RemoveAll(entry => entry != null && string.Equals(ResolveSkillId(entry.skillName), skill, StringComparison.OrdinalIgnoreCase));
        }
        skillStateVersion++;
    }
}
#endif

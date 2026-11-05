using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skill Unlock Config")]
public class SkillUnlockConfig : LvConfig
{
    [Obsolete("Use LvConfig instead. This class is kept only for legacy references and compatibility.")]
    public new void EnsureDefaultEntries()
    {
        base.EnsureDefaultEntries();
    }

    [Obsolete("Use LvConfig.GetSkillsForLevel(level) instead.")]
    public new string[] GetSkillsForLevel(int level)
    {
        return base.GetSkillsForLevel(level);
    }

    [Obsolete("Use LvConfig.GetLevelEntry(level) instead.")]
    public new LevelEntry GetLevelEntry(int level)
    {
        return base.GetLevelEntry(level);
    }

    [Obsolete("Use LvConfig.ToLevelTable() instead.")]
    public new LevelTable ToLevelTable()
    {
        return base.ToLevelTable();
    }
}

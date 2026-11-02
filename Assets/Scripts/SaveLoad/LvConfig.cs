using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Bóng Ngủ/Lv Config")]
public class LvConfig : ScriptableObject
{
    [System.Serializable]
    public class LevelEntry
    {
        [Header("Level")]
        public int level = 1;

        [Header("Exp cần để lên level này")]
        public int expRequired;

        [Header("Thông số level")]
        public int maxHp = 60;
        public int atk = 8;
        public int def = 3;
        public int maxRareAmmo = 2;

        [Header("Skill mở ở level này")]
        public List<string> skills = new List<string>();
    }

    [System.Serializable]
    private class LevelJsonRoot
    {
        public List<LevelJsonEntry> levels = new List<LevelJsonEntry>();
    }

    [System.Serializable]
    private class LevelJsonEntry
    {
        public int lv;
        public int expRequired;
        public int maxHp;
        public int atk;
        public int def;
        public int maxRareAmmo;
        public string skill;
    }

    [Header("Bảng level luôn dùng asset này")]
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

    public List<LevelEntry> Levels => levels;

    public bool LoadDefaultFromJson()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/Level");
        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            return false;

        LevelJsonRoot root = JsonUtility.FromJson<LevelJsonRoot>(jsonAsset.text);
        if (root == null || root.levels == null || root.levels.Count == 0)
            return false;

        levels = new List<LevelEntry>();
        foreach (LevelJsonEntry item in root.levels.OrderBy(entry => entry.lv))
        {
            if (item == null)
                continue;

            LevelEntry entry = new LevelEntry
            {
                level = item.lv > 0 ? item.lv : levels.Count + 1,
                expRequired = item.expRequired,
                maxHp = item.maxHp,
                atk = item.atk,
                def = item.def,
                maxRareAmmo = item.maxRareAmmo,
                skills = new List<string>()
            };

            if (!string.IsNullOrWhiteSpace(item.skill))
                entry.skills.Add(item.skill.Trim());

            levels.Add(entry);
        }

        return levels.Count > 0;
    }

    public void EnsureDefaultEntries()
    {
        if (levels != null && levels.Count > 0)
            return;

        if (LoadDefaultFromJson())
            return;

        levels = new List<LevelEntry>();

        int[] xp = { 0, 120, 280, 480, 740, 1050, 1420, 1860, 2380, 2980, 3660, 4440, 5320, 6320, 7440, 8700, 10100, 11660, 13400, 15340 };
        int[] hp = { 60, 72, 85, 98, 112, 126, 142, 158, 175, 193, 212, 232, 253, 276, 300, 326, 354, 384, 416, 450 };
        int[] atk = { 8, 10, 12, 14, 17, 19, 22, 25, 28, 31, 35, 39, 43, 48, 54, 60, 67, 74, 82, 91 };
        int[] def = { 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 15, 16, 18, 20, 22, 24, 27, 30, 33, 36 };
        int[] ammo = { 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 12 };

        for (int i = 0; i < xp.Length; i++)
        {
            LevelEntry entry = new LevelEntry
            {
                level = i + 1,
                expRequired = xp[i],
                maxHp = hp[i],
                atk = atk[i],
                def = def[i],
                maxRareAmmo = ammo[i],
                skills = new List<string>()
            };
            levels.Add(entry);
        }
    }

    public LevelEntry GetLevelEntry(int level)
    {
        if (levels == null)
            return null;

        return levels.FirstOrDefault(entry => entry != null && entry.level == level);
    }

    public string[] GetSkillsForLevel(int level)
    {
        LevelEntry entry = GetLevelEntry(level);
        if (entry == null || entry.skills == null)
            return System.Array.Empty<string>();

        return entry.skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct()
            .ToArray();
    }

    public LevelTable ToLevelTable()
    {
        LevelTable table = new LevelTable();
        if (levels == null)
            return table;

        foreach (LevelEntry entry in levels.OrderBy(entry => entry.level))
        {
            if (entry == null)
                continue;

            table.levels.Add(new LevelData
            {
                lv = entry.level,
                expRequired = entry.expRequired,
                maxHp = entry.maxHp,
                atk = entry.atk,
                def = entry.def,
                maxRareAmmo = entry.maxRareAmmo,
            });
        }

        return table;
    }
}

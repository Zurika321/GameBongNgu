using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bộ lưu modifier mặc định, gắn trên Player. PlayerProgressionService tự tìm (hoặc tự thêm nếu thiếu) component này
/// và gọi GetFinalValue trong GetAttackPower / GetDefensePower / GetMaxHealth / ModifyStat.
/// Công thức: (base + tổng Flat) * (1 + tổng AddPercent) * tích Multiply.
/// </summary>
public class RuntimeStatModifierStore : MonoBehaviour, IStatModifierReceiver
{
    [Serializable]
    private class RuntimeModifier
    {
        public string sourceId;
        public PlayerStatType stat;
        public StatModifierType type;
        public float value;
    }

    [SerializeField] private List<RuntimeModifier> modifiers = new List<RuntimeModifier>();

    /// <summary>Phát ra mỗi khi danh sách modifier thay đổi (thêm hoặc gỡ).</summary>
    public event Action Changed;

    public void AddModifier(string sourceId, PlayerStatType stat, StatModifierType modifierType, float value)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            Debug.LogWarning("[RuntimeStatModifierStore] sourceId không được để trống.", this);
            return;
        }

        modifiers.RemoveAll(m => m != null && m.sourceId == sourceId
            && m.stat == stat && m.type == modifierType);
        modifiers.Add(new RuntimeModifier
        {
            sourceId = sourceId,
            stat = stat,
            type = modifierType,
            value = value
        });
        Changed?.Invoke();
    }

    public void RemoveModifiers(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return;
        int removed = modifiers.RemoveAll(m => m == null || m.sourceId == sourceId);
        if (removed > 0) Changed?.Invoke();
    }

    public float GetFinalValue(PlayerStatType stat, float baseValue)
    {
        float flat = 0f;
        float addPercent = 0f;
        float multiplier = 1f;

        foreach (RuntimeModifier modifier in modifiers)
        {
            if (modifier == null || modifier.stat != stat) continue;
            switch (modifier.type)
            {
                case StatModifierType.Flat:
                    flat += modifier.value;
                    break;
                case StatModifierType.AddPercent:
                    addPercent += modifier.value;
                    break;
                case StatModifierType.Multiply:
                    multiplier *= modifier.value;
                    break;
            }
        }

        return (baseValue + flat) * (1f + addPercent) * multiplier;
    }

    /// <summary>Mô tả các modifier đang có (dùng cho log debug).</summary>
    public string Describe()
    {
        if (modifiers.Count == 0) return "(không có modifier)";
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (RuntimeModifier modifier in modifiers)
        {
            if (modifier == null) continue;
            builder.AppendLine(modifier.sourceId + ": " + modifier.stat + " " + modifier.type + " " + modifier.value);
        }
        return builder.ToString();
    }

    private void OnDestroy()
    {
        modifiers.Clear();
    }
}


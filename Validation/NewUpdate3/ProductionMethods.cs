using System;
using System.Collections.Generic;
using UnityEngine;
public partial class AnimationHarness {
    private void Update()
    {
        if (GameplayPause.IsPaused) return;
        if (customFrames != null) { UpdateCustomFrames(); return; }
        if (!isPlaying || currentAnimation == null || currentAnimationDirection == null)
            return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta < 0f) delta = 0f;

        float previousFrame = currentFrame;
        float previousElapsed = elapsed;
        elapsed += delta;
        float fps = Mathf.Max(1f, currentAnimationDirection.frameRate);
        float frameCount = Mathf.Max(1, currentAnimationDirection.frameCount);
        float rawFrame = elapsed * fps;

        if (holdingSkillCharge)
        {
            int castStart = FindAudioBinding(CurrentAnimation)?.castStartFrame ?? currentAnimation.castStartFrame;
            // Stop just before Cast so its SFX/effects only start on release.
            float chargeEnd = Mathf.Max(0f, castStart - 0.001f);
            rawFrame = Mathf.Min(rawFrame, chargeEnd);
            elapsed = rawFrame / fps;
        }

        if (currentAnimation.loop && !playingSkill)
        {
            int previousCycle = Mathf.FloorToInt(previousElapsed * fps / frameCount);
            int currentCycle = Mathf.FloorToInt(rawFrame / frameCount);
            bool wrapped = currentCycle > previousCycle;
            currentFrame = Mathf.Repeat(rawFrame, frameCount);
            if (wrapped && !currentAnimation.isCombat) PlayAnimationStartSound(currentAnimation);
            TriggerCombatPhaseCrossings(previousFrame, currentFrame, wrapped);
        }
        else
        {
            currentFrame = Mathf.Min(rawFrame, frameCount);
            TriggerCombatPhaseCrossings(previousFrame, currentFrame, false);
        }

        ApplyAnimationPose(currentFrame);
        ApplyBlockStates(currentFrame);

        if ((!currentAnimation.loop || playingSkill) && !holdingSkillCharge && rawFrame >= frameCount && !completedRaised)
        {
            isPlaying = false;
            completedRaised = true;
            playingSkill = false;
            holdingSkillCharge = false;
            OnAnimationCompleted?.Invoke(currentAnimation.name);
        }
    }
}
public partial class LootHarness {
    private bool HasRewardItem(ZoneDropDefinition drop, PlayerProgressionService progress)
    {
        if (progress.Data.GetItemAmount(drop.itemName) > 0) return true;
        if (progress.Data.storageContainers != null)
            foreach (var storage in progress.Data.storageContainers)
                if (storage?.items != null && storage.items.Exists(item => item != null && item.itemName == drop.itemName && item.amount > 0)) return true;
        return progress.Data.worldDrops != null && progress.Data.worldDrops.Exists(item => item != null && item.itemName == drop.itemName && item.amount > 0 && !progress.HasCollectedWorldItem(item.itemId));
    }
    private void SpawnDrops(SpawnedEnemy enemy, PlayerProgressionService progress)
    {
        EnemyCombatConfig config = enemyCombatConfig;
        if (config == null)
        {
            EnemySkillController skills = enemy.Object != null ? enemy.Object.GetComponentInChildren<EnemySkillController>(true) : null;
            config = skills != null ? skills.CombatConfig : null;
        }
        if (config == null)
        {
            Debug.LogWarning("[ZoneEnemySpawner] Chưa gán Enemy Combat Config để nhận bảng rơi đồ: " + enemyDisplayName, this);
            return;
        }
        ZoneDropDefinition[] drops = config.drops;
        if (drops == null || drops.Length == 0) return;
        if (itemPickupPrefab == null)
        {
            Debug.LogError($"[ZoneEnemySpawner] Cannot drop loot for {enemyDisplayName}: item pickup prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < drops.Length; i++)
        {
            ZoneDropDefinition drop = drops[i];
            if (drop == null || string.IsNullOrWhiteSpace(drop.itemName) || drop.amount <= 0 || (drop.onlyIfMissing && HasRewardItem(drop, progress)))
                continue;
            float chance = Mathf.Clamp01(drop.dropChance);
            if (chance <= 0f || (chance < 1f && UnityEngine.Random.value >= chance)) continue;

            string itemId = $"{enemy.Id}:DROP:{i}";
            if (progress.HasCollectedWorldItem(itemId)) continue;
            Vector2 position = enemy.Object.transform.position;
            progress.RegisterWorldDrop(itemId, drop.itemName, drop.amount, position);
            GameObject pickup = Instantiate(itemPickupPrefab, position, Quaternion.identity, transform);
            WorldItemPickup worldItem = pickup.GetComponent<WorldItemPickup>();
            if (worldItem == null)
            {
                Debug.LogError($"[ZoneEnemySpawner] Item pickup prefab '{itemPickupPrefab.name}' has no WorldItemPickup component.", pickup);
                Destroy(pickup);
                continue;
            }

            worldItem.Configure(itemId, drop.itemName, drop.amount);
        }
    }
    private sealed class SpawnedEnemy
    {
        public string Id { get; }
        public GameObject Object { get; }
        public int Index { get; }
        public SpawnedEnemy(string id, GameObject gameObject, int index)
        {
            Id = id;
            Object = gameObject;
            Index = index;
        }
    }
    private int GetExperienceReward() => Mathf.Max(0, experiencePerEnemy);
}

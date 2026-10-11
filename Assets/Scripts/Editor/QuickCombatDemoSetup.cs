using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using UnityEngine.Rendering.Universal;

public static class QuickCombatDemoSetup
{
    private const string GeneratedRootName = "DEMO_GENERATED_CONTENT";
    private const string SceneFolder = "Assets/Scenes/Zones";
    private const string EnemyFolder = "Assets/Generated/Session1To2";
    private const string CmCamPrefabPath = "Assets/Prefabs/other/CmCam.prefab";
    private static readonly ZoneSpec[] Zones =
    {
        new ZoneSpec("Zone2", "Zone 2 - Thị Trấn Rệu Rã & Vùng Lân Cận", "Session5Complete", "S5_BossDefeated", "5", "6A", 1,
            new PlaceSpec("Nhà Kho Đá", "Session 4B", "Nhà Kho Đá bỏ hoang ngoài rìa làng.", "BloodBat", "Én"),
            new PlaceSpec("Quảng Trường Trung Tâm", "Session 5, 6A", "Trung tâm thị trấn và chiến trường ban đêm.", "TownUndead|MutatedCrow", "Bà Tư Bán Thuốc|Nhi", "Đàn Sói Sương Mù"),
            new PlaceSpec("Quán Trọ Ven Sông", "Session 7A", "Quán trọ nhỏ bên sông, nơi Khải và Nhi xây dựng lòng tin.", "", "Nhi"),
            new PlaceSpec("Con Đường Rừng Sương", "Session 6B", "Đường mòn nối thị trấn với vùng đền cổ.", "GhostHound", "Bé Tí")),
        new ZoneSpec("Zone3", "Zone 3 - Ngôi Đền Cổ & Xưởng Rèn", "Session9AComplete", "S9A_CaveWardenDefeated", "9A", "9B", 1,
            new PlaceSpec("Ngôi Đền Cổ", "Session 8A", "Đền đá cổ chìm dưới rễ cây khổng lồ.", "ShadowSpider", "Bóng Ma Nạn Nhân Đầu Tiên", "Vệ Binh Đá Cổ"),
            new PlaceSpec("Hang Động Ngầm", "Session 9A", "Hang động dưới đền, nơi thực hiện nghi lễ giải thoát.", "MirrorShade", "Ẩn Sĩ Mù", "Người Gác Hang"),
            new PlaceSpec("Xưởng Rèn Sườn Núi", "Session 9B", "Xưởng rèn bỏ hoang trên sườn núi.", "ShadowSpider", "Lão Rèn Cốc"),
            new PlaceSpec("Bìa Rừng Cháy", "Session 8B", "Khu rừng cháy xém sau cơn giận dữ của Khải.", "GhostHound", "Thầy Hạo")),
        new ZoneSpec("Zone4", "Zone 4 - Đỉnh Đồi & Đại Sảnh Nghi Lễ", "Session13AComplete", "S13A_ConfrontationComplete", "13A", "14", 2,
            new PlaceSpec("Đỉnh Đồi", "Session 10", "Đỉnh đồi nhìn xuống đền cổ, nơi chuẩn bị trước trận chiến.", "", "Nhi"),
            new PlaceSpec("Không Gian Tiềm Thức", "Session 11 (ẩn)", "Không gian ký ức vỡ vụn, chỉ vào được qua giấc ngủ.", "", "Vô Diện"),
            new PlaceSpec("Đại Sảnh Nghi Lễ", "Session 12, 13A/13B/13C", "Đấu trường boss cuối và nơi diễn ra nghi lễ.", "TownUndead", "Ẩn Sĩ Mù|Thầy Hạo", "Vô Diện")),
        new ZoneSpec("Zone5", "Zone 5 - Hầm Mộ Bí Mật (Secret)", "Session14Complete", "S15A_HaoSecretDefeated", "15A", "15B", 1,
            new PlaceSpec("Quảng Trường Hậu Chiến", "Session 14", "Quảng trường hồi sinh sau đêm cuối cùng.", "", "Dân Làng (tổng hợp)"),
            new PlaceSpec("Hầm Mộ Bí Mật", "Session 15A/15B (ẩn)", "Hầm mộ dưới am thờ, lưu tên nạn nhân qua nhiều thế kỷ.", "", "Hồn Ma Nạn Nhân Cũ", "Thầy Hạo - Thật Diện Mạo"))
    };

    [MenuItem("Tools/Game Setup/Update Demo Zones 2-5")]
    private static void UpdateDemoZones()
    {
        if (!EditorUtility.DisplayDialog("Update Demo Zones", "Cập nhật scene Zone 2–5 theo dữ liệu demo. CoreCombatDemo không nằm trong danh sách scene được mở hoặc lưu.", "Cập nhật", "Hủy")) return;
        BuildDemoZones();
    }

    public static void BuildDemoZonesFromBatchMode()
    {
        BuildDemoZones();
    }

    private static void BuildDemoZones()
    {
        EnsureFolder("Assets/Scenes"); EnsureFolder(SceneFolder);
        foreach (ZoneSpec zone in Zones) BuildZone(zone);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[QuickCombatDemoSetup] Updated generated demo scenes Zone2–Zone5. CoreCombatDemo was not modified.");
    }

    private static void BuildZone(ZoneSpec zone)
    {
        string path = SceneFolder + "/" + zone.sceneName + ".unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == GeneratedRootName || root.name == zone.sceneName + "Main")
                UnityEngine.Object.DestroyImmediate(root);
        SceneManager.SetActiveScene(scene);
        Transform zoneMain = new GameObject(zone.sceneName + "Main").transform;
        GameObject player = CreatePlayer(null);
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.gameObject.AddComponent<AudioListener>();
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camera.gameObject.AddComponent<CinemachineBrain>();

        GameObject cmCamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CmCamPrefabPath);
        GameObject cmCamObject = cmCamPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(cmCamPrefab, scene)
            : new GameObject("CmCam");
        cmCamObject.name = "CmCam";
        CinemachineCamera cmCam = cmCamObject.GetComponent<CinemachineCamera>();
        CinemachineConfiner2D confiner = cmCamObject.GetComponent<CinemachineConfiner2D>();
        if (cmCam != null) cmCam.Follow = player.transform;

        GameObject mapBounusObject = new GameObject("MapBounus");
        Transform mapBounus = mapBounusObject.transform;
        Transform[] placeRoots = CreateZonePlaces(zoneMain, mapBounus, zone);
        player.transform.position = placeRoots[0].position + new Vector3(8f, 0f, 0f);
        GameObject skillsRuntime = new GameObject("SkillActions");
        skillsRuntime.AddComponent<SkillActions>();
        GameObject manager = new GameObject("Player Progression SaveData"); manager.transform.SetParent(zoneMain, false);
        manager.AddComponent<PlayerProgressionService>();
        GameObject director = new GameObject("Story Event Settings"); director.transform.SetParent(zoneMain, false);
        director.AddComponent<StoryEventDirector>();
        ConnectedStoryDirector connectedStory = zoneMain.gameObject.AddComponent<ConnectedStoryDirector>();
        SerializedObject storyData = new SerializedObject(connectedStory);
        storyData.FindProperty("player").objectReferenceValue = player.transform;
        storyData.FindProperty("mapBounous").objectReferenceValue = mapBounus;
        storyData.FindProperty("cameraConfiner").objectReferenceValue = confiner;
        storyData.FindProperty("showStoryStatus").boolValue = false;
        SerializedProperty spawnPoints = storyData.FindProperty("zoneSpawnPoints");
        spawnPoints.arraySize = placeRoots.Length;
        for (int i = 0; i < placeRoots.Length; i++)
        {
            spawnPoints.GetArrayElementAtIndex(i).FindPropertyRelative("currentZone").stringValue = zone.places[i].name;
            spawnPoints.GetArrayElementAtIndex(i).FindPropertyRelative("startTransform").objectReferenceValue = placeRoots[i];
        }
        storyData.ApplyModifiedPropertiesWithoutUndo();
        StoryEventDirector storyDirector = director.GetComponent<StoryEventDirector>();
        CreateWorld(zoneMain, placeRoots, zone, storyDirector);
        for (int i = 0; i < zone.places.Length; i++)
        {
            string[] npcNames = zone.places[i].npcs.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int n = 0; n < npcNames.Length; n++)
                CreateNpc(placeRoots[i], npcNames[n], zone.places[i].name, new Vector3(-3f + n * 3f, 1f, 0f));
        }
        CreateSkillDisplay(zoneMain, zone);
        CreatePortals(zoneMain, zone, placeRoots);
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static Transform[] CreateZonePlaces(Transform zoneMain, Transform mapBounus, ZoneSpec zone)
    {
        Transform[] roots = new Transform[zone.places.Length];
        float firstX = -((zone.places.Length - 1) * 24f) * 0.5f;
        Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyFolder + "/Floor.png");
        for (int i = 0; i < zone.places.Length; i++)
        {
            PlaceSpec place = zone.places[i];
            float centerX = firstX + i * 24f;
            GameObject area = new GameObject(zone.sceneName.Replace("Zone", "Zone ") + " - " + place.name);
            area.transform.SetParent(zoneMain, false);
            area.transform.position = new Vector3(centerX, 0f, 0f);
            roots[i] = area.transform;
            CreateLabel(area.transform, place.name, new Vector3(0f, 6.5f, 0f), 0.55f);
            CreateLabel(area.transform, place.session, new Vector3(0f, 5.6f, 0f), 0.23f);
            CreateLabel(area.transform, place.description, new Vector3(0f, 4.7f, 0f), 0.18f);

            GameObject mapRegion = new GameObject(place.name);
            mapRegion.transform.SetParent(mapBounus, false);
            mapRegion.transform.position = new Vector3(centerX, 0f, 0f);
            PolygonCollider2D polygon = mapRegion.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;
            polygon.SetPath(0, new[]
            {
                new Vector2(-12f, -8f), new Vector2(12f, -8f),
                new Vector2(12f, 8f), new Vector2(-12f, 8f)
            });
            MapZoneRegion mapZone = mapRegion.AddComponent<MapZoneRegion>();
            SerializedObject mapZoneData = new SerializedObject(mapZone);
            mapZoneData.FindProperty("displayName").stringValue = place.name;
            mapZoneData.FindProperty("mapColor").colorValue = confinerColor(i);
            mapZoneData.FindProperty("polygon").objectReferenceValue = polygon;
            mapZoneData.ApplyModifiedPropertiesWithoutUndo();

            GameObject floor = new GameObject("Ground - " + place.name);
            floor.transform.SetParent(area.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0f, 2f);
            floor.transform.localScale = new Vector3(23.8f, 15.8f, 1f);
            SpriteRenderer renderer = floor.AddComponent<SpriteRenderer>();
            renderer.sprite = floorSprite;
            renderer.color = confinerColor(i);
            renderer.sortingOrder = -10;
        }
        return roots;
    }

    private static Color confinerColor(int index)
    {
        Color[] colors = { new Color(0.16f, 0.13f, 0.18f), new Color(0.13f, 0.18f, 0.19f),
            new Color(0.19f, 0.16f, 0.13f), new Color(0.15f, 0.14f, 0.2f) };
        return colors[index % colors.Length];
    }

    private static GameObject CreatePlayer(Transform parent)
    {
        GameObject player = new GameObject("Player"); player.tag = "Player"; player.transform.SetParent(parent); player.transform.position = Vector3.zero;
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<BoxCollider2D>(); player.AddComponent<HealthController>(); player.AddComponent<PlayerController>();
        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/QuickCombatDemo/PlayerPlaceholder.png");
        if (playerSprite == null) playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyFolder + "/Body.png");
        if (playerSprite != null) player.AddComponent<SpriteRenderer>().sprite = playerSprite;
        GameObject attack = new GameObject("AttackController"); attack.transform.SetParent(player.transform, false);
        PlayerSkillController skillController = attack.AddComponent<PlayerSkillController>();
        SerializedObject skillData = new SerializedObject(skillController);
        SerializedProperty slots = skillData.FindProperty("skillSlots"); slots.arraySize = 2;
        string[] skillNames = { "BasicSlash", "DodgeRoll" }; string[] keys = { "j", "space" };
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("skillName").stringValue = skillNames[i];
            slot.FindPropertyRelative("pcKey").stringValue = keys[i];
            slot.FindPropertyRelative("requiredUnlockSkill").stringValue = i == 1 ? "Né Tránh Nhanh" : string.Empty;
            slot.FindPropertyRelative("damage").floatValue = i == 0 ? 12f : 0f;
            slot.FindPropertyRelative("debug").boolValue = false;
        }
        skillData.ApplyModifiedPropertiesWithoutUndo();
        GameObject hitbox = new GameObject("HitBoxPlayer"); hitbox.tag = "HitBoxPlayer"; hitbox.transform.SetParent(player.transform, false);
        hitbox.AddComponent<BoxCollider2D>().isTrigger = true;
        return player;
    }

    private static void CreateWorld(Transform parent, Transform[] placeRoots, ZoneSpec zone, StoryEventDirector storyDirector)
    {
        string[] prefabIds = { "Rat", "GhostHound", "ShadowSpider", "MutatedCrow", "MirrorShade", "BloodBat", "TownUndead" };
        GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + "/WorldItem.prefab");
        for (int placeIndex = 0; placeIndex < zone.places.Length; placeIndex++)
        {
            PlaceSpec place = zone.places[placeIndex];
            string[] enemyNames = place.enemies.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int enemyIndex = 0; enemyIndex < enemyNames.Length; enemyIndex++)
            {
                string enemyId = enemyNames[enemyIndex];
                GameObject prefab = null;
                foreach (string id in prefabIds)
                    if (enemyId == id) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + "/" + id + ".prefab");
                if (prefab == null) continue;
                GameObject spawnerObject = new GameObject("SP_" + enemyId);
                spawnerObject.transform.SetParent(placeRoots[placeIndex], false);
                spawnerObject.transform.localPosition = new Vector3(-4f + enemyIndex * 4f, -1f, 0f);
                ZoneEnemySpawner spawner = spawnerObject.AddComponent<ZoneEnemySpawner>();
                SerializedObject serialized = new SerializedObject(spawner);
                serialized.FindProperty("sessionId").stringValue = zone.sceneName + ":" + place.name;
                serialized.FindProperty("enemyIdPrefix").stringValue = enemyId.ToUpperInvariant();
                serialized.FindProperty("enemyDisplayName").stringValue = GetEnemyDisplayName(enemyId, zone, place);
                serialized.FindProperty("spawnCount").intValue = enemyId == "TownUndead" ? 4 : 2;
                serialized.FindProperty("enemyPrefab").objectReferenceValue = prefab;
                serialized.FindProperty("spawnWhenPlayerNearby").boolValue = true;
                serialized.FindProperty("itemPickupPrefab").objectReferenceValue = pickup;
                serialized.FindProperty("experiencePerEnemy").intValue = GetEnemyExperience(enemyId);
                ConfigureDrops(serialized.FindProperty("drops"), enemyId, zone, place);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (string.IsNullOrWhiteSpace(place.boss)) continue;
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + "/MirrorShade.prefab");
            if (bossPrefab == null) continue;
            GameObject bossSpawn = new GameObject("SP_BOSS_" + place.boss);
            bossSpawn.transform.SetParent(placeRoots[placeIndex], false);
            bossSpawn.transform.localPosition = new Vector3(0f, -3f, 0f);
            ZoneEnemySpawner bossSpawner = bossSpawn.AddComponent<ZoneEnemySpawner>();
            SerializedObject bossData = new SerializedObject(bossSpawner);
            bossData.FindProperty("sessionId").stringValue = zone.sceneName + ":" + place.name;
            bossData.FindProperty("enemyIdPrefix").stringValue = "BOSS_" + place.name.ToUpperInvariant();
            bossData.FindProperty("enemyDisplayName").stringValue = place.boss;
            bossData.FindProperty("spawnCount").intValue = 1;
            bossData.FindProperty("enemyPrefab").objectReferenceValue = bossPrefab;
            bossData.FindProperty("itemPickupPrefab").objectReferenceValue = pickup;
            bossData.FindProperty("experiencePerEnemy").intValue = 200;
            ConfigureBossDrops(bossData.FindProperty("drops"), place.boss);
            bossData.FindProperty("spawnWhenPlayerNearby").boolValue = true;
            bossData.FindProperty("activationRadius").floatValue = 7f;
            bool drivesZoneProgress = placeIndex == zone.bossPlaceIndex;
            if (drivesZoneProgress)
            {
                bossData.FindProperty("storyEventDirector").objectReferenceValue = storyDirector;
                bossData.FindProperty("eventIdOnCleared").stringValue = zone.clearEvent;
            }
            bossData.ApplyModifiedPropertiesWithoutUndo();
            if (drivesZoneProgress)
            {
                bossSpawner.ConfigureClearEvent(storyDirector, zone.clearEvent);
                ConfigureClearEvent(storyDirector, zone);
            }
        }
        if (pickup != null)
        {
            for (int i = 0; i < zone.places.Length; i++)
            {
                string sessionPrefix = zone.sceneName + ":" + zone.places[i].name;
                CreateLoot(placeRoots[i], pickup, "Băng Đạn Cũ", sessionPrefix + ":ITEM:AMMO", new Vector3(-8f, 0f, 0f));
                CreateLoot(placeRoots[i], pickup, "Vải Băng Bó", sessionPrefix + ":ITEM:BANDAGE", new Vector3(8f, 0f, 0f));
            }
        }
    }

    private static string GetEnemyDisplayName(string enemyId, ZoneSpec zone, PlaceSpec place)
    {
        if (zone.sceneName == "Zone3")
        {
            if (place.name == "Ngôi Đền Cổ" && enemyId == "ShadowSpider") return "Rễ Cây Quỷ Ám";
            if (place.name == "Hang Động Ngầm" && enemyId == "MirrorShade") return "Tinh Thể Quỷ Ám";
            if (place.name == "Xưởng Rèn Sườn Núi") return "Quỷ Lùn Lò Rèn";
            if (place.name == "Bìa Rừng Cháy" && enemyId == "GhostHound") return "Thú Cháy Rừng";
        }
        if (zone.sceneName == "Zone4" && place.name == "Đại Sảnh Nghi Lễ" && enemyId == "TownUndead")
            return "Tay Sai Vô Diện";
        switch (enemyId)
        {
            case "BloodBat": return "Dơi Máu";
            case "TownUndead": return "Xác Sống Thị Dân";
            case "MutatedCrow": return "Quạ Xác Thối";
            case "GhostHound": return "Sói Sương Đơn Độc";
            case "ShadowSpider": return "Nhện Bóng Tối";
            case "MirrorShade": return "Bóng Gương";
            default: return enemyId;
        }
    }

    private static int GetEnemyExperience(string enemyId)
    {
        switch (enemyId)
        {
            case "TownUndead": return 15;
            case "BloodBat": return 18;
            case "MutatedCrow": return 20;
            case "GhostHound": return 24;
            case "ShadowSpider": return 25;
            case "MirrorShade": return 35;
            default: return 15;
        }
    }

    private static void ConfigureBossDrops(SerializedProperty drops, string bossName)
    {
        string[] names;
        float[] chances;
        switch (bossName)
        {
            case "Đàn Sói Sương Mù": names = new[] { "Da Sói Đầu Đàn", "Bùa Hồi Máu" }; chances = new[] { 0.50f, 0.30f }; break;
            case "Vệ Binh Đá Cổ": names = new[] { "Đá Nguyền Cổ", "Lõi Đá Cổ" }; chances = new[] { 1f, 0.35f }; break;
            case "Người Gác Hang": names = new[] { "Ngọc Bóng Tối", "Băng Đạn Bạc" }; chances = new[] { 0.40f, 0.25f }; break;
            case "Thầy Hạo - Thật Diện Mạo": names = new[] { "Lõi Vòng Lặp" }; chances = new[] { 1f }; break;
            default: names = Array.Empty<string>(); chances = Array.Empty<float>(); break;
        }
        drops.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty entry = drops.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("itemName").stringValue = names[i];
            entry.FindPropertyRelative("amount").intValue = 1;
            entry.FindPropertyRelative("dropChance").floatValue = chances[i];
        }
    }
    private static void ConfigureClearEvent(StoryEventDirector director, ZoneSpec zone)
    {
        SerializedObject serialized = new SerializedObject(director);
        SerializedProperty events = serialized.FindProperty("events");
        events.arraySize = 1;
        SerializedProperty definition = events.GetArrayElementAtIndex(0);
        definition.FindPropertyRelative("eventId").stringValue = zone.clearEvent;
        definition.FindPropertyRelative("oneShot").boolValue = true;
        SerializedProperty action = definition.FindPropertyRelative("onTriggered");
        action.FindPropertyRelative("completeSessionId").stringValue = zone.completeSession;
        action.FindPropertyRelative("nextSessionId").stringValue = zone.nextSession;
        action.FindPropertyRelative("flagsToSet").arraySize = 1;
        action.FindPropertyRelative("flagsToSet").GetArrayElementAtIndex(0).stringValue = "Session" + zone.completeSession + "Complete";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void ConfigureDrops(SerializedProperty drops, string enemy, ZoneSpec zone, PlaceSpec place)
    {
        string[] names;
        float[] chances;
        if (zone.sceneName == "Zone3" && place.name == "Xưởng Rèn Sườn Núi")
        {
            names = new[] { "Quặng Quỷ" }; chances = new[] { 0.20f };
        }
        else if (zone.sceneName == "Zone3" && place.name == "Bìa Rừng Cháy")
        {
            names = new[] { "Da Cháy Xém" }; chances = new[] { 0.25f };
        }
        else if (zone.sceneName == "Zone3" && place.name == "Hang Động Ngầm")
        {
            names = new[] { "Mảnh Tinh Thể" }; chances = new[] { 0.40f };
        }
        else
        switch (enemy)
        {
            case "BloodBat": names = new[] { "Cánh Dơi", "Máu Đóng Băng" }; chances = new[] { 0.20f, 0.15f }; break;
            case "TownUndead": names = new[] { "Vải Vụn", "Đồng Xu Cũ" }; chances = new[] { 0.50f, 0.30f }; break;
            case "MutatedCrow": names = new[] { "Lông Thối" }; chances = new[] { 0.20f }; break;
            case "GhostHound": names = new[] { "Da Sói Sương" }; chances = new[] { 0.30f }; break;
            case "ShadowSpider": names = new[] { "Rễ Cây Độc", "Nhựa Cổ Thụ" }; chances = new[] { 0.30f, 0.15f }; break;
            case "MirrorShade": names = new[] { "Mảnh Gương Vỡ" }; chances = new[] { 0.35f }; break;
            default: names = Array.Empty<string>(); chances = Array.Empty<float>(); break;
        }
        drops.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty entry = drops.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("itemName").stringValue = names[i];
            entry.FindPropertyRelative("amount").intValue = 1;
            entry.FindPropertyRelative("dropChance").floatValue = chances[i];
        }
    }
    private static void CreateLoot(Transform parent, GameObject prefab, string item, string id, Vector3 position)
    {
        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab); obj.name = "INT_" + item; obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
        WorldItemPickup pickup = obj.GetComponent<WorldItemPickup>(); if (pickup != null) pickup.Configure(id, item, 1);
    }

    private static void CreateNpc(Transform parent, string name, string location, Vector3 position)
    {
        GameObject npc = new GameObject("NPC_" + name); npc.transform.SetParent(parent, false); npc.transform.localPosition = position;
        SpriteRenderer npcRenderer = npc.AddComponent<SpriteRenderer>();
        npcRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyFolder + "/Body.png");
        npcRenderer.color = new Color(0.75f, 0.55f, 0.9f);
        CircleCollider2D collider = npc.AddComponent<CircleCollider2D>(); collider.isTrigger = true;
        GameObject dialogue = new GameObject("DLG"); dialogue.transform.SetParent(npc.transform, false);
        DemoNpcDialogueProfile profile = dialogue.AddComponent<DemoNpcDialogueProfile>();
        SerializedObject serialized = new SerializedObject(profile); serialized.FindProperty("npcName").stringValue = name;
        SerializedProperty nodes = serialized.FindProperty("dialogue"); nodes.arraySize = 1;
        nodes.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue = GetNpcDialogue(name, location);
        nodes.GetArrayElementAtIndex(0).FindPropertyRelative("nextNodeIndex").intValue = -1;
        nodes.GetArrayElementAtIndex(0).FindPropertyRelative("choices").arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateLabel(npc.transform, name, new Vector3(0f, 0.8f, 0f), 0.25f);
    }

    private static string GetNpcDialogue(string name, string location)
    {
        switch (name)
        {
            case "Én": return "Tôi sẽ canh cho tới sáng. Dù có chuyện gì, tôi cũng không mở cửa đâu.";
            case "Bà Tư": return "Cậu mà đẩy lui được lũ quái đêm nay, cả thị trấn này mang ơn cậu.";
            case "Nhi": return "Tôi không sợ anh. Tôi chỉ sợ anh tự hủy hoại mình trước khi để tôi giúp.";
            case "Bà Tư Bán Thuốc": return "Cậu mà đánh lui được lũ quái đêm nay, cả thị trấn này mang ơn cậu suốt đời.";
            case "Bóng Ma Nạn Nhân Đầu Tiên": return "Ta chỉ muốn ngủ yên... nhưng nó không cho ta ngủ...";
            case "Bé Tí": return "Chị con biến mất rồi. Chú giúp con tìm chị ấy được không?";
            case "Dân Làng (tổng hợp)": return "Cậu ấy đã cứu chúng ta...";
            case "Hồn Ma Nạn Nhân Cũ": return "Nơi này lưu giữ những người đã bị vòng lặp quên lãng.";
            case "Thầy Hạo": return "Con đang biến thành thứ mà con từng sợ hãi nhất đấy.";
            case "Lão Rèn Cốc": return "Muốn rèn vũ khí thì mang đủ vật liệu đến đây. Đừng để lời nguyền điều khiển tay con.";
            case "Ẩn Sĩ Mù": return "Ta không cần đôi mắt để nghe thấy thứ đang bám theo con.";
            case "Vô Diện": return "Ngươi nghĩ mình đã thoát khỏi ta sao? Ta chỉ đang nghỉ ngơi thôi.";
            default: return name + " đang ở " + location + ". Hãy cẩn thận, nơi này không an toàn.";
        }
    }
    private static void CreateSkillDisplay(Transform parent, ZoneSpec zone)
    {
        GameObject skills = new GameObject("GRP_Skills_" + zone.sceneName); skills.transform.SetParent(parent); skills.transform.position = new Vector3(0f, -5f, 0f);
        string[] names = { "Chém Cơ Bản", "Đỡ Đòn", "Né Tránh Nhanh", "Nộ Khí", "Kháng Choáng", "Kháng Độc/Chảy Máu" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject skill = new GameObject("SKILL_" + names[i]); skill.transform.SetParent(skills.transform);
            skill.transform.localPosition = new Vector3((i - 2.5f) * 1.5f, 0f, 0f);
            CreateLabel(skill.transform, names[i], Vector3.zero, 0.18f);
        }
    }

    private static void CreatePortals(Transform parent, ZoneSpec zone, Transform[] placeRoots)
    {
        int index = Array.IndexOf(Zones, zone);
        string[] previousScenes = { "CoreCombatDemo", "Zone2", "Zone3", "Zone4" };
        string[] previousZones = { "Đồi Cao (Am Thờ)", "Con Đường Rừng Sương", "Bìa Rừng Cháy", "Đại Sảnh Nghi Lễ" };
        Vector2[] previousPositions = { new Vector2(44f, 0f), new Vector2(36f, 0f), new Vector2(36f, 0f), new Vector2(12f, 0f) };
        Vector3 leftDoor = placeRoots[0].position + new Vector3(-10.5f, 0f, 0f);
        CreatePortal(parent, "DOOR_Back", previousScenes[index], previousZones[index], leftDoor,
            previousPositions[index], string.Empty);
        if (index < Zones.Length - 1)
        {
            string[] nextZones = { "Ngôi Đền Cổ", "Đỉnh Đồi", "Quảng Trường Hậu Chiến" };
            Vector2 nextPosition = index switch
            {
                0 => new Vector2(-36f, 0f),
                1 => new Vector2(-24f, 0f),
                _ => new Vector2(-12f, 0f)
            };
            Vector3 rightDoor = placeRoots[placeRoots.Length - 1].position + new Vector3(10.5f, 0f, 0f);
            CreatePortal(parent, "DOOR_Forward", Zones[index + 1].sceneName, nextZones[index], rightDoor,
                nextPosition, zone.requiredFlag);
        }
    }

    private static void CreatePortal(Transform parent, string name, string scene, string destinationZone,
        Vector3 position, Vector2 arrivalPosition, string flag)
    {
        GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.position = position;
        BoxCollider2D collider = obj.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(1.5f, 2f);
        FlaggedScenePortal portal = obj.AddComponent<FlaggedScenePortal>();
        SerializedObject serialized = new SerializedObject(portal); serialized.FindProperty("destinationScene").stringValue = scene;
        serialized.FindProperty("destinationZone").stringValue = destinationZone;
        serialized.FindProperty("requiredFlag").stringValue = flag;
        serialized.FindProperty("arrivalPosition").vector2Value = arrivalPosition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CreateLabel(obj.transform, scene, new Vector3(0f, 1.3f, 0f), 0.2f);
    }

    private static void CreateLabel(Transform parent, string text, Vector3 position, float size)
    {
        GameObject label = new GameObject("Label"); label.transform.SetParent(parent, false); label.transform.localPosition = position;
        TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 48; mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
        mesh.color = Color.white;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (ZoneSpec zone in Zones)
        {
            string path = SceneFolder + "/" + zone.sceneName + ".unity";
            if (!scenes.Exists(entry => entry.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/'); string current = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
    }

    private sealed class ZoneSpec
    {
        public string sceneName, displayName, requiredFlag, clearEvent, completeSession, nextSession;
        public int bossPlaceIndex;
        public PlaceSpec[] places;

        public ZoneSpec(string scene, string title, string flag, string eventId,
            string session, string next, int progressBossIndex, params PlaceSpec[] locations)
        {
            sceneName = scene;
            displayName = title;
            requiredFlag = flag;
            clearEvent = eventId;
            completeSession = session;
            nextSession = next;
            bossPlaceIndex = progressBossIndex;
            places = locations;
        }
    }

    private sealed class PlaceSpec
    {
        public string name, session, description, enemies, npcs, boss;

        public PlaceSpec(string placeName, string relatedSession, string shortDescription,
            string enemyIds, string residentNames, string bossName = "")
        {
            name = placeName;
            session = relatedSession;
            description = shortDescription;
            enemies = enemyIds;
            npcs = residentNames;
            boss = bossName;
        }
    }
}


















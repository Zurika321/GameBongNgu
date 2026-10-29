using UnityEngine;
using Unity.Cinemachine;
using System;
using UnityEngine.SceneManagement;

public class ConnectedStoryDirector : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform mapBounous;
    [SerializeField] private CinemachineConfiner2D cameraConfiner;
    [Serializable]
    private class ZoneSpawnPoint
    {
        public string currentZone;
        public Transform startTransform;
    }

    [SerializeField] private ZoneSpawnPoint[] zoneSpawnPoints = Array.Empty<ZoneSpawnPoint>();
    [SerializeField, Min(0.5f)] private float interactionRange = 2.25f;
    [SerializeField] private InventoryStorage storage;
    [SerializeField, Min(0.1f)] private float storageRange = 2.25f;
    [SerializeField, Min(0.1f), Tooltip("Refreshes cached zone colliders and dialogue NPCs at this interval.")]
    private float objectCacheRefreshInterval = 1f;
    private PolygonCollider2D[] zoneRegions = Array.Empty<PolygonCollider2D>();
    private DemoNpcDialogueProfile[] dialogueProfiles = Array.Empty<DemoNpcDialogueProfile>();
    private float nextObjectCacheRefresh;
    private DemoNpcDialogueProfile nearbyDialogue;
    private DemoNpcDialogueProfile activeDialogue;
    private PolygonCollider2D appliedPolygon;
    private PlayerProgressionService progress;
    private bool initialized;
    private bool statusPanelVisible = false;

    public bool IsInitialized => initialized;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        RefreshObjectCaches();
        nextObjectCacheRefresh = Time.unscaledTime + Mathf.Max(0.1f, objectCacheRefreshInterval);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshObjectCaches();
        nextObjectCacheRefresh = Time.unscaledTime + Mathf.Max(0.1f, objectCacheRefreshInterval);
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        RefreshObjectCaches();
        nextObjectCacheRefresh = Time.unscaledTime + Mathf.Max(0.1f, objectCacheRefreshInterval);
    }

    private void RefreshObjectCaches()
    {
        RefreshZoneRegions();
        dialogueProfiles = FindObjectsByType<DemoNpcDialogueProfile>(FindObjectsSortMode.None);
    }

    private void RefreshZoneRegions()
    {
        zoneRegions = mapBounous != null
            ? mapBounous.GetComponentsInChildren<PolygonCollider2D>(true)
            : Array.Empty<PolygonCollider2D>();
    }

    private void Awake()
    {
        if (PlayerProgressionService.ShouldDeferConnectedStoryDirector)
            enabled = false;
    }

    private void Start()
    {
        progress = PlayerProgressionService.Instance;
        if (progress == null)
        {
            Debug.LogError("[ConnectedStoryDirector] PlayerProgressionService is required.", this);
            enabled = false;
            return;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }
        if (player == null)
        {
            Debug.LogError("[ConnectedStoryDirector] Could not find the Player-tagged object.", this);
            enabled = false;
            return;
        }

        RestorePlayerAtCurrentZone();
        ApplyConfinerForCurrentZone();
        UpdateCurrentZone(true);

        initialized = true;
        // if (!progress.IsContinueRestorePending) UpdateStory();
    }

    private void Update()
    {
        if (!initialized || progress == null) return;
        if (progress.IsContinueRestorePending) return;
        if (Time.unscaledTime >= nextObjectCacheRefresh)
        {
            RefreshObjectCaches();
            nextObjectCacheRefresh = Time.unscaledTime + Mathf.Max(0.1f, objectCacheRefreshInterval);
        }
        if (!GameplayPause.IsPaused && Input.GetKeyDown(KeyCode.F1))
            statusPanelVisible = !statusPanelVisible;
        UpdateCurrentZone(true);
        UpdateDialogue();

        // UpdateStory();
        // UpdateCurrentZone();

        // Camera mainCamera = Camera.main;
        // if (mainCamera != null)
        // {
        //     Vector3 position = mainCamera.transform.position;
        //     position.x = player.position.x;
        //     position.y = player.position.y;
        //     mainCamera.transform.position = position;
        // }

        if (!GameplayPause.IsPaused && Input.GetKeyDown(KeyCode.F) && IsNearStorage())
            ToggleStorageTransfer();
    }

    private void OnGUI()
    {
        if (!initialized || GameplayPause.IsPaused || progress == null) return;

        // DrawDialogue();
        if (!statusPanelVisible) return;

        GUILayout.BeginArea(new Rect(12f, 12f, 330f, 205f), GUI.skin.box);
        GUILayout.Label("BÓNG NGỦ  |  SESSION " + progress.Data.currentSession);
        GUILayout.Label("WASD di chuyển   J tấn công   E tương tác   F1 ẩn/hiện");
        GUILayout.Label("LT " + progress.Data.lt + "   QT " + progress.Data.qt
            + "   Lv " + progress.Data.level + "   EXP " + progress.Data.exp);        
        // LevelTable levels = DataManager.GetData<LevelTable>(Tables.Level);
        // int maxHp = level != null ? level.maxHp : 60;
        // GUILayout.Label("Hp: " + progress.Data.hp + " / " + maxHp);
        // if (level != null) GUILayout.Label($"ATK {level.atk}   DEF {level.def}");
        GUILayout.Space(4f);
        GUILayout.Label("Mục tiêu: " + GetObjective());

        if (IsNearStorage())
        {
            GUILayout.Label(storage.DisplayName + " — nhấn F để cất/lấy toàn bộ đồ.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cất toàn bộ")) progress.StoreAll(storage.ContainerId);
            if (GUILayout.Button("Lấy toàn bộ")) progress.WithdrawAll(storage.ContainerId);
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Lưu tiến trình ngay")) progress.SaveManual();
        GUILayout.EndArea();
    }

    // private void UpdateStory()
    // {
    //     if (houseRats != null && houseRats.IsCleared)
    //         SetFlagOnce("Session1Ready");

    //     if (villageHound != null && villageHound.IsCleared)
    //         SetFlagOnce("Session2AReady");

    //     ChoiceRecord diaryChoice = progress.Data.choices.Find(choice => choice.sessionId == "2B");
    //     if (diaryChoice != null && !progress.Data.HasFlag("DiaryChoiceHandled"))
    //     {
    //         SetFlagOnce("DiaryChoiceHandled");
    //         if (diaryChoice.choiceId == "B1")
    //         {
    //             SetFlagOnce("DiaryRead");
    //         }
    //         else if (diaryChoice.choiceId == "B2")
    //         {
    //             SetFlagOnce("DiaryBurned");
    //             progress.RemoveItem("Nhật Ký Nạn Nhân");
    //         }

    //         if (atticSpiders != null) atticSpiders.Spawn();
    //     }

    //     if (atticSpiders != null && atticSpiders.IsCleared
    //         && progress.Data.choices.Exists(choice => choice.sessionId == "2B"))
    //     {
    //         SetFlagOnce("Session2BReady");
    //         SetFlagOnce("HouseExitOpen");
    //         SetFlagOnce("Session3Unlocked");
    //         CompleteSessionOnce("2B", "3", "Đồi Cao (Am Thờ)", "Session2BComplete");
    //     }

    //     if (hillCrows != null && hillCrows.IsCleared)
    //         SetFlagOnce("Session3Ready");
    //     if (dreamShades != null && dreamShades.IsCleared)
    //         SetFlagOnce("Session4AReady");
    //     if (warehouseBats != null && warehouseBats.IsCleared)
    //         SetFlagOnce("Session4BReady");
    //     if (plazaUndead != null && plazaCrows != null
    //         && plazaUndead.IsCleared && plazaCrows.IsCleared)
    //         SetFlagOnce("Session5Ready");

    //     ApplyChoice("1", choice =>
    //     {
    //         if (choice == "A")
    //         {
    //             SetFlagOnce("HouseExitOpen");
    //             SetFlagOnce("Session1RouteA");
    //             // CompleteSessionOnce("1", "2A", "Nhà Hoang", "Session1Complete");
    //         }
    //         else if (choice == "B")
    //         {
    //             SetFlagOnce("AtticStairsOpen");
    //             SetFlagOnce("Session1RouteB");
    //             // CompleteSessionOnce("1", "2B", "Nhà Hoang", "Session1Complete");
    //         }
    //     });

    //     ApplyChoice("2A", choice =>
    //     {
    //         SetFlagOnce("Session3Unlocked");
    //         // CompleteSessionOnce("2A", "3", "Làng Ven Sông", "Session2AComplete");
    //     });

    //     ApplyChoice("3", choice =>
    //     {
    //         if (choice == "A")
    //         {
    //             SetFlagOnce("HaoTalismanAccepted");
    //             SetFlagOnce("Session4AUnlocked");
    //             // CompleteSessionOnce("3", "4A", "Đồi Cao (Am Thờ)", "Session3Complete");
    //         }
    //         else
    //         {
    //             SetFlagOnce("HaoTalismanRefused");
    //             SetFlagOnce("Session4BUnlocked");
    //             // CompleteSessionOnce("3", "4B", "Đồi Cao (Am Thờ)", "Session3Complete");
    //         }
    //     });

    //     ApplyChoice("4A", choice =>
    //     {
    //         SetFlagOnce(choice == "A" ? "ListenedToVoDien" : "ResistedVoDien");
    //         SetFlagOnce("Session5Unlocked");
    //         // CompleteSessionOnce("4A", "5", "Quảng Trường Trung Tâm", "Session4AComplete");
    //     });

    //     ApplyChoice("4B", choice =>
    //     {
    //         SetFlagOnce(choice == "A" ? "SelfImprisoned" : "EnGuarded");
    //         if (choice == "B") SetFlagOnce("EnAtRisk");
    //         SetFlagOnce("Session5Unlocked");
    //         // CompleteSessionOnce("4B", "5", "Quảng Trường Trung Tâm", "Session4BComplete");
    //     });

    //     ApplyChoice("5", choice =>
    //     {
    //         SetFlagOnce(choice == "A" ? "DefendedTown" : "LeftTown");
    //         // CompleteSessionOnce("5", "5", "Quảng Trường Trung Tâm", "Session5Complete");
    //     });

    //     SetProfileActive(corpseProfile, houseRats != null && houseRats.IsCleared);
    //     SetProfileActive(chiefProfile, progress.Data.HasFlag("Session1RouteA")
    //         && villageHound != null && villageHound.IsCleared);
    //     SetProfileActive(haoProfile, progress.Data.HasFlag("Session3Unlocked")
    //         && hillCrows != null && hillCrows.IsCleared);
    //     SetProfileActive(shadeProfile, progress.Data.HasFlag("Session4AUnlocked")
    //         && dreamShades != null && dreamShades.IsCleared);
    //     SetProfileActive(swallowProfile, progress.Data.HasFlag("Session4BUnlocked")
    //         && warehouseBats != null && warehouseBats.IsCleared);
    //     SetProfileActive(herbalistProfile, progress.Data.HasFlag("Session5Unlocked")
    //         && progress.Data.HasFlag("Session5Ready"));
    // }

    // private void CompleteSessionOnce(string sessionId, string nextSessionId, string flag)//, string zone
    // {
    //     if (progress.Data.HasFlag(flag)) return;
    //     progress.BeginStoryEventBatch();
    //     try
    //     {
    //         progress.CompleteSession(sessionId, nextSessionId);//, zone);
    //         progress.SetFlag(flag);
    //     }
    //     finally
    //     {
    //         progress.EndStoryEventBatch();
    //     }
    // }

    // private void SetFlagOnce(string flag)
    // {
    //     if (!progress.Data.HasFlag(flag)) progress.SetFlag(flag);
    // }

    private void RestorePlayerAtCurrentZone()
    {
        // Portals/Continue restore an exact saved arrival point. Do not replace it
        // with the zone's default spawn point after the scene loads.
        if (progress.Data.HasFlag("HasSavedPlayerPosition"))
        {
            player.position = new Vector3(progress.Data.playerX, progress.Data.playerY, player.position.z);
            return;
        }

        string currentZone = progress.Data.currentZone;
        if (zoneSpawnPoints != null)
        {
            foreach (ZoneSpawnPoint spawn in zoneSpawnPoints)
            {
                if (spawn == null || spawn.startTransform == null) continue;
                if (!string.Equals(spawn.currentZone, currentZone, StringComparison.OrdinalIgnoreCase)) continue;

                player.position = spawn.startTransform.position;
                return;
            }
        }

        PolygonCollider2D region = FindRegion(currentZone);
        if (region != null)
        {
            player.position = region.transform.position;
            return;
        }

        Debug.LogWarning(
            $"[ConnectedStoryDirector] No spawn point is configured for currentZone '{currentZone}'; moving the player to (0, 0, 0).",
            this);
        player.position = Vector3.zero;
    }

    public void SetMapScene(Scene mapScene)
    {
        Transform newMap = null;
        if (mapScene.IsValid() && mapScene.isLoaded)
        {
            foreach (GameObject root in mapScene.GetRootGameObjects())
            {
                if (root.name == "MapBounus")
                {
                    newMap = root.transform;
                    break;
                }
            }
        }

        if (newMap == null)
        {
            Debug.LogError($"[ConnectedStoryDirector] MapBounus was not found in scene '{mapScene.name}'.", this);
            return;
        }

        mapBounous = newMap;
        appliedPolygon = null;
        RefreshZoneRegions();
        if (player == null || progress == null) return;
        RestorePlayerAtCurrentZone();
        ApplyConfinerForCurrentZone();
        UpdateCurrentZone(false);
    }

    private PolygonCollider2D FindRegion(string zoneName)
    {
        if (mapBounous == null || string.IsNullOrWhiteSpace(zoneName)) return null;
        if (zoneRegions == null || zoneRegions.Length == 0) RefreshZoneRegions();
        foreach (PolygonCollider2D region in zoneRegions)
        {
            if (region != null && string.Equals(region.gameObject.name, zoneName, StringComparison.OrdinalIgnoreCase))
                return region;
        }
        return null;
    }

    private void UpdateCurrentZone(bool saveOnChange)
    {
        if (player == null || mapBounous == null || progress == null || progress.Data == null) return;
        foreach (PolygonCollider2D region in zoneRegions)
        {
            if (region == null || !region.OverlapPoint(player.position)) continue;
            string zoneName = region.gameObject.name;
            if (!string.Equals(progress.Data.currentZone, zoneName, StringComparison.Ordinal))
            {
                progress.SetCurrentZone(zoneName);
                if (saveOnChange) progress.Save();
            }
            ApplyConfiner(region);
            return;
        }
    }

    private void ApplyConfiner(PolygonCollider2D polygon)
    {
        if (cameraConfiner == null || polygon == null || appliedPolygon == polygon) return;
        cameraConfiner.BoundingShape2D = polygon;
        cameraConfiner.InvalidateBoundingShapeCache();
        appliedPolygon = polygon;
    }

    private void UpdateDialogue()
    {
        if (activeDialogue != null)
        {
            if (Input.GetKeyDown(KeyCode.E)) activeDialogue = null;
            return;
        }
        nearbyDialogue = null;
        if (player == null) return;
        float best = interactionRange * interactionRange;
        foreach (DemoNpcDialogueProfile profile in dialogueProfiles)
        {
            if (profile == null) continue;
            float distance = ((Vector2)profile.transform.position - (Vector2)player.position).sqrMagnitude;
            if (distance > best) continue;
            nearbyDialogue = profile;
            best = distance;
        }
        if (nearbyDialogue != null && Input.GetKeyDown(KeyCode.E)) activeDialogue = nearbyDialogue;
    }

    // private void DrawDialogue()
    // {
    //     if (activeDialogue != null)
    //     {
    //         GUI.Box(new Rect(20f, Screen.height - 170f, Screen.width - 40f, 145f), activeDialogue.NpcName);
    //         DemoDialogueNode[] nodes = activeDialogue.Dialogue;
    //         string text = nodes != null && nodes.Length > 0 && nodes[0] != null ? nodes[0].text : "...";
    //         GUI.Label(new Rect(40f, Screen.height - 130f, Screen.width - 80f, 65f), text);
    //         if (GUI.Button(new Rect(Screen.width - 145f, Screen.height - 62f, 105f, 32f), "Đóng (E)")) activeDialogue = null;
    //     }
    //     else if (nearbyDialogue != null)
    //         GUI.Box(new Rect(Screen.width * 0.5f - 110f, Screen.height - 55f, 220f, 36f), "E — Nói chuyện: " + nearbyDialogue.NpcName);
    // }

    private void ApplyConfinerForCurrentZone()
    {
        if (mapBounous == null)
        {
            GameObject mapObject = GameObject.Find("MapBounous");
            if (mapObject != null)
            {
                mapBounous = mapObject.transform;
                RefreshZoneRegions();
            }
        }

        if (cameraConfiner == null)
            cameraConfiner = FindFirstObjectByType<CinemachineConfiner2D>(FindObjectsInactive.Include);

        if (mapBounous == null)
        {
            Debug.LogWarning("[ConnectedStoryDirector] Không tìm thấy MapBounous.", this);
            return;
        }
        if (cameraConfiner == null)
        {
            Debug.LogWarning("[ConnectedStoryDirector] Không tìm thấy CinemachineConfiner2D.", this);
            return;
        }

        string zoneName = progress.Data.currentZone;
        Transform[] zones = mapBounous.GetComponentsInChildren<Transform>(true);
        foreach (Transform zone in zones)
        {
            if (!string.Equals(zone.name, zoneName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            PolygonCollider2D polygon = zone.GetComponent<PolygonCollider2D>();
            if (polygon == null)
            {
                Debug.LogWarning($"[ConnectedStoryDirector] MapBounous/{zone.name} không có PolygonCollider2D.", zone);
                return;
            }

            cameraConfiner.BoundingShape2D = polygon;
            cameraConfiner.InvalidateBoundingShapeCache();
            return;
        }

        Debug.LogWarning($"[ConnectedStoryDirector] Không tìm thấy zone '{zoneName}' bên trong MapBounous.", mapBounous);
    }
    private string GetObjective()
    {
        if (!progress.Data.HasFlag("Session1Ready")) return "Hạ 3 chuột trong Nhà Hoang.";
        if (!progress.Data.choices.Exists(choice => choice.sessionId == "1"))
            return "Tới cái xác trong nhà và nhấn E để quyết định.";
        if (progress.Data.HasFlag("Session1RouteB")
            && !progress.Data.choices.Exists(choice => choice.sessionId == "2B"))
            return "Lên gác mái, nhặt Nhật Ký rồi chọn xem chi tiết hoặc đốt trong Túi Đồ.";
        if (progress.Data.HasFlag("Session1RouteB") && !progress.Data.HasFlag("Session2BComplete"))
            return "Đánh bại bầy nhện trên gác mái.";
        if (progress.Data.HasFlag("Session1RouteA")
            && !progress.Data.choices.Exists(choice => choice.sessionId == "2A"))
            return progress.Data.HasFlag("Session2AReady")
                ? "Nói chuyện với Trưởng Làng Tứ."
                : "Đi tới Làng Ven Sông và hạ Chó Săn Ma.";
        if (!progress.Data.HasFlag("Session3Unlocked")) return "Đi theo đường làng lên Đồi Cao, gặp Thầy Hạo.";
        if (!progress.Data.HasFlag("Session3Ready")) return "Hạ đàn Quạ Đen Dị Dạng ở Am Thờ.";
        if (!progress.Data.choices.Exists(choice => choice.sessionId == "3"))
            return "Nói chuyện với Thầy Trừ Tà Hạo.";
        if (progress.Data.HasFlag("Session4AUnlocked"))
        {
            if (!progress.Data.choices.Exists(choice => choice.sessionId == "4A"))
                return progress.Data.HasFlag("Session4AReady")
                    ? "Nói chuyện với Vô Diện trong Hành Lang Gương."
                    : "Qua cánh gương ở Am Thờ và hạ các Bóng Gương.";
        }
        if (progress.Data.HasFlag("Session4BUnlocked"))
        {
            if (!progress.Data.choices.Exists(choice => choice.sessionId == "4B"))
                return progress.Data.HasFlag("Session4BReady")
                    ? "Nói chuyện với Én ở Nhà Kho Đá."
                    : "Tới Nhà Kho Đá và hạ bầy Dơi Máu.";
        }
        if (progress.Data.HasFlag("Session5Complete")) return "Đã hoàn thành demo đến hết Session 5.";
        if (!progress.Data.HasFlag("Session5Ready")) return "Tới Quảng Trường và chống lại bầy quái.";
        return "Nói chuyện với Bà Tư Bán Thuốc để kết thúc demo ở Session 5.";
    }

    // private void UpdateCurrentZone()
    // {
    //     Vector3 position = player.position;
    //     string zone = position.x < -90f
    //         ? position.y > 30f ? "Hành Lang Gương" : position.y > 10f ? "Gác Mái Nhà Hoang" : "Nhà Hoang"
    //         : position.x < 16f ? "Rừng Thông Ven Sông"
    //         : position.x < 36f ? "Làng Ven Sông"
    //         : position.x < 58f ? "Đồi Cao (Am Thờ)"
    //         : position.x < 80f ? "Nhà Kho Đá"
    //         : "Quảng Trường Trung Tâm";
    //     if (progress.Data.currentZone == zone) return;
    //     progress.SetCurrentZone(zone);
    //     progress.Save();
    // }

    private bool IsNearStorage()
    {
        if (storage == null)
            storage = FindAnyObjectByType<InventoryStorage>();

        return player != null && storage != null
            && Vector2.Distance(player.position, storage.transform.position)
                <= Mathf.Max(storageRange, storage.InteractionRange);
    }

    private void ToggleStorageTransfer()
    {
        if (storage == null) return;
        var stored = progress.GetStoredItems(storage.ContainerId);
        if (stored.Count > 0) progress.WithdrawAll(storage.ContainerId);
        else progress.StoreAll(storage.ContainerId);
    }
}

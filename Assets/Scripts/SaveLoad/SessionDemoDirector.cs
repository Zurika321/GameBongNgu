// using System.Collections.Generic;
// using UnityEngine;

// public class SessionDemoDirector : MonoBehaviour
// {
//     [Header("Player and stage groups")]
//     [SerializeField] private Transform player;
//     [SerializeField] private Transform session1Anchor;
//     [SerializeField] private Transform session2AAnchor;
//     [SerializeField] private Transform session2BAnchor;
//     [SerializeField] private GameObject session1Area;
//     [SerializeField] private GameObject session2AArea;
//     [SerializeField] private GameObject session2BArea;

//     [Header("Enemy spawn zones")]
//     [SerializeField] private ZoneEnemySpawner session1Spawner;
//     [SerializeField] private ZoneEnemySpawner session2ASpawner;
//     [SerializeField] private ZoneEnemySpawner session2BSpawner;

//     [Header("Storage")]
//     [SerializeField] private InventoryStorage storage;
//     [SerializeField, Min(0.1f)] private float storageRange = 2.25f;

//     private PlayerProgressionService progress;
//     private SessionTable sessionTable;
//     private string activeSession = "1";
//     private bool isComplete;
//     private bool statusPanelVisible = true;

//     private void Start()
//     {
//         progress = PlayerProgressionService.Instance;
//         if (progress == null)
//         {
//             Debug.LogError("[SessionDemoDirector] PlayerProgressionService is required in the scene.", this);
//             enabled = false;
//             return;
//         }

//         if (player == null)
//         {
//             GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
//             if (playerObject != null) player = playerObject.transform;
//         }
//         if (player == null)
//         {
//             Debug.LogError("[SessionDemoDirector] Could not find the Player-tagged object.", this);
//             enabled = false;
//             return;
//         }

//         sessionTable = DataManager.GetData<SessionTable>(Tables.Session);
//         string savedSession = progress.Data.currentSession;
//         if (savedSession == "2A" || savedSession == "2B")
//         {
//             EnterSession(savedSession, false);
//         }
//         else if (savedSession == "3")
//         {
//             SetAreaActive(null);
//             activeSession = "3";
//             isComplete = true;
//         }
//         else
//         {
//             EnterSession("1", false);
//         }
//     }

//     private void Update()
//     {
//         if (player == null || progress == null) return;

//         if (!GameplayPause.IsPaused && Input.GetKeyDown(KeyCode.F1))
//             statusPanelVisible = !statusPanelVisible;

//         if (activeSession == "1" && session1Spawner != null && session1Spawner.IsCleared)
//         {
//             activeSession = "1-choice";
//             if (!progress.Data.HasFlag("Session1Ready")) progress.SetFlag("Session1Ready");
//         }
//         else if (activeSession == "2A" && session2ASpawner != null && session2ASpawner.IsCleared)
//         {
//             activeSession = "2A-choice";
//             if (!progress.Data.HasFlag("Session2AReady")) progress.SetFlag("Session2AReady");
//         }
//         else if (activeSession == "2B" && session2BSpawner != null && session2BSpawner.IsCleared
//             && progress.HasCollectedWorldItem("SESSION2B-DIARY"))
//         {
//             activeSession = "2B-choice";
//             if (!progress.Data.HasFlag("Session2BReady")) progress.SetFlag("Session2BReady");
//         }

//         ProcessDialogueChapterChoice();

//         Camera mainCamera = Camera.main;
//         if (mainCamera != null)
//         {
//             Vector3 cameraPosition = mainCamera.transform.position;
//             cameraPosition.x = player.position.x;
//             cameraPosition.y = player.position.y;
//             mainCamera.transform.position = cameraPosition;
//         }

//         if (Input.GetKeyDown(KeyCode.F) && IsNearStorage())
//             ToggleStorageTransfer();
//     }

//     private void OnGUI()
//     {
//         if (GameplayPause.IsPaused || !statusPanelVisible || progress == null || progress.Data == null) return;

//         GUILayout.BeginArea(new Rect(12f, 12f, 360f, Screen.height - 24f), GUI.skin.box);
//         GUILayout.Label("BÓNG NGỦ  |  SESSION 1–2");
//         GUILayout.Label("WASD di chuyển   J chém   Space né (mở ở Lv2)   E nhặt vật phẩm");
//         GUILayout.Label("F1 Ẩn/hiện bảng thông số");
//         GUILayout.Space(6f);

//         LevelTable levels = DataManager.GetData<LevelTable>(Tables.Level);
//         LevelData level = levels.levels != null ? levels.levels.Find(value => value.lv == progress.Data.level) : null;
//         int maxHp = level != null ? level.maxHp : 60;
//         GUILayout.Label($"Lv {progress.Data.level}   EXP {progress.Data.exp}   HP {progress.Data.hp}/{maxHp}");
//         if (level != null) GUILayout.Label($"ATK {level.atk}   DEF {level.def}");
//         GUILayout.Label($"LT {progress.Data.lt}   QT {progress.Data.qt}   Đạn hiếm {progress.Data.rareAmmo}");
//         GUILayout.Label("Kỹ năng: " + (progress.Data.unlockedSkills.Count == 0
//             ? "chưa có"
//             : string.Join(", ", progress.Data.unlockedSkills)));
//         GUILayout.Space(6f);

//         SessionData session = GetSession(activeSession);
//         if (session != null)
//         {
//             GUILayout.Label(activeSession + " — " + session.name);
//             GUILayout.Label("Nhiệm vụ: " + session.mission);
//             GUILayout.Label("Bối cảnh: " + session.scenery);
//         }
//         else if (isComplete || activeSession == "3")
//         {
//             GUILayout.Label("Đã hoàn tất demo đến hết Session 2.");
//             GUILayout.Label("Tiến trình đã lưu. Session 3 chưa nằm trong phạm vi demo.");
//         }

//         DrawStageActions();

//         if (IsNearStorage())
//         {
//             GUILayout.Space(6f);
//             GUILayout.Label(storage.DisplayName + " — nhấn F để cất/lấy toàn bộ đồ.");
//             GUILayout.BeginHorizontal();
//             if (GUILayout.Button("Cất toàn bộ")) progress.StoreAll(storage.ContainerId);
//             if (GUILayout.Button("Lấy toàn bộ")) progress.WithdrawAll(storage.ContainerId);
//             GUILayout.EndHorizontal();
//             DrawStoredItems();
//         }

//         GUILayout.Space(6f);
//         if (GUILayout.Button("Lưu tiến trình ngay")) progress.SaveManual();
//         GUILayout.EndArea();
//     }

//     private void DrawStageActions()
//     {
//         switch (activeSession)
//         {
//             case "1":
//                 GUILayout.Label(session1Spawner != null && session1Spawner.IsCleared
//                     ? "Cả ba con chuột đã bị hạ. Chọn cách xử lý dấu vết:"
//                     : "Mục tiêu: hạ 3 Chuột Cống Biến Dị trong căn nhà hoang.");
//                 break;
//             case "1-choice":
//                 GUILayout.Label("Hãy tìm Mira và nói chuyện để chọn hướng đi tiếp.");
//                 break;
//             case "2A":
//                 GUILayout.Label("Mục tiêu: hạ Chó Săn Ma ở cổng Làng Ven Sông.");
//                 break;
//             case "2A-choice":
//                 GUILayout.Label("Hãy nói chuyện với Trưởng Làng Tứ để quyết định.");
//                 break;
//             case "2B":
//                 GUILayout.Label("Mục tiêu: hạ 4 Nhện Bóng Tối và nhặt Nhật Ký Nạn Nhân.");
//                 if (session2BSpawner != null && session2BSpawner.IsCleared
//                     && !progress.HasCollectedWorldItem("SESSION2B-DIARY"))
//                     GUILayout.Label("Khu vực đã an toàn. Hãy đi tới cuốn nhật ký phát sáng.");
//                 break;
//             case "2B-choice":
//                 GUILayout.Label("Hãy tìm Mira và nói chuyện để quyết định số phận cuốn nhật ký.");
//                 break;
//             default:
//                 if (isComplete) GUILayout.Label("Đã hoàn tất demo đến hết Session 2.");
//                 break;
//         }
//     }

//     private void DrawStoredItems()
//     {
//         List<InventoryEntry> items = progress.GetStoredItems(storage.ContainerId);
//         if (items.Count == 0)
//         {
//             GUILayout.Label("Trong rương chưa có vật phẩm.");
//             return;
//         }

//         GUILayout.Label("Đang cất:");
//         foreach (InventoryEntry item in items)
//         {
//             if (item != null && item.amount > 0)
//                 GUILayout.Label($"▪ {item.itemName}  x{item.amount}");
//         }
//     }

//     private void ProcessDialogueChapterChoice()
//     {
//         string choiceId = GetRecordedChoice(activeSession == "1-choice" ? "1"
//             : activeSession == "2A-choice" ? "2A"
//             : activeSession == "2B-choice" ? "2B" : null);
//         if (string.IsNullOrEmpty(choiceId)) return;

//         if (activeSession == "1-choice")
//         {
//             if (choiceId == "A")
//             {
//                 progress.SetFlag("BodyBuried");
//                 progress.CompleteSession("1", "2A");//, "Làng Ven Sông");
//                 EnterSession("2A", true);
//             }
//             else if (choiceId == "B")
//             {
//                 progress.SetFlag("BodyInvestigated");
//                 progress.CompleteSession("1", "2B");//, "Gác Mái Nhà Hoang");
//                 EnterSession("2B", true);
//             }
//         }
//         else if (activeSession == "2A-choice" && (choiceId == "A1" || choiceId == "A2"))
//         {
//             ApplyFinalChoice("2A", choiceId);
//         }
//         else if (activeSession == "2B-choice" && (choiceId == "B1" || choiceId == "B2"))
//             ApplyFinalChoice("2B", choiceId);
//     }

//     private string GetRecordedChoice(string sessionId)
//     {
//         if (string.IsNullOrEmpty(sessionId) || progress.Data.choices == null) return null;
//         ChoiceRecord record = progress.Data.choices.Find(choice => choice.sessionId == sessionId);
//         return record != null ? record.choiceId : null;
//     }

//     private void ApplyFinalChoice(string sessionId, string choiceId)
//     {
//         if (sessionId == "2A")
//         {
//             progress.SetFlag(choiceId == "A1" ? "ToldVillageLie" : "ConfessedToVillage");
//         }
//         else if (choiceId == "B1")
//         {
//             progress.SetFlag("DiaryRead");
//         }
//         else if (choiceId == "B2")
//         {
//             progress.SetFlag("DiaryBurned");
//             progress.RemoveItem("Nhật Ký Nạn Nhân");
//         }

//         progress.CompleteSession(sessionId, "3");
//         //,sessionId == "2A" ? "Làng Ven Sông" : "Gác Mái Nhà Hoang");
//         SetAreaActive(null);
//         activeSession = "3";
//         isComplete = true;
//     }

//     private void EnterSession(string sessionId, bool movePlayer)
//     {
//         activeSession = sessionId;
//         isComplete = false;

//         GameObject area = sessionId == "1" ? session1Area : sessionId == "2A" ? session2AArea : session2BArea;
//         SetAreaActive(area);

//         Transform anchor = sessionId == "1" ? session1Anchor : sessionId == "2A" ? session2AAnchor : session2BAnchor;
//         if (movePlayer && anchor != null)
//             player.position = anchor.position + new Vector3(-2f, 0f, 0f);

//         string zoneName = sessionId == "1" ? "Nhà Hoang"
//             : sessionId == "2A" ? "Làng Ven Sông"
//             : "Gác Mái Nhà Hoang";
//         progress.SetCurrentZone(zoneName);
//         progress.Save();

//         ZoneEnemySpawner spawner = sessionId == "1" ? session1Spawner
//             : sessionId == "2A" ? session2ASpawner : session2BSpawner;
//         if (spawner != null) spawner.Spawn();
//     }

//     private void SetAreaActive(GameObject activeArea)
//     {
//         if (session1Area != null) session1Area.SetActive(session1Area == activeArea);
//         if (session2AArea != null) session2AArea.SetActive(session2AArea == activeArea);
//         if (session2BArea != null) session2BArea.SetActive(session2BArea == activeArea);
//     }

//     private SessionData GetSession(string id)
//     {
//         if (sessionTable.sessions == null) return null;
//         return sessionTable.sessions.Find(entry => entry.id == id);
//     }

//     private bool IsNearStorage()
//     {
//         return player != null && storage != null
//             && Vector2.Distance(player.position, storage.transform.position)
//                 <= Mathf.Max(storageRange, storage.InteractionRange);
//     }

//     private void ToggleStorageTransfer()
//     {
//         List<InventoryEntry> stored = progress.GetStoredItems(storage.ContainerId);
//         if (stored.Count > 0) progress.WithdrawAll(storage.ContainerId);
//         else progress.StoreAll(storage.ContainerId);
//     }
// }

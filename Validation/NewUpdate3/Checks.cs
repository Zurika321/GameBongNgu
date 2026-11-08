using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UnityEngine {
 public class Transform {public Vector2 position;}
 public struct Vector2 {public float x,y;}
 public struct Quaternion {public static Quaternion identity;}
 public class GameObject {public string name="Prefab";public Transform transform=new();public WorldItemPickup Pickup=new();public EnemySkillController Skills;public T GetComponent<T>() where T:class=>Pickup as T;public T GetComponentInChildren<T>(bool inactive) where T:class=>Skills as T;}
 public static class Debug {public static void LogWarning(string text,object source){}public static void LogError(string text,object source){}}
 public static class Random {public static float value;}
 public static class Time {public static float deltaTime,unscaledDeltaTime;}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static int FloorToInt(float a)=>(int)Math.Floor(a);public static float Repeat(float a,float b)=>a-(float)Math.Floor(a/b)*b;public static float Clamp01(float a)=>Math.Clamp(a,0,1);}
}
public class WorldItemPickup {public string Id,Name;public int Amount;public void Configure(string id,string name,int amount){Id=id;Name=name;Amount=amount;}}
public class EnemyCombatConfig {public ZoneDropDefinition[] drops=Array.Empty<ZoneDropDefinition>();}
public class EnemySkillController {public EnemyCombatConfig CombatConfig;}
public class ZoneDropDefinition {public string itemName;public int amount=1;public float dropChance;public bool onlyIfMissing;}
public class InventoryEntry {public string itemName;public int amount;}
public class StorageContainer {public List<InventoryEntry> items=new();}
public class WorldDrop {public string itemId,itemName;public int amount;}
public class Save {public List<InventoryEntry> inventory=new();public List<StorageContainer> storageContainers=new();public List<WorldDrop> worldDrops=new();public int GetItemAmount(string name)=>inventory.Where(i=>i.itemName==name).Sum(i=>i.amount);}
public class PlayerProgressionService {public Save Data=new();public HashSet<string> Collected=new();public bool HasCollectedWorldItem(string id)=>Collected.Contains(id);public void RegisterWorldDrop(string id,string name,int amount,Vector2 position)=>Data.worldDrops.Add(new(){itemId=id,itemName=name,amount=amount});}
public partial class LootHarness {
 public EnemyCombatConfig enemyCombatConfig;public GameObject itemPickupPrefab=new();public string enemyDisplayName="Actor";public Transform transform=new();public List<GameObject> Spawned=new();public int experiencePerEnemy;public int Experience()=>GetExperienceReward();
 private GameObject Instantiate(GameObject prefab,Vector2 position,Quaternion rotation,Transform parent){var go=new GameObject();Spawned.Add(go);return go;}
 private void Destroy(GameObject go)=>Spawned.Remove(go);
 public void Drop(PlayerProgressionService progress,GameObject actor=null)=>SpawnDrops(new SpawnedEnemy("stable-enemy:1",actor??new GameObject(),0),progress);
}
public static class GameplayPause {public static bool IsPaused;}
public partial class AnimationHarness {
 public class Animation {public bool loop,isCombat;public int castStartFrame;public string name="Run";}
 public class Direction {public float frameRate=10;public int frameCount=10;}
 public class Binding {public int castStartFrame=3;}
 public Animation currentAnimation=new(){loop=true};public Direction currentAnimationDirection=new();public object customFrames;public bool useUnscaledTime,isPlaying=true,completedRaised,playingSkill,holdingSkillCharge;
 public float elapsed,currentFrame;public int Sounds,Poses,Blocks,Completions;public Action<string> OnAnimationCompleted;public bool Wrapped;
 public AnimationHarness(){OnAnimationCompleted=_=>Completions++;}
 private void UpdateCustomFrames(){}private Binding FindAudioBinding(string name)=>new();private string CurrentAnimation=>currentAnimation.name;
 private void ApplyAnimationPose(float frame)=>Poses++;private void ApplyBlockStates(float frame)=>Blocks++;
 private void TriggerCombatPhaseCrossings(float previous,float next,bool wrapped)=>Wrapped=wrapped;
 private void PlayAnimationStartSound(Animation animation)=>Sounds++;
 public void Step(float delta){Time.deltaTime=Time.unscaledDeltaTime=delta;Update();}
}
class Checks {
 static int count;static void Assert(bool ok,string label){if(!ok)throw new Exception(label);count++;}
 static ZoneDropDefinition Drop(float chance=1,bool missing=false)=>new(){itemName="Stone",dropChance=chance,amount=1,onlyIfMissing=missing};
 static void Main(){
  var loop=new AnimationHarness();loop.Step(.9f);Assert(loop.Sounds==0,"no replay before loop boundary");loop.Step(.2f);Assert(loop.Sounds==1&&loop.Wrapped,"Run SFX replays at boundary");Assert(Math.Abs(loop.currentFrame-1)<.001f,"loop pose remains correct");loop.Step(1);Assert(loop.Sounds==2,"next loop replays again");
  GameplayPause.IsPaused=true;loop.Step(2);Assert(loop.Sounds==2,"pause blocks replay");GameplayPause.IsPaused=false;loop.isPlaying=false;loop.Step(2);Assert(loop.Sounds==2,"stopped animation remains silent");
  var one=new AnimationHarness();one.currentAnimation.loop=false;one.Step(1.1f);one.Step(1);Assert(one.Sounds==0&&one.Completions==1,"one-shot does not repeat SFX");
  var combat=new AnimationHarness();combat.currentAnimation.isCombat=true;combat.Step(1.1f);Assert(combat.Sounds==0&&combat.Wrapped,"combat keeps phase SFX routing");
  var skill=new AnimationHarness{playingSkill=true};skill.Step(1.1f);Assert(skill.Sounds==0&&skill.Completions==1,"skill remains one-shot even if JSON loop flag set");
  var a=new LootHarness{enemyCombatConfig=new(){drops=new[]{Drop()}}};var p=new PlayerProgressionService();UnityEngine.Random.value=.9f;a.Drop(p);Assert(a.Spawned.Count==1,"100 percent loot is guaranteed");Assert(p.Data.worldDrops.Single().itemId=="stable-enemy:1:DROP:0","save drop ID format preserved");Assert(a.Spawned.Single().Pickup.Name=="Stone","configured loot name used");
  a=new(){enemyCombatConfig=new(){drops=new[]{Drop(0)}}};a.Drop(new());Assert(a.Spawned.Count==0,"zero chance does not drop");
  a=new(){enemyCombatConfig=new(){drops=new[]{Drop()}}};UnityEngine.Random.value=1;a.Drop(new());Assert(a.Spawned.Count==1,"100 percent includes upper RNG endpoint");
  a=new(){enemyCombatConfig=new(){drops=new[]{Drop(.2f)}}};UnityEngine.Random.value=.3f;a.Drop(new());Assert(a.Spawned.Count==0,"asset probability honored instead of hardcoded item rate");UnityEngine.Random.value=.1f;a.Drop(new());Assert(a.Spawned.Count==1,"configured lower random value drops");
  a=new(){enemyCombatConfig=new(){drops=new[]{Drop(1,true)}}};p=new();p.Data.inventory.Add(new(){itemName="Stone",amount=1});a.Drop(p);Assert(a.Spawned.Count==0,"unique loot skips owned inventory item");
  p=new();p.Data.storageContainers.Add(new(){items=new(){new(){itemName="Stone",amount=1}}});a.Drop(p);Assert(a.Spawned.Count==0,"unique loot skips stored item");
  p=new();p.Data.worldDrops.Add(new(){itemId="other",itemName="Stone",amount=1});a.Drop(p);Assert(a.Spawned.Count==0,"unique loot skips uncollected drop");
  p.Collected.Add("other");a.Drop(p);Assert(a.Spawned.Count==1,"consumed/absent unique item may drop again");
  a=new(){enemyCombatConfig=new(){drops=new[]{Drop()}}};p=new();p.Collected.Add("stable-enemy:1:DROP:0");a.Drop(p);Assert(a.Spawned.Count==0,"collected stable ID does not duplicate");
  a=new();a.Drop(new(),new GameObject{Skills=new(){CombatConfig=new(){drops=new[]{Drop()}}}});Assert(a.Spawned.Count==1,"fallback uses actor combat profile");
  a=new(){enemyCombatConfig=new()};a.Drop(new());Assert(a.Spawned.Count==0,"explicit empty profile never falls back to another loot table");
  a=new(){itemPickupPrefab=null,enemyCombatConfig=new(){drops=new[]{Drop()}}};p=new();a.Drop(p);Assert(p.Data.worldDrops.Count==0,"missing pickup does not register phantom loot");
  a=new(){experiencePerEnemy=123,enemyDisplayName="Chuột Cống Biến Dị"};Assert(a.Experience()==123,"Inspector reward overrides former name table");a.experiencePerEnemy=-2;Assert(a.Experience()==0,"negative EXP clamped");
  Console.WriteLine($"PASS: {count} production Update/loot method checks with API doubles; not Unity Play Mode.");
 }
}

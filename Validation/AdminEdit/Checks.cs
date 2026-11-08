using System;
using System.Linq;
using System.Collections.Generic;
namespace UnityEngine {public enum FindObjectsInactive{Exclude}public enum FindObjectsSortMode{None}public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);}}
namespace UnityEngine.UI {public class GraphicRaycaster {public bool enabled=true;}}
namespace UnityEngine {
 public static class GUI {public static string Focus;public static void SetNextControlName(string key){}public static string GetNameOfFocusedControl()=>Focus;}
 public static class GUILayout {public static bool Edited;public static string Input;public static void BeginHorizontal(){}public static void EndHorizontal(){}public static void Label(string text,object option){}public static object Width(float value)=>null;public static object MinWidth(float value)=>null;public static string TextField(string current,object option){UnityEditor.EditorGUI.Changed=Edited;return Input??current;}}
}
namespace UnityEditor {public static class EditorGUI {public static bool Changed;public static void BeginChangeCheck()=>Changed=false;public static bool EndChangeCheck()=>Changed;}}
public static class GameplayPause {public static bool IsPaused;public static int Pauses,Resumes;public static void Pause(){IsPaused=true;Pauses++;}public static void Resume(){IsPaused=false;Resumes++;}}
public partial class AdminHarness {
 public bool visible,ownsPause,freezeGameWhenOpen=true;public static bool AnyOpen;public Dictionary<string,string> buffers=new();public List<UnityEngine.UI.GraphicRaycaster> blockedRaycasters=new();public UnityEngine.UI.GraphicRaycaster[] Raycasters=Array.Empty<UnityEngine.UI.GraphicRaycaster>();
 private T[] FindObjectsByType<T>(UnityEngine.FindObjectsInactive a,UnityEngine.FindObjectsSortMode b)=>Raycasters.OfType<T>().ToArray();private void Refresh(){}
 public void Open()=>OpenMenu();public void Close()=>CloseMenu();public static bool Parse(string text,out float result)=>TryParseFloat(text,out result);
 private bool lastTextEdited;
 public int EditInt(int value)=>IntRow("Number","number",value);public float EditFloat(float value)=>FloatRow("Number","number",value);public bool EditVector(ref float value)=>VecComponent("number",ref value);
}
public class Save {public int level=1,exp,hp;public List<string> unlockedSkills=new();public List<Equipped> equippedSkills=new();}
public class Equipped {public string skillName;}
public class SkillConfigEntry {public string skillId,name;}
public class SkillConfig {public List<SkillConfigEntry> Skills=new();}
public class LevelData {public int lv,expRequired;}
public class LevelTable {public List<LevelData> levels=new();}
public class HealthController {public float Max,Current;public void ConfigureHealth(float max,float hp){Max=max;Current=hp;}}
public partial class PlayerProgressionService {
 public Save Data=new();public SkillConfig Config=new(){Skills=new(){new(){skillId="Active",name="Active display"},new(){skillId="Passive",name="Passive display"}}};public LevelTable Levels=new(){levels=new(){new(){lv=1,expRequired=0},new(){lv=2,expRequired=123}}};public object player=new();public HealthController playerHealth=new();public int skillStateVersion,Saves,Syncs;public event Action<string> SkillUnlocked;public event Action StatsChanged;
 private LevelTable GetLevelTable()=>Levels;private LevelData FindLevel(LevelTable table,int level)=>table.levels.FirstOrDefault(x=>x.lv==level);
 private SkillConfig ResolveSkillConfig()=>Config;public SkillConfigEntry ResolveSkillEntry(string name)=>Config.Skills.FirstOrDefault(x=>string.Equals(x.skillId,name,StringComparison.OrdinalIgnoreCase)||string.Equals(x.name,name,StringComparison.OrdinalIgnoreCase));public string ResolveSkillId(string name)=>ResolveSkillEntry(name)?.skillId??name;
 private bool HasUnlockedSkill(string skill)=>Data.unlockedSkills?.Any(id=>ResolveSkillId(id)==ResolveSkillId(skill))??false;
 private void UnlockSkillsForLevel(int level,bool notify){if(level==2&&!HasUnlockedSkill("Active"))ChangeDebugSkill("Active",true);}
 private void SyncPlayerSkillControllers()=>Syncs++;private void FindPlayer()=>player=new();private int GetMaxHealth()=>Data.level*10;private void Save()=>Saves++;
}
class Checks {
 static int count;static void Check(bool yes,string label){if(!yes)throw new Exception(label);count++;}
 static void Main(){
  var p=new PlayerProgressionService();int notified=0,stats=0;p.SkillUnlocked+=_=>notified++;p.StatsChanged+=()=>stats++;
  Check(p.DebugSetSkillUnlocked("Active display",true)&&p.Data.unlockedSkills.SequenceEqual(new[]{"Active"}),"display name resolves to canonical skill ID");
  Check(p.Syncs==1&&p.Saves==1&&notified==1&&p.skillStateVersion==1,"unlock updates controller/version/event/save");
  p.DebugSetSkillUnlocked("Active",true);Check(p.Data.unlockedSkills.Count==1&&notified==1,"duplicate unlock not added");
  p.Data.equippedSkills.Add(new(){skillName="Active"});p.DebugSetSkillUnlocked("Active",false);Check(p.Data.unlockedSkills.Count==0&&p.Data.equippedSkills.Count==0,"locking equipped skill removes loadout entry");
  int saves=p.Saves;Check(!p.DebugSetSkillUnlocked("obsolete-name",true)&&p.Saves==saves,"unknown names never enter new save");
  p.DebugSetAllSkillsUnlocked(true);Check(p.Data.unlockedSkills.Count==2&&p.Data.unlockedSkills.Contains("Passive"),"unlock all reads current SkillConfig");
  p.Data.unlockedSkills.Add("legacy-unknown");p.Data.equippedSkills.Add(new(){skillName="Active"});p.DebugSetAllSkillsUnlocked(false);Check(p.Data.unlockedSkills.Count==0&&p.Data.equippedSkills.Count==0,"lock all clears unlocked/loadout including old unknown entries");
  Check(p.DebugSetLevel(2)&&p.Data.level==2&&p.Data.exp==123&&p.Data.unlockedSkills.Contains("Active"),"set level uses configured EXP/unlocks");
  Check(p.Data.hp==20&&p.playerHealth.Current==20&&p.playerHealth.Max==20&&stats==1,"set level refreshes health/stats");
  saves=p.Saves;Check(!p.DebugSetLevel(20)&&p.Data.level==2&&p.Saves==saves,"missing level does not corrupt save");
  p.Data=null;Check(!p.DebugSetLevel(1)&&!p.DebugSetSkillUnlocked("Active",true),"unready save safely rejected");p.DebugSetAllSkillsUnlocked(true);
  GameplayPause.IsPaused=false;GameplayPause.Pauses=GameplayPause.Resumes=0;
  var on=new UnityEngine.UI.GraphicRaycaster();var off=new UnityEngine.UI.GraphicRaycaster{enabled=false};var menu=new AdminHarness{Raycasters=new[]{on,off}};menu.buffers["stale"]="old";menu.Open();
  Check(menu.visible&&AdminHarness.AnyOpen&&menu.buffers.Count==0,"opening shows menu and clears stale edit buffers");
  Check(GameplayPause.IsPaused&&menu.ownsPause&&!on.enabled&&!off.enabled,"admin acquires pause and blocks mouse click-through");
  menu.Close();Check(!GameplayPause.IsPaused&&GameplayPause.Resumes==1&&on.enabled&&!off.enabled&&!AdminHarness.AnyOpen,"closing restores only previously enabled UI and owned pause");
  menu.Close();Check(GameplayPause.Resumes==1,"close is idempotent");
  GameplayPause.IsPaused=true;menu.Open();Check(!menu.ownsPause,"existing inventory pause is not acquired by admin");menu.Close();Check(GameplayPause.IsPaused&&GameplayPause.Resumes==1,"existing pause preserved on close");
  GameplayPause.IsPaused=false;menu.freezeGameWhenOpen=false;menu.Open();Check(!GameplayPause.IsPaused&&!on.enabled,"nonfreeze mode still blocks UI click-through");menu.Close();
  Check(AdminHarness.Parse("1,5",out var number)&&number==1.5f,"decimal comma remains supported");
  Check(!AdminHarness.Parse("NaN",out _)&&!AdminHarness.Parse("Infinity",out _)&&!AdminHarness.Parse("broken",out _),"invalid/nonfinite debug values rejected");
  UnityEngine.GUI.Focus="number";menu.buffers["number"]="10";UnityEngine.GUILayout.Edited=false;UnityEngine.GUILayout.Input=null;
  Check(menu.EditInt(123)==123,"repaint cannot restore stale EXP over a command result");Check(menu.EditFloat(25)==25,"repaint cannot restore stale HP over heal result");
  UnityEngine.GUILayout.Edited=true;UnityEngine.GUILayout.Input="0";Check(menu.EditInt(123)==0,"intentional zero edit applies");UnityEngine.GUILayout.Input="bad";Check(menu.EditInt(123)==123,"invalid integer does not change data");
  UnityEngine.GUILayout.Input="NaN";Check(menu.EditFloat(25)==25,"nonfinite field edit rejected");UnityEngine.GUILayout.Edited=false;float component=9;Check(!menu.EditVector(ref component)&&component==9,"unchanged vector field does not overwrite runtime value");
  Console.WriteLine($"PASS: {count} production AdminEdit command/pause checks with API doubles; not Unity Play Mode.");
 }
}

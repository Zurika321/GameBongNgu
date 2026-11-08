using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
class Checks {
 static int count; static void Assert(bool ok,string name){if(!ok)throw new Exception(name);count++;}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static void Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static (GameObject go,EnemySkillController cast,EnemySkillScheduler ai,EnemyController movement) Actor(EnemySkillSlot[] slots,SkillConfig cfg=null,EnemyCombatConfig profile=null){
  Time.time=100;GameplayPause.IsPaused=false;SkillActions.Instance=new();
  var go=new GameObject();var h=go.Add<HealthController>();var move=go.Add<EnemyController>();go.Add<RuntimeStatModifierStore>();go.Add<CharacterAnimation>();var cast=go.Add<EnemySkillController>();var ai=go.Add<EnemySkillScheduler>();
  Set(cast,"skillSlots",slots);if(cfg!=null)Set(cast,"skillConfig",cfg);if(profile!=null)Set(cast,"combatConfig",profile);
  Call(cast,"Awake");Call(ai,"Awake");Call(ai,"Start");return(go,cast,ai,move);
 }
 static GameObject Target(float distance=1){var t=new GameObject();t.transform.position=new Vector3(distance,0);t.Add<HealthController>();return t;}
 static EnemySkillSlot Slot(string action="ACT_HIT",int priority=0)=>new(){skillName=action,priority=priority,castLockSeconds=0,cooldown=2,detectionSize=new Vector2(2,2)};
 static void Main(){
  var a=Actor(new[]{Slot()});var target=Target();
  Assert(a.cast.TryCast(0,target),"legacy cast");Assert(SkillActions.Instance.casts.Single().ActionId=="ACT_HIT","action ID routed");
  Assert(!a.cast.TryCast(0,target),"cooldown prevents duplicate");Time.time+=2;Assert(a.cast.TryCast(0,target),"cooldown expires");
  a.go.GetComponent<RuntimeStatModifierStore>().speed=2;Time.time+=1;Assert(a.cast.TryCast(0,target),"attack speed changes cooldown");
  GameplayPause.IsPaused=true;Time.time+=10;Assert(!a.cast.TryCast(0,target),"pause blocks cast");GameplayPause.IsPaused=false;
  a.go.GetComponent<HealthController>().CurrentHealth=0;Assert(!a.cast.TryCast(0,target),"dead enemy cannot attack");
  var cfg=new SkillConfig();cfg.entries["unique"]=new(){skillId="unique",actionId="SHARED",context=new(){damageMultiplier=1.5f,cooldown=4}};
  var profile=new EnemyCombatConfig{maxHealth=150,attack=0,needsAttack=true,skills=new[]{new EnemySkillSlot{skillId="unique",castLockSeconds=0}}};
  a=Actor(null,cfg,profile);Assert(a.go.GetComponent<HealthController>().MaxHealth==150,"configured health initialized");
  Assert(!a.cast.TryCast(0,target),"unknown boss ATK blocked");profile.attack=12;Assert(a.cast.TryCast(0,target),"assigned boss ATK accepted");
  Assert(SkillActions.Instance.casts.Single().Damage==18,"base ATK times skill multiplier");Assert(SkillActions.Instance.casts.Single().SkillId=="unique"&&a.go.GetComponent<CharacterAnimation>().Last=="unique","skill ID used for animation");
  cfg.entries["unique"].context.animationName=" ClawCast "; Time.time+=4; Assert(a.cast.TryCast(0,target) && a.go.GetComponent<CharacterAnimation>().Last=="ClawCast" && SkillActions.Instance.casts.Last().SkillId=="unique", "custom visual name preserves gameplay skill ID");
  Assert(profile.skills[0].used==false&&a.cast.Slots[0].used,"runtime slots cloned per instance");
  a.cast.InheritAttack(20);Time.time+=4;Assert(a.cast.TryCast(0,target)&&SkillActions.Instance.casts.Last().Damage==30,"summon inherits owner ATK");
  a.cast.Slots[0].oncePerCombat=true;Time.time+=4;Assert(!a.cast.TryCast(0,target),"once per combat enforced");a.cast.BeginCombat();Assert(a.cast.TryCast(0,target),"new combat resets once marker");
  var low=Slot("BASIC");var special=Slot("SPECIAL",10);special.initialDelay=.2f;
  a=Actor(new[]{low,special});a.movement.Target=target.transform;
  Call(a.ai,"Update");Assert(SkillActions.Instance.casts.Last().ActionId=="BASIC","initial delay respected");
  Call(a.ai,"Update");Assert(SkillActions.Instance.casts.Last().ActionId=="SPECIAL","special priority");
  a.movement.Target=null;Call(a.ai,"Update");Assert(SkillActions.Instance.cancelled>=2,"leaving combat cancels actions");
  a=Actor(new[]{Slot()});a.movement.Target=Target(3).transform;Call(a.ai,"Update");Assert(SkillActions.Instance.casts.Count==0,"outside range ignored");
  a.cast.Slots[0].minimumDistance=2;a.movement.Target=target.transform;Call(a.ai,"Update");Assert(SkillActions.Instance.casts.Count==0,"projectile minimum range respected");
  a=Actor(new[]{Slot()});a.cast.Slots[0].phases=new[]{2};Assert(!a.cast.TryCast(0,target),"phase-specific cast gated");a.cast.SetPhase(2);Assert(a.cast.TryCast(0,target),"phase enables cast");
  a=Actor(new[]{Slot()});a.cast.Slots[0].needsInspectorSetup=true;Assert(!a.cast.TryCast(0,target),"unconfigured slot blocked");
  a=Actor(new[]{Slot()});a.cast.Slots[0].castLockSeconds=.2f;Assert(a.cast.TryCast(0,target)&&a.movement.IsActionLocked,"cast locks movement");Call(a.cast,"Update");Call(a.cast,"Update");Assert(!a.movement.IsActionLocked,"cast movement lock releases");
  a=Actor(new[]{Slot()});a.cast.TryCast(0,target);Time.time+=.5f;var save=new PlayerSaveData();a.cast.CaptureCooldowns(save,"ENEMY_SAVE");Assert(save.skillCooldowns.Single().skillId=="ACT_HIT"&&save.skillCooldowns.Single().remainingSeconds==1.5f,"legacy cooldown save key preserved");
  a.cast.RestoreCooldowns(save,"ENEMY_SAVE");Assert(!a.cast.TryCast(0,target),"restored cooldown enforced");Time.time+=1.5f;Assert(a.cast.TryCast(0,target),"restored cooldown expires");
  a=Actor(new[]{Slot()});var phase=a.go.Add<EnemyPhaseController>();Call(phase,"Awake");Set(a.cast,"combatConfig",new EnemyCombatConfig{phases=new[]{new EnemyPhaseRule{phase=2,healthBelow=.4f},new EnemyPhaseRule{phase=3,healthBelow=.25f,manualOnly=true}}});
  a.go.GetComponent<HealthController>().CurrentHealth=39;Call(phase,"Update");Assert(a.cast.Phase==2,"known HP threshold transitions");a.go.GetComponent<HealthController>().CurrentHealth=10;Call(phase,"Update");Assert(a.cast.Phase==2,"undefined QT gate stays manual");phase.SetPhase(3);Assert(a.cast.Phase==3,"story event can enter manual phase");
  a=Actor(new[]{Slot()});a.cast.Slots[0].skipWhileActionRunning=true;SkillActions.Instance.running=true;
  Assert(!a.cast.TryCast(0,target),"persistent buff not restarted every frame");SkillActions.Instance.running=false;
  Assert(a.cast.TryCast(0,target),"conditional buff may start again after it ends");
  var legacy=Slot();legacy.downFrames=new[]{new Sprite()};a=Actor(new[]{legacy});a.cast.TryCast(0,target);
  Assert(ReferenceEquals(SkillActions.Instance.casts.Last().downFrames,legacy.downFrames),"legacy directional frames preserved");
  Console.WriteLine($"PASS: {count} controller/scheduler/phase checks using production source (Unity API test doubles; no Play Mode claim).");
 }
}

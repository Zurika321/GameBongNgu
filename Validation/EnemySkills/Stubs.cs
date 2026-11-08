using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace UnityEngine {
 public class Object {public string name="stub";}
 public class ScriptableObject:Object {}
 public class Component:Object {
  public GameObject gameObject; public Transform transform=>gameObject.transform;
  public T GetComponent<T>()=>gameObject.GetComponent<T>();
  public T GetComponentInParent<T>()=>gameObject.GetComponentInParent<T>();
  public T GetComponentInChildren<T>()=>gameObject.GetComponent<T>();
 }
 public class MonoBehaviour:Component {public bool enabled=true; public void StopAllCoroutines(){} }
 public class Transform:Component {public Vector3 position;}
 public class GameObject:Object {
  public Transform transform; public GameObject parent; readonly List<Component> components=new();
  public GameObject(){transform=new Transform{gameObject=this};}
  public T Add<T>() where T:Component,new(){var x=new T{gameObject=this};components.Add(x);return x;}
  public T GetComponent<T>()=>components.OfType<T>().FirstOrDefault();
  public T GetComponentInChildren<T>()=>GetComponent<T>();
  public T GetComponentInParent<T>() {var c=GetComponent<T>();return c is not null?c:parent!=null?parent.GetComponentInParent<T>():default;}
 }
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static implicit operator Vector3(Vector2 a)=>new(a.x,a.y);}
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero=>new(0,0);public static Vector2 right=>new(1,0);
  public float sqrMagnitude=>x*x+y*y;public float magnitude=>MathF.Sqrt(sqrMagnitude);
  public void Normalize(){float n=magnitude;if(n>0){x/=n;y/=n;}}
  public static Vector2 operator+(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);
  public static implicit operator Vector2(Vector3 a)=>new(a.x,a.y);
 }
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float n,float a,float b)=>Math.Clamp(n,a,b);public static float Abs(float n)=>Math.Abs(n);}
 public static class Time {public static float time,deltaTime=.1f;}
 public static class Debug {public static void LogError(object x,object y=null){}public static void LogWarning(object x,object y=null){} }
 public class Collider2D:Component {public bool CompareTag(string tag)=>true;}
 public static class Physics2D {public static Collider2D[] OverlapCircleAll(Vector2 a,float b)=>Array.Empty<Collider2D>();public static Collider2D[] OverlapBoxAll(Vector2 a,Vector2 b,float c)=>Array.Empty<Collider2D>();}
 public class Sprite:Object{}
 public static class JsonUtility {static readonly JsonSerializerOptions Options=new(){IncludeFields=true};public static string ToJson(object o)=>JsonSerializer.Serialize(o,o.GetType(),Options);public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,Options);}
 public class SerializeField:Attribute{} public class Tooltip:Attribute{public Tooltip(string s){}} public class Header:Attribute{public Header(string s){}} public class Min:Attribute{public Min(float f){}}public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}public class TextArea:Attribute{}public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int i){}}public class CreateAssetMenu:Attribute{public string menuName;}
}
namespace UnityEngine.Serialization {public class FormerlySerializedAs:Attribute{public FormerlySerializedAs(string s){}}}
namespace UnityEngine.Events {public class UnityEvent<T>{public void Invoke(T t){}}}
public class RegistryId:Attribute{public RegistryId(RegistryIdType t){}}public enum RegistryIdType{Skill}
public interface IHealth {float CurrentHealth{get;}float MaxHealth{get;}bool IsDead{get;}}
public class HealthController:UnityEngine.MonoBehaviour,IHealth {
 public float CurrentHealth{get;set;}=100;public float MaxHealth{get;set;}=100;public bool IsDead=>CurrentHealth<=0;public void ConfigureHealth(float a,float b){MaxHealth=a;CurrentHealth=b;}public void SetInvincible(bool v){}
}
public class EnemyController:UnityEngine.MonoBehaviour {public UnityEngine.Transform Target;public bool IsActionLocked;public UnityEngine.Vector2 FacingDirection=>UnityEngine.Vector2.right;public void LockMovement(bool b)=>IsActionLocked=b;}
public enum PlayerStatType{Attack,AttackSpeed}
public class RuntimeStatModifierStore:UnityEngine.MonoBehaviour {public float speed=1,attack=1;public float GetFinalValue(PlayerStatType t,float x)=>t==PlayerStatType.AttackSpeed?speed:x*attack;}
public class CharacterAnimation:UnityEngine.MonoBehaviour {public string Last;public void CancelSkillAnimation(){}public void SetFacing(UnityEngine.Vector2 d){}public bool PlayAnimationSkill(string id){Last=id;return true;}public void BeginSkillCast(){} }
public class SkillContextConfig {public string animationName;public float cooldown,damageMultiplier=1,range,speed,duration,stunTime,chargeTime,endTime;public bool debug,canPierce,breaksGuard,bypassesMeleeResistance;public UnityEngine.Vector2 skillSize;public SkillModuleConfig modules;public SkillEffectAnimation effectAnimation;public DameDotConfig dotConfig;public object actionDamageOverrides;}
public class SkillConfigEntry {public string skillId,actionId;public bool needsInspectorSetup;public SkillContextConfig context=new();}
public class SkillConfig {public Dictionary<string,SkillConfigEntry> entries=new();public SkillConfigEntry FindEntry(string id)=>id!=null&&entries.TryGetValue(id,out var e)?e:null;public SkillContextConfig GetContextConfig(string id,string weapon)=>FindEntry(id)?.context;}
public class SkillModuleConfig{}public class SkillEffectAnimation{}public class DameDotConfig{}
public class ConditionConfig {public bool result=true;}
public static class ConditionGate {public static bool Evaluate(SkillContext c,ConditionConfig cfg)=>cfg?.result??false;}
public enum SkillCasterType{Player,Enemy}
public class SkillContext {public UnityEngine.Sprite[] downFrames,upFrames,rightFrames,leftFrames;public UnityEngine.GameObject Caster,Target;public SkillCasterType CasterType;public string SkillId,ActionId,AnimationName;public UnityEngine.Vector2 Position,Direction,TargetPosition,Size;public float Damage,Range,Speed,Duration,StunTime,ChargeTime,EndTime;public bool Debug,CanPierce,BreaksGuard,BypassesMeleeResistance;public DameDotConfig DotConfig;public SkillModuleConfig Modules;public SkillEffectAnimation EffectAnimation;public object ActionDamageOverrides;}
public class SkillActions {public static SkillActions Instance=new();public List<SkillContext> casts=new();public int cancelled;public bool running;public bool IsActionRunning(string action,UnityEngine.GameObject caster)=>running;public bool HasRegisteredSkill(string id)=>!string.IsNullOrWhiteSpace(id);public void Activate(string a,SkillContext c)=>casts.Add(c);public void RemoveActionBuffs(string a,UnityEngine.GameObject o)=>cancelled++;}
public static class GameplayPause {public static bool IsPaused;}
public class SkillCooldownSnapshot {public string ownerId,skillId;public float remainingSeconds;}
public class PlayerSaveData {public List<SkillCooldownSnapshot> skillCooldowns;public bool HasFlag(string s)=>false;}
public class PlayerProgressionService {public static PlayerProgressionService Instance;public PlayerSaveData Data;}

public class ZoneDropDefinition {}

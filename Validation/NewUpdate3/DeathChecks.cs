using System;
using System.Linq;
public enum AnimState { Idle, Run, Attack, Skill, Hurt, Die, Stun }
public partial class AnimationHarness {
 public class Data {public Animation[] animations;public string modelName="Fixture";}
 public class Rig {public string name="down";}
 public Data data;public object rigRoot=new();public Rig currentRigData=new();public string currentDirection="down";public bool startingSkill;
 public AnimState CurrentState=>CurrentAnimation=="Death"?AnimState.Die:Enum.TryParse(CurrentAnimation,out AnimState state)?state:AnimState.Idle;
 private Animation FindAnimation(string name)=>data.animations.FirstOrDefault(a=>a.name==name);
 private Direction FindAnimationDirection(Animation animation,string direction)=>new();
 private bool TryLoadJson(out Data result,out string error){result=null;error="not loaded";return false;}
 private void Warn(string id,string message){}private string GetAnimationNames()=>string.Join(",",data.animations.Select(a=>a.name));private void BuildDirectionRig(string direction){}
}
class DeathChecks {
 static int count;static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
 static AnimationHarness Actor(string death="Die")=>new(){currentAnimation=null,isPlaying=false,data=new(){animations=new[]{new AnimationHarness.Animation{name=death},new AnimationHarness.Animation{name="Idle",loop=true},new AnimationHarness.Animation{name="Hurt"}}}};
 static void Main(){
  GameplayPause.IsPaused=false;
  var a=Actor();Check(a.Play("Die"),"death starts");a.Step(1.1f);Check(a.completedRaised&&!a.isPlaying&&a.currentFrame==10,"death completes at final frame");int sound=a.Sounds;
  for(int i=0;i<60;i++)a.Play("Die");Check(a.currentFrame==10&&!a.isPlaying&&a.Sounds==sound,"repeated controller requests preserve final pose and sound count");
  Check(a.Play("Die",true)&&a.currentFrame==0&&a.isPlaying&&a.Sounds==sound+1,"explicit restart remains supported");
  a.Step(1.1f);a.Play("Idle");Check(a.currentAnimation.name=="Idle"&&a.isPlaying,"revival can return to Idle");a.Play("Die");Check(a.currentFrame==0&&a.currentAnimation.name=="Die","subsequent death starts normally");
  a=Actor("Death");a.Play("Death");a.Step(1.1f);a.Play("Death");Check(a.completedRaised&&a.currentFrame==10&&!a.isPlaying,"legacy Death alias also holds final pose");
  a=Actor();a.Play("Hurt");a.Step(1.1f);a.Play("Hurt");Check(a.currentFrame==0&&a.isPlaying,"other completed animations retain existing replay behavior");
  a=Actor();a.Play("Idle");a.Step(.2f);a.Play("Idle");Check(Math.Abs(a.currentFrame-2)<.001f,"loop requests do not reset animation");
  Console.WriteLine($"PASS: {count} production Play/Update death regression checks with API doubles; not Unity Play Mode.");
 }
}

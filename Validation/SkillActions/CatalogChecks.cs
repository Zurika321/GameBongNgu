using System; using System.IO; using System.Linq; using System.Text.Json;
class Checks {
static int count;
static void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
static void Main(){
var catalog=JsonSerializer.Deserialize<SkillActionCatalog>(File.ReadAllText("Validation/SkillActions/parsed-catalog.json"),new JsonSerializerOptions{IncludeFields=true});
Check(catalog.Validate().Count==0,"all 43 recipes validate");Check(catalog.actions.Count==43,"43 action IDs");Check(catalog.actions.Sum(a=>a.steps.Count)==66,"66 steps");
var combo=catalog.Find("act_melee_2");Check(combo!=null,"case insensitive lookup");Check(Math.Abs(SkillActionMath.ScheduledTime(combo.steps[0],1)-0.55f)<0.00001,"two-hit timing");Check(SkillActionMath.Damage(100,combo.steps[0],0)==60,"60 percent damage");Check(SkillActionMath.Damage(100,combo.steps[0],99)==60,"last configured percent clamps");
var pulses=catalog.Find("ACT_AOE_PULSE_3X").steps[0];Check(pulses.repeatCount==3&&SkillActionMath.ScheduledTime(pulses,2)==2.5f,"three pulses end at 2.5 seconds");Check(SkillActionMath.Damage(100,pulses,2)==120,"final pulse damage");
var explode=catalog.Find("ACT_SELF_EXPLODE");Check(explode.allowDeadCaster&&SkillActionMath.Damage(0,explode.steps[1],0)==20,"fixed explosion damage with zero ATK");
Check(catalog.Find("ACT_PROJ_EXPLOSIVE").steps[1].trigger==ActionTrigger.ProjectileEnd,"explosion waits for projectile completion");Check(catalog.Find("ACT_PARRY_COUNTER").steps[1].trigger==ActionTrigger.Defended,"counter waits for successful defense");Check(catalog.Find("ACT_CURSE_TRANSFER").steps[1].trigger==ActionTrigger.ChallengeFailed,"curse waits for failed challenge");
var poison=catalog.Find("ACT_MELEE_1_POISON").steps[1];Check(poison.trigger==ActionTrigger.Hit&&Math.Abs(poison.chance-0.3f)<0.00001,"poison is a 30 percent on-hit effect");
var channel=catalog.Find("ACT_HEAL_LT_CHANNEL");Check(channel.requiresHold&&channel.cancelOnMove&&channel.cancelOnDamage&&channel.steps[0].repeatCount==0,"channel release/movement/damage cancellation configured");
var aura=catalog.Find("ACT_AURA_SLOW_CONTACT").steps[0];Check(aura.followCaster&&aura.lifetime==0,"infinite following aura configured");
var five=catalog.Find("ACT_MELEE_5_STUN_FINISH");Check(five.steps[1].requirePreviousHit&&five.steps[2].sourceIteration==4,"missed combo cancels; stun only final hit");
Check(catalog.Find("ACT_CONE_SWEEP_STUN").steps[1].shape==HitboxShape.Cone,"cone geometry configured");Check(Math.Abs(catalog.Find("ACT_MELEE_3").steps[0].sizeGrowthPerRepeat-0.15f)<0.0001,"combo hitbox growth configured");
catalog.actions.Add(new SkillActionDefinition{actionId=combo.actionId.ToLowerInvariant(),steps=combo.steps});Check(catalog.Validate().Any(e=>e.Contains("Duplicate")),"duplicate IDs rejected");catalog.actions.RemoveAt(catalog.actions.Count-1);
float interval=pulses.interval;pulses.interval=0;Check(catalog.Validate().Count>0,"zero repeat interval rejected");pulses.interval=interval;
float delay=poison.delay;poison.delay=float.NaN;Check(catalog.Validate().Count>0,"nonfinite timing rejected");poison.delay=delay;
int source=poison.sourceStep;poison.sourceStep=1;Check(catalog.Validate().Count>0,"self event dependency rejected");poison.sourceStep=source;
Check(catalog.Validate().Count==0,"catalog restored valid after negative cases");Console.WriteLine(count+" catalog/timing checks passed; no Unity engine simulation.");
}}

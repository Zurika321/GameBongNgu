using System;
namespace UnityEngine {
public class ScriptableObject {} public class CreateAssetMenuAttribute:Attribute { public string fileName,menuName; }
public class TooltipAttribute:Attribute {public TooltipAttribute(string value){}}
public class MinAttribute:Attribute {public MinAttribute(float value){}} public class RangeAttribute:Attribute {public RangeAttribute(float a,float b){}}
public class TextAreaAttribute:Attribute {} public class HeaderAttribute:Attribute {public HeaderAttribute(string text){}}
public struct Vector2 {public float x,y;}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);}
}
public enum HitboxShape {Box,Circle,Cone}
public class SkillModuleConfig {}
public class DameDotConfig {}
public class ConditionConfig {}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
namespace UnityEngine {
 public class PropertyAttribute:Attribute{}
 public class HideInInspector:Attribute{}public class Header:Attribute{public Header(string name){}}
 public class TooltipAttribute:Attribute{public TooltipAttribute(string text){}}
 public class MonoBehaviour {public Dictionary<string,string> Fields=new();}
 public class GameObject {public List<MonoBehaviour> Components=new();public T[] GetComponentsInChildren<T>(bool inactive)=>Components.OfType<T>().ToArray();}
 public static class Application{public static bool isPlaying;}
 public static class Debug {public static List<string> Logs=new();public static void Log(string message,object source)=>Logs.Add(message);public static void LogWarning(string message,object source)=>Logs.Add(message);public static void LogError(string message,object source)=>Logs.Add(message);}
}
namespace UnityEngine.SceneManagement {
 public class Scene {public string path;public bool isLoaded,isDirty;public List<UnityEngine.GameObject> Roots=new();public bool IsValid()=>!string.IsNullOrEmpty(path);public UnityEngine.GameObject[] GetRootGameObjects()=>Roots.ToArray();}
 public static class SceneManager {public static Dictionary<string,Scene> Scenes=new();public static Scene Active=new(){path="Core",isLoaded=true};public static Scene GetActiveScene()=>Active;public static Scene GetSceneByPath(string path)=>path!=null&&Scenes.TryGetValue(path,out var s)?s:new();public static bool SetActiveScene(Scene scene){Active=scene;return true;}}
}
namespace UnityEditor {
 public class SceneAsset{}
 public static class AssetDatabase {public static T LoadAssetAtPath<T>(string path) where T:class=>SceneManager.Scenes.ContainsKey(path??"")?new SceneAsset() as T:null;}
 public static class Undo {public static int Records;public static void RecordObject(object value,string message)=>Records++;}
 public static class EditorApplication {public static bool isPlayingOrWillChangePlaymode;}
 public static class EditorUtility {public static void SetDirty(object value){}}
 public static class PrefabUtility {public static void RecordPrefabInstancePropertyModifications(object value){}}
 public enum SerializedPropertyType {String}
 public class SerializedProperty {readonly List<KeyValuePair<string,string>> pairs;int index=-1;public SerializedProperty(Dictionary<string,string> fields)=>pairs=fields.ToList();public bool Next(bool children)=>++index<pairs.Count;public string name=>pairs[index].Key;public string stringValue=>pairs[index].Value;public SerializedPropertyType propertyType=>SerializedPropertyType.String;}
 public class SerializedObject {readonly UnityEngine.MonoBehaviour target;public SerializedObject(UnityEngine.MonoBehaviour value)=>target=value;public SerializedProperty GetIterator()=>new(target.Fields);}
}
namespace UnityEditor.SceneManagement {
 public enum OpenSceneMode {Additive}
 public static class EditorSceneManager {
  public static int Opened,Closed,Saved;public static bool FailSave;
  public static Scene OpenScene(string path,OpenSceneMode mode){Opened++;var scene=SceneManager.Scenes[path];scene.isLoaded=true;SceneManager.Active=scene;return scene;}
  public static bool CloseScene(Scene scene,bool remove){Closed++;scene.isLoaded=false;return true;}
  public static void MarkSceneDirty(Scene scene)=>scene.isDirty=true;
  public static bool SaveScene(Scene scene){Saved++;if(FailSave)return false;scene.isDirty=false;return true;}
 }
}
public class StoryEventDefinition {public string eventId;public bool oneShot=true;public object Content;public List<object> conditions=new(),onTriggeredActions=new();}
public class StoryEventDirector:UnityEngine.MonoBehaviour {public string name="Director";public IReadOnlyList<StoryEventDefinition> EventDefinitions=Array.Empty<StoryEventDefinition>();public void ConfigureEvents(StoryEventDefinition[] entries)=>EventDefinitions=entries;}
public sealed partial class GameIdRegistry {
 public List<string> GetList(RegistryIdKind type)=>GetEventList(type);
}

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
class Checks {
 static int count;static void Check(bool yes,string name){if(!yes)throw new Exception(name);count++;}
 static string PathFor(int zone)=>Path.GetFullPath($"Validation/StoryEvents/FixtureZone{zone}.unity.txt");
 static (GameIdRegistry registry,Scene scene,StoryEventDirector director) Setup(int zone=1,bool loaded=true){
  Application.isPlaying=false;EditorApplication.isPlayingOrWillChangePlaymode=false;EditorSceneManager.FailSave=false;EditorSceneManager.Opened=EditorSceneManager.Closed=EditorSceneManager.Saved=0;Undo.Records=0;
  var registry=new GameIdRegistry();var director=new StoryEventDirector();var scene=new Scene{path=PathFor(zone),isLoaded=loaded};scene.Roots.Add(new(){Components={director}});SceneManager.Scenes.Clear();SceneManager.Scenes[scene.path]=scene;SceneManager.Active=new(){path="Core",isLoaded=true};registry.GetEventZone(zone).scenePath=scene.path;File.WriteAllText(scene.path,"");return(registry,scene,director);
 }
 static void Main(){
  Check((int)RegistryIdType.Flag==0&&(int)RegistryIdType.Sfx==6,"existing category numeric values preserved");
  Check(new RegistryIdAttribute(RegistryIdType.Event.Zone1).IdType==RegistryIdKind.EventZone1,"requested nested attribute syntax");
  var t=Setup();t.registry.GetList(RegistryIdType.Event.Zone1).Add("A");t.registry.GetList(RegistryIdType.Event.Zone2).Add("B");
  Check(t.registry.GetList(RegistryIdType.Event.Zone1).SequenceEqual(new[]{"A"}),"zone lists isolated");Check(t.registry.GetList(RegistryIdType.Event.Scene).SequenceEqual(new[]{"A","B"}),"shared prefab may choose all zones");
  Check(t.registry.GetEventKindForScene(t.scene.path)==RegistryIdType.Event.Zone1,"scene scope matches configured path");Check(t.registry.GetEventKindForScene("")==RegistryIdType.Event.Scene,"unassigned scene stays unscoped");
  t.registry.GetEventZone(3).eventIds=null;Check(t.registry.GetList(RegistryIdType.Event.Zone3).Count==0,"old assets missing zone arrays initialize safely");
  Check(GameIdRegistry.MissingStoryEventIds(new[]{"A"," A ","B","",null},new[]{"A"}).SequenceEqual(new[]{"B"}),"missing IDs trim/deduplicate without altering existing");
  t.registry.GetEventZone(1).eventIds.AddRange(new[]{"A"," C ",""});var original=new StoryEventDefinition{eventId="A",oneShot=false,Content=new()};original.onTriggeredActions.Add(new());t.director.ConfigureEvents(new[]{original});
  Check(t.registry.SyncStoryEventsInZone(1)==1,"append only missing registered event");Check(ReferenceEquals(t.director.EventDefinitions[0],original)&&!original.oneShot&&original.onTriggeredActions.Count==1,"existing payload and order preserved");
  var blank=t.director.EventDefinitions.Last();Check(blank.eventId=="C"&&blank.oneShot&&blank.Content==null&&blank.onTriggeredActions.Count==0&&blank.conditions.Count==0,"new event uses empty constructor defaults");
  Check(t.scene.isDirty&&EditorSceneManager.Saved==0,"loaded scene retains user save/Undo");Check(t.registry.SyncStoryEventsInZone(1)==0&&t.director.EventDefinitions.Count==2,"repeat sync is idempotent");
  var untouched=new StoryEventDirector();SceneManager.Scenes[PathFor(2)]=new(){path=PathFor(2),isLoaded=true,Roots={new(){Components={untouched}}}};t.registry.GetEventZone(1).eventIds.Add("D");t.registry.SyncStoryEventsInZone(1);Check(untouched.EventDefinitions.Count==0,"only chosen zone scene changes");
  t=Setup(5,false);t.registry.GetEventZone(5).eventIds.Add("Secret");Check(t.registry.SyncStoryEventsInZone(5)==1&&EditorSceneManager.Opened==1&&EditorSceneManager.Saved==1&&EditorSceneManager.Closed==1,"unloaded zone opens/saves/closes");Check(SceneManager.Active.path=="Core","active scene restored");
  t=Setup();t.scene.Roots.Clear();t.registry.GetEventZone(1).eventIds.Add("A");Check(t.registry.SyncStoryEventsInZone(1)==0&&Undo.Records==0,"missing director does not create objects or events");
  t=Setup();t.scene.Roots[0].Components.Add(new StoryEventDirector());t.registry.GetEventZone(1).eventIds.Add("A");Check(t.registry.SyncStoryEventsInZone(1)==0&&t.director.EventDefinitions.Count==0,"multiple directors block ambiguous append");
  t.director.name="Main";var other=(StoryEventDirector)t.scene.Roots[0].Components.Last();other.name="Other";other.ConfigureEvents(new[]{new StoryEventDefinition{eventId="A"}});t.registry.GetEventZone(1).directorObjectName="Main";Check(t.registry.SyncStoryEventsInZone(1)==0,"existing ID in another director is not duplicated");t.registry.GetEventZone(1).eventIds.Add("New");Check(t.registry.SyncStoryEventsInZone(1)==1&&t.director.EventDefinitions.Single().eventId=="New"&&other.EventDefinitions.Single().eventId=="A","chosen director receives only globally missing IDs");
  t=Setup();Application.isPlaying=true;Check(t.registry.SyncStoryEventsInZone(1)==0&&Undo.Records==0,"Play Mode blocks editing");Application.isPlaying=false;
  t=Setup(3,false);t.registry.GetEventZone(3).eventIds.Add("A");EditorSceneManager.FailSave=true;bool failed=false;try{t.registry.SyncStoryEventsInZone(3);}catch(InvalidOperationException){failed=true;}Check(failed&&t.scene.isLoaded&&t.scene.isDirty&&EditorSceneManager.Closed==0,"failed save keeps dirty scene open");Check(SceneManager.Active.path=="Core","failed save restores active scene");
  t=Setup();t.director.Fields["eventId"]="DraftUnsaved";Check(t.registry.CollectEventIdsFromZoneScene(1)==1&&t.registry.GetEventZone(1).eventIds.Contains("DraftUnsaved"),"collector includes unsaved loaded scene fields");Check(t.registry.CollectEventIdsFromZoneScene(1)==0,"collector is additive/idempotent");
  t=Setup(4,false);File.WriteAllText(t.scene.path,"  eventId: Plain\n  eventIdOnCleared: null\n  onOpenedEventId: ~\n  - eventId: \"Escaped\\u0111\"\n  eventId: Plain\n");Check(t.registry.CollectEventIdsFromZoneScene(4)==2,"disk parser skips YAML null, deduplicates and decodes escapes");Check(t.registry.GetEventZone(4).eventIds.Contains("Escapedđ"),"Unicode ID preserved");
  Console.WriteLine($"PASS: {count} production event registry/sync checks with Unity Editor API doubles (not Unity scene execution).");
 }
}

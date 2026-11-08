from pathlib import Path
import re,json,yaml

def graph(path):
    text=Path(path).read_text(encoding='utf-8-sig');matches=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.S|re.M))
    assert len({m[2] for m in matches})==len(matches),('duplicate fileID',path)
    return {int(m[2]):(int(m[1]),yaml.safe_load(m[3])) for m in matches}
def asset(p):return next(iter(graph(p)[11400000][1].values()))
metas={}
for p in Path('Assets').rglob('*.meta'):
    m=re.search(r'guid: (\w+)',p.read_text(encoding='utf-8-sig'))
    if m:metas[m[1]]=Path(str(p)[:-5])
checks=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1
def walk(value,docs,path):
    if isinstance(value,dict):
        if 'guid' in value:
            g=value['guid'];package_guids={'d199490a83bb2b844b9695cbf13b01ef','4e29b1a8efbd4b44bb3f3716e73f07ff','5f7201a12d95ffc409449d95f23cf332','fe87c0e1cc204ed48ad3b37840f39efc'}
            check(g in metas or g in package_guids or g.startswith('0000000000000000'),('missing external GUID',path,value))
        elif 'fileID' in value:check(value['fileID']==0 or value['fileID'] in docs,('missing internal ID',path,value))
        for v in value.values():walk(v,docs,path)
    elif isinstance(value,list):
        for v in value:walk(v,docs,path)

manifest=json.loads(Path('Validation/NewUpdate3/manifest.json').read_text(encoding='utf-8'))
check(len(manifest['sources'])==22,'22 Excel actor profiles')
itemnames={asset(p)['itemName'] for p in Path('Assets/Resources/ItemDefinitions').glob('*.asset')}
registry=asset('Assets/Resources/GameIdRegistry.asset')
for key,source in manifest['sources'].items():
    cfg=asset(Path('Assets/Resources/EnemyCombatConfigs')/(key+'.asset'))
    check(cfg['drops']==source['drops'],('loot differs from Excel manifest',key))
    for drop in cfg['drops']:
        check(drop['itemName'] in itemnames,('missing ItemDefinition',drop))
        check(drop['itemName'] in registry['itemIds'],('missing registry item',drop))
        check(0<=drop['dropChance']<=1 and drop['amount']>0,('invalid chance',drop))
    prefab=Path('Assets/Prefabs/Spawer')/('Spawner_'+key+'.prefab');docs=graph(prefab)
    for _,doc in docs.values():walk(doc,docs,prefab)
    spawner=docs[1002][1]['MonoBehaviour']
    check(metas[spawner['enemyCombatConfig']['guid']].stem==key,('incorrect config link',key))
    check(metas[spawner['enemyPrefab']['guid']].stem==key,('incorrect actor link',key))
    check(spawner['persistentSpawnerId']=='' and spawner['eventIdOnCleared']=='' and spawner['storyEventDirector']['fileID']==0,'prefab carries no encounter save/event identity')
    check('drops' not in spawner and spawner['spawnWhenPlayerNearby']==1,'spawner consumes config and activates on proximity')

spawnerguid='b6481c0424572324f82ca0c988310a0d'
for path in Path('Assets/Scenes').rglob('*.unity'):
    docs=graph(path)
    for id,(typ,doc) in docs.items():
        d=next(iter(doc.values()))
        if typ==114 and d.get('m_Script',{}).get('guid')==spawnerguid:
            check('drops' not in d and d.get('enemyCombatConfig',{}).get('guid') in metas,('unmigrated scene spawner',path,id))

path=Path('Assets/Scenes/Zones/CoreCombatDemo.unity');docs=graph(path)
# Validate all newly authored UI and the existing objects changed to reference it.
changed={665555998,365656876,1335727484,1335727485,1904630811,2039772059,2039772060,813503377,665555997}
for id,(typ,doc) in docs.items():
    if id>=2200000000 or id in changed:walk(doc,docs,path)
    d=next(iter(doc.values()))
    if typ==224:
        for child in d.get('m_Children',[]):
            childid=child['fileID'];other=next(iter(docs[childid][1].values()))
            check(other['m_Father']['fileID']==id,('parent/child mismatch',id,childid))
    if typ==1:
        for c in d['m_Component']:
            comp=next(iter(docs[c['component']['fileID']][1].values()))
            check(comp.get('m_GameObject',{}).get('fileID')==id,('component owner mismatch',id,c))
inv=docs[665555998][1]['MonoBehaviour']
check(inv['playerStatusButton']['fileID']!=0 and inv['playerStatusHUD']['fileID']!=0,'inventory status button wired')
check(inv['skillPopupIcon']['fileID']!=0 and inv['skillPopupTitle']['fileID']!=0 and inv['closeSkillPopupButton']['fileID']!=0,'skill popup controls wired')
check(inv['moveSkillPopupToSlot']==0,'popup uses fixed card layout')
hud=docs[inv['playerStatusHUD']['fileID']][1]['MonoBehaviour']
check(hud['statusText']['fileID']!=0 and hud['closeButton']['fileID']!=0 and metas[hud['levelConfig']['guid']].name=='Lv Config.asset','status popup refs wired')
skillconfig=asset('Assets/Resources/SkillConfig.asset')
check(skillconfig['fallbackSkillIcon']['guid'] in metas,'shared skill icon assigned')
basic=next(x for x in skillconfig['skills'] if x['skillId']=='BasicSlash')
check(basic['icon']['guid']=='6eb2d73eab62b5d498a203ebb3d8dd3a','existing BasicSlash icon preserved')
print(f'PASS: {checks} asset/reference assertions; 22 Excel loot profiles and spawner prefabs, scene UI graph and registry.')

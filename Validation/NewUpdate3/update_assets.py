"""Import loot from the workbook and edit only the requested inventory UI/spawner fields."""
from pathlib import Path
import re, copy, uuid, unicodedata, json
import yaml, openpyxl

HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
ROOT = Path.cwd()
def meta(path, folder=False):
    p=Path(str(path)+'.meta')
    if not p.exists(): p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if folder else ''),encoding='utf-8')
    return re.search(r'guid: (\w+)',p.read_text(encoding='utf-8-sig')).group(1)
def ref(path, id=11400000, type=2): return dict(fileID=id,guid=meta(path),type=type)
def loadasset(path): return yaml.safe_load(Path(path).read_text(encoding='utf-8-sig').split('MonoBehaviour:',1)[1])
def saveasset(path,d): Path(path).write_text(HEADER+'--- !u!114 &11400000\n'+yaml.safe_dump({'MonoBehaviour':d},sort_keys=False,allow_unicode=True),encoding='utf-8');meta(path)
def norm(name): return ''.join(c for c in unicodedata.normalize('NFD',name.lower().replace('đ','d')) if unicodedata.category(c)!='Mn')
def slug(name): return ''.join(w.capitalize() for w in re.findall('[a-z0-9]+',norm(name)))
class Graph:
    def __init__(self,p):
        self.p=Path(p);self.text=self.p.read_text(encoding='utf-8-sig');self.docs={};self.raw={};self.dirty=set();self.next=2200000000
        for m in re.finditer(r'--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=--- !u!|\Z)',self.text,re.S):
            id=int(m[2]);self.docs[id]=(int(m[1]),yaml.safe_load(m[3]));self.raw[id]=m[0]
    def get(self,id):
        self.dirty.add(id);return next(iter(self.docs[id][1].values()))
    def add(self,typ,kind,data):
        while self.next in self.docs:self.next+=1
        id=self.next;self.next+=1;self.docs[id]=(typ,{kind:copy.deepcopy(data)});self.dirty.add(id);return id
    def save(self):
        def dump(id):
            typ,d=self.docs[id];return f'--- !u!{typ} &{id}\n'+yaml.safe_dump(d,allow_unicode=True,sort_keys=False,width=150)
        def replace(m):
            id=int(m[2]);return dump(id) if id in self.dirty else m[0]
        s=re.sub(r'--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=--- !u!|\Z)',replace,self.text,flags=re.S)
        # New child objects can appear after SceneRoots; Unity resolves them by fileID.
        s+=''.join(dump(id) for id in self.docs if id not in self.raw)
        self.p.write_text(s,encoding='utf-8')

w=openpyxl.load_workbook('Assets/ForAIAgentEdit/BongNgu_GameDesign.xlsx',data_only=True)
configs={p.stem:(p,loadasset(p)) for p in Path('Assets/Resources/EnemyCombatConfigs').glob('*.asset')}
def keyname(name):
    n=norm(name)
    if n.startswith('thay hao'): return 'thay hao'
    if n.startswith('vo dien'): return 'vo dien'
    return n
byname={keyname(d['displayName']):(p,d) for p,d in configs.values()}
loot_names=set();source_rows={}
for sheet in ['Database Quai','Database Boss']:
    for row in list(w[sheet].values)[1:]:
        if not row[0]:continue
        p,cfg=byname[keyname(row[0])];drops=[]
        for m in re.finditer(r'([^,()]+?)\s*\(([^)]*\d+%[^)]*)\)',row[7] or ''):
            name=m[1].strip();amount_match=re.search(r'\s+x(\d+)$',name);amount=int(amount_match[1]) if amount_match else 1
            name=re.sub(r'\s+x\d+$','',name);chance=int(re.search(r'(\d+)%',m[2])[1])/100
            drops.append(dict(itemName=name,amount=amount,dropChance=chance,onlyIfMissing=1 if 'chưa có' in m[2] else 0));loot_names.add(name)
        cfg['drops']=drops;saveasset(p,cfg);source_rows[p.stem]={'sheet':sheet,'rowName':row[0],'location':row[1],'drops':drops}
# The other five profiles are outside Database Quai/Boss (two sprite extras and three summons).
for key,(p,cfg) in configs.items():
    if key not in source_rows:
        cfg.setdefault('drops',[]);saveasset(p,cfg)

# Loot must have real ItemDefinition entries so it can be inspected/used by inventory lookup.
items={loadasset(p)['itemName']:p for p in Path('Assets/Resources/ItemDefinitions').glob('*.asset')}
itemrows={r[0]:r for r in list(w['Database Item'].values)[1:] if r[0]}
newitems=[]
for name in sorted(loot_names):
    if name in items:continue
    r=itemrows.get(name);kind=str(r[1]) if r else ''
    category=3 if 'Đạn' in name else 4 if any(x in kind.lower() for x in ['nhiệm vụ','nghi lễ','cốt truyện']) or name in ['Đá Nguyền Cổ','Lõi Vòng Lặp'] else 2 if 'hồi' in kind.lower() else 5
    d=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},m_GameObject={'fileID':0},m_Enabled=1,m_EditorHideFlags=0,m_Script=ref('Assets/Scripts/Inventory/ItemDefinition.cs',11500000,3),m_Name='Loot_'+slug(name),m_EditorClassIdentifier='',itemName=name,description=r[5] if r else 'Vật phẩm rơi theo Database Quai / Database Boss.',category=category,weaponType=0,icon={'fileID':0},maxStack=99,attackBonus=0,defenseBonus=0,maxHealthBonus=0,healAmount=0,isReadable=0,canBeBurned=0,readingText='',rarity=r[2] if r else '',dropSource=r[4] if r else 'BongNgu_GameDesign.xlsx: Item Roi Ra / Item Thuong')
    p=Path('Assets/Resources/ItemDefinitions')/(d['m_Name']+'.asset');saveasset(p,d);items[name]=p;newitems.append(name)

spawnerguid=meta('Assets/Scripts/AI/ZoneEnemySpawner.cs')
encounters={}
for scene in Path('Assets/Scenes').rglob('*.unity'):
    g=Graph(scene);changed=False
    for id,(typ,doc) in list(g.docs.items()):
        d=doc.get('MonoBehaviour',{})
        if d.get('m_Script',{}).get('guid')!=spawnerguid:continue
        k=keyname(d['enemyDisplayName'])
        if k not in byname: raise ValueError('Unmapped scene enemy '+d['enemyDisplayName'])
        p,cfg=byname[k];d=g.get(id);d.pop('drops',None);d['enemyCombatConfig']=ref(p)
        encounters.setdefault(p.stem,[]).append(copy.deepcopy(d));changed=True
    if changed:g.save()

out=Path('Assets/Prefabs/Spawer');out.mkdir(exist_ok=True);meta(out,True)
pickup=Path('Assets/Generated/Session1To2/WorldItem.prefab')
assert meta(pickup)=='16314a09178c8004aac87bb8113999ff'
pickuptext=pickup.read_text(encoding='utf-8-sig');pickupgo=int(re.search(r'--- !u!1 &(\d+)',pickuptext)[1])
base=copy.deepcopy(next(iter(encounters.values()))[0])
prefabrecords=[]
for key,source in source_rows.items():
    p,cfg=configs[key];template=copy.deepcopy(encounters.get(key,[base])[0]);enemy=next(Path('Assets/Prefabs').rglob(key+'.prefab'))
    enemygo=int(re.search(r'--- !u!1 &(\d+)',enemy.read_text(encoding='utf-8-sig'))[1])
    template.update(m_GameObject={'fileID':1000},sessionId=str(source['location']),enemyIdPrefix=key,enemyDisplayName=cfg['displayName'],enemyPrefab=ref(enemy,enemygo,3),enemyCombatConfig=ref(p),itemPickupPrefab=ref(pickup,pickupgo,3),storyEventDirector={'fileID':0},eventIdOnCleared='',requiredFlag='',persistentSpawnerId='',spawnWhenPlayerNearby=1)
    # Prefabs are reusable definitions, not a copy of a specific story encounter or save identity.
    template['spawnCount']=1
    go=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},serializedVersion=6,m_Component=[{'component':{'fileID':1001}},{'component':{'fileID':1002}}],m_Layer=0,m_Name='Spawner_'+key,m_TagString='Untagged',m_Icon={'fileID':0},m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=1)
    tr=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},m_GameObject={'fileID':1000},serializedVersion=2,m_LocalRotation=dict(x=0,y=0,z=0,w=1),m_LocalPosition=dict(x=0,y=0,z=0),m_LocalScale=dict(x=1,y=1,z=1),m_ConstrainProportionsScale=0,m_Children=[],m_Father={'fileID':0},m_LocalEulerAnglesHint=dict(x=0,y=0,z=0))
    target=out/('Spawner_'+key+'.prefab')
    if target.exists():raise ValueError('Refuse duplicate generation '+str(target))
    target.write_text(HEADER+''.join(f'--- !u!{typ} &{id}\n'+yaml.safe_dump({kind:d},sort_keys=False,allow_unicode=True) for typ,id,kind,d in [(1,1000,'GameObject',go),(4,1001,'Transform',tr),(114,1002,'MonoBehaviour',template)]),encoding='utf-8');meta(target);prefabrecords.append(str(target))

# Keep existing icon choices; add an Inspector-configurable fallback for the 26 missing icons.
p=Path('Assets/Resources/SkillConfig.asset');d=loadasset(p);d['fallbackSkillIcon']=ref('Assets/Generated/QuickCombatDemo/SkillPlaceholder.png',21300000,3);saveasset(p,d)

# Scene-authored UI, borrowing the project's existing uGUI component templates.
g=Graph('Assets/Scenes/Zones/CoreCombatDemo.unity')
def clone(id): return copy.deepcopy(next(iter(g.docs[id][1].values())))
def rgba(r,g,b,a=1):return dict(r=r,g=g,b=b,a=a)
def component(go,template,typ=114,kind='MonoBehaviour'):
    d=clone(template);d['m_GameObject']={'fileID':go};id=g.add(typ,kind,d);g.get(go)['m_Component'].append({'component':{'fileID':id}});return id
def ui(name,parent,pos,size,image=False):
    d=clone(2100000001);d.update(m_Name=name,m_Component=[],m_Layer=5,m_IsActive=1);go=g.add(1,'GameObject',d)
    rt=clone(2100000002);rt.update(m_GameObject={'fileID':go},m_Father={'fileID':parent},m_Children=[],m_AnchorMin=dict(x=.5,y=.5),m_AnchorMax=dict(x=.5,y=.5),m_Pivot=dict(x=.5,y=.5),m_AnchoredPosition=dict(x=pos[0],y=pos[1]),m_SizeDelta=dict(x=size[0],y=size[1]))
    r=g.add(224,'RectTransform',rt);g.get(go)['m_Component'].append({'component':{'fileID':r}});g.get(parent)['m_Children'].append({'fileID':r})
    component(go,2100000003,222,'CanvasRenderer')
    im=component(go,2100000004) if image else None
    if im:
        v=g.get(im);v['m_Sprite']={'fileID':0};v['m_Type']=0;v['m_Color']=rgba(.09,.12,.16,.98)
    return go,r,im
def text(name,parent,value,pos,size,font=17,align=3):
    go,rt,_=ui(name,parent,pos,size);id=component(go,412912861);d=g.get(id);d['m_Text']=value;d['m_Color']=rgba(.94,.92,.85);d['m_RaycastTarget']=0;d['m_FontData'].update(m_FontSize=font,m_Alignment=align,m_RichText=0,m_VerticalOverflow=0,m_HorizontalOverflow=0);return id
def button(name,parent,label,pos,size):
    go,rt,im=ui(name,parent,pos,size,True);b=component(go,2100000005);d=g.get(b);d['m_TargetGraphic']={'fileID':im};d['m_OnClick']={'m_PersistentCalls':{'m_Calls':[]}};g.get(im)['m_Color']=rgba(.20,.29,.30)
    text(name+' Label',rt,label,(0,0),(size[0]-8,size[1]-4),16,4);return b
def mono(go,script,**fields):
    d=clone(665555998)
    d={k:v for k,v in d.items() if k.startswith('m_')};d.update(m_GameObject={'fileID':go},m_Script=ref(script,11500000,3),m_EditorClassIdentifier='',**fields)
    id=g.add(114,'MonoBehaviour',d);g.get(go)['m_Component'].append({'component':{'fileID':id}});return id
inventory=g.get(665555998)
assert 'playerStatusButton' not in inventory,'UI already updated'
statusbutton=button('Player Status Button',813503377,'THÔNG SỐ',(-82,275),(160,38))
go,rt,bg=ui('Player Status Popup',665555997,(0,0),(610,440),True)
text('Status Heading',rt,'THÔNG SỐ NHÂN VẬT',(-30,183),(480,38),23)
close=button('Close Player Status',rt,'×',(266,183),(36,36))
stats=text('Player Stats',rt,'Đang tải dữ liệu…',(0,-12),(554,338),19)
hud=mono(go,'Assets/Scripts/UI/PlayerStatusHUD.cs',portrait={'fileID':0},statusText={'fileID':stats},closeButton={'fileID':close},refreshInterval=.15,player={'fileID':0},levelConfig=ref('Assets/Resources/Lv Config.asset'))
g.get(go)['m_IsActive']=0
inventory.update(playerStatusButton={'fileID':statusbutton},playerStatusHUD={'fileID':hud},moveSkillPopupToSlot=0)
g.get(365656876)['playerStatusHUD']={'fileID':hud}
popup=g.get(1335727484);popup['m_Name']='Equipped Skill Popup';popup['m_Layer']=5;popup['m_IsActive']=0
r=g.get(1335727485);r.update(m_AnchoredPosition=dict(x=200,y=0),m_SizeDelta=dict(x=340,y=300))
component(1335727484,2100000003,222,'CanvasRenderer');im=component(1335727484,2100000004);d=g.get(im);d.update(m_Color=rgba(.07,.10,.14,.98),m_Sprite={'fileID':0},m_Type=0,m_RaycastTarget=1)
ig,ir,ii=ui('Skill Popup Icon',1335727485,(-126,102),(48,48),True);g.get(ii).update(m_PreserveAspect=1,m_RaycastTarget=0,m_Color=rgba(1,1,1))
title=text('Skill Popup Title',1335727485,'Kỹ năng',(6,102),(200,50),19)
desc=text('Skill Popup Description',1335727485,'', (0,20),(300,102),16,0)
text('Skill Key Label',1335727485,'PHÍM KÍCH HOẠT',(-73,-63),(158,28),14)
text('Skill Key Hint',1335727485,'Nhập phím, nhấn Enter để lưu.',(0,-119),(300,26),13,4)
close=button('Close Skill Popup',1335727485,'×',(145,129),(28,28))
inventory.update(skillPopupIcon={'fileID':ii},skillPopupTitle={'fileID':title},skillPopupDescription={'fileID':desc},closeSkillPopupButton={'fileID':close})
g.get(1904630811).update(m_AnchoredPosition=dict(x=0,y=-98),m_SizeDelta=dict(x=290,y=32))
g.get(2039772059).update(m_AnchoredPosition=dict(x=88,y=-63),m_SizeDelta=dict(x=105,y=32))
g.get(2039772060)['m_ContentType']=0
# Keep existing slot identities/hierarchy, ensuring their image child does not intercept clicks.
for buttonid in [1208728779,1985028399,936168410]:
    b=g.get(buttonid);rootgo=b['m_GameObject']['fileID'];gort=next(c['component']['fileID'] for c in g.get(rootgo)['m_Component'] if g.docs[c['component']['fileID']][0]==224)
    # Existing slot child Images are already authored in this scene.
    for child in g.get(gort)['m_Children']:
        childgo=g.get(child['fileID'])['m_GameObject']['fileID']
        for c in g.get(childgo)['m_Component']:
            id=c['component']['fileID'];d=next(iter(g.docs[id][1].values()))
            if d.get('m_Script',{}).get('guid')==g.get(2100000004)['m_Script']['guid']:g.get(id).update(m_RaycastTarget=0,m_Color=rgba(1,1,1),m_PreserveAspect=1)
            if d.get('m_Script',{}).get('guid')==g.get(2100000005)['m_Script']['guid']:g.get(id)['m_Enabled']=0
g.save()
Path('Validation/NewUpdate3/manifest.json').write_text(json.dumps(dict(sources=source_rows,prefabs=prefabrecords,newItems=newitems),ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported loot:',len(source_rows),'profiles; created',len(prefabrecords),'spawners;',len(newitems),'ItemDefinitions; inventory UI updated.')

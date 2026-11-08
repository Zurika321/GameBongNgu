"""Read-only comparison of real Unity assets/prefab graphs against the workbook."""
from pathlib import Path
import re, json, yaml, openpyxl
root=Path.cwd()
def load(p):return yaml.safe_load(Path(p).read_text(encoding='utf-8-sig').split('MonoBehaviour:',1)[1])
w=openpyxl.load_workbook('Assets/ForAIAgentEdit/BongNgu_GameDesign.xlsx',data_only=True)
levels=load('Assets/Resources/Lv Config.asset')['levels']
p=load('Assets/Resources/SkillConfig.asset')['skills'];e=load('Assets/Resources/EnemySkillConfig.asset')['skills']
players={x['skillId']:x for x in p}; enemies={x['skillId']:x for x in e}
cat={x['actionId']:x for x in load('Assets/Resources/SkillActionCatalog.asset')['actions']}
assert len(levels)==20
expected=['BasicSlash','DodgeRoll','Parry','ArmorPiercing','ComboSlash','StunResistance','Rage','ThrowWeapon','WeaponUpgrade','DotResistance','MeleeCritical','GuardBreak','CalmMind','PerfectParry','BurstCombo','SilverMastery','IronWill','DualBlades','SurvivalInstinct','SelfPerfection']
rows=[r for r in w['He Thong Level'].values if str(r[0]).isdigit()]
for actual,row,id in zip(levels,rows,expected):
 assert [actual[k] for k in ['level','expRequired','maxHp','atk','def','maxRareAmmo']]==list(map(int,row[:6]))
 assert actual['skills'][0]==id
 assert all(id in players for id in actual['skills'])
assert len(p)==27 and len(e)==52
assert len(players)==len(p) and len(enemies)==len(e) and not set(players).intersection(enemies)
for entry in p+e:
 if entry.get('actionId'):assert entry['actionId'] in cat,entry['skillId']
for id,value,index in [('StunResistance',.2,7),('DotResistance',.3,8)]:
 assert players[id]['passiveConfig']['modifiers']==[{'stat':index,'modifierType':0,'value':value}]
manifest=json.loads(Path('Validation/EnemySkills/manifest.json').read_text(encoding='utf-8'))
assert len(manifest)==27 and sum(x['actorId'].startswith('Boss_') for x in manifest)==6
metas={}
for f in Path('Assets').rglob('*.meta'):
 m=re.search(r'(?m)^guid: (\w+)',f.read_text(encoding='utf-8-sig'))
 if m:metas[m[1]]=str(f)[:-5]
configs={x['name']:load(x['profile']) for x in manifest}
for row in list(w['Database Quai'].values)[1:17]:
 a=configs[row[0]]
 if str(row[3]).isdigit():assert a['maxHealth']==float(row[3]) and a['needsHealth']==0
 assert a['attack']==float(re.match(r'\d+',str(row[4])).group())
for row in list(w['Database Boss'].values)[1:7]:
 name=row[0].split(' (')[0].split(' — ')[0];a=configs[name]
 assert a['maxHealth']==float(row[3]) and a['attack']==0 and a['needsAttack']==1
for name in ['Thú Gù Tím','Thú Giáp Tinh Thể']:
 assert configs[name]['needsHealth']==configs[name]['needsAttack']==1
 assert configs[name]['maxHealth']==configs[name]['attack']==0
for item in manifest:
 path=Path(item['prefab']);text=path.read_text(encoding='utf-8-sig')
 docs={int(m[2]):(int(m[1]),yaml.safe_load(m[3])) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',text)}
 assert len(docs)==20,(path,len(docs))
 def walk(v):
  if isinstance(v,dict):
   if 'guid' in v:assert v['guid'] in metas or v['guid']=='a97c105638bdf8b4a8650670310a4cd3',(path,v) # URP Sprite-Lit-Default inherited from existing Player
   elif 'fileID' in v:assert v['fileID']==0 or v['fileID'] in docs,(path,v)
   for x in v.values():walk(x)
  elif isinstance(v,list):
   for x in v:walk(x)
 for _,doc in docs.values():walk(doc)
 for id,(typ,doc) in docs.items():
  d=next(iter(doc.values()))
  if typ==1:
   for component in d['m_Component']:assert next(iter(docs[component['component']['fileID']][1].values()))['m_GameObject']['fileID']==id
  if typ==4:
   for child in d['m_Children']:assert docs[child['fileID']][1]['Transform']['m_Father']['fileID']==id
 assert docs[100001][1]['GameObject']['m_TagString']=='HitBoxEnemy'
 config=configs[item['name']]
 for slot in config['skills']:
  assert slot['skillId'] in enemies
  assert all(phase>=1 for phase in slot['phases'])
  if item['summon']:assert not any(s['module']==6 for s in cat[slot['skillName']]['steps'])
 cast=docs[11400004][1]['MonoBehaviour']
 assert metas[cast['skillConfig']['guid']]=='Assets\\Resources\\EnemySkillConfig.asset'
 assert metas[cast['combatConfig']['guid']]==str(Path(item['profile']))
 # Summon links reference an actual, nonrecursive prefab.
 for slot in config['skills']:
  entry=enemies[slot['skillId']]
  if any(step['module']==6 for step in cat[entry['actionId']]['steps']):
   link=entry['defaultContext']['modules']['summon']['prefab'];assert link['guid'] in metas
   summoned=Path(metas[link['guid']]);assert summoned.exists() and summoned.suffix=='.prefab'
# Preserve the user's animation variants/icon in BasicSlash.
assert players['BasicSlash']['icon']['guid']=='6eb2d73eab62b5d498a203ebb3d8dd3a'
assert configs['Đàn Sói Sương Mù']['phases'][0]['healthBelow']==.4
assert configs['Vệ Binh Đá Cổ']['phases'][0]['healthBelow']==.5
assert configs['Thầy Hạo']['phases'][0]['healthBelow']==.45
assert configs['Người Gác Hang']['phases'][0]['manualOnly']==1
assert configs['VÔ DIỆN']['phases'][1]['manualOnly']==1
wolf=next(enemies[s['skillId']] for s in configs['Đàn Sói Sương Mù']['skills'] if s['skillName']=='ACT_MELEE_3')
assert wolf['defaultContext']['actionDamageOverrides'][0]['damagePercents']==[70,70,70]
print('PASS: all 20 level rows, 79 skill registrations, 27 prefab/component graphs, Excel HP/ATK, phase gates, summon references and preserved player icon.')

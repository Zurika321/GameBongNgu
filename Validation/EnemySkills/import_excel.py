"""One-off, reviewable import for 2026-11-08. Run from project root; writes only named assets/prefabs."""
import copy, re, unicodedata, uuid, json
from pathlib import Path
import yaml, openpyxl
BASE=Path.cwd()
WORKBOOK=BASE/'Assets/ForAIAgentEdit/BongNgu_GameDesign.xlsx'
w=openpyxl.load_workbook(WORKBOOK,data_only=True)
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
def meta(p,folder=False):
 p=Path(p);m=Path(str(p)+'.meta')
 if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if folder else ''),encoding='utf-8')
 return re.search(r'guid: (\w+)',m.read_text()).group(1)
def folder(p):
 p=Path(p);p.mkdir(parents=True,exist_ok=True);meta(p,True);return p
def guid(p):return meta(Path(p))
def ref(p,id=11400000,t=2):return {'fileID':id,'guid':guid(p),'type':t}
def load(p):return yaml.safe_load(Path(p).read_text(encoding='utf-8-sig').split('MonoBehaviour:',1)[1])
def asset(p,data):
 Path(p).write_text(HEADER+'--- !u!114 &11400000\n'+yaml.safe_dump({'MonoBehaviour':data},allow_unicode=True,sort_keys=False,width=150),encoding='utf-8');meta(p)
def mono(go,script,**fields):
 return dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},m_GameObject={'fileID':go},m_Enabled=1,m_EditorHideFlags=0,m_Script=ref(script,11500000,3),m_Name='',m_EditorClassIdentifier='',**fields)
def so(name,script,**fields):
 d=mono(0,script,**fields);d['m_Name']=name;return d
def norm(s):return ''.join(c for c in unicodedata.normalize('NFD',str(s).replace('Đ','D').replace('đ','d')) if unicodedata.category(c)!='Mn').lower().strip()
def slug(s):return ''.join(x.capitalize() for x in re.findall('[a-z0-9]+',norm(s)))
cat={x['actionId']:x for x in load('Assets/Resources/SkillActionCatalog.asset')['actions']}
sp=Path('Assets/Resources/SkillConfig.asset');sk=load(sp)
existing={x['skillId']:x for x in sk['skills']}
if Path('Assets/Resources/EnemySkillConfig.asset').exists():existing.update({x['skillId']:x for x in load('Assets/Resources/EnemySkillConfig.asset')['skills']})

def context(action,cooldown=0,overrides=None):
 a=cat.get(action,{})
 steps=a.get('steps',[])
 first=next((s for s in steps if s['module'] in (0,1) and not s.get('telegraph')),None)
 mods={}
 for s in steps:
  if s.get('useSkillModuleSettings'):mods.update(copy.deepcopy(s.get('settings',{})))
 return dict(skillSize=copy.deepcopy(first['size']) if first else {'x':0,'y':0},range=first['forwardOffset'] if first else 0,speed=first['speed'] if first else 0,duration=first['lifetime'] if first else 0,stunTime=0,damageMultiplier=1,cooldown=cooldown,chargeTime=0,endTime=0,canPierce=0,debug=1,dotConfig={'activationType':0},modules=mods,actionDamageOverrides=overrides or [],breaksGuard=0,bypassesMeleeResistance=0,effectAnimation={'castFrames':[],'endFrames':[],'fourDirections':0,'frameInterval':0.06})

def entry(id,name,action='',typ=1,cooldown=0,**fields):
 d=copy.deepcopy(existing.get(id,{}))
 d.update(skillId=id,name=name,actionId=action,type=typ,legacyContextMigrated=1)
 if id!='BasicSlash':d.update(defaultContext=context(action,cooldown),weaponVariants=[],buffConfig={'duration':0,'modifiers':[]},passiveConfig={'trigger':0,'chance':1,'duration':0,'modifiers':[]},weaponRequirement={'acceptedTypes':[],'acceptedWeaponIds':[]},icon={'fileID':0},weaponId='')
 d.update(fields); existing[id]=d;return d

def modifier(stat,value):return {'stat':stat,'modifierType':0,'value':value}
# Player IDs are stable and separate from reusable action IDs.
player_ids=['BasicSlash','ComboSlash','ArmorPiercing','PerfectParry','BurstCombo','DualBlades','DodgeRoll','Parry','StunResistance','DotResistance','CalmMind','IronWill','Rage','ThrowWeapon','BorrowedPower','DemonClaw','ReverseAssimilation','Symbiosis','WeaponUpgrade','MeleeCritical','GuardBreak','SilverMastery','SurvivalInstinct','SelfPerfection']
player_actions=['ACT_MELEE_1','ACT_MELEE_2','ACT_THRUST_PIERCE','ACT_PARRY_COUNTER','ACT_MELEE_5_STUN_FINISH','ACT_BUFF_DUAL_BLADE','ACT_DODGE_ROLL','ACT_PARRY','','','ACT_HEAL_LT_CHANNEL','','ACT_BUFF_ATK_TIMED','ACT_PROJ_1','','ACT_MELEE_1_SELFCOST','ACT_BUFF_BORROW_POWER','','','','ACT_THRUST_PIERCE','','','']
player_cd=[.35,0,4,2,10,30,1,1,0,0,0,0,0,1,0,0,60,0,0,0,0,0,0,0]
passive={'StunResistance','DotResistance','IronWill','BorrowedPower','Symbiosis','WeaponUpgrade','MeleeCritical','SilverMastery','SurvivalInstinct','SelfPerfection'}
player=[]
tree={norm(r[2]):r for r in list(w['Cay Ky Nang'].values)[1:] if r[2]}
for i,row in enumerate(list(w['Skill Player'].values)[1:25]):
 id=player_ids[i];typ=0 if id=='BasicSlash' else 2 if id in passive else 1
 d=entry(id,row[2],player_actions[i],typ,player_cd[i],branch=row[0],tier=row[1],effect=row[5],unlockCondition='',requires='',setupNotes='Nguồn: Skill Player / He Thong Level. Cooldown có dấu ~ là đề xuất trong Excel.',needsInspectorSetup=0)
 t=tree.get(norm(row[2]))
 if t:d['unlockCondition']=t[3];d['requires']=t[4]
 if id=='StunResistance':d['passiveConfig']['modifiers']=[modifier(7,.2)]
 if id=='DotResistance':d['passiveConfig']['modifiers']=[modifier(8,.3)]
 if id=='MeleeCritical':
  d['passiveConfig']['modifiers']=[modifier(4,.15)]
  d['needsInspectorSetup']=1;d['setupNotes']='Đã đăng ký +15% CriticalChance. Hệ damage hiện chưa roll chí mạng cận chiến; cần nối consumer trước khi bật passive.'
 if id=='BorrowedPower':
  d['needsInspectorSetup']=1;d['setupNotes']='Cần nối lựa chọn A Session 7B; +15% CHỈ cận chiến và QT +1 mỗi 3 trận cần adapter theo vòng đời trận.'
 if id=='IronWill':
  d['needsInspectorSetup']=1;d['setupNotes']='Cần cờ LT cao xuyên suốt game và cấp cờ miễn nhiễm dùng 1 lần cho ChallengeMeter; Excel chưa nêu ngưỡng LT.'
 if id in ('WeaponUpgrade','SilverMastery','SurvivalInstinct','Symbiosis'):
  d['needsInspectorSetup']=1
  d['setupNotes']={'WeaponUpgrade':'Excel chưa có công thức nâng 1 bậc và bảng chỉ số vũ khí theo bậc.','SilverMastery':'Cần consumer phân loại đạn bạc/boss để cộng 25% đúng mục tiêu.','SurvivalInstinct':'Cần metadata nhận đòn chí mạng để tự né 10%; không biến thành active né thủ công.','Symbiosis':'Excel chưa xác định action/gameplay; cần cờ Ending Cộng Sinh và NG+.'}[id]
 if id=='Rage':d['needsInspectorSetup']=1;d['setupNotes']='Gán Modules/Condition: HitCount, Required Count, Within Seconds; Excel chưa chốt số đòn tích nộ.'
 if id=='GuardBreak':d['defaultContext']['breaksGuard']=1;d['needsInspectorSetup']=1;d['setupNotes']='Cần cổng giới hạn 1 lần/trận; Excel chưa định nghĩa ranh giới trận, không cho spam phá khiên.'
 if id=='ThrowWeapon':d['defaultContext']['actionDamageOverrides']=[{'stepIndex':0,'damagePercents':[40]}]
 player.append(d)
for id,name,damage in [('ShotOld','Bắn đạn Cũ',120),('ShotCopper','Bắn đạn Đồng',160),('ShotSilver','Bắn đạn Bạc',220)]:
 d=entry(id,name,'ACT_PROJ_1',1,0,branch='Vũ khí tầm xa',tier='',effect='Tiêu hao 1 viên đạn; '+str(damage)+'% ATK (đề xuất Excel).',needsInspectorSetup=1,setupNotes='Cần gán loại vũ khí/đạn và nối tiêu hao đạn trước khi bật; không cho bắn miễn phí.')
 d['defaultContext']['actionDamageOverrides']=[{'stepIndex':0,'damagePercents':[damage]}]
 d['defaultContext']['breaksGuard']=int(id=='ShotSilver');player.append(d)
levels=load('Assets/Resources/Lv Config.asset')
level_ids=['BasicSlash','DodgeRoll','Parry','ArmorPiercing','ComboSlash','StunResistance','Rage','ThrowWeapon','WeaponUpgrade','DotResistance','MeleeCritical','GuardBreak','CalmMind','PerfectParry','BurstCombo','SilverMastery','IronWill','DualBlades','SurvivalInstinct','SelfPerfection']
levels['levels']=[]
for row in w['He Thong Level'].values:
 if not str(row[0]).isdigit():continue
 lv,xp,hp,atk,df,ammo=map(int,row[:6]); skills=[level_ids[lv-1]]
 if lv==20:skills+=player_ids[:18] # explicitly unlock whole tree per He Thong Level; unresolved gameplay stays marked.
 levels['levels'].append(dict(level=lv,expRequired=xp,maxHp=hp,atk=atk,def_=df,maxRareAmmo=ammo,skills=skills))
 levels['levels'][-1]['def']=levels['levels'][-1].pop('def_')
 for d in player:
  if d['skillId']==level_ids[lv-1]:d['unlockCondition']='Đạt Lv'+str(lv)+' — theo He Thong Level (ưu tiên mốc này khi cây kỹ năng lệch).'
asset('Assets/Resources/Lv Config.asset',levels)

# Build actor registrations from Map Skill-Action; basic attacks absent there are supplied by ACT_MELEE_1.
actors=[]
for row in list(w['Database Quai'].values)[1:17]:
 actors.append(dict(name=row[0],id='Enemy_'+slug(row[0]),boss=False,hp=float(row[3]) if str(row[3]).replace('.','',1).isdigit() else 0,atk=row[4] if isinstance(row[4],(int,float)) else int(re.match(r'\d+',str(row[4])).group()) if re.match(r'\d+',str(row[4])) else 0,notes=row[5],slots=[],phases=[]))
for name in ['Thú Gù Tím','Thú Giáp Tinh Thể']:
 actors.append(dict(name=name,id='Enemy_'+slug(name),boss=False,hp=0,atk=0,missing=True,notes='Tên tạm trong Skill Enemy; Excel thiếu HP/ATK — để gán Inspector theo xác nhận.',slots=[],phases=[]))
for row in list(w['Database Boss'].values)[1:7]:
 name=row[0].split(' (')[0].split(' — ')[0]
 actors.append(dict(name=name,id='Boss_'+slug(name),boss=True,hp=float(row[3]),atk=0,notes=row[5],slots=[],phases=[]))
def actor_match(name):
 n=norm(name).split('\n')[0].strip();return next((a for a in actors if norm(a['name'])==n),None)
def locktime(action):
 # Only casting steps lock selection. Long-lived effects/buffs/periodic summons remain independently cancellable.
 return max([s.get('delay',0)+max(0,s.get('repeatCount',1)-1)*s.get('interval',0)+max(.02,s.get('lifetime',0)) for s in cat[action]['steps'] if s.get('trigger',0)==0 and s['module'] in (0,1,7,10,12)]+[0])
def add(a,name,action,cd=0,trigger=0,phases=None,priority=1,overrides=None):
 id=a['id']+'_'+slug(name)
 d=entry(id,a['name']+' / '+name,action,1,cd,branch='Boss' if a['boss'] else 'Enemy',tier='',effect='',unlockCondition='Enemy: '+a['name'],requires='',setupNotes='Nguồn Map Skill-Action. Trường thời gian/tầm còn thiếu giữ cooldown 0 hoặc lấy geometry từ catalog; chỉnh Inspector theo animation thực.',needsInspectorSetup=0)
 if overrides:d['defaultContext']['actionDamageOverrides']=overrides
 # Projectile detection range derives from catalog travel; melee from authored geometry.
 damage_steps=[s for s in cat[action]['steps'] if s['module'] in (0,1) and not s.get('telegraph')]
 ranges=[s.get('travelDistance',0) if s['module']==1 else s.get('forwardOffset',0)+(s.get('size',{}).get('x',0) if s.get('shape')==1 else s.get('size',{}).get('x',0)*.5) for s in damage_steps]
 distance=max(ranges+[context(action)['modules'].get('replay',{}).get('range',0)])
 if not distance:distance=0 # self effects are expanded to sensing range below
 slot=dict(skillId=id,skillName=action,trigger=trigger,phases=phases or [],minimumDistance=0,initialDelay=0,priority=priority,oncePerCombat=0,requiresAttack=int(bool(damage_steps) and action!='ACT_SELF_EXPLODE'),needsInspectorSetup=0,setupNotes='',castCondition={'kind':0},castLockSeconds=locktime(action),cooldown=cd,shape=1,detectionSize={'x':distance,'y':distance},detectionOffset={'x':0,'y':0},detectTag='HitBoxPlayer',damage=a['atk'])
 a['slots'].append(slot);a.setdefault('entries',[]).append(d);return d,slot
# The row order is the actual authored action map, not inferred JSON registrations.
for row in list(w['Map Skill-Action'].values)[1:46]:
 if not row[0] or not str(row[3]).startswith('ACT_'):continue
 a=actor_match(row[1])
 if not a:continue
 name,action=row[2],row[3]; d,s=add(a,name,action)
 d['effect']=str(row[4] or '')+'; '+str(row[5] or '')
 if action in ('ACT_BUFF_ATKSPEED_RAMP','ACT_AURA_SLOW_CONTACT','ACT_RESIST_MELEE_50','ACT_SUMMON_2_EVERY_20S','ACT_SUMMON_ADDS_CONTINUOUS'):
  s['trigger']=1;s['oncePerCombat']=1
 if action=='ACT_SELF_EXPLODE':s['trigger']=3;s['oncePerCombat']=1
 if action=='ACT_IMMUNE_MELEE_CYCLE':s['trigger']=2;s['phases']=[2]
 if action=='ACT_BUFF_SPEED_PHASE':s['trigger']=2;s['phases']=[2];s['oncePerCombat']=0
 if action=='ACT_BUFF_SPEED_CONDITIONAL':s['castCondition']={'kind':3,'threshold':.3};s['oncePerCombat']=0;s['skipWhileActionRunning']=1
 if action=='ACT_REFLECT_20':s['castCondition']={'kind':7,'requiredCount':3,'withinSeconds':3}
 if action=='ACT_SUMMON_2_EVERY_20S':s['phases']=[1];s['initialDelay']=20
 if action=='ACT_MELEE_3' and a['name']=='Đàn Sói Sương Mù':
  s['phases']=[2];s['castCondition']={'kind':4,'wallMask':{'m_Bits':0},'wallDistance':.5};s['needsInspectorSetup']=1;s['setupNotes']='Gán Cast Condition/Wall Mask theo layer tường thật rồi tắt Needs Inspector Setup.'
  d['defaultContext']['actionDamageOverrides']=[{'stepIndex':0,'damagePercents':[70,70,70]}]
 if a['boss']:
  if '(P1)' in name:s['phases']=[1]
  if '(P2)' in name:s['phases']=[2]
  if '(P3)' in name:s['phases']=[3]
  if action=='ACT_SPLIT_2':s['phases']=[1];s['trigger']=2
  if action=='ACT_CLONE_SHADOW_2':s['phases']=[2];s['trigger']=2
  if action=='ACT_QTE_POSSESS':s['phases']=[3];s['trigger']=2
  if action=='ACT_BLIND_5S':s['phases']=[2]
  if action=='ACT_BLIND_8S':s['phases']=[1]
  if action=='ACT_CURSE_TRANSFER':
   s['phases']=[1];s['trigger']=2;s['needsInspectorSetup']=1;s['setupNotes']='Gán Challenge drain/gain/duration và trạng thái Curse; nối UI + kết quả truyện. Excel chưa chốt các thông số này.'
  if action=='ACT_REPLAY_LAST':
   d['defaultContext']['modules']['replay']={'selection':2 if a['name']=='Thầy Hạo' else 1,'range':10,'damageFraction':1}
   if a['name']=='Thầy Hạo':s['phases']=[2];d['setupNotes']+=' Lịch sử replay phải được chốt từ Session 12; chưa có nguồn snapshot xuyên scene.'
 # Known cooldowns from Skill Enemy / boss basic attacks.
 if a['name']=='Thú Gù Tím':d['defaultContext']['cooldown']={'ACT_MELEE_2':1.5,'ACT_AOE_CIRCLE_1':8,'ACT_PROJ_POISON':10}.get(action,0)
 if a['name']=='Nhện Bóng Tối':d['defaultContext']['cooldown']={'ACT_MELEE_1_POISON':1.5,'ACT_ZONE_SLOW_WEB':9,'ACT_PROJ_POISON':10}.get(action,0)
 if a['name']=='Thú Giáp Tinh Thể':d['defaultContext']['cooldown']={'ACT_MELEE_1':1.8,'ACT_CONE_SWEEP_STUN':9,'ACT_PROJ_EXPLOSIVE':12}.get(action,0)
 if a['boss'] and action=='ACT_MELEE_1':d['defaultContext']['cooldown']=2 if a['name']=='Vệ Binh Đá Cổ' else 1.5
 if action=='ACT_QTE_POSSESS':
  s['needsInspectorSetup']=1;s['setupNotes']='Gán Challenge/QTE drain/gain/duration, flag miễn nhiễm, UI và kết quả thất bại Session 13C; ngưỡng QT do event truyện cấp phase 3.'
  d['defaultContext']['modules']['challenge']['immunityFlag']='';d['defaultContext']['modules']['challenge']['consumeImmunity']=1
 # Reflection/replay are not basic attack replacements.
 if not a['boss'] and action in ('ACT_MELEE_1_STUN','ACT_MELEE_1_POISON','ACT_MELEE_1_BLEED','ACT_MELEE_1_LIFESTEAL','ACT_MELEE_1','ACT_MELEE_2'):s['priority']=0
 if action=='ACT_REPLAY_LAST' and not a['boss']:d['defaultContext']['modules']['replay']={'selection':0,'range':10,'damageFraction':1};s['priority']=0
# A living minion cannot summon its own replacements; that action belongs to Vo Dien.
minion=actor_match('Tay Sai Vô Diện')
for a in actors:
 if a is minion:
  for s in a['slots']:existing.pop(s['skillId'],None)
  a['slots']=[];a['entries']=[]
 if not a['boss'] and a['name'] not in ('Tinh Thể Quỷ Ám','Hồn Ma Nạn Nhân Cũ') and not any(s['priority']==0 for s in a['slots']):
  add(a,'Đánh thường','ACT_MELEE_1',priority=0)
 if a['name']=='Đàn Sói Sương Mù':a['phases']=[dict(phase=2,healthBelow=.4,manualOnly=0,requiredProgressFlag='',notes='Dưới 40% HP.')]
 if a['name']=='Vệ Binh Đá Cổ':a['phases']=[dict(phase=2,healthBelow=.5,manualOnly=0,requiredProgressFlag='',notes='Dưới 50% HP.')]
 if a['name']=='Người Gác Hang':a['phases']=[dict(phase=2,healthBelow=0,manualOnly=1,requiredProgressFlag='',notes='Excel chưa nêu ngưỡng chuyển phase. Gọi EnemyPhaseController.SetPhase(2) hoặc cấu hình ngưỡng rồi tắt Manual Only.')]
 if a['name']=='VÔ DIỆN':
  a['phases']=[dict(phase=2,healthBelow=.6,manualOnly=0,requiredProgressFlag='',notes='Dưới 60% HP.'),dict(phase=3,healthBelow=.25,manualOnly=1,requiredProgressFlag='',notes='Dưới 25% HP + QT cao xuyên suốt game. Chưa có ngưỡng/cờ thật; gán điều kiện rồi tắt Manual Only hoặc gọi event truyện.')]
  d,s=add(a,'Triệu hồi tay sai','ACT_SUMMON_ADDS_CONTINUOUS',trigger=1,priority=2);s['oncePerCombat']=1
 if a['name']=='Thầy Hạo':a['phases']=[dict(phase=2,healthBelow=.45,manualOnly=0,requiredProgressFlag='',notes='Dưới 45% HP.')]

profile_folder=folder('Assets/Resources/EnemyCombatConfigs')
# Prefabs for summoned units have only a basic attack, avoiding recursive boss clones/summons.
clones=[]
for source,name,hpf in [('Đàn Sói Sương Mù','Sói con',0),('Người Gác Hang','Bản sao Người Gác Hang',.5),('VÔ DIỆN','Bản sao Vô Diện',.15)]:
 a=actor_match(source)
 c=dict(name=name,id='Summon_'+slug(source),boss=False,hp=a['hp']*hpf,atk=0,missing=True,notes='Bản triệu hồi: không có kỹ năng triệu hồi. ATK nhận từ chủ khi spawn; HP nhận theo recipe hoặc cần gán nếu Excel chưa nêu.',slots=[],phases=[],summon=True)
 if source=='Đàn Sói Sương Mù':c['hp']=0
 add(c,'Đánh thường','ACT_MELEE_1',priority=0);clones.append(c)
actors+=clones
# Reserve every meta before linking references.
for a in actors:
 a['profile']=profile_folder/(a['id']+'.asset')
 pf=folder('Assets/Prefabs/'+('Bosses' if a['boss'] else 'Enemies')+('/Summons' if a.get('summon') else ''))
 a['prefab']=pf/(a['id']+'.prefab');meta(a['profile']);meta(a['prefab'])
for a in actors:
 for d in a.get('entries',[]):
  action=d['actionId']
  if action in ('ACT_SUMMON_2_EVERY_20S','ACT_SUMMON_ADDS_CONTINUOUS','ACT_CLONE_SHADOW_2','ACT_SPLIT_2'):
   child=minion if action=='ACT_SUMMON_ADDS_CONTINUOUS' else clones[0] if action=='ACT_SUMMON_2_EVERY_20S' else clones[1] if action=='ACT_SPLIT_2' else clones[2]
   d['defaultContext'].setdefault('modules',{}).setdefault('summon',{})['prefab']=ref(child['prefab'],100000,3)
 # Distances not specified for non-hitbox effects use the shared serialized sensing radius.
 a['sense']=max([s['detectionSize']['x'] for s in a['slots']]+[2])
 for s in a['slots']:
  if not s['detectionSize']['x']:s['detectionSize']={'x':a['sense'],'y':a['sense']}
  if s['trigger']==1:s['castLockSeconds']=0
  if s['skillName']=='ACT_BUFF_SPEED_CONDITIONAL':s['castLockSeconds']=0
 ghost=a['name']=='Hồn Ma Nạn Nhân Cũ'
 missing_hp=a['hp']==0 and not ghost
 a['data']=so(a['id'],'Assets/Scripts/Combat/EnemyCombatConfig.cs',actorId=a['id'],displayName=a['name'],boss=int(a['boss']),maxHealth=a['hp'],attack=a['atk'],needsHealth=int(missing_hp),needsAttack=int(a['boss'] or a.get('missing',False)),invincible=int(ghost),setupNotes=str(a['notes'])+'\nCần gán CharacterAnimation/Unity Json Asset và ảnh skill cast/end. Chưa có art enemy/boss đã xác nhận trong dự án.\n'+('Excel thiếu ATK: gán Attack trước khi test chiêu gây damage.' if a['boss'] else '') ,phases=a['phases'],skills=a['slots'])
 asset(a['profile'],a['data'])
# Keep enemy skills out of the player's equip/debug-unlock lists.
enemy_sp=Path('Assets/Resources/EnemySkillConfig.asset')
enemy_data=copy.deepcopy(sk);enemy_data['m_Name']='EnemySkillConfig';enemy_data['animationVariants']=[]
enemy_data['skills']=[d for id,d in existing.items() if id.startswith(('Enemy_','Boss_','Summon_'))]
asset(enemy_sp,enemy_data)
sk['skills']=[d for id,d in existing.items() if not id.startswith(('Enemy_','Boss_','Summon_'))];asset(sp,sk)

# Use Unity's existing serialization templates for built-in components.
scene=Path('Assets/Scenes/Zones/CoreCombatDemo.unity').read_text(encoding='utf-8-sig')
docs={int(m[2]):(int(m[1]),yaml.safe_load(m[3])) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',scene)}
player_go=next(d['GameObject'] for t,d in docs.values() if t==1 and d['GameObject']['m_Name']=='Player')
templates={}
for c in player_go['m_Component']:
 typ,d=docs[c['component']['fileID']]
 if typ in (50,61,212):templates[typ]=copy.deepcopy(d)
# CircleCollider from another serialized scene object.
circle_scene=Path('Assets/Scenes/Zones/Zone1.unity').read_text(encoding='utf-8-sig')
circle=next(yaml.safe_load(m[1]) for m in re.finditer(r'(?ms)^--- !u!58 &-?\d+\n(.*?)(?=^--- !u!|\Z)',circle_scene))
templates[58]=circle

def go(id,name,components,tag='Untagged'):
 return {'GameObject':dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},serializedVersion=6,m_Component=[{'component':{'fileID':i}} for i in components],m_Layer=0,m_Name=name,m_TagString=tag,m_Icon={'fileID':0},m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=1)}
def tr(goid,parent,children):
 return {'Transform':dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject={'fileID':0},m_PrefabInstance={'fileID':0},m_PrefabAsset={'fileID':0},m_GameObject={'fileID':goid},serializedVersion=2,m_LocalRotation={'x':0,'y':0,'z':0,'w':1},m_LocalPosition={'x':0,'y':0,'z':0},m_LocalScale={'x':1,'y':1,'z':1},m_ConstrainProportionsScale=0,m_Children=[{'fileID':i} for i in children],m_Father={'fileID':parent},m_LocalEulerAnglesHint={'x':0,'y':0,'z':0})}
for a in actors:
 ds=[]
 def push(t,i,d):ds.append((t,i,d))
 push(1,100000,go(100000,a['id'],[400000,500000,610000,2120000,11400001,11400002,11400003,11400004,11400005,11400006,11400007,11400008]))
 push(4,400000,tr(100000,0,[400001,400002]))
 for t,i in [(50,500000),(61,610000),(212,2120000)]:
  d=copy.deepcopy(templates[t]);v=next(iter(d.values()));v['m_GameObject']={'fileID':100000}
  if t==212:v['m_Sprite']={'fileID':0};v['m_WasSpriteAssigned']=0
  push(t,i,d)
 components=[(11400001,'Assets/Scripts/Controller/HealthController.cs',dict(maxHealth=max(1,a['hp']),currentHealth=max(1,a['hp']),showHealthBar=0)),(11400002,'Assets/Scripts/Controller/EnemyController.cs',dict(moveSpeed=3,stopDistance=min([s['detectionSize']['x'] for s in a['slots'] if s['priority']==0]+[1]),characterAnimation={'fileID':11400003})),(11400003,'Assets/Scripts/Animation/CharacterAnimation.cs',dict(unityJsonAsset={'fileID':0},legacyUnityJson='',pixelsPerUnitOverride=0,useUnscaledTime=0,audioBindings=[],logAnimationWarnings=1)),(11400004,'Assets/Scripts/Combat/EnemySkillController.cs',dict(combatConfig=ref(a['profile']),skillConfig=ref(enemy_sp),skillSlots=[],ownerOverride={'fileID':100000})),(11400005,'Assets/Scripts/Combat/EnemySkillScheduler.cs',dict(skills={'fileID':11400004},movement={'fileID':11400002})),(11400006,'Assets/Scripts/Combat/EnemyPhaseController.cs',dict(skills={'fileID':11400004},health={'fileID':11400001},onPhaseChanged={'m_PersistentCalls':{'m_Calls':[]}})),(11400007,'Assets/Scripts/Combat/RuntimeStatModifierStore.cs',dict(modifiers=[])),(11400008,'Assets/Scripts/Combat/SkillBase/SelfBuff.cs',{})]
 for i,script,fields in components:push(114,i,{'MonoBehaviour':mono(100000,script,**fields)})
 push(1,100001,go(100001,'Hurtbox',[400001,610001],'HitBoxEnemy'));push(4,400001,tr(100001,400000,[]))
 d=copy.deepcopy(templates[61]);d['BoxCollider2D'].update(m_GameObject={'fileID':100001},m_IsTrigger=1);push(61,610001,d)
 push(1,100002,go(100002,'DetectionZone',[400002,580002,11400009]));push(4,400002,tr(100002,400000,[]))
 d=copy.deepcopy(templates[58]);v=d['CircleCollider2D'];v.update(m_GameObject={'fileID':100002},m_IsTrigger=1,m_Radius=a['sense'],m_Offset={'x':0,'y':0});push(58,580002,d)
 push(114,11400009,{'MonoBehaviour':mono(100002,'Assets/Scripts/AI/ZoneSee.cs',radius=a['sense'],enemyBody={'fileID':400000},enemyController={'fileID':11400002},showGizmo=1,gizmoColor={'r':0,'g':1,'b':0,'a':.25})})
 a['prefab'].write_text(HEADER+''.join('--- !u!'+str(t)+' &'+str(i)+'\n'+yaml.safe_dump(d,allow_unicode=True,sort_keys=False,width=150) for t,i,d in ds),encoding='utf-8')
manifest=[dict(actorId=a['id'],name=a['name'],profile=str(a['profile']),prefab=str(a['prefab']),skills=[s['skillId'] for s in a['slots']],summon=bool(a.get('summon'))) for a in actors]
Path('Validation/EnemySkills/manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported',len(levels['levels']),'levels;',len(player),'player skills;',len(existing),'total skill entries;',len(actors),'profiles/prefabs (3 summons).')

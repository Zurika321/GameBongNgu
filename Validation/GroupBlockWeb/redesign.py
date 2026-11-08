from pathlib import Path
import hashlib, re, json

folder = Path(r'C:\Users\NAM VO\OneDrive\1text code\link2\link\BoneAnimationStudio')
target = folder / 'BoneAnimation_Unity_GroupBlock_NewUpdate4.html'
source = target if target.exists() else folder / 'BoneAnimation_Unity.html'
original = source.read_bytes()
if b'/* GroupBlock Inspector layout */' in original:
    saved = json.loads(Path('Validation/GroupBlockWeb/redesign-source.json').read_text(encoding='utf-8'))
    assert hashlib.sha256(original).hexdigest() == saved['afterSha256'], 'HTML changed since last patch; inspect before updating.'
    original = Path('Validation/GroupBlockWeb/before-redesign.html').read_bytes()
s = original.decode('utf-8-sig').replace('\r\n', '\n')
validation = Path('Validation/GroupBlockWeb')
validation.joinpath('before-redesign.html').write_bytes(original)
new_ui = validation.joinpath('group-ui.js').read_text(encoding='utf-8')
start = s.index('function currentGroup(){')
end = s.index('function normalizeModel(m){', start)
s = s[:start] + new_ui + "\ndocument.addEventListener('dragend',()=>{treeDragPayload=null;document.querySelectorAll('.group-drop.drag-target').forEach(node=>node.classList.remove('drag-target'));});\n" + s[end:]
old = "function treeDragStart(e,type,id){treeDragPayload={type,id};e.dataTransfer.effectAllowed='move';e.dataTransfer.setData('text/plain',type+':'+id);}"
assert s.count(old) == 1
s = s.replace(old, "function treeDragStart(e,type,id){e.stopPropagation();treeDragPayload={type,id};e.dataTransfer.effectAllowed='move';e.dataTransfer.setData('text/plain',type+':'+id);if(type==='k')e.dataTransfer.setData('application/x-bone-block',String(id));}")
css = '''
/* GroupBlock Inspector layout */
#R>.side-tabs{position:sticky;top:-8px;z-index:6;background:var(--p);padding:5px 0}
.group-preview-list{position:sticky;top:35px;background:var(--p);z-index:4;padding:5px 0 10px;border-bottom:1px solid var(--l);max-height:240px;overflow:auto}
.group-preview-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(100px,1fr);gap:8px;align-items:center;margin:7px 0}
.group-preview-row label{font-weight:650;overflow-wrap:anywhere;color:var(--t)}.group-preview-row select{width:100%;min-height:34px}
.group-create{margin:14px 0}.group-create>label{display:block;color:var(--m);font-size:12px;margin-bottom:5px}
.group-inline{display:flex;gap:5px;margin:7px 0;align-items:center}.group-inline input{flex:1;width:0;min-width:0;min-height:32px}.group-inline button{flex:none;min-height:32px}
.group-editor-card{margin:12px 0;padding:10px;border:1px solid var(--l);border-radius:8px;background:#202630}
.group-card-head,.group-variant-title{display:flex;align-items:center;justify-content:space-between;gap:8px}.group-card-head strong,.group-variant-title strong{overflow-wrap:anywhere;min-width:0}.group-remove{font-size:11px;color:#ffb7ae}
.group-add-variant{padding-top:5px;border-top:1px solid var(--l)}.group-variant-card{padding:9px;margin:10px 0 0;border:1px solid #414b5a;border-radius:6px;background:#262e39}.group-variant-card.is-current{border-color:var(--a)}
.group-variant-title span{font-size:11px;color:var(--m);text-align:right}.group-drop{min-height:92px;border:2px dashed #66768c;border-radius:6px;margin:9px 0;padding:10px;background:#1b222c;transition:background .1s,border-color .1s}.group-drop.drag-target{background:#20463f;border-color:#65dfbe;box-shadow:inset 0 0 0 1px #65dfbe}.group-drop-label{font-size:12px;text-align:center;color:#b0c0d2;pointer-events:none;margin-bottom:5px}
.group-entry{white-space:normal;margin:5px 0;padding:5px;border:1px solid #46556a;border-radius:4px;background:#303b49}.group-entry span{min-width:0;overflow-wrap:anywhere}.group-entry small{color:var(--m)}.group-add-selected{width:100%;font-size:12px;min-height:34px}.group-help{font-size:12px;color:var(--m);line-height:1.5;margin:10px 0}.group-feedback{font-size:12px;line-height:1.5;color:#8ee2cb;overflow-wrap:anywhere}.group-feedback:empty{display:none}
'''
s = s.replace('</style>', css + '</style>', 1)
assert 'id="groupChoose"' not in s and 'id="groupVariantChoose"' not in s
target.write_text(s, encoding='utf-8')
validation.joinpath('redesigned-app.js').write_text(re.search(r'<script>([\s\S]*?)</script>', s).group(1), encoding='utf-8')
validation.joinpath('redesign-source.json').write_text(json.dumps({'path':str(target),'beforeSha256':hashlib.sha256(original).hexdigest(),'afterSha256':hashlib.sha256(target.read_bytes()).hexdigest(),'originalHtmlUntouched':str(folder/'BoneAnimation_Unity.html')},indent=2),encoding='utf-8')
print('Updated separate GroupBlock HTML; group dropdown removed, per-group preview rows and per-variant drop targets added.')

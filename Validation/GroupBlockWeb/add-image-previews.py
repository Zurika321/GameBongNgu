from pathlib import Path
import re

source = Path(r'C:\Users\NAM VO\OneDrive\1text code\link2\link\BoneAnimationStudio\BoneAnimation_Unity.html')
text = source.read_text(encoding='utf-8')
css = '''
/* Inline image previews for block properties and block keyframes. */
.property-image-preview{margin:5px 0 10px;padding:6px;border:1px solid var(--l);border-radius:5px;background:#191e26;min-width:0}
.property-image-preview img{display:block;width:100%;height:86px;object-fit:contain;image-rendering:pixelated;background:repeating-conic-gradient(#303642 0% 25%,#242a34 0% 50%) 0/16px 16px}
.property-image-preview figcaption{font-size:11px;color:var(--m);overflow-wrap:anywhere;margin-top:4px}
.block-image-choice-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:4px;margin:6px 0;max-height:260px;overflow:auto}
.block-image-choice{padding:4px;min-width:0;text-align:center}.block-image-choice.selected{border-color:var(--a);background:#245b58}
.block-image-choice img{display:block;width:100%;height:50px;object-fit:contain;image-rendering:pixelated;background:#171c24}
.block-image-choice span{display:block;font-size:10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
'''
assert '/* Inline image previews for block properties' not in text
text = text.replace('</style>', css + '</style>', 1)
helper = '''function renderPropertyImagePreview(name,caption=''){
 const src=imageSrc(name);
 return `<figure class="property-image-preview">${src?`<img src="${esc(src)}" alt="${esc(name)}" loading="lazy">`:'<div class="status">'+(name?'Không tìm thấy dữ liệu ảnh':'Chưa gán ảnh')+'</div>'}<figcaption>${esc(caption)}${caption&&name?' · ':''}${esc(name||'')}</figcaption></figure>`;
}
function renderBlockKeyImagePicker(block,item,f,index){
 const choices=[{name:'',label:'Ảnh gốc theo hướng',image:imageForDirection(block)},...LIB.map(name=>({name,label:name,image:name}))];
 return renderPropertyImagePreview(item.image||imageForDirection(block),item.image?'Ảnh thay thế':'Ảnh gốc theo hướng')+
 `<details class="block-image-picker"><summary>Chọn ảnh bằng hình (${LIB.length} ảnh)</summary><div class="block-image-choice-grid">${choices.map(choice=>`<button type="button" class="block-image-choice ${(item.image||'')===choice.name?'selected':''}" title="${esc(choice.label)}" data-frame="${esc(f)}" data-index="${index}" data-image="${esc(choice.name)}" onclick="updateBlockKeyItem(+this.dataset.frame,+this.dataset.index,'image',this.dataset.image)">${imageSrc(choice.image)?`<img src="${esc(imageSrc(choice.image))}" alt="${esc(choice.label)}" loading="lazy">`:'<span>Không có ảnh</span>'}<span>${esc(choice.label)}</span></button>`).join('')}</div></details>`;
}
'''
text = text.replace('function renderBlockKeyframeProperties(){', helper + 'function renderBlockKeyframeProperties(){', 1)
needle = '<button class="danger" onclick="removeBlockKeyItem(${f},${index})">'
assert text.count(needle) == 1
text = text.replace(needle, '    ${renderBlockKeyImagePicker(block,item,f,index)}\n' + needle, 1)
old = next(line for line in text.splitlines() if "+['down','up','left','right'].map(dir=>" in line and 'openLibraryForDirection' in line)
new = '''   +['down','up','left','right'].map(dir=>{const key=({down:'img',up:'imgUp',left:'imgLeft',right:'imgRight'})[dir],label=({down:'Down (mặt trước)',up:'Up (mặt sau)',left:'Left (trái)',right:'Right (phải)'})[dir],assigned=o[key]||'',effective=imageForDirection(o,dir);return `<div class="row"><label>${label}</label><button data-direction="${dir}" data-image="${esc(assigned)}" onclick="openLibraryForDirection(this.dataset.direction,this.dataset.image)">Chọn / đổi ảnh</button></div>${renderPropertyImagePreview(effective,!assigned&&effective?'Dùng ảnh Down':label)}`}).join('')'''
text = text.replace(old, new, 1)
output = Path(__file__).with_name('image-previews.html')
output.write_text(text, encoding='utf-8')
scripts = re.findall(r'<script\b[^>]*>([\s\S]*?)</script>',text,re.I)
Path(__file__).with_name('image-previews-app.js').write_text('\n'.join(scripts),encoding='utf-8')
print('Prepared preview update:', output)

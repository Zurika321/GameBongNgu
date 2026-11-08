/* GroupBlock editor: one preview row per group, directly editable variant cards. */
function currentGroup(){return M.groupBlocks?.[selectedGroupIndex]||null;}
function groupNameTaken(name,except=-1){return M.groupBlocks.some((g,i)=>i!==except&&g.name.toLocaleLowerCase()===name.toLocaleLowerCase());}
function groupMessage(text){const node=$('groupStatus');if(node)node.textContent=text;}
function groupRefresh(text=''){renderGroupBlock();renderList();draw();refreshSpriteVariantOptions();groupMessage(text);}
function renderGroupBlock(){
 const host=$('groupBlockPanel');if(!host)return;
 const groups=M.groupBlocks||[];selectedGroupIndex=Math.max(0,Math.min(selectedGroupIndex,groups.length-1));
 host.innerHTML=`<div class="group-preview-list">${groups.map((g,gi)=>`<div class="group-preview-row"><label for="groupPreview${gi}" title="${esc(g.name)}">group-${esc(g.name)}</label><select id="groupPreview${gi}" aria-label="Biến thể ${esc(g.name)}" onchange="selectGroupBlockVariantAt(${gi},this.value)"><option value="" ${g.defaultVariant===''?'selected':''}>None / không chọn</option>${g.variants.map(v=>`<option value="${esc(v.name)}" ${v.name===g.defaultVariant?'selected':''}>${esc(v.name)}</option>`).join('')}</select></div>`).join('')}</div>
 <div class="group-create"><label for="groupNameInput">Tạo group mới</label><div class="group-inline"><input id="groupNameInput" placeholder="Ví dụ: Weapon, Outfit"><button type="button" onclick="addGroupBlock()">＋ Tạo</button></div></div>
 <div id="groupStatus" class="group-feedback" role="status" aria-live="polite"></div>
 ${groups.length?'':'<p class="group-help">Tạo group → thêm biến thể → kéo block từ Skeleton vào ô của biến thể.</p>'}
 ${groups.map((g,gi)=>`<section class="group-editor-card"><div class="group-card-head"><strong>group-${esc(g.name)}</strong><button type="button" class="group-remove" onclick="removeGroupBlock(${gi})" title="Xóa group, giữ block gốc">Xóa group</button></div>
 <div class="group-inline"><input id="groupRename${gi}" aria-label="Tên group ${esc(g.name)}" value="${esc(g.name)}"><button type="button" onclick="renameGroupBlock(${gi})">Đổi tên</button></div>
 <div class="group-inline group-add-variant"><input id="groupVariantInput${gi}" aria-label="Biến thể mới của ${esc(g.name)}" placeholder="Tên biến thể, ví dụ Kiếm"><button type="button" onclick="addGroupBlockVariant(${gi})">＋ Biến thể</button></div>
 ${g.variants.length?'':'<p class="group-help">Thêm một biến thể để có ô nhận block.</p>'}
 ${g.variants.map((v,vi)=>`<div class="group-variant-card ${g.defaultVariant===v.name?'is-current':''}"><div class="group-variant-title"><strong>${esc(v.name)}</strong><span>${v.blockIds.length} block${g.defaultVariant===v.name?' · đang hiện':''}</span></div>
 <div class="group-inline"><input id="variantRename${gi}_${vi}" aria-label="Tên biến thể ${esc(v.name)}" value="${esc(v.name)}"><button type="button" onclick="renameGroupBlockVariant(${gi},${vi})">Đổi tên</button><button type="button" onclick="removeGroupBlockVariant(${gi},${vi})" aria-label="Xóa biến thể ${esc(v.name)}">×</button></div>
 <div class="group-drop" data-group-index="${gi}" data-variant-index="${vi}" ondragover="groupDragOver(event)" ondragleave="groupDragLeave(event)" ondrop="groupDropBlock(event,${gi},${vi})"><div class="group-drop-label">↓ Kéo block từ Skeleton vào ${esc(v.name)}</div>
 ${v.blockIds.map(id=>{const k=M.blocks.find(b=>b.id===id);return `<div class="group-entry"><span>▣ ${esc(k?.name||'Block #'+id)} <small>#${id}${k?'':' · hướng khác'}</small></span><button type="button" onclick="removeGroupBlockMember(${id},${gi},${vi})" aria-label="Bỏ block ${id}">×</button></div>`;}).join('')}</div>
 <button type="button" class="group-add-selected" onclick="addSelectedBlockToGroup(${gi},${vi})">＋ Thêm block đang chọn</button></div>`).join('')}</section>`).join('')}
 <p class="group-help">Mỗi block thuộc một biến thể. Kéo sang ô khác sẽ chuyển liên kết, không xóa block. Chọn None ở hàng trên để bỏ vũ khí/trang phục của riêng group đó. Có thể Undo.</p>`;
}
function addGroupBlock(){const name=($('groupNameInput')?.value||'').trim();if(!name)return groupMessage('Nhập tên group mới.');if(groupNameTaken(name))return groupMessage('Tên group đã tồn tại.');checkpointUndo();M.groupBlocks.push({name,defaultVariant:'',variants:[]});selectedGroupIndex=M.groupBlocks.length-1;groupRefresh('Đã tạo group-'+name+'. Thêm biến thể ở khung bên dưới.');$('groupVariantInput'+selectedGroupIndex)?.focus();}
function renameGroupBlock(gi=selectedGroupIndex){const g=M.groupBlocks[gi],name=($('groupRename'+gi)?.value||'').trim();if(!g||!name)return groupMessage('Tên group không được để trống.');if(groupNameTaken(name,gi))return groupMessage('Tên group đã tồn tại.');checkpointUndo();g.name=name;groupRefresh('Đã đổi tên group.');}
function removeGroupBlock(gi=selectedGroupIndex){if(!M.groupBlocks[gi])return;checkpointUndo();M.groupBlocks.splice(gi,1);selectedGroupIndex=Math.max(0,Math.min(selectedGroupIndex,M.groupBlocks.length-1));groupRefresh('Đã bỏ group; block gốc vẫn ở Skeleton.');}
function selectGroupBlockVariantAt(gi,name){const g=M.groupBlocks[gi];if(!g||(name&&!g.variants.some(v=>v.name===name)))return;checkpointUndo();g.defaultVariant=name;selectedGroupIndex=gi;groupRefresh('group-'+g.name+': '+(name||'None / không chọn'));}
function selectGroupBlockVariant(name){selectGroupBlockVariantAt(selectedGroupIndex,name);}
function addGroupBlockVariant(gi=selectedGroupIndex){const g=M.groupBlocks[gi],name=($('groupVariantInput'+gi)?.value||'').trim();if(!g||!name)return groupMessage('Nhập tên biến thể trước.');if(g.variants.some(v=>v.name.toLocaleLowerCase()===name.toLocaleLowerCase()))return groupMessage('Tên biến thể đã tồn tại trong group.');checkpointUndo();g.variants.push({name,blockIds:[]});g.defaultVariant=name;selectedGroupIndex=gi;groupRefresh('Đã tạo '+name+'. Kéo block vào đúng ô của bộ này.');}
function renameGroupBlockVariant(gi,vi){const g=M.groupBlocks[gi],v=g?.variants[vi],name=($('variantRename'+gi+'_'+vi)?.value||'').trim();if(!v||!name)return groupMessage('Tên biến thể không được để trống.');if(g.variants.some(x=>x!==v&&x.name.toLocaleLowerCase()===name.toLocaleLowerCase()))return groupMessage('Tên biến thể đã tồn tại.');checkpointUndo();if(g.defaultVariant===v.name)g.defaultVariant=name;v.name=name;groupRefresh('Đã đổi tên biến thể.');}
function removeGroupBlockVariant(gi,vi){const g=M.groupBlocks[gi],v=g?.variants[vi];if(!v)return;checkpointUndo();if(g.defaultVariant===v.name)g.defaultVariant='';g.variants.splice(vi,1);groupRefresh('Đã bỏ biến thể; không xóa block gốc.');}
function groupTargetVariant(gi,vi){const g=M.groupBlocks[gi];return {g,v:Number.isInteger(vi)?g?.variants[vi]:g?.variants.find(v=>v.name===g.defaultVariant)};}
function addGroupBlockMember(id,gi=selectedGroupIndex,vi=null){
 id=+id;const {g,v}=groupTargetVariant(gi,vi);if(!g||!v)return groupMessage('Tạo biến thể rồi thả vào ô của bộ đó.');const block=M.blocks.find(k=>k.id===id);if(!block)return groupMessage('Block không có ở hướng hiện tại.');if(v.blockIds.includes(id))return groupMessage('Block này đã nằm trong '+v.name+'.');
 checkpointUndo();let moved=false;for(const owner of M.groupBlocks)for(const variant of owner.variants){if(variant.blockIds.includes(id))moved=true;variant.blockIds=variant.blockIds.filter(n=>n!==id);}v.blockIds.push(id);selectedGroupIndex=gi;groupRefresh((moved?'Đã chuyển ':'Đã thêm ')+block.name+' → '+g.name+' / '+v.name+'.');
}
function removeGroupBlockMember(id,gi=selectedGroupIndex,vi=null){const {v}=groupTargetVariant(gi,vi);if(!v)return;checkpointUndo();v.blockIds=v.blockIds.filter(n=>n!==+id);groupRefresh('Đã bỏ liên kết; block gốc vẫn còn.');}
function addSelectedBlockToGroup(gi=selectedGroupIndex,vi=null){if(sel?.t!=='k')return groupMessage('Chọn một block ở Skeleton bên trái, rồi bấm nút này.');addGroupBlockMember(sel.id,gi,vi);}
function groupDragOver(e){
 const types=Array.from(e.dataTransfer?.types||[]),isBlock=treeDragPayload?.type==='k'||types.includes('application/x-bone-block');
 if(!isBlock)return;e.preventDefault();e.stopPropagation();e.currentTarget.classList.add('drag-target');if(e.dataTransfer)e.dataTransfer.dropEffect='move';
}
function groupDragLeave(e){if(!e.currentTarget.contains(e.relatedTarget))e.currentTarget.classList.remove('drag-target');}
function groupDropBlock(e,gi=selectedGroupIndex,vi=null){
 e.preventDefault();e.stopPropagation();e.currentTarget.classList.remove('drag-target');const dt=e.dataTransfer,raw=dt?.getData('application/x-bone-block')||dt?.getData('text/plain')||'',match=/^(?:k:)?(\d+)$/.exec(raw),payload=treeDragPayload;treeDragPayload=null;
 const id=payload?.type==='k'?+payload.id:match?+match[1]:null;if(Number.isInteger(id))addGroupBlockMember(id,gi,vi);else groupMessage('Hãy kéo một Block từ Skeleton, không phải Bone hoặc ảnh.');
}

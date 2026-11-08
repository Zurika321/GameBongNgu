const fs=require('fs'),path=require('path'),{spawn}=require('child_process'),{pathToFileURL}=require('url');
const base=__dirname, profile=path.join(base,'browser-profile-'+Date.now());fs.mkdirSync(profile,{recursive:true});
const browser=spawn('C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--disable-background-networking','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
let ws,seq=0;const pending=new Map(),events=[];let checks=0;
function assert(ok,label){if(!ok)throw Error(label);checks++;console.log('PASS '+label);}
async function rpc(method,params={}){return new Promise((resolve,reject)=>{let id=++seq;const t=setTimeout(()=>{pending.delete(id);reject(Error('CDP timeout '+method));},15000);pending.set(id,{resolve:v=>{clearTimeout(t);resolve(v)},reject});ws.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await rpc('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value;}
async function bounds(selector){return evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2};})()`);}
async function drag(source,destination){
 const from=await bounds(source),to=await bounds(destination);let intercepted;
 const onEvent=msg=>{if(msg.method==='Input.dragIntercepted')intercepted=msg.params.data;};events.push(onEvent);
 await rpc('Input.setInterceptDrags',{enabled:true});
 await rpc('Input.dispatchMouseEvent',{type:'mouseMoved',x:from.x,y:from.y});
 await rpc('Input.dispatchMouseEvent',{type:'mousePressed',x:from.x,y:from.y,button:'left',clickCount:1});
 await rpc('Input.dispatchMouseEvent',{type:'mouseMoved',x:from.x+25,y:from.y+2,button:'left',buttons:1});
 for(let i=0;i<30&&!intercepted;i++)await sleep(30);
 if(!intercepted)throw Error('Native drag did not start');
 for(const type of ['dragEnter','dragOver','drop'])await rpc('Input.dispatchDragEvent',{type,x:to.x,y:to.y,data:intercepted});
 await rpc('Input.dispatchMouseEvent',{type:'mouseReleased',x:to.x,y:to.y,button:'left',clickCount:1});
 events.splice(events.indexOf(onEvent),1);await rpc('Input.setInterceptDrags',{enabled:false});await sleep(40);
 return intercepted.dragOperationsMask;
}
(async()=>{
 try{
  const portFile=path.join(profile,'DevToolsActivePort');for(let i=0;i<100&&!fs.existsSync(portFile);i++)await sleep(100);
  if(!fs.existsSync(portFile))throw Error('Chrome did not start');const port=fs.readFileSync(portFile,'utf8').split('\n')[0];
  const tabs=await(await fetch('http://127.0.0.1:'+port+'/json/list')).json();ws=new WebSocket(tabs[0].webSocketDebuggerUrl);
  await new Promise((r,j)=>{ws.addEventListener('open',r,{once:true});ws.addEventListener('error',j,{once:true});});
  ws.addEventListener('message',e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(p){pending.delete(m.id);m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}}else for(const f of [...events])f(m);});
  const errors=[];events.push(m=>{if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.exception?.description||m.params.exceptionDetails.text);});
  await rpc('Runtime.enable');await rpc('Page.enable');await rpc('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
  const file='C:\\Users\\NAM VO\\OneDrive\\1text code\\link2\\link\\BoneAnimationStudio\\BoneAnimation_Unity_GroupBlock_NewUpdate4.html';
  await rpc('Page.navigate',{url:pathToFileURL(file).href});
  let ready=false;for(let i=0;i<100;i++){ready=await evaluate("typeof M!=='undefined'&&typeof renderGroupBlock==='function'&&document.readyState==='complete'");if(ready)break;await sleep(100);}assert(ready,'HTML boots in Chrome');
  await evaluate(`(()=>{M=normalizeModel({nid:104,dir:'down',bones:[{id:1,name:'hand',p:0,x:0,y:0,a:0,l:60}],blocks:[101,102,103].map((id,i)=>({id,name:['SwordBlock','AxeBlock','OutfitBlock'][i],b:1,s:0,e:1,h:15,visible:true,demo:false,col:'#4aaabb'})),groupBlocks:[]});mode='rig';sel=null;showRightTab('group');renderAll();draw();})()`);
  await evaluate("$('groupNameInput').value='Weapon';addGroupBlock();$('groupVariantInput0').value='Sword';addGroupBlockVariant(0);$('groupVariantInput0').value='Axe';addGroupBlockVariant(0);");
  assert(await evaluate("document.querySelector('#groupChoose')===null&&document.querySelectorAll('.group-preview-row').length===1&&document.querySelector('.group-preview-row label').textContent==='group-Weapon'"),'group name left, variant selector right; old group selector removed');
  assert(await evaluate("document.querySelectorAll('.group-drop').length===2"),'every variant has its own drop target');
  await evaluate("selectGroupBlockVariantAt(0,'');");
  const mask=await drag('.tree-row[data-type="k"][data-id="101"]','.group-drop[data-group-index="0"][data-variant-index="0"]');
  assert(mask===16,'native Skeleton drag uses move operation');
  assert(await evaluate("M.groupBlocks[0].variants[0].blockIds.includes(101)"),'actual Chrome drag/drop adds block while preview is None');
  await drag('.tree-row[data-type="k"][data-id="102"]','.group-drop[data-group-index="0"][data-variant-index="1"]');
  assert(await evaluate("M.groupBlocks[0].variants[1].blockIds.includes(102)"),'actual Chrome drag/drop adds another variant');
  await evaluate("selectGroupBlockVariantAt(0,'Sword');");
  assert(await evaluate("groupBlockAllows(101)&&!groupBlockAllows(102)&&groupBlockAllows(103)"),'variant selection filters blocks without hiding ungrouped body');
  await evaluate("$('groupNameInput').value='Outfit';addGroupBlock();$('groupVariantInput1').value='Tunic';addGroupBlockVariant(1);sel={t:'k',id:103};addSelectedBlockToGroup(1,0);selectGroupBlockVariantAt(0,'');");
  assert(await evaluate("!groupBlockAllows(101)&&!groupBlockAllows(102)&&groupBlockAllows(103)"),'None hides only Weapon; independent Outfit remains');
  await evaluate("addGroupBlockMember(101,0,1);");assert(await evaluate("!M.groupBlocks[0].variants[0].blockIds.includes(101)&&M.groupBlocks[0].variants[1].blockIds.includes(101)"),'moving membership keeps block exclusive');
  await evaluate("undoAction();");assert(await evaluate("M.groupBlocks[0].variants[0].blockIds.includes(101)&&!M.groupBlocks[0].variants[1].blockIds.includes(101)"),'Undo restores moved membership');
  await evaluate("$('groupRename0').value='Weapons';renameGroupBlock(0);$('variantRename0_0').value='Blade';renameGroupBlockVariant(0,0);selectGroupBlockVariantAt(0,'Blade');");
  assert(await evaluate("M.groupBlocks[0].name==='Weapons'&&M.groupBlocks[0].defaultVariant==='Blade'"),'rename keeps selection and membership');
  const payload=await evaluate("(()=>{const pack=modelPackage(),unity=unityBuildPayload();return{groups:pack.model.groupBlocks,exportGroups:unity.groupBlocks,blocks:unity.directions[0].blocks.map(b=>b.id)};})()");
  assert(payload.groups.length===2&&payload.exportGroups.length===2&&payload.blocks.includes(101)&&payload.blocks.includes(102),'web save and Unity export preserve groups and hidden blocks');
  await evaluate("globalThis.testGroupPack=modelPackage();loadPackage(testGroupPack);");
  assert(await evaluate("M.groupBlocks[0].name==='Weapons'&&M.groupBlocks[0].variants[0].blockIds.includes(101)"),'project round trip retains group schema');
  await evaluate("removeGroupBlockVariant(0,0);");assert(await evaluate("M.blocks.some(b=>b.id===101)"),'removing a variant retains original block');
  // Plain text fallback without the in-memory payload also works.
  await evaluate(`(()=>{treeDragPayload=null;const dt=new DataTransfer();dt.setData('text/plain','k:101');const zone=document.querySelector('.group-drop[data-group-index="0"]');zone.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));})()`);
  assert(await evaluate("M.groupBlocks[0].variants[0].blockIds.includes(101)"),'text/plain fallback accepts valid block ID');
  const n=await evaluate("M.groupBlocks[0].variants[0].blockIds.length");
  await evaluate(`(()=>{treeDragPayload={type:'b',id:1};const dt=new DataTransfer();dt.setData('text/plain','b:1');document.querySelector('.group-drop').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));})()`);
  assert(await evaluate("M.groupBlocks[0].variants[0].blockIds.length")==n,'bone drag cannot be assigned as a block');
  await evaluate("showRightTab('properties');");assert(await evaluate("!$('rightPropertiesPane').hidden&&$('rightGroupPane').hidden"),'Properties tab remains available');
  await evaluate("showRightTab('group');");
  const shot=await rpc('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(base,'groupblock-redesign.png'),Buffer.from(shot.data,'base64'));
  assert(errors.length===0,'no uncaught page errors');
  fs.writeFileSync(path.join(base,'browser-result.json'),JSON.stringify({checks,errors,nativeDrag:true,file},null,2));console.log('COMPLETE '+checks+' checks');
 }catch(error){console.error(error.stack);process.exitCode=1;}
 finally{if(ws){try{await rpc('Browser.close');}catch{}ws.close();}browser.kill();}
})();

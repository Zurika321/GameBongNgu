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

  const file=path.join(base,'image-previews.html');
  await rpc('Page.navigate',{url:pathToFileURL(file).href});
  for(let i=0;i<100;i++){if(await evaluate("typeof M!=='undefined'&&document.readyState==='complete'"))break;await sleep(100);}
  assert(await evaluate("typeof renderPropertyImagePreview==='function'"),'preview HTML boots');
  await evaluate(`(()=>{const png='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==';LIB=['body.png',"weapon's.png"];for(const n of LIB)imageData[n]=png;M=normalizeModel({nid:104,dir:'down',bones:[{id:1,name:'hand',p:0,x:0,y:0,a:0,l:60}],blocks:[{id:101,name:'Block',b:1,s:0,e:1,h:15,img:'body.png',imgUp:"weapon's.png",demo:true}],groupBlocks:[]});mode='rig';sel={t:'k',id:101};renderAll();renderProps();})()`);
  assert(await evaluate("document.querySelectorAll('#props .property-image-preview img').length===4"),'four direction previews including fallback');
  assert(await evaluate("document.querySelectorAll('#props .property-image-preview')[2].textContent.includes('Dùng ảnh Down')"),'unassigned direction identifies fallback');
  assert(await evaluate("document.querySelectorAll('#props .property-image-preview')[1].textContent.includes(\"weapon's.png\")"),'quoted filename rendered safely');
  await evaluate("mode='anim';frame=0;blockKeyMap()['0']=[{blockId:101,visible:true,image:''}];renderProps();");
  assert(await evaluate("document.querySelector('.block-key-item .property-image-preview figcaption').textContent.includes('Ảnh gốc')"),'keyframe default image preview');
  assert(await evaluate("document.querySelectorAll('.block-image-choice').length===3"),'thumbnail choices include original and every asset');
  await evaluate("document.querySelectorAll('.block-image-choice')[2].click();");
  assert(await evaluate("blockKeyMap()['0'][0].image===\"weapon's.png\""),'thumbnail click stores exact filename');
  assert(await evaluate("document.querySelector('.block-key-item .property-image-preview figcaption').textContent.includes(\"weapon's.png\")"),'selected preview refreshes');
  await evaluate("document.querySelector('.block-image-choice').click();");
  assert(await evaluate("blockKeyMap()['0'][0].image===''"),'original thumbnail resets override');
  await evaluate("updateBlockKeyItem(0,0,'image',\"weapon's.png\");");
  assert(await evaluate("document.querySelector('.block-key-item .property-image-preview figcaption').textContent.includes(\"weapon's.png\")"),'select handler updates preview too');
  assert(await evaluate("!renderPropertyImagePreview('missing.png').includes('<img')"),'missing image has clear placeholder');
  assert(errors.length===0,'no uncaught browser errors');
  console.log('COMPLETE '+checks+' preview checks');
 }catch(error){console.error(error.stack);process.exitCode=1;}
 finally{if(ws){try{await rpc('Browser.close');}catch{}ws.close();}browser.kill();}
})();

import * as THREE from 'three';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';

const $=id=>document.getElementById(id);
const status=message=>$('status').textContent=message;
const clock=new THREE.Clock();
const loader=new GLTFLoader();
let comparing=false, syncing=false, playing=false, time=0, duration=0;
const views={};

function createView(id){
  const el=$('view'+id),scene=new THREE.Scene();
  scene.background=new THREE.Color(0x272d36);
  const camera=new THREE.PerspectiveCamera(42,1,.01,1000);
  camera.position.set(4,3,5);
  const renderer=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});
  renderer.setPixelRatio(Math.min(devicePixelRatio,2));
  renderer.outputColorSpace=THREE.SRGBColorSpace;
  renderer.toneMapping=THREE.ACESFilmicToneMapping;
  el.prepend(renderer.domElement);
  const controls=new OrbitControls(camera,renderer.domElement);
  controls.target.set(0,1,0);controls.update();
  scene.add(new THREE.HemisphereLight(0xffffff,0x526078,2.2));
  const key=new THREE.DirectionalLight(0xfff1e0,3);key.position.set(5,8,4);scene.add(key);
  const fill=new THREE.DirectionalLight(0xc6dcff,1.5);fill.position.set(-5,4,-4);scene.add(fill);
  const grid=new THREE.GridHelper(20,20,0x626b79,0x404753);scene.add(grid);
  const v={id,el,scene,camera,renderer,controls,grid,root:null,mixer:null,action:null,clips:[],helpers:[],materials:new Set(),serial:0,clip:$('clip'+id)};
  controls.addEventListener('change',()=>{if(comparing&&!syncing){syncing=true;const other=views[id==='A'?'B':'A'];if(other)copyCamera(v,other);syncing=false;}});
  new ResizeObserver(()=>{const w=el.clientWidth,h=el.clientHeight;if(w&&h){renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();}}).observe(el);
  el.addEventListener('dragover',e=>e.preventDefault());
  el.addEventListener('drop',e=>{e.preventDefault();const file=e.dataTransfer.files[0];if(file)loadLocal(v,file);});
  v.clip.addEventListener('change',()=>chooseClip(v,Number(v.clip.value)));
  return v;
}
function copyCamera(a,b){b.camera.position.copy(a.camera.position);b.camera.up.copy(a.camera.up);b.camera.near=a.camera.near;b.camera.far=a.camera.far;b.camera.updateProjectionMatrix();b.controls.target.copy(a.controls.target);b.controls.update();}
function disposeRoot(root){
  const geometries=new Set(),materials=new Set(),textures=new Set();
  root.traverse(o=>{if(o.geometry)geometries.add(o.geometry);for(const m of (Array.isArray(o.material)?o.material:o.material?[o.material]:[])){materials.add(m);for(const value of Object.values(m))if(value?.isTexture)textures.add(value);}});
  geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());textures.forEach(t=>t.dispose());
}
function clearView(v){
  if(v.mixer&&v.root){v.mixer.stopAllAction();v.mixer.uncacheRoot(v.root);}
  for(const h of v.helpers){v.scene.remove(h);h.geometry.dispose();h.material.dispose();}
  v.helpers=[];v.materials.clear();
  if(v.root){v.scene.remove(v.root);disposeRoot(v.root);}
  v.root=null;v.mixer=null;v.action=null;v.clips=[];
}
async function load(v,source,label){
  const serial=++v.serial;status('Carregando '+label+'…');
  try{
    const gltf=typeof source==='string'?await loader.loadAsync(source):await loader.parseAsync(await source.arrayBuffer(),'');
    if(serial!==v.serial){disposeRoot(gltf.scene);return;}
    clearView(v);v.root=gltf.scene;v.scene.add(v.root);v.clips=gltf.animations;v.mixer=new THREE.AnimationMixer(v.root);
    let meshes=0,vertices=0,bones=0;
    v.root.traverse(o=>{if(o.isMesh){meshes++;vertices+=o.geometry.attributes.position?.count||0;for(const m of (Array.isArray(o.material)?o.material:[o.material]))if(m)v.materials.add(m);}if(o.isBone)bones++;});
    if(bones){const helper=new THREE.SkeletonHelper(v.root);helper.visible=$('bones').checked;v.scene.add(helper);v.helpers.push(helper);}
    $('title'+v.id).textContent=v.id+' · '+label;
    $('info'+v.id).textContent=meshes+' meshes · '+vertices.toLocaleString('pt-BR')+' vértices · '+bones+' ossos · '+v.clips.length+' clipes';
    v.el.querySelector('.empty').hidden=true;
    v.clip.replaceChildren(new Option(v.clips.length?'Pose base / sem clipe':'Sem clipes','-1'));
    v.clips.forEach((c,i)=>v.clip.add(new Option(c.name||'Clipe '+(i+1),String(i))));
    v.clip.disabled=!v.clips.length;
    chooseClip(v,v.clips.length?0:-1);
    applyDisplay();fit('perspective');status('Carregado: '+label);
  }catch(e){if(serial===v.serial)status('Falha ao abrir '+label+': '+e.message);}
}
function loadLocal(v,file){if(!file.name.toLowerCase().endsWith('.glb'))return status('Use um GLB autocontido exportado do Blender.');load(v,file,file.name);}
function chooseClip(v,index){
  v.mixer?.stopAllAction();v.action=null;v.clip.value=String(index);
  if(v.clips[index]){v.action=v.mixer.clipAction(v.clips[index]);v.action.setLoop(THREE.LoopOnce,1);v.action.clampWhenFinished=true;v.action.play();}
  playing=false;time=0;updateDuration();seek(0);
}
function activeViews(){return comparing?[views.A,views.B]:[views.A];}
function updateDuration(){duration=Math.max(0,...activeViews().map(v=>v.action?.getClip().duration||0));$('seek').max=duration||1;$('seek').disabled=!duration;$('play').disabled=!duration;if(!duration)playing=false;updatePlay();}
function updatePlay(){$('play').textContent=playing?'Pausar':'Reproduzir';}
function seek(t){
  time=Math.max(0,Math.min(t,duration));
  for(const v of activeViews())if(v.action){
    const end=v.action.getClip().duration;
    v.action.reset().play();v.mixer.setTime(Math.min(time,Math.max(0,end-0.000001)));
  }
  $('seek').value=time;$('time').textContent=time.toFixed(2)+' / '+duration.toFixed(2)+' s';
}
function fit(kind){
  const box=new THREE.Box3();
  for(const v of activeViews())if(v.root){v.root.updateMatrixWorld(true);box.union(new THREE.Box3().setFromObject(v.root));}
  if(box.isEmpty())return;
  const center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3());
  const radius=Math.max(.1,size.length()/2),v=views.A;
  const dirs={perspective:[1,.65,1],front:[0,0,1],side:[1,0,0],back:[0,0,-1],top:[0,1,.0001]};
  const dir=new THREE.Vector3(...(dirs[kind]||dirs.perspective)).normalize();
  const minAspect=Math.min(...activeViews().map(w=>w.camera.aspect||1));
  const half=Math.min(THREE.MathUtils.degToRad(v.camera.fov/2),Math.atan(Math.tan(THREE.MathUtils.degToRad(v.camera.fov/2))*minAspect));
  const distance=radius/Math.sin(half)*1.15;
  syncing=true;
  v.controls.target.copy(center);v.camera.position.copy(center).addScaledVector(dir,distance);
  v.camera.near=Math.max(.001,radius/1000);v.camera.far=Math.max(100,radius*100);v.camera.updateProjectionMatrix();v.controls.update();
  if(comparing)copyCamera(v,views.B);syncing=false;
}
function applyDisplay(){for(const v of Object.values(views)){v.grid.visible=$('grid').checked;v.helpers.forEach(h=>h.visible=$('bones').checked);v.materials.forEach(m=>{if('wireframe'in m)m.wireframe=$('wire').checked;});}}
function toggleCompare(force){comparing=typeof force==='boolean'?force:!comparing;$('panelB').hidden=!comparing;$('fileBLabel').hidden=!comparing;$('panels').classList.toggle('comparing',comparing);$('compare').setAttribute('aria-pressed',String(comparing));if(comparing)copyCamera(views.A,views.B);updateDuration();seek(Math.min(time,duration));}
async function refresh(){
  try{const response=await fetch('/api/previews',{cache:'no-store'});if(!response.ok)throw Error('HTTP '+response.status);const items=await response.json();const list=$('library');list.replaceChildren();
    if(!items.length){const p=document.createElement('p');p.textContent='Nenhum preview publicado. Use studio.py publish ou abra um GLB.';list.append(p);}
    for(const item of items){const card=document.createElement('div');card.className='entry';const name=document.createElement('strong');name.textContent=item.name;const meta=document.createElement('small');meta.textContent=item.mode+' · '+(item.frame??'—')+' · '+item.createdAt;card.append(name,meta);
      for(const id of ['A','B']){const b=document.createElement('button');b.textContent='Abrir '+id;b.onclick=()=>{if(id==='B')toggleCompare(true);load(views[id],item.url,item.name);};card.append(b);}list.append(card);}
  }catch(e){status('Biblioteca indisponível: '+e.message);}
}
try{
  views.A=createView('A');views.B=createView('B');
  $('compare').onclick=()=>toggleCompare();$('refresh').onclick=refresh;
  for(const id of ['A','B'])$('file'+id).onchange=e=>{if(e.target.files[0])loadLocal(views[id],e.target.files[0]);e.target.value='';};
  for(const b of document.querySelectorAll('[data-view]'))b.onclick=()=>fit(b.dataset.view);
  $('fit').onclick=()=>fit('perspective');for(const id of ['grid','bones','wire'])$(id).onchange=applyDisplay;
  $('play').onclick=()=>{if(!duration)return;if(time>=duration)seek(0);playing=!playing;updatePlay();};
  $('reset').onclick=()=>{playing=false;updatePlay();seek(0);};$('seek').oninput=e=>{playing=false;updatePlay();seek(Number(e.target.value));};
  $('capture').onclick=()=>{views.A.renderer.render(views.A.scene,views.A.camera);views.A.renderer.domElement.toBlob(blob=>{if(!blob)return;const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='animation-studio-A.png';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});};
  function tick(){requestAnimationFrame(tick);const dt=Math.min(clock.getDelta(),.1);if(playing&&duration){let t=time+dt*Number($('speed').value);if(t>=duration){if($('loop').checked)t%=duration;else{t=duration;playing=false;updatePlay();}}seek(t);}for(const v of activeViews())v.renderer.render(v.scene,v.camera);}
  refresh();setInterval(refresh,5000);tick();
}catch(e){status('Não foi possível iniciar WebGL: '+e.message);}

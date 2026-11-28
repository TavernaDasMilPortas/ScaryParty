import http from 'node:http';
import { createReadStream } from 'node:fs';
import { readdir, readFile, stat, realpath } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const previewRoot = path.join(root, 'Previews');
const types = {'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.glb':'model/gltf-binary','.wasm':'application/wasm','.png':'image/png'};
const slug = /^[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}$/;

async function catalog() {
  const entries = await readdir(previewRoot, {withFileTypes:true}).catch(e => {if(e.code==='ENOENT') return []; throw e;});
  const result = [];
  for (const e of entries) {
    if (!e.isDirectory() || !slug.test(e.name)) continue;
    try {
      const dir = path.join(previewRoot,e.name);
      const m = JSON.parse(await readFile(path.join(dir,'manifest.json'),'utf8'));
      if (!(await stat(path.join(dir,'model.glb'))).isFile()) continue;
      result.push({id:e.name,name:String(m.name||e.name),mode:String(m.mode||'pose'),frame:m.frame,createdAt:String(m.createdAt||''),source:String(m.source||''),url:'/previews/'+e.name+'/model.glb'});
    } catch { /* Incomplete or malformed previews are not published. */ }
  }
  return result.sort((a,b)=>b.createdAt.localeCompare(a.createdAt)||a.id.localeCompare(b.id));
}

async function sendFile(req,res,base,relative) {
  const target = path.resolve(base,relative);
  const safeBase = await realpath(base);
  const actual = await realpath(target);
  if (!actual.startsWith(safeBase+path.sep)) {res.writeHead(403).end();return;}
  const info = await stat(actual);
  const ext = path.extname(actual).toLowerCase();
  if (!info.isFile() || !types[ext]) {res.writeHead(404).end();return;}
  res.writeHead(200,{'Content-Type':types[ext],'Content-Length':info.size,'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});
  if(req.method==='HEAD') return res.end();
  const stream = createReadStream(actual);
  stream.on('error',()=>res.destroy());
  stream.pipe(res);
}

export function createServer() {
  return http.createServer(async (req,res)=>{
    try {
      if (!/^(localhost|127\.0\.0\.1)(:\d+)?$/i.test(req.headers.host||'')) {res.writeHead(403).end();return;}
      if (!['GET','HEAD'].includes(req.method)) {res.writeHead(405).end();return;}
      const url = new URL(req.url,'http://localhost');
      const pathname = decodeURIComponent(url.pathname);
      if (pathname==='/api/previews') {
        const body=JSON.stringify(await catalog());
        res.writeHead(200,{'Content-Type':types['.json'],'Cache-Control':'no-store'});
        return res.end(req.method==='HEAD'?undefined:body);
      }
      if (pathname==='/' || pathname==='/index.html') return await sendFile(req,res,root,'index.html');
      if (pathname==='/viewer.mjs'||pathname==='/style.css') return await sendFile(req,res,root,pathname.slice(1));
      if (pathname.startsWith('/three/')) return await sendFile(req,res,path.join(root,'node_modules','three'),pathname.slice(7));
      const model = /^\/previews\/([a-zA-Z0-9][a-zA-Z0-9_-]{0,99})\/model\.glb$/.exec(pathname);
      if(model) return await sendFile(req,res,previewRoot,model[1]+'/model.glb');
      res.writeHead(404).end('Not found');
    } catch(e) {if(!res.headersSent)res.writeHead(e.code==='ENOENT'?404:400);res.end('Unavailable');}
  });
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const port=Number(process.env.ANIMATION_STUDIO_PORT||8765);
  if(!Number.isInteger(port)||port<1024||port>65535)throw Error('Invalid port');
  const server=createServer();
  server.on('error',e=>{console.error(e.message);process.exitCode=1;});
  server.listen(port,'127.0.0.1',()=>console.log('Animation Studio: http://127.0.0.1:'+port));
}

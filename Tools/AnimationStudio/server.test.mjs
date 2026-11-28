import {test} from 'node:test';
import assert from 'node:assert/strict';
import {once} from 'node:events';
import http from 'node:http';
import {createServer} from './server.mjs';
test('local viewer, catalog and restricted routes',async()=>{
  const s=createServer();s.listen(0,'127.0.0.1');await once(s,'listening');
  const base='http://127.0.0.1:'+s.address().port;
  try{
    assert.equal((await fetch(base)).status,200);
    assert.equal((await fetch(base+'/viewer.mjs')).status,200);
    assert.ok(Array.isArray(await (await fetch(base+'/api/previews')).json()));
    assert.equal((await fetch(base+'/SourceModels/personagem.blend')).status,404);
    assert.equal((await fetch(base+'/api/previews',{method:'POST'})).status,405);
    assert.equal((await fetch(base+'/previews/unknown/model.glb')).status,404);
    const status=await new Promise((resolve,reject)=>{http.get(base,{headers:{Host:'untrusted.example'}},r=>{r.resume();resolve(r.statusCode);}).on('error',reject);});
    assert.equal(status,403);
  }finally{await new Promise(resolve=>s.close(resolve));}
});

import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
const script=readFileSync(new URL('../VideoFrame.js',import.meta.url),'utf8');
const video=(extra={})=>({readyState:4,videoWidth:640,videoHeight:360,paused:false,ended:false,currentTime:5,...extra});
function capture(videos,{black=false,blocked=false}={}) {
  const canvas={getContext:()=>({fillRect(){},drawImage(){if(blocked)throw Error('SecurityError')},getImageData:()=>({data:new Uint8Array(black?[0,0,0,255]:[255,50,0,255])})}),toDataURL:()=> 'data:image/png;base64,TEST'};
  return vm.runInNewContext(script,{document:{title:'Example video',querySelectorAll:()=>videos,createElement:()=>canvas}});
}
test('capture ordinary video',()=>assert.equal(capture([video()]).png,'TEST'));
test('protected black frame reports unavailable',()=>assert.equal(capture([video({mediaKeys:{}})],{black:true}).error,'Protected video — frame unavailable'));
test('ordinary black scene is still captured',()=>assert.equal(capture([video()],{black:true}).png,'TEST'));
test('encrypted video is allowed if frames are available',()=>assert.equal(capture([video({mediaKeys:{}})]).png,'TEST'));
test('blocked origin reports unsupported capture',()=>assert.match(capture([video()],{blocked:true}).error,/does not allow/));
test('playing video wins over paused larger video',()=>assert.equal(capture([video({paused:true,videoWidth:1920,currentTime:10}),video()]).time,5));
test('largest playing video wins',()=>assert.equal(capture([video(),video({videoWidth:1920,currentTime:20})]).time,20));
test('unready video is ignored',()=>assert.equal(capture([video({readyState:0})]),null));

import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';
const script = readFileSync(new URL('../PlaybackHealth.js', import.meta.url), 'utf8');
function inspect({message='', visible=true, video=true, code=0, ad=false}={}) {
  return vm.runInNewContext(script, {
    URL, location:{href:'https://www.youtube.com/watch?v=sample'}, navigator:{onLine:true},
    getComputedStyle:()=>({display:visible?'block':'none',visibility:'visible'}),
    document:{querySelector:s=>s==='video'?(video?{paused:false,ended:false,currentTime:123,duration:600,readyState:4,error:code?{code}:null}:null):s==='#movie_player'?{classList:{contains:c=>ad&&c==='ad-showing'}}:{innerText:message,getClientRects:()=>visible?[{}]:[]}}
  });
}
test('detect exact reported message even if video disappeared',()=>assert.equal(inspect({message:'Something went wrong',video:false}).retryError,true));
test('detect refresh request',()=>assert.equal(inspect({message:'Please refresh this page'}).retryError,true));
test('ignore hidden error leftovers',()=>assert.equal(inspect({message:'Something went wrong',visible:false}).retryError,false));
test('ignore sign-in and unavailable video notices',()=>{for(const message of ['Sign in to confirm your age','This video is unavailable','Video paused. Continue watching?']) assert.equal(inspect({message}).retryError,false);});
test('capture media error, identity, and position',()=>{const s=inspect({code:2});assert.equal(s.mediaError,2);assert.equal(s.videoId,'sample');assert.equal(s.time,123);});
test('recognize ads for recovery exclusion',()=>assert.equal(inspect({ad:true}).ad,true));

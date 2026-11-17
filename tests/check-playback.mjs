// Run with the app in --diagnostics mode and a moving video already playing.
// No dependencies; works with Node 22 or later.
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import assert from 'node:assert/strict';
const folder = join(process.env.LOCALAPPDATA, 'DinnerTray');
const read = async () => JSON.parse(await readFile(join(folder, 'diagnostics.json'), 'utf8'));
const before = await read();
await new Promise(r => setTimeout(r, 5000));
const after = await read();
assert.equal(after.hasVideo, true, 'A video must be connected');
assert.equal(after.paused, false, 'The video must be playing');
assert.equal(after.error, null, 'Frame capture must not report an error');
assert.ok(Date.now() - new Date(after.updated).getTime() < 3000, 'Diagnostics must be live');
assert.ok(after.frameCount - before.frameCount >= 20, 'Must deliver at least 4 frames per second');
assert.ok(after.changedFrames - before.changedFrames >= 5, 'Actual pixels must change');
assert.ok(after.videoTime > before.videoTime + 2, 'Video time must advance');
const png = await readFile(join(folder, 'last-frame.png'));
assert.equal(png.readUInt32BE(16), 64);
assert.equal(png.readUInt32BE(20), 64);
console.log(JSON.stringify({ result: 'PASS', frames: after.frameCount - before.frameCount, changedFrames: after.changedFrames - before.changedFrames, videoSeconds: after.videoTime - before.videoTime, image: '64 × 64 PNG' }, null, 2));

(() => {
  // Prefer playing, large video elements over previews and inactive players.
  const v = [...document.querySelectorAll('video')]
    .filter(v => v.readyState >= 2 && v.videoWidth > 0)
    .sort((a, b) => Number(!b.paused && !b.ended) - Number(!a.paused && !a.ended) ||
      b.videoWidth * b.videoHeight - a.videoWidth * a.videoHeight)[0];
  if (!v) return null;
  const result = {title: document.title.replace(/ - YouTube$/, ''), paused: v.paused, time: v.currentTime};
  try {
    const c = globalThis.__dinnerTrayCanvas || (globalThis.__dinnerTrayCanvas = document.createElement('canvas'));
    // Reset also clears a canvas tainted by a previous cross-origin video.
    c.width = 64; c.height = 64;
    const ctx = c.getContext('2d', {alpha: false});
    ctx.fillStyle = '#000'; ctx.fillRect(0, 0, 64, 64);
    const scale = Math.min(64 / v.videoWidth, 64 / v.videoHeight);
    const w = v.videoWidth * scale, h = v.videoHeight * scale;
    ctx.drawImage(v, (64-w)/2, (64-h)/2, w, h);
    if (v.mediaKeys) {
      const pixels = ctx.getImageData(0, 0, 64, 64).data;
      let hasColor = false;
      for (let i = 0; i < pixels.length; i += 4)
        if (pixels[i] || pixels[i+1] || pixels[i+2]) { hasColor = true; break; }
      if (!hasColor) {
        result.error = 'Protected video — frame unavailable';
        return result;
      }
    }
    result.png = c.toDataURL('image/png').split(',')[1];
  } catch(e) {
    result.error = v.mediaKeys ? 'Protected video — frame unavailable' : 'This site does not allow video frame capture';
  }
  return result;
})()

(() => {
  const v = document.querySelector('video');
  const url = new URL(location.href);
  const videoId = url.searchParams.get('v') || (/^\/shorts\/([^/?]+)/.exec(url.pathname)?.[1]) || '';
  const player = document.querySelector('#movie_player');
  const error = document.querySelector('.ytp-error-content-wrap');
  const style = error && getComputedStyle(error);
  const visible = !!error && error.getClientRects().length > 0 && style.display !== 'none' && style.visibility !== 'hidden';
  // Only transient player messages; do not reload sign-in, age, or unavailable-video notices.
  const retryError = visible && /refresh|reload|try again|playback error|an error (?:has )?occurred|something went wrong/i.test(error.innerText || '');
  return {
    videoId, hasVideo: !!v, paused: v?.paused ?? true, ended: v?.ended ?? false,
    time: Number.isFinite(v?.currentTime) ? v.currentTime : 0,
    duration: Number.isFinite(v?.duration) ? v.duration : 0,
    ready: v?.readyState ?? 0,
    live: v?.duration === Infinity || !!player?.classList.contains('ytp-live'),
    ad: !!player?.classList.contains('ad-showing'), online: navigator.onLine,
    mediaError: v?.error?.code || 0, retryError: !!retryError
  };
})()

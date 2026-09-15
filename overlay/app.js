/**
 * Neverwinter Lockbox Overlay — OBS Browser Source
 *
 * Receives rolls from Streamer.bot WebSocket:
 *   1) Preferred: General.Custom with type "lockbox.open" (full prize payload)
 *   2) Secondary: Twitch.RewardRedemption matching CONFIG.rewardTitle
 *      (teaser only unless prize fields are somehow present)
 *
 * Demo: open with ?demo=1 to play a fake open without Streamer.bot.
 */

(function () {
  'use strict';

  // ===========================================================================
  // CUSTOMIZE
  // ===========================================================================
  const CONFIG = {
    host: '127.0.0.1',
    port: 8080,
    // Must match Twitch reward title + OpenLockbox.cs REWARD_TITLE
    rewardTitle: 'Open Lockbox',
    // How long the reveal stays on screen (ms)
    displayMs: 9000,
    // Delay after chest opens before prize text appears
    revealDelayMs: 650,
    // Show tiny connection status in corner (set false for clean OBS)
    showStatus: true,
  };

  // ===========================================================================

  const stage = document.getElementById('stage');
  const glowRing = document.getElementById('glowRing');
  const viewerEl = document.getElementById('viewerName');
  const boxEl = document.getElementById('boxName');
  const prizeEl = document.getElementById('prizeName');
  const rarityEl = document.getElementById('rarityBadge');
  const statusEl = document.getElementById('status');
  const prizePanel = document.getElementById('prizePanel');

  let hideTimer = null;
  let busy = false;
  let client = null;

  const params = new URLSearchParams(window.location.search);
  const demoMode = params.get('demo') === '1' || params.get('demo') === 'true';
  if (params.has('host')) CONFIG.host = params.get('host');
  if (params.has('port')) CONFIG.port = parseInt(params.get('port'), 10) || CONFIG.port;
  if (params.has('displayMs')) CONFIG.displayMs = parseInt(params.get('displayMs'), 10) || CONFIG.displayMs;

  function setStatus(text, cls) {
    if (!CONFIG.showStatus || !statusEl) return;
    statusEl.textContent = text;
    statusEl.className = 'status' + (cls ? ' ' + cls : '');
  }

  function normalizeRarity(r) {
    const s = (r || 'common').toLowerCase().trim();
    const allowed = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic'];
    return allowed.includes(s) ? s : 'common';
  }

  /**
   * @param {{ userName: string, boxName: string, prizeName: string, prizeRarity: string }} data
   */
  function playOpen(data) {
    if (!data) return;
    if (busy) {
      // Queue-simple: restart with latest
      clearTimeout(hideTimer);
      resetStageImmediate();
    }
    busy = true;

    const rarity = normalizeRarity(data.prizeRarity);
    const userName = data.userName || 'Viewer';
    const boxName = data.boxName || 'Lockbox';
    const prizeName = data.prizeName || 'Unknown Prize';

    viewerEl.textContent = userName;
    boxEl.textContent = boxName;
    prizeEl.textContent = prizeName;
    rarityEl.textContent = rarity.toUpperCase();

    // Reset classes
    stage.className = 'stage';
    glowRing.className = 'glow-ring rarity-' + rarity;
    rarityEl.className = 'rarity-badge rarity-' + rarity;
    prizeEl.className = 'prize-name rarity-' + rarity;

    // Force reflow then animate
    void stage.offsetWidth;
    stage.classList.add('visible');
    stage.classList.remove('hidden');
    stage.classList.add('opening');

    setTimeout(function () {
      stage.classList.add('revealed');
    }, CONFIG.revealDelayMs);

    clearTimeout(hideTimer);
    hideTimer = setTimeout(hideStage, CONFIG.displayMs);
  }

  /** Teaser when we only saw RewardRedemption (no rolled prize yet). */
  function playTeaser(userName, userInput) {
    if (busy) return;
    viewerEl.textContent = userName || 'Viewer';
    boxEl.textContent = userInput || 'a Lockbox';
    prizeEl.textContent = '…';
    rarityEl.textContent = 'OPENING';
    rarityEl.className = 'rarity-badge';
    prizeEl.className = 'prize-name';
    glowRing.className = 'glow-ring';

    stage.className = 'stage visible teaser';
    // Auto-clear teaser if Custom never arrives
    clearTimeout(hideTimer);
    hideTimer = setTimeout(hideStage, 4000);
  }

  function hideStage() {
    stage.classList.remove('visible', 'opening', 'revealed', 'teaser');
    stage.classList.add('hidden');
    busy = false;
  }

  function resetStageImmediate() {
    stage.className = 'stage hidden';
    busy = false;
  }

  /** Extract lockbox.open payload from General.Custom event shapes. */
  function extractCustomPayload(eventData) {
    // @streamerbot/client typically: { event: {...}, data: { ...broadcast fields } }
    const d = eventData && (eventData.data !== undefined ? eventData.data : eventData);
    if (!d) return null;

    // WebsocketBroadcastJson may land as object or nested string
    let payload = d;
    if (typeof d === 'string') {
      try { payload = JSON.parse(d); } catch (e) { return null; }
    }
    // Some builds nest under .data again or .message
    if (payload && typeof payload.data === 'string') {
      try { payload = JSON.parse(payload.data); } catch (e) { /* keep */ }
    }
    if (payload && payload.data && typeof payload.data === 'object' && payload.data.type) {
      payload = payload.data;
    }
    if (payload && payload.message && typeof payload.message === 'object') {
      payload = payload.message;
    }

    if (payload && payload.type === 'lockbox.open') return payload;
    // Also accept if fields are present without type (defensive)
    if (payload && payload.prizeName && (payload.boxName || payload.boxId)) {
      payload.type = payload.type || 'lockbox.open';
      return payload;
    }
    return null;
  }

  function onCustomEvent(eventData) {
    const payload = extractCustomPayload(eventData);
    if (!payload) return;
    playOpen({
      userName: payload.userName,
      boxName: payload.boxName || payload.boxId,
      prizeName: payload.prizeName,
      prizeRarity: payload.prizeRarity,
    });
  }

  function onRewardRedemption(eventData) {
    const d = (eventData && eventData.data) || eventData || {};
    const title =
      d.rewardTitle ||
      d.reward_title ||
      d.title ||
      (d.reward && (d.reward.title || d.reward.Title)) ||
      '';
    if (!title || title.toLowerCase() !== CONFIG.rewardTitle.toLowerCase()) return;

    const userName =
      d.user_name || d.userName || d.displayName || d.login ||
      (d.user && (d.user.name || d.user.display_name)) || 'Viewer';
    const userInput = d.user_input || d.userInput || d.rawInput || d.input || '';

    // Prefer waiting for Custom for the real prize. Show brief teaser only.
    // If Custom already fired first, busy will block teaser.
    playTeaser(userName, userInput);
  }

  function connectStreamerBot() {
    if (typeof window.StreamerbotClient !== 'function') {
      setStatus('Waiting for @streamerbot/client…', '');
      window.addEventListener('sb-client-ready', connectStreamerBot, { once: true });
      // Fallback poll
      setTimeout(function () {
        if (!client && typeof window.StreamerbotClient === 'function') connectStreamerBot();
      }, 1500);
      return;
    }

    setStatus('Connecting ' + CONFIG.host + ':' + CONFIG.port + '…', '');

    try {
      client = new window.StreamerbotClient({
        host: CONFIG.host,
        port: CONFIG.port,
        // Subscribe to sources we need
        subscriptions: {
          General: ['Custom'],
          Twitch: ['RewardRedemption'],
        },
        onConnect: function () {
          setStatus('WS connected', 'ok');
        },
        onDisconnect: function () {
          setStatus('WS disconnected — retrying…', 'error');
        },
        onError: function (err) {
          setStatus('WS error', 'error');
          console.warn('[lockbox overlay] WS error', err);
        },
      });

      // Preferred: rolled prize broadcast from OpenLockbox.cs
      client.on('General.Custom', onCustomEvent);

      // Secondary: teaser on redeem (prize comes from Custom)
      client.on('Twitch.RewardRedemption', onRewardRedemption);

      // Some client versions use wildcard / raw message — belt and suspenders
      if (typeof client.on === 'function') {
        try {
          client.on('*', function (ev, data) {
            // Ignore unless it looks like our payload
            const p = extractCustomPayload(data);
            if (p) onCustomEvent(data);
          });
        } catch (e) { /* optional */ }
      }
    } catch (err) {
      setStatus('Client init failed', 'error');
      console.error(err);
    }
  }

  function runDemo() {
    setStatus('DEMO MODE (?demo=1)', 'ok');
    const demos = [
      {
        userName: 'DemoKnight',
        boxName: 'Dragon Cult Lockbox',
        prizeName: 'Azure Wyrmling Mount',
        prizeRarity: 'mythic',
      },
      {
        userName: 'ScrollHoarder',
        boxName: 'Leaping Flame Lockbox',
        prizeName: 'Rough Astral Diamonds',
        prizeRarity: 'common',
      },
      {
        userName: 'JusticarJay',
        boxName: 'Lockbox of Justice',
        prizeName: 'Hammer of Absolute Law',
        prizeRarity: 'epic',
      },
    ];
    let i = 0;
    function next() {
      playOpen(demos[i % demos.length]);
      i += 1;
      setTimeout(next, CONFIG.displayMs + 1500);
    }
    setTimeout(next, 800);
  }

  // Boot
  stage.classList.add('hidden');
  if (demoMode) {
    runDemo();
  } else {
    connectStreamerBot();
  }
})();

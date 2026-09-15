/**
 * Neverwinter Lockbox Overlay — OBS / Streamlabs Browser Source
 * Native WebSocket to Streamer.bot (no CDN) — works with file:// sources.
 * Listens for General.Custom from CPH.WebsocketBroadcastJson (type lockbox.open)
 * Demo: ?demo=1
 */
(function () {
  'use strict';

  var CONFIG = {
    host: '127.0.0.1',
    port: 8080,
    endpoint: '/',
    displayMs: 9000,
    revealDelayMs: 650,
    showStatus: true,
    reconnectMs: 2000
  };

  var stage = document.getElementById('stage');
  var glowRing = document.getElementById('glowRing');
  var viewerEl = document.getElementById('viewerName');
  var boxEl = document.getElementById('boxName');
  var prizeEl = document.getElementById('prizeName');
  var rarityEl = document.getElementById('rarityBadge');
  var statusEl = document.getElementById('status');

  var hideTimer = null;
  var busy = false;
  var ws = null;
  var reconnectTimer = null;

  var params = new URLSearchParams(window.location.search);
  var demoMode = params.get('demo') === '1' || params.get('demo') === 'true';
  if (params.has('host')) CONFIG.host = params.get('host');
  if (params.has('port')) CONFIG.port = parseInt(params.get('port'), 10) || CONFIG.port;

  function setStatus(text, cls) {
    if (!CONFIG.showStatus || !statusEl) return;
    statusEl.textContent = text;
    statusEl.className = 'status' + (cls ? ' ' + cls : '');
  }

  function normalizeRarity(r) {
    var s = (r || 'common').toLowerCase().trim();
    var allowed = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic'];
    return allowed.indexOf(s) >= 0 ? s : 'common';
  }

  function playOpen(data) {
    if (!data) return;
    if (busy) {
      clearTimeout(hideTimer);
      stage.className = 'stage hidden';
      busy = false;
    }
    busy = true;

    var rarity = normalizeRarity(data.prizeRarity);
    viewerEl.textContent = data.userName || 'Viewer';
    boxEl.textContent = data.boxName || 'Lockbox';
    prizeEl.textContent = data.prizeName || 'Unknown Prize';
    rarityEl.textContent = rarity.toUpperCase();

    stage.className = 'stage';
    glowRing.className = 'glow-ring rarity-' + rarity;
    rarityEl.className = 'rarity-badge rarity-' + rarity;
    prizeEl.className = 'prize-name rarity-' + rarity;

    void stage.offsetWidth;
    stage.classList.add('visible');
    stage.classList.remove('hidden');
    stage.classList.add('opening');

    setTimeout(function () {
      stage.classList.add('revealed');
    }, CONFIG.revealDelayMs);

    clearTimeout(hideTimer);
    hideTimer = setTimeout(function () {
      stage.classList.remove('visible', 'opening', 'revealed');
      stage.classList.add('hidden');
      busy = false;
    }, CONFIG.displayMs);
  }

  function extractLockboxPayload(msg) {
    if (!msg) return null;
    var obj = msg;
    if (typeof obj === 'string') {
      try { obj = JSON.parse(obj); } catch (e) { return null; }
    }

    // Event wrapper from Streamer.bot
    var source = obj.event && obj.event.source;
    var type = obj.event && obj.event.type;
    var data = obj.data;

    if (source && String(source).toLowerCase() === 'general' && type === 'Custom') {
      return coercePayload(data);
    }

    // Direct payload
    return coercePayload(obj);
  }

  function coercePayload(data) {
    if (data == null) return null;
    if (typeof data === 'string') {
      try { data = JSON.parse(data); } catch (e) { return null; }
    }
    // Nested shapes
    if (data && typeof data.data === 'string') {
      try { data = JSON.parse(data.data); } catch (e) { /* keep */ }
    }
    if (data && data.data && typeof data.data === 'object' && (data.data.type || data.data.prizeName)) {
      data = data.data;
    }
    if (!data || typeof data !== 'object') return null;
    if (data.type === 'lockbox.open' || (data.prizeName && (data.boxName || data.boxId))) {
      return data;
    }
    return null;
  }

  function handleMessage(raw) {
    var msg;
    try {
      msg = typeof raw === 'string' ? JSON.parse(raw) : raw;
    } catch (e) {
      return;
    }
    // Ignore request responses
    if (msg.status && msg.id && !msg.event) return;

    var payload = extractLockboxPayload(msg);
    if (payload) {
      setStatus('Lockbox open!', 'ok');
      playOpen({
        userName: payload.userName,
        boxName: payload.boxName || payload.boxId,
        prizeName: payload.prizeName,
        prizeRarity: payload.prizeRarity
      });
    }
  }

  function connect() {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
    var url = 'ws://' + CONFIG.host + ':' + CONFIG.port + (CONFIG.endpoint || '/');
    setStatus('Connecting ' + url + '…', '');
    try {
      ws = new WebSocket(url);
    } catch (e) {
      setStatus('WS create failed', 'error');
      scheduleReconnect();
      return;
    }

    ws.onopen = function () {
      setStatus('WS connected — subscribing…', 'ok');
      var sub = {
        request: 'Subscribe',
        id: 'lockbox-sub-' + Date.now(),
        events: { General: ['Custom'] }
      };
      ws.send(JSON.stringify(sub));
      setStatus('WS connected', 'ok');
    };

    ws.onmessage = function (ev) {
      handleMessage(ev.data);
    };

    ws.onerror = function () {
      setStatus('WS error', 'error');
    };

    ws.onclose = function () {
      setStatus('WS disconnected — retrying…', 'error');
      scheduleReconnect();
    };
  }

  function scheduleReconnect() {
    if (reconnectTimer) return;
    reconnectTimer = setTimeout(connect, CONFIG.reconnectMs);
  }

  function runDemo() {
    setStatus('DEMO MODE (?demo=1)', 'ok');
    var demos = [
      { userName: 'DemoKnight', boxName: 'Dragon Cult Lockbox', prizeName: 'Azure Wyrmling Mount', prizeRarity: 'mythic' },
      { userName: 'ScrollHoarder', boxName: 'Leaping Flame Lockbox', prizeName: 'Rough Astral Diamonds', prizeRarity: 'common' },
      { userName: 'JusticarJay', boxName: 'Lockbox of Justice', prizeName: 'Hammer of Absolute Law', prizeRarity: 'epic' }
    ];
    var i = 0;
    function next() {
      playOpen(demos[i % demos.length]);
      i += 1;
      setTimeout(next, CONFIG.displayMs + 1500);
    }
    setTimeout(next, 800);
  }

  stage.classList.add('hidden');
  if (demoMode) {
    runDemo();
  } else {
    connect();
  }
})();

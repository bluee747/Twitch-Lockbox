/**
 * Neverwinter Lockbox Overlay — OBS / Streamlabs Browser Source
 * Native WebSocket to Streamer.bot (no CDN) — works with file:// sources.
 * Listens for General.Custom from CPH.WebsocketBroadcastJson (type lockbox.open)
 * Demo: ?demo=1   Inventory panel: ?inventory=1
 */
(function () {
  'use strict';

  var CONFIG = {
    host: '127.0.0.1',
    port: 8080,
    endpoint: '/',
    displayMs: 9500,
    revealDelayMs: 700,
    showStatus: true,
    reconnectMs: 2000
  };

  var RARITY_GEMS = {
    common: '⚪',
    uncommon: '🟢',
    rare: '🔵',
    epic: '🟣',
    legendary: '🟠',
    mythic: '🔴'
  };

  var stage = document.getElementById('stage');
  var glowRing = document.getElementById('glowRing');
  var particles = document.getElementById('particles');
  var viewerEl = document.getElementById('viewerName');
  var boxEl = document.getElementById('boxName');
  var prizeEl = document.getElementById('prizeName');
  var rarityEl = document.getElementById('rarityBadge');
  var statusEl = document.getElementById('status');
  var prizeIconEmoji = document.getElementById('prizeIconEmoji');
  var prizeIconImg = document.getElementById('prizeIconImg');
  var prizeIconGem = document.getElementById('prizeIconGem');
  var invFlash = document.getElementById('invFlash');
  var invFlashText = document.getElementById('invFlashText');
  var invPanel = document.getElementById('invPanel');
  var invList = document.getElementById('invList');

  var hideTimer = null;
  var busy = false;
  var ws = null;
  var reconnectTimer = null;
  var recentOpens = [];

  var params = new URLSearchParams(window.location.search);
  var demoMode = params.get('demo') === '1' || params.get('demo') === 'true';
  var showInvPanel = params.get('inventory') === '1' || params.get('inventory') === 'true';
  if (params.has('host')) CONFIG.host = params.get('host');
  if (params.has('port')) CONFIG.port = parseInt(params.get('port'), 10) || CONFIG.port;

  if (showInvPanel && invPanel) {
    invPanel.hidden = false;
  }

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

  function isEmojiIcon(icon) {
    if (!icon || typeof icon !== 'string') return false;
    var t = icon.trim();
    if (!t) return false;
    if (/^(https?:|data:|\/|\.|icons\/)/i.test(t)) return false;
    if (/\.(png|jpg|jpeg|gif|webp|svg)(\?|$)/i.test(t)) return false;
    return t.length <= 8;
  }

  function isImageIcon(icon) {
    if (!icon || typeof icon !== 'string') return false;
    var t = icon.trim();
    return /^(https?:|data:)/i.test(t) || /\.(png|jpg|jpeg|gif|webp|svg)(\?|$)/i.test(t) || t.indexOf('icons/') === 0 || t.indexOf('./') === 0;
  }

  function setPrizeIcon(icon, rarity) {
    prizeIconEmoji.hidden = true;
    prizeIconImg.hidden = true;
    prizeIconGem.hidden = true;
    prizeIconImg.removeAttribute('src');

    if (isEmojiIcon(icon)) {
      prizeIconEmoji.textContent = icon.trim();
      prizeIconEmoji.hidden = false;
      return;
    }
    if (isImageIcon(icon)) {
      prizeIconImg.src = icon.trim();
      prizeIconImg.hidden = false;
      prizeIconImg.onerror = function () {
        prizeIconImg.hidden = true;
        prizeIconGem.textContent = RARITY_GEMS[rarity] || '💎';
        prizeIconGem.hidden = false;
      };
      return;
    }
    prizeIconGem.textContent = RARITY_GEMS[rarity] || '💎';
    prizeIconGem.hidden = false;
  }

  function spawnParticles() {
    if (!particles) return;
    particles.innerHTML = '';
    for (var i = 0; i < 18; i++) {
      var p = document.createElement('div');
      p.className = 'particle';
      var angle = (Math.PI * 2 * i) / 18;
      var dist = 60 + Math.random() * 100;
      p.style.left = (50 + Math.cos(angle) * 10) + '%';
      p.style.top = (55 + Math.sin(angle) * 8) + '%';
      p.style.setProperty('--dx', (Math.cos(angle) * dist) + 'px');
      p.style.setProperty('--dy', (Math.sin(angle) * dist - 40) + 'px');
      p.style.animationDelay = (Math.random() * 0.35) + 's';
      particles.appendChild(p);
    }
  }

  function pushRecent(data) {
    recentOpens.unshift({
      user: data.userName || 'Viewer',
      prize: data.prizeName || '?',
      rarity: normalizeRarity(data.prizeRarity),
      icon: data.icon || ''
    });
    if (recentOpens.length > 12) recentOpens.length = 12;
    if (!showInvPanel || !invList) return;
    invList.innerHTML = '';
    for (var i = 0; i < recentOpens.length; i++) {
      var r = recentOpens[i];
      var li = document.createElement('li');
      var iconBit = isEmojiIcon(r.icon) ? (r.icon + ' ') : '';
      li.textContent = iconBit + '@' + r.user + ' — [' + r.rarity.toUpperCase() + '] ' + r.prize;
      invList.appendChild(li);
    }
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
    setPrizeIcon(data.icon, rarity);

    var count = data.inventoryCount;
    if (count != null && count !== '' && invFlash && invFlashText) {
      invFlash.hidden = false;
      invFlashText.textContent = '@' + (data.userName || 'Viewer') + ' inventory: ' + count + ' opens';
    } else if (invFlash) {
      invFlash.hidden = true;
    }

    pushRecent(data);
    spawnParticles();

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
      if (particles) particles.innerHTML = '';
      busy = false;
    }, CONFIG.displayMs);
  }

  function extractLockboxPayload(msg) {
    if (!msg) return null;
    var obj = msg;
    if (typeof obj === 'string') {
      try { obj = JSON.parse(obj); } catch (e) { return null; }
    }

    var source = obj.event && obj.event.source;
    var type = obj.event && obj.event.type;
    var data = obj.data;

    if (source && String(source).toLowerCase() === 'general' && type === 'Custom') {
      return coercePayload(data);
    }

    return coercePayload(obj);
  }

  function coercePayload(data) {
    if (data == null) return null;
    if (typeof data === 'string') {
      try { data = JSON.parse(data); } catch (e) { return null; }
    }
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
    if (msg.status && msg.id && !msg.event) return;

    var payload = extractLockboxPayload(msg);
    if (payload) {
      setStatus('WS connected · open!', 'ok');
      playOpen({
        userName: payload.userName,
        boxName: payload.boxName || payload.boxId,
        prizeName: payload.prizeName,
        prizeRarity: payload.prizeRarity,
        prizeId: payload.prizeId,
        icon: payload.icon || payload.prizeImage || '',
        inventoryCount: payload.inventoryCount
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
      { userName: 'DemoKnight', boxName: 'Wild Adventures Lockbox', prizeName: 'Star Angler Mount', prizeRarity: 'mythic', icon: '🐴', inventoryCount: 12, prizeId: 'wa-mount' },
      { userName: 'ScrollHoarder', boxName: 'Dragon Cult Lockbox', prizeName: 'Dragon Cult Progression Pack', prizeRarity: 'rare', icon: '📈', inventoryCount: 4, prizeId: 'dc-prog' },
      { userName: 'JusticarJay', boxName: 'Lockbox of Justice', prizeName: 'Tarmalune Trade Bar Jackpot', prizeRarity: 'epic', icon: '💰', inventoryCount: 27, prizeId: 'j-jack' },
      { userName: 'EncoreFan', boxName: 'Wild Adventures Lockbox', prizeName: 'Encore the Virtuoso Companion', prizeRarity: 'mythic', icon: '🐾', inventoryCount: 1, prizeId: 'wa-comp' }
    ];
    var i = 0;
    function next() {
      playOpen(demos[i % demos.length]);
      i += 1;
      setTimeout(next, CONFIG.displayMs + 1600);
    }
    setTimeout(next, 700);
  }

  stage.classList.add('hidden');
  if (demoMode) {
    runDemo();
  } else {
    connect();
  }
})();

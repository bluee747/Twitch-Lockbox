# Twitch Lockbox (Neverwinter)

Streamer.bot listens to the **Channel Points** reward **Open Lockbox**. An OBS **browser source** plays a chest-open animation with the prize reveal.

```
Channel Points  "Open Lockbox" (100 pts)
   →  Streamer.bot OpenLockbox.cs (roll from data/boxes.json)
   →  append data/inventory.json + chat announce
   →  WebSocket broadcast lockbox.open
   →  overlay chest animation in OBS
```

**Free `!lockbox` / `!open` commands are disabled.** Opens are Channel Points only.  
Optional free chat: `!inventory` → `ShowInventory.cs` (view recent prizes).

**Drop rates in `data/boxes.json` are stream-approximate stand-ins, not official Cryptic tables.**

PC install path: `D:\Overlays\Twitch-Lockbox\`

---

## Folder layout

```
├── README.md
├── assets/                  ← channel-point reward art (+ sizes)
├── data/
│   ├── boxes.json           ← loot tables (weights ~10000)
│   └── inventory.json       ← per-viewer open history (auto-written)
├── streamerbot/
│   ├── OpenLockbox.cs       ← Channel Points open (paste into Execute C#)
│   ├── ShowInventory.cs     ← optional !inventory
│   └── actions.md           ← Streamer.bot + OBS steps
└── overlay/
    ├── index.html           ← OBS Browser Source
    ├── style.css
    ├── app.js               ← native WS to 127.0.0.1:8080
    ├── chest-closed.png     ← closed lockbox art (from reward PNG)
    └── icons/               ← optional image icons (emoji used by default)
```

---

## Quick start

1. Sync/clone to `D:\Overlays\Twitch-Lockbox\`
2. Streamer.bot: enable **WebSocket Server** (port `8080`)
3. Create Channel Points reward **Open Lockbox** (Require Text = yes)
4. Action → Reward Redemption → paste `streamerbot/OpenLockbox.cs`  
   Confirm `BOXES_JSON_PATH` / `INVENTORY_JSON_PATH` match your PC paths
5. OBS → Browser Source → `overlay/index.html` at 1920×1080
6. Test animation: `overlay/index.html?demo=1`
7. Live: redeem **Open Lockbox** (optional text = box id/alias)

Full checklist: **`streamerbot/actions.md`**

After updating files on the PC: in OBS, **Refresh cache of current page** on the Browser Source (or toggle the source off/on).

---

## Drop-rate tiers (weights sum 10000)

| Tier | Weight | Approx. |
|------|--------|---------|
| Mythic Grand Prize (mount + companion split 22+23) | 45 | ~0.45% (~1/250) |
| Mythic Insignia | 50 | ~0.5% |
| Epic/Legendary pack | 125 | ~1.25% |
| Tarmalune Trade Bar Jackpot | 60 | ~0.6% |
| Progression pack | 2350 | ~23.5% |
| Companion pack | 2350 | ~23.5% |
| Enchantment pack | 2350 | ~23.5% |
| Common filler | 2670 | ~26.7% |

Each item has: `id`, `name`, `rarity`, `weight`, `icon` (emoji or path), `category`.

Default box: **`wild-adventures`** (Star Angler mount / Encore the Virtuoso companion).

---

## Inventory

- Stored in `data/inventory.json` under `viewers[loginLower].opens[]`
- Last **~50** opens per viewer kept
- Broadcast includes `inventoryCount`; overlay flashes `@user inventory: N opens`
- Optional chat: `!inventory` lists last few prizes (Twitch-length truncated)
- Overlay panel of recent opens: `overlay/index.html?inventory=1`

---

## Overlay

- Closed chest art: `overlay/chest-closed.png`
- Animation: scale/rotate + glow + particles, then brightness “open” + large prize icon
- Prize `icon`: emoji → big text; URL/path → `<img>`; else rarity gem
- Status corner: WebSocket connected
- Demo: `?demo=1`

---

## Troubleshooting

| Problem | Check |
|---------|-------|
| No animation | WebSocket Server on? Overlay without stale cache? Refresh OBS browser source |
| “boxes.json not found” | Absolute `BOXES_JSON_PATH` on the Streamer.bot PC |
| Chat `!lockbox` does nothing / refuses | Expected — Channel Points only; use `!inventory` to view |
| Wrong prize vs chat | Overlay uses the Custom broadcast — don’t re-roll in the page |

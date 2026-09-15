# Neverwinter Lockbox Opener (Twitch / Streamer.bot)

A complete **streamer scaffold** for Neverwinter-flavored channel-point lockbox opens: weighted loot tables, Streamer.bot C# roll logic, chat announce, and an OBS Browser Source overlay with chest animation.

**Rates in `data/boxes.json` are approximate streamer-editable stand-ins — not official Cryptic drop tables.**

Built for George’s Streamer.bot + OBS setup. No Twitch Extension code, no secrets, no build step.

---

## What this is

Viewers redeem a Channel Point reward (**Open Lockbox**), type a box name (or leave blank for default), Streamer.bot rolls a weighted prize from JSON, posts chat, broadcasts JSON to the WebSocket overlay, and OBS plays a chest-open reveal with rarity coloring.

---

## Folder layout

```
neverwinter-lockbox-opener/
├── README.md                 ← you are here
├── data/
│   └── boxes.json            ← loot tables (edit weights here)
├── streamerbot/
│   ├── OpenLockbox.cs        ← paste into Execute C# Code
│   └── actions.md            ← detailed Streamer.bot wire-up
└── overlay/
    ├── index.html            ← OBS Browser Source entry
    ├── style.css
    └── app.js                ← Streamer.bot WS client + animation
```

---

## Zero-to-working (George)

### A. Unpack

1. Unzip so you have a stable path, e.g.  
   `C:\Overlays\neverwinter-lockbox-opener\`
2. Note the full path to `data\boxes.json`.

### B. Streamer.bot

1. **Servers / Clients → WebSocket Server** → Enable (port **8080**).
2. Create Twitch Channel Point reward **Open Lockbox** with **Require User Input**  
   Prompt example: `Box name (dragon, flame, justice, omens)`.
3. New Action → trigger on that reward redemption.
4. Sub-action **Execute C# Code** → paste `streamerbot/OpenLockbox.cs`.
5. Edit `BOXES_JSON_PATH` to your absolute `boxes.json` path.
6. Save / compile.  
   Full checklist: **`streamerbot/actions.md`**.

### C. OBS Browser Source

1. Sources → **Browser** → Create.
2. **Local file** → browse to `overlay/index.html`  
   (or URL: `file:///C:/Overlays/neverwinter-lockbox-opener/overlay/index.html`)
3. Width **1920**, Height **1080**.
4. Check **Shutdown source when not visible** = off (keep WS alive), or refresh after Streamer.bot starts.
5. Transparent background is built-in (CSS `background: transparent`).

**Layout test without Streamer.bot:**

```
file:///.../overlay/index.html?demo=1
```

Cycles fake mythic/common/epic opens.

### D. Test live

1. Start Streamer.bot (WS on).
2. Open overlay (no `?demo=1`).
3. Redeem **Open Lockbox** with `dragon` or empty input.
4. Expect: chat line + overlay chest → rarity prize.

---

## Starter boxes

| Id | Display name | Aliases (examples) |
|----|--------------|--------------------|
| `dragon-cult` | Dragon Cult Lockbox | dragon, cult, dc |
| `leaping-flame` | Leaping Flame Lockbox | flame, leaping, lf |
| `justice` | Lockbox of Justice | justice, loj |
| `dark-omens` | Dark Omens Lockbox | dark, omens, do |

Empty input → `DEFAULT_BOX_ID` (`dragon-cult`) or first enabled box.

---

## Editing drop rates

Open `data/boxes.json`. Each item has `"weight"` (relative integer). Higher = more common. Mythic/chase should stay tiny.

```json
{ "id": "dc-chase-companion", "name": "Tiamat's Chosen Companion", "rarity": "mythic", "weight": 4, "image": null }
```

Set `"enabled": false` on a box to hide it from rolls/help text. Add new boxes by copying a block. C# re-reads the file every redeem — no Streamer.bot restart required for rate tweaks.

Optional `"image"` strings are passed through as `prizeImage` / `lockboxImage` for future art; the stock overlay does not require them.

---

## How the overlay gets the prize

1. **Preferred:** `OpenLockbox.cs` calls `CPH.WebsocketBroadcastJson` with  
   `{ "type": "lockbox.open", "userName", "boxName", "prizeName", "prizeRarity", ... }`.  
   Overlay listens to **`General.Custom`** and plays the full animation.
2. **Secondary:** Overlay also listens to **`Twitch.RewardRedemption`** (title match) for a short “opening…” teaser. The real prize still comes from the Custom broadcast.
3. Streamer.bot **SetArgument** (`prizeName`, `prizeRarity`, `boxId`, …) is for *other* action steps (alerts, Discord, etc.), not for the browser overlay.

---

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| Overlay never animates | WebSocket Server enabled? Overlay host/port = 127.0.0.1:8080? Use `?demo=1` to verify OBS layout. |
| Chat works, overlay doesn’t | Confirm C# reaches `WebsocketBroadcastJson`; check Streamer.bot logs. Reload Browser Source after SB starts. |
| `boxes.json not found` | Set `BOXES_JSON_PATH` to an absolute path Streamer.bot can read. |
| Unknown lockbox message | Use id/alias from table above; check `enabled: true`. |
| CDN / client script blocked | OBS needs network for jsDelivr (`@streamerbot/client`). Allow online, or vendor the ESM locally later. |
| Compile error on Newtonsoft | Streamer.bot ships Newtonsoft.Json; update SB if missing. |
| Invalid box still takes points | Set `CANCEL_ON_INVALID_BOX = true` (default); ensure redemption id args exist on your trigger. |

---

## Constraints / notes

- Vanilla HTML/CSS/JS only.
- Entertainment overlay — not affiliated with Cryptic / Perfect World / Neverwinter.
- Customize comments are marked in `OpenLockbox.cs` and `overlay/app.js` (`CONFIG`).

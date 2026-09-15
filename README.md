# Twitch Lockbox (Neverwinter)

Streamer.bot listens to **chat commands**. An OBS **browser source** plays the open animation when a command succeeds.

```
Chat  !lockbox [box]
   →  Streamer.bot (roll from data/boxes.json)
   →  chat announce + WebSocket broadcast
   →  overlay animation in OBS
```

**Drop rates in `data/boxes.json` are streamer-editable stand-ins, not official Cryptic tables.**

---

## Folder layout

```
├── README.md
├── data/boxes.json          ← loot tables (edit weights here)
├── streamerbot/
│   ├── OpenLockbox.cs       ← paste into Execute C# Code
│   └── actions.md           ← Streamer.bot + OBS steps
└── overlay/
    ├── index.html           ← OBS Browser Source
    ├── style.css
    └── app.js               ← connects to Streamer.bot WS
```

---

## Quick start

1. Clone this repo to something like `C:\Overlays\Twitch-Lockbox\`
2. In Streamer.bot: enable **WebSocket Server** (port `8080`)
3. Create action with chat commands `!lockbox` and `!open` → paste `streamerbot/OpenLockbox.cs`
4. Set `BOXES_JSON_PATH` in the C# to your `data\boxes.json` path
5. OBS → Browser Source → `overlay/index.html` at 1920×1080
6. Test animation: `overlay/index.html?demo=1`
7. Live: type `!lockbox` in chat

Full checklist: **`streamerbot/actions.md`**

---

## Commands

| Command | Result |
|---------|--------|
| `!lockbox` | Open default box |
| `!lockbox dragon` | Open that box (id / alias) |
| `!lockbox list` | List box ids |
| `!open …` | Alias of `!lockbox` |

Channel points are optional; same C# handles both.

---

## Starter boxes

| Id | Name |
|----|------|
| `dragon-cult` | Dragon Cult Lockbox |
| `leaping-flame` | Leaping Flame Lockbox |
| `justice` | Lockbox of Justice |
| `dark-omens` | Lockbox of Dark Omens |

Edit weights in `data/boxes.json` anytime; the C# re-reads the file each open.

---

## Troubleshooting

| Problem | Check |
|---------|--------|
| No animation | WebSocket Server on? Overlay without `?demo=1`? Browser source not shut down? |
| “boxes.json not found” | `BOXES_JSON_PATH` absolute path on the Streamer.bot PC |
| Command does nothing | Trigger is Chat Command `!lockbox` on the action that runs the C# |
| Wrong prize vs chat | Overlay must use the Custom broadcast (it does) — don’t re-roll in the page |

# Streamer.bot wire-up — chat command → overlay animation

## How it works

1. Viewer types in chat: `!lockbox` or `!lockbox dragon`
2. Streamer.bot Command trigger runs **Execute C# Code** (`OpenLockbox.cs`)
3. C# rolls loot from `data/boxes.json`, announces in chat
4. C# calls `CPH.WebsocketBroadcastJson` with `{ "type": "lockbox.open", ... }`
5. OBS **Browser Source** (`overlay/index.html`) is connected to Streamer.bot’s WebSocket Server and plays the open animation

The overlay does **not** parse chat itself. Streamer.bot listens to chat; the browser source only listens for the broadcast.

---

## 1. Enable WebSocket Server

1. Open **Streamer.bot**
2. **Servers / Clients → WebSocket Server** → Enable
3. Default: `ws://127.0.0.1:8080` (match overlay `CONFIG.port`)

## 2. Point `boxes.json` at a real path

Clone or copy this repo somewhere stable, e.g.:

`C:\Overlays\Twitch-Lockbox\data\boxes.json`

Edit `BOXES_JSON_PATH` at the top of `OpenLockbox.cs` to that absolute path.

## 3. Create the chat commands (primary)

1. **Actions** → add action named e.g. `Open Lockbox`
2. **Triggers** → **Twitch → Chat Command** (or Commands)
   - Command: `!lockbox`
   - Also add `!open` (same action) if you want both
3. **Sub-Actions** → **Core → Execute C# Code**
   - Paste entire `OpenLockbox.cs`
   - Compile / save

### Commands viewers use

| Chat | Effect |
|------|--------|
| `!lockbox` | Open default box (`DEFAULT_BOX_ID`) |
| `!lockbox dragon` | Open matching box (id / alias / partial name) |
| `!lockbox list` | List enabled box ids |
| `!open` / `!open justice` | Same as `!lockbox` |

## 4. OBS Browser Source (animation)

1. Sources → **Browser**
2. Local file → `overlay/index.html` (from this repo)
3. Width **1920**, Height **1080**
4. Keep source active while streaming so the WebSocket stays connected

**Layout test without Streamer.bot:** open `overlay/index.html?demo=1`

**Live test:** Streamer.bot running + overlay open (no demo) → type `!lockbox` in chat → chest animation + prize reveal.

## 5. Optional — Channel Points

Same C# works on a Channel Point redemption:

1. Reward title `Open Lockbox`, Require Text = yes (box name)
2. Trigger: Reward Redemption → same action / Execute C#

## 6. Arguments set after a successful roll

| Argument | Meaning |
|----------|---------|
| `userName` | Viewer |
| `boxId` / `boxName` | Resolved box |
| `prizeName` / `prizeRarity` / `prizeId` | Rolled item |

## 7. Overlay event contract

```json
{
  "type": "lockbox.open",
  "userName": "ViewerName",
  "boxId": "dragon-cult",
  "boxName": "Dragon Cult Lockbox",
  "prizeName": "Azure Wyrmling Mount",
  "prizeRarity": "mythic",
  "source": "chat"
}
```

Overlay listens for **`General.Custom`** and plays the animation when `type` is `lockbox.open`.

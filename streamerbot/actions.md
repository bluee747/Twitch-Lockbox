# Streamer.bot wire-up — Channel Points → overlay animation

## How it works

1. Viewer redeems Channel Points reward **Open Lockbox** (100 pts; optional text = box name/alias)
2. Streamer.bot Reward Redemption trigger runs **Execute C# Code** (`OpenLockbox.cs`)
3. C# rolls loot from `data/boxes.json`, appends to `data/inventory.json`, announces in chat
4. C# calls `CPH.WebsocketBroadcastJson` with `{ "type": "lockbox.open", "icon", "prizeId", "inventoryCount", ... }`
5. OBS **Browser Source** (`overlay/index.html`) plays the chest open + prize reveal

**Free `!lockbox` / `!open` chat opens are disabled.** Opening is Channel Points only.

Optional: free chat command `!inventory` → `ShowInventory.cs` (view recent prizes only).

---

## 1. Enable WebSocket Server

1. Open **Streamer.bot**
2. **Servers / Clients → WebSocket Server** → Enable
3. Default: `ws://127.0.0.1:8080` (match overlay `CONFIG.port`)

## 2. Point JSON paths at the PC install

Repo path on streamer PC:

`D:\Overlays\Twitch-Lockbox\`

Constants at the top of both C# files:

| Constant | Default |
|----------|---------|
| `BOXES_JSON_PATH` | `D:\Overlays\Twitch-Lockbox\data\boxes.json` |
| `INVENTORY_JSON_PATH` | `D:\Overlays\Twitch-Lockbox\data\inventory.json` |

## 3. Channel Points reward (required for opens)

1. Twitch → Channel Points → create **Open Lockbox** (e.g. 100 pts)
2. **Require Viewer to Enter Text** = Yes (box id / alias, or blank for default `wild-adventures`)
3. Use `assets/open-lockbox-reward.png` (or the 112/56/28 variants) as the reward icon
4. In Streamer.bot: **Actions** → `Open Lockbox`
5. **Triggers** → **Twitch → Channel Point Redemption** → reward **Open Lockbox**
6. **Sub-Actions** → **Core → Execute C# Code** → paste entire `OpenLockbox.cs` → compile / save

Do **not** attach chat commands `!lockbox` / `!open` to this action.

### Reward text examples

| Text | Effect |
|------|--------|
| *(empty)* | Default box (`DEFAULT_BOX_ID` = `wild-adventures`) |
| `dragon` / `dragon-cult` | Dragon Cult Lockbox |
| `list` | List enabled box ids (no open / no charge cancel if configured) |

## 4. Optional — `!inventory` (view only)

1. **Actions** → add action e.g. `Show Lockbox Inventory`
2. **Triggers** → **Command Triggered** / Chat Command `!inventory`
3. **Sub-Actions** → Execute C# Code → paste `ShowInventory.cs` only
4. Does **not** open boxes or spend points

## 5. OBS Browser Source (animation)

1. Sources → **Browser**
2. Local file → `D:\Overlays\Twitch-Lockbox\overlay\index.html`
3. Width **1920**, Height **1080**
4. Keep source active while streaming so the WebSocket stays connected
5. After pulling updates: **Refresh cache of current page** on the Browser Source (or toggle visibility)

**Layout test without Streamer.bot:** open `overlay/index.html?demo=1`

**Inventory flash:** included on each open (`@{user} inventory: N opens`). Optional panel: `?inventory=1`

## 6. Arguments set after a successful roll

| Argument | Meaning |
|----------|---------|
| `userName` | Viewer display name |
| `boxId` / `boxName` | Resolved box |
| `prizeName` / `prizeRarity` / `prizeId` | Rolled item |
| `lockboxIcon` | Emoji or icon path from boxes.json |
| `inventoryCount` | Opens stored for that viewer (max 50 kept) |

## 7. Overlay event contract

```json
{
  "type": "lockbox.open",
  "userName": "ViewerName",
  "boxId": "wild-adventures",
  "boxName": "Wild Adventures Lockbox",
  "prizeId": "wild-adventures-mythic-mount",
  "prizeName": "Star Angler Mount",
  "prizeRarity": "mythic",
  "icon": "🐴",
  "inventoryCount": 3,
  "source": "channel-points"
}
```

Overlay listens for **`General.Custom`** and plays when `type` is `lockbox.open`.

## 8. Inventory file

`data/inventory.json`:

```json
{
  "viewers": {
    "loginlower": {
      "displayName": "DisplayName",
      "opens": [
        {
          "ts": "2026-09-15T12:00:00.0000000Z",
          "boxId": "wild-adventures",
          "boxName": "Wild Adventures Lockbox",
          "prizeId": "...",
          "prizeName": "...",
          "prizeRarity": "mythic",
          "icon": "🐴"
        }
      ]
    }
  }
}
```

Last **~50** opens per viewer are retained.

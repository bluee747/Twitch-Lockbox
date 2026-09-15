# Streamer.bot Wire-up — Neverwinter Lockbox Opener

## 1. Enable WebSocket Server

1. Open **Streamer.bot**
2. Go to **Servers / Clients → WebSocket Server**
3. Enable the server
4. Default: `ws://127.0.0.1:8080` (leave port **8080** unless you change it in the overlay CONFIG too)
5. Optionally allow auto-start with Streamer.bot

The OBS overlay connects here and listens for:
- `General.Custom` — preferred (carries the rolled prize from `CPH.WebsocketBroadcastJson`)
- `Twitch.RewardRedemption` — fallback / secondary (title match only; prize comes from Custom)

## 2. Create Channel Point Reward

On Twitch (Creator Dashboard → Viewer Rewards → Channel Points):

| Setting | Value |
|--------|--------|
| Title | `Open Lockbox` (must match C# `REWARD_TITLE` and overlay `CONFIG.rewardTitle`) |
| Cost | Your choice (e.g. 500–2000) |
| Require Viewer to Enter Text | **Yes** |
| Prompt | `Box name (dragon-cult, leaping-flame, justice, dark-omens)` |
| Skip Reward Requests Queue | Optional (recommended for auto-fulfill after C# runs) |

## 3. Create the Streamer.bot Action

1. **Actions** → add action, name it e.g. `Open Lockbox`
2. **Triggers** → add **Twitch → Channel Reward Redemption** (or Reward Redemption Add)
   - Filter / select reward: **Open Lockbox**
3. **Sub-Actions** (order matters):

### Sub-action A — Execute C# Code
- Add **Core → Execute C# Code**
- Paste the entire contents of `OpenLockbox.cs`
- **Edit** `BOXES_JSON_PATH` to the absolute path of `data/boxes.json` on *this* PC
- Optionally edit `DEFAULT_BOX_ID`, `REWARD_TITLE`, `CANCEL_ON_INVALID_BOX`
- Compile / save (Streamer.bot should show no errors)

### Sub-action B — (Optional) Send Message
The C# already calls `CPH.SendMessage`. You can skip an extra Send Message, or add one that uses arguments:

```
%userName% opened %boxName% → [%prizeRarity%] %prizeName%
```

### Sub-action C — (Optional) Fulfill redemption
If you did not skip the queue: add **Twitch → Reward Redemption → Fulfill** (or Complete) using `%rewardId%` / `%redemptionId%`.

## 4. Arguments set by the C# script

Downstream sub-actions can use:

| Argument | Meaning |
|----------|---------|
| `userName` | Viewer who redeemed |
| `boxId` | Resolved box id |
| `boxName` | Display name |
| `prizeName` | Rolled item name |
| `prizeRarity` | common / uncommon / rare / epic / legendary / mythic |
| `prizeId` | Item id from JSON |
| `lockboxImage` | Optional image path/URL from JSON |

## 5. Overlay event contract

After a successful roll, C# calls:

```csharp
CPH.WebsocketBroadcastJson("{ \"type\": \"lockbox.open\", ... }");
```

Clients receive this as **`General.Custom`**. Payload fields (inside `data` — see overlay `app.js`):

```json
{
  "type": "lockbox.open",
  "userName": "ViewerName",
  "boxId": "dragon-cult",
  "boxName": "Dragon Cult Lockbox",
  "prizeId": "dc-mythic-mount",
  "prizeName": "Azure Wyrmling Mount",
  "prizeRarity": "mythic",
  "prizeImage": "",
  "rewardTitle": "Open Lockbox",
  "rewardId": "...",
  "redemptionId": "..."
}
```

**Preferred path:** overlay animates from this Custom payload (includes the real roll).  
**RewardRedemption-only:** overlay may show a “opening…” teaser, but the prize reveal needs the Custom broadcast (or chat parse — not implemented).

## 6. Test redeem

1. Start Streamer.bot (WebSocket enabled)
2. Open overlay in a browser: `overlay/index.html?demo=1` to verify animation without Twitch
3. Remove `?demo=1`, keep Streamer.bot running, redeem **Open Lockbox** with input `dragon` or leave blank
4. Confirm chat message + OBS overlay animation
5. Try an invalid name → should get help text listing valid boxes (and optional cancel)

## 7. Editing drop rates

Edit `data/boxes.json` → change `weight` values (higher = more common). No Streamer.bot restart needed if the C# re-reads the file every redeem (it does). Reload OBS browser source only if you changed overlay files.

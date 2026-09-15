// =============================================================================
// Neverwinter Lockbox Opener — Streamer.bot Execute C# Code (CPHInline)
// Paste this entire file into an Execute C# Code sub-action.
//
// Primary trigger: Twitch chat command  !lockbox  or  !open  [box name]
// Optional:       Channel Point Reward "Open Lockbox" (Require User Input)
//
// Flow: chat/redeem → roll from boxes.json → chat announce → WebsocketBroadcastJson
//       → OBS browser source overlay plays the open animation (General.Custom)
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // -------------------------------------------------------------------------
    // CUSTOMIZE THESE
    // -------------------------------------------------------------------------
    // Absolute path to boxes.json on the machine running Streamer.bot.
    // Example Windows: @"C:\Overlays\Twitch-Lockbox\data\boxes.json"
    private const string BOXES_JSON_PATH = @"C:\Overlays\Twitch-Lockbox\data\boxes.json";

    // Used when the command has no box argument — change to any enabled box id.
    private const string DEFAULT_BOX_ID = "dragon-cult";

    // Chat command names (without !). Keep in sync with Streamer.bot Command triggers.
    private static readonly string[] COMMAND_NAMES = { "lockbox", "open" };

    // Optional channel-point title (only used in broadcast payload / overlay teaser).
    private const string REWARD_TITLE = "Open Lockbox";

    // If true and this run came from a channel-point redeem, cancel on invalid box.
    private const bool CANCEL_ON_INVALID_BOX = true;

    // -------------------------------------------------------------------------

    public bool Execute()
    {
        string userName = "";
        string rewardId = "";
        string redemptionId = "";

        CPH.TryGetArg("userName", out userName);
        if (string.IsNullOrWhiteSpace(userName))
            CPH.TryGetArg("user", out userName);
        CPH.TryGetArg("rewardId", out rewardId);
        CPH.TryGetArg("redemptionId", out redemptionId);

        userName = (userName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "UnknownViewer";

        string boxInput = ExtractBoxInput();

        JObject root;
        try
        {
            if (!File.Exists(BOXES_JSON_PATH))
            {
                CPH.SendMessage($"[Lockbox] boxes.json not found at: {BOXES_JSON_PATH} — edit BOXES_JSON_PATH in OpenLockbox.cs");
                return false;
            }
            string json = File.ReadAllText(BOXES_JSON_PATH, Encoding.UTF8);
            root = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            CPH.LogError("[Lockbox] Failed to load boxes.json: " + ex.Message);
            CPH.SendMessage("[Lockbox] Could not load loot tables. Check Streamer.bot logs.");
            return false;
        }

        JArray boxesArr = root["boxes"] as JArray;
        if (boxesArr == null || boxesArr.Count == 0)
        {
            CPH.SendMessage("[Lockbox] No boxes defined in boxes.json.");
            return false;
        }

        List<BoxDef> enabled = new List<BoxDef>();
        foreach (JToken t in boxesArr)
        {
            BoxDef b = t.ToObject<BoxDef>();
            if (b != null && b.enabled)
                enabled.Add(b);
        }

        // !lockbox list  /  !boxes-style help
        if (IsListRequest(boxInput))
        {
            string options = string.Join(", ", enabled.Select(b => b.id));
            CPH.SendMessage($"@{userName} Lockboxes: {options} — use !lockbox <name>");
            return true;
        }

        BoxDef box = ResolveBox(enabled, boxInput);

        if (box == null)
        {
            string options = string.Join(", ", enabled.Select(b => b.displayName + " (" + b.id + ")"));
            CPH.SendMessage($"@{userName} Unknown lockbox \"{boxInput}\". Try: {options}");
            if (CANCEL_ON_INVALID_BOX && !string.IsNullOrEmpty(redemptionId))
            {
                try { CPH.TwitchRedemptionCancel(rewardId, redemptionId); } catch { /* older SB */ }
            }
            return false;
        }

        if (box.items == null || box.items.Count == 0)
        {
            CPH.SendMessage($"[Lockbox] Box \"{box.displayName}\" has no items configured.");
            return false;
        }

        ItemDef prize = WeightedRoll(box.items);
        if (prize == null)
        {
            CPH.SendMessage("[Lockbox] Roll failed — check item weights.");
            return false;
        }

        CPH.SetArgument("prizeName", prize.name);
        CPH.SetArgument("prizeRarity", prize.rarity);
        CPH.SetArgument("prizeId", prize.id ?? "");
        CPH.SetArgument("boxId", box.id);
        CPH.SetArgument("boxName", box.displayName);
        CPH.SetArgument("userName", userName);
        CPH.SetArgument("lockboxImage", prize.image ?? "");

        CPH.SendMessage($"🔐 @{userName} opened a {box.displayName} and received [{prize.rarity.ToUpper()}] {prize.name}!");

        // OBS browser source listens for this → runs the open animation
        string payload = BuildLockboxJson(userName, box, prize, rewardId, redemptionId);
        try
        {
            CPH.WebsocketBroadcastJson(payload);
        }
        catch (Exception ex)
        {
            CPH.LogWarn("[Lockbox] WebsocketBroadcastJson failed (is WebSocket Server enabled?): " + ex.Message);
        }

        return true;
    }

    /// <summary>
    /// Prefer Streamer.bot Command "input" / "rawInput", else strip !lockbox/!open from full message.
    /// </summary>
    private string ExtractBoxInput()
    {
        string input = "";
        CPH.TryGetArg("input", out input);
        if (string.IsNullOrWhiteSpace(input))
            CPH.TryGetArg("rawInput", out input);
        if (string.IsNullOrWhiteSpace(input))
            CPH.TryGetArg("userInput", out input);

        string message = "";
        CPH.TryGetArg("msg", out message);
        if (string.IsNullOrWhiteSpace(message))
            CPH.TryGetArg("message", out message);

        if (!string.IsNullOrWhiteSpace(input))
            return input.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return "";

        string m = message.Trim();
        // Strip leading !command
        foreach (string cmd in COMMAND_NAMES)
        {
            var re = new Regex(@"^!" + Regex.Escape(cmd) + @"\b", RegexOptions.IgnoreCase);
            if (re.IsMatch(m))
            {
                m = re.Replace(m, "", 1).Trim();
                break;
            }
        }
        return m;
    }

    private static bool IsListRequest(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        string k = input.Trim().ToLowerInvariant();
        return k == "list" || k == "help" || k == "boxes" || k == "?";
    }

    private BoxDef ResolveBox(List<BoxDef> enabled, string input)
    {
        if (enabled == null || enabled.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(input))
        {
            BoxDef byDefault = enabled.FirstOrDefault(b =>
                string.Equals(b.id, DEFAULT_BOX_ID, StringComparison.OrdinalIgnoreCase));
            return byDefault ?? enabled[0];
        }

        string key = Normalize(input);
        foreach (BoxDef b in enabled)
        {
            if (Normalize(b.id) == key)
                return b;
            if (Normalize(b.displayName) == key)
                return b;
            if (b.aliases != null)
            {
                foreach (string a in b.aliases)
                {
                    if (Normalize(a) == key)
                        return b;
                }
            }
        }

        foreach (BoxDef b in enabled)
        {
            if (Normalize(b.id).Contains(key) || key.Contains(Normalize(b.id)))
                return b;
            if (Normalize(b.displayName).Contains(key))
                return b;
            if (b.aliases != null && b.aliases.Any(a => Normalize(a).Contains(key) || key.Contains(Normalize(a))))
                return b;
        }

        return null;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return new string(s.ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_' && c != '\'')
            .ToArray());
    }

    private ItemDef WeightedRoll(List<ItemDef> items)
    {
        var valid = items.Where(i => i != null && i.weight > 0).ToList();
        if (valid.Count == 0) return null;

        int total = valid.Sum(i => i.weight);
        int roll = new Random().Next(0, total);
        int cumulative = 0;
        foreach (ItemDef item in valid)
        {
            cumulative += item.weight;
            if (roll < cumulative)
                return item;
        }
        return valid[valid.Count - 1];
    }

    private string BuildLockboxJson(string userName, BoxDef box, ItemDef prize, string rewardId, string redemptionId)
    {
        var obj = new JObject
        {
            ["type"] = "lockbox.open",
            ["userName"] = userName ?? "",
            ["boxId"] = box.id ?? "",
            ["boxName"] = box.displayName ?? "",
            ["prizeId"] = prize.id ?? "",
            ["prizeName"] = prize.name ?? "",
            ["prizeRarity"] = prize.rarity ?? "",
            ["prizeImage"] = prize.image ?? "",
            ["rewardTitle"] = REWARD_TITLE,
            ["rewardId"] = rewardId ?? "",
            ["redemptionId"] = redemptionId ?? "",
            ["source"] = string.IsNullOrEmpty(redemptionId) ? "chat" : "channel-points"
        };
        return obj.ToString(Formatting.None);
    }

    public class BoxDef
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public List<string> aliases { get; set; }
        public bool enabled { get; set; }
        public List<ItemDef> items { get; set; }
    }

    public class ItemDef
    {
        public string id { get; set; }
        public string name { get; set; }
        public string rarity { get; set; }
        public int weight { get; set; }
        public string image { get; set; }
    }
}

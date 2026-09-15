// =============================================================================
// Neverwinter Lockbox Opener — Streamer.bot Execute C# Code (CPHInline)
// CHANNEL POINTS ONLY — reward "Open Lockbox" (free !lockbox/!open disabled).
// No System.Linq / Regex — works with default Streamer.bot references.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // Absolute paths on the streamer PC (edit if you install elsewhere):
    private const string BOXES_JSON_PATH = @"D:\Overlays\Twitch-Lockbox\data\boxes.json";
    private const string INVENTORY_JSON_PATH = @"D:\Overlays\Twitch-Lockbox\data\inventory.json";
    // Relative-friendly (same folder layout): data\boxes.json and data\inventory.json
    // next to the repo root when run from D:\Overlays\Twitch-Lockbox\

    private const string DEFAULT_BOX_ID = "wild-adventures";
    private const string REWARD_TITLE = "Open Lockbox";
    private const bool CANCEL_ON_INVALID_BOX = true;
    private const int MAX_OPENS_PER_VIEWER = 50;

    public bool Execute()
    {
        string userName = "";
        string userLogin = "";
        string rewardId = "";
        string redemptionId = "";
        string rewardName = "";

        CPH.TryGetArg("userName", out userName);
        if (string.IsNullOrWhiteSpace(userName))
            CPH.TryGetArg("user", out userName);
        CPH.TryGetArg("userLogin", out userLogin);
        if (string.IsNullOrWhiteSpace(userLogin))
            CPH.TryGetArg("userIdName", out userLogin);
        if (string.IsNullOrWhiteSpace(userLogin))
            userLogin = userName;
        CPH.TryGetArg("rewardId", out rewardId);
        CPH.TryGetArg("redemptionId", out redemptionId);
        CPH.TryGetArg("rewardName", out rewardName);
        if (string.IsNullOrWhiteSpace(rewardName))
            CPH.TryGetArg("rewardTitle", out rewardName);

        userName = (userName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "UnknownViewer";
        userLogin = (userLogin ?? userName).Trim().ToLowerInvariant();

        // Channel points only — reject free chat command opens
        if (string.IsNullOrEmpty(redemptionId))
        {
            CPH.SendMessage("@" + userName + " Lockboxes open via Channel Points reward \"" + REWARD_TITLE + "\" only. Use !inventory to see your recent prizes.");
            return false;
        }

        string boxInput = ExtractBoxInput();

        JObject root;
        try
        {
            if (!File.Exists(BOXES_JSON_PATH))
            {
                CPH.SendMessage("[Lockbox] boxes.json not found at: " + BOXES_JSON_PATH + " — edit BOXES_JSON_PATH in OpenLockbox.cs");
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

        if (IsListRequest(boxInput))
        {
            CPH.SendMessage("@" + userName + " Lockboxes: " + JoinBoxIds(enabled));
            return true;
        }

        BoxDef box = ResolveBox(enabled, boxInput);

        if (box == null)
        {
            CPH.SendMessage("@" + userName + " Unknown lockbox \"" + boxInput + "\". Try: " + JoinBoxLabels(enabled));
            if (CANCEL_ON_INVALID_BOX && !string.IsNullOrEmpty(redemptionId))
            {
                try { CPH.TwitchRedemptionCancel(rewardId, redemptionId); } catch { }
            }
            return false;
        }

        if (box.items == null || box.items.Count == 0)
        {
            CPH.SendMessage("[Lockbox] Box \"" + box.displayName + "\" has no items configured.");
            return false;
        }

        ItemDef prize = WeightedRoll(box.items);
        if (prize == null)
        {
            CPH.SendMessage("[Lockbox] Roll failed — check item weights.");
            return false;
        }

        string icon = prize.icon ?? "";
        int inventoryCount = AppendInventory(userLogin, userName, box, prize, icon);

        CPH.SetArgument("prizeName", prize.name);
        CPH.SetArgument("prizeRarity", prize.rarity);
        CPH.SetArgument("prizeId", prize.id ?? "");
        CPH.SetArgument("boxId", box.id);
        CPH.SetArgument("boxName", box.displayName);
        CPH.SetArgument("userName", userName);
        CPH.SetArgument("lockboxIcon", icon);
        CPH.SetArgument("inventoryCount", inventoryCount.ToString());

        CPH.SendMessage("🔐 @" + userName + " opened a " + box.displayName + " and received [" + (prize.rarity ?? "").ToUpper() + "] " + prize.name + "! (opens: " + inventoryCount + ")");

        string payload = BuildLockboxJson(userName, box, prize, icon, inventoryCount, rewardId, redemptionId);
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

    private int AppendInventory(string loginLower, string displayName, BoxDef box, ItemDef prize, string icon)
    {
        JObject invRoot;
        try
        {
            if (File.Exists(INVENTORY_JSON_PATH))
            {
                string raw = File.ReadAllText(INVENTORY_JSON_PATH, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(raw))
                    invRoot = new JObject();
                else
                    invRoot = JObject.Parse(raw);
            }
            else
            {
                invRoot = new JObject();
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn("[Lockbox] inventory read failed, starting fresh: " + ex.Message);
            invRoot = new JObject();
        }

        JObject viewers = invRoot["viewers"] as JObject;
        if (viewers == null)
        {
            viewers = new JObject();
            invRoot["viewers"] = viewers;
        }

        JObject viewer = viewers[loginLower] as JObject;
        if (viewer == null)
        {
            viewer = new JObject();
            viewers[loginLower] = viewer;
        }
        viewer["displayName"] = displayName;

        JArray opens = viewer["opens"] as JArray;
        if (opens == null)
        {
            opens = new JArray();
            viewer["opens"] = opens;
        }

        JObject entry = new JObject
        {
            ["ts"] = DateTime.UtcNow.ToString("o"),
            ["boxId"] = box.id ?? "",
            ["boxName"] = box.displayName ?? "",
            ["prizeId"] = prize.id ?? "",
            ["prizeName"] = prize.name ?? "",
            ["prizeRarity"] = prize.rarity ?? "",
            ["icon"] = icon ?? ""
        };
        opens.Add(entry);

        while (opens.Count > MAX_OPENS_PER_VIEWER)
            opens.RemoveAt(0);

        try
        {
            string dir = Path.GetDirectoryName(INVENTORY_JSON_PATH);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(INVENTORY_JSON_PATH, invRoot.ToString(Formatting.Indented), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            CPH.LogError("[Lockbox] inventory write failed: " + ex.Message);
        }

        return opens.Count;
    }

    private string ExtractBoxInput()
    {
        string input = "";
        CPH.TryGetArg("input", out input);
        if (string.IsNullOrWhiteSpace(input))
            CPH.TryGetArg("rawInput", out input);
        if (string.IsNullOrWhiteSpace(input))
            CPH.TryGetArg("userInput", out input);
        if (string.IsNullOrWhiteSpace(input))
            CPH.TryGetArg("rawInputEscaped", out input);

        if (!string.IsNullOrWhiteSpace(input))
            return input.Trim();
        return "";
    }

    private static bool IsListRequest(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        string k = input.Trim().ToLowerInvariant();
        return k == "list" || k == "help" || k == "boxes" || k == "?";
    }

    private static string JoinBoxIds(List<BoxDef> enabled)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < enabled.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(enabled[i].id);
        }
        return sb.ToString();
    }

    private static string JoinBoxLabels(List<BoxDef> enabled)
    {
        StringBuilder sb = new StringBuilder();
        int max = enabled.Count < 8 ? enabled.Count : 8;
        for (int i = 0; i < max; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(enabled[i].displayName);
            sb.Append(" (");
            sb.Append(enabled[i].id);
            sb.Append(")");
        }
        if (enabled.Count > max) sb.Append(", …");
        return sb.ToString();
    }

    private BoxDef ResolveBox(List<BoxDef> enabled, string input)
    {
        if (enabled == null || enabled.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(input))
        {
            for (int i = 0; i < enabled.Count; i++)
            {
                if (string.Equals(enabled[i].id, DEFAULT_BOX_ID, StringComparison.OrdinalIgnoreCase))
                    return enabled[i];
            }
            return enabled[0];
        }

        string key = Normalize(input);

        for (int i = 0; i < enabled.Count; i++)
        {
            BoxDef b = enabled[i];
            if (Normalize(b.id) == key) return b;
            if (Normalize(b.displayName) == key) return b;
            if (b.aliases != null)
            {
                for (int a = 0; a < b.aliases.Count; a++)
                {
                    if (Normalize(b.aliases[a]) == key) return b;
                }
            }
        }

        for (int i = 0; i < enabled.Count; i++)
        {
            BoxDef b = enabled[i];
            string nid = Normalize(b.id);
            string ndn = Normalize(b.displayName);
            if (nid.Contains(key) || key.Contains(nid) || ndn.Contains(key))
                return b;
            if (b.aliases != null)
            {
                for (int a = 0; a < b.aliases.Count; a++)
                {
                    string na = Normalize(b.aliases[a]);
                    if (na.Contains(key) || key.Contains(na))
                        return b;
                }
            }
        }

        return null;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder();
        string lower = s.ToLowerInvariant();
        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '\'')
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private ItemDef WeightedRoll(List<ItemDef> items)
    {
        List<ItemDef> valid = new List<ItemDef>();
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ItemDef it = items[i];
            if (it != null && it.weight > 0)
            {
                valid.Add(it);
                total += it.weight;
            }
        }
        if (valid.Count == 0 || total <= 0) return null;

        int roll = new Random().Next(0, total);
        int cumulative = 0;
        for (int i = 0; i < valid.Count; i++)
        {
            cumulative += valid[i].weight;
            if (roll < cumulative)
                return valid[i];
        }
        return valid[valid.Count - 1];
    }

    private string BuildLockboxJson(string userName, BoxDef box, ItemDef prize, string icon, int inventoryCount, string rewardId, string redemptionId)
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
            ["icon"] = icon ?? "",
            ["inventoryCount"] = inventoryCount,
            ["rewardTitle"] = REWARD_TITLE,
            ["rewardId"] = rewardId ?? "",
            ["redemptionId"] = redemptionId ?? "",
            ["source"] = "channel-points"
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
        public string icon { get; set; }
        public string category { get; set; }
        public string image { get; set; }
    }
}

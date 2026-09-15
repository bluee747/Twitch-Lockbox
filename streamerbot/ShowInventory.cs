// =============================================================================
// ShowInventory — Streamer.bot Execute C# Code (CPHInline)
// Free chat command: !inventory  (viewing only — does NOT open boxes)
// No System.Linq / Regex — works with default Streamer.bot references.
// =============================================================================
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    // Same path as OpenLockbox.cs — edit if needed:
    private const string INVENTORY_JSON_PATH = @"D:\Overlays\Twitch-Lockbox\data\inventory.json";
    // Relative-friendly: data\inventory.json under the repo root.
    private const int SHOW_LAST = 5;
    private const int MAX_CHAT_CHARS = 450;

    public bool Execute()
    {
        string userName = "";
        string userLogin = "";

        CPH.TryGetArg("userName", out userName);
        if (string.IsNullOrWhiteSpace(userName))
            CPH.TryGetArg("user", out userName);
        CPH.TryGetArg("userLogin", out userLogin);
        if (string.IsNullOrWhiteSpace(userLogin))
            userLogin = userName;

        userName = (userName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "UnknownViewer";
        userLogin = (userLogin ?? userName).Trim().ToLowerInvariant();

        if (!File.Exists(INVENTORY_JSON_PATH))
        {
            CPH.SendMessage("@" + userName + " No inventory yet — redeem Channel Points \"" + "Open Lockbox" + "\" to start collecting!");
            return true;
        }

        JObject root;
        try
        {
            string raw = File.ReadAllText(INVENTORY_JSON_PATH, Encoding.UTF8);
            root = JObject.Parse(raw);
        }
        catch (Exception ex)
        {
            CPH.LogError("[Inventory] read failed: " + ex.Message);
            CPH.SendMessage("@" + userName + " Inventory temporarily unavailable.");
            return false;
        }

        JObject viewers = root["viewers"] as JObject;
        if (viewers == null || viewers[userLogin] == null)
        {
            CPH.SendMessage("@" + userName + " Inventory empty — redeem \"Open Lockbox\" with Channel Points!");
            return true;
        }

        JObject viewer = viewers[userLogin] as JObject;
        JArray opens = viewer["opens"] as JArray;
        if (opens == null || opens.Count == 0)
        {
            CPH.SendMessage("@" + userName + " Inventory empty — redeem \"Open Lockbox\" with Channel Points!");
            return true;
        }

        int total = opens.Count;
        int start = total - SHOW_LAST;
        if (start < 0) start = 0;

        StringBuilder sb = new StringBuilder();
        sb.Append("@");
        sb.Append(userName);
        sb.Append(" inventory (");
        sb.Append(total);
        sb.Append(" opens): ");

        for (int i = start; i < total; i++)
        {
            JObject o = opens[i] as JObject;
            if (o == null) continue;
            if (i > start) sb.Append(" | ");
            string rarity = (string)o["prizeRarity"] ?? "";
            string prize = (string)o["prizeName"] ?? "?";
            string box = (string)o["boxName"] ?? "";
            string icon = (string)o["icon"] ?? "";
            if (!string.IsNullOrEmpty(icon) && icon.Length <= 4)
            {
                sb.Append(icon);
                sb.Append(" ");
            }
            if (!string.IsNullOrEmpty(rarity))
            {
                sb.Append("[");
                sb.Append(rarity.ToUpperInvariant());
                sb.Append("] ");
            }
            sb.Append(prize);
            if (!string.IsNullOrEmpty(box))
            {
                sb.Append(" (");
                sb.Append(ShortBox(box));
                sb.Append(")");
            }
        }

        string msg = sb.ToString();
        if (msg.Length > MAX_CHAT_CHARS)
            msg = msg.Substring(0, MAX_CHAT_CHARS - 1) + "…";

        CPH.SendMessage(msg);
        return true;
    }

    private static string ShortBox(string boxName)
    {
        if (string.IsNullOrEmpty(boxName)) return "";
        string s = boxName.Replace(" Lockbox", "").Replace("Lockbox of ", "");
        if (s.Length > 18) s = s.Substring(0, 18);
        return s;
    }
}

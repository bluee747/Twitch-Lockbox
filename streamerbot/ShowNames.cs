// =============================================================================
// ShowNames — Streamer.bot Execute C# Code (CPHInline)
// Free chat command: !names
// Lists enabled lockbox ids viewers can type when redeeming "Open Lockbox".
// Does NOT open boxes. No System.Linq / Regex.
// =============================================================================
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string BOXES_JSON_PATH = @"D:\Overlays\Twitch-Lockbox\data\boxes.json";
    private const int MAX_CHAT_CHARS = 450;

    public bool Execute()
    {
        string userName = "";
        CPH.TryGetArg("userName", out userName);
        if (string.IsNullOrWhiteSpace(userName))
            CPH.TryGetArg("user", out userName);
        userName = (userName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "viewer";

        if (!File.Exists(BOXES_JSON_PATH))
        {
            CPH.SendMessage("@" + userName + " Lockbox list unavailable (boxes.json missing).");
            return false;
        }

        JObject root;
        try
        {
            string raw = File.ReadAllText(BOXES_JSON_PATH, Encoding.UTF8);
            root = JObject.Parse(raw);
        }
        catch (Exception ex)
        {
            CPH.LogError("[Names] read failed: " + ex.Message);
            CPH.SendMessage("@" + userName + " Could not load lockbox names.");
            return false;
        }

        JArray boxes = root["boxes"] as JArray;
        if (boxes == null || boxes.Count == 0)
        {
            CPH.SendMessage("@" + userName + " No lockboxes configured.");
            return true;
        }

        // Build id list (enabled only)
        StringBuilder ids = new StringBuilder();
        int count = 0;
        for (int i = 0; i < boxes.Count; i++)
        {
            JObject b = boxes[i] as JObject;
            if (b == null) continue;
            bool enabled = true;
            if (b["enabled"] != null)
                enabled = b["enabled"].Type != JTokenType.Boolean || (bool)b["enabled"];
            if (!enabled) continue;

            string id = b["id"] != null ? b["id"].ToString() : "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (count > 0) ids.Append(", ");
            ids.Append(id);
            count++;
        }

        if (count == 0)
        {
            CPH.SendMessage("@" + userName + " No enabled lockboxes.");
            return true;
        }

        string header = "@" + userName + " Lockbox ids for Open Lockbox (" + count + "): ";
        string all = ids.ToString();

        // Twitch length: send one or two messages
        if (header.Length + all.Length <= MAX_CHAT_CHARS)
        {
            CPH.SendMessage(header + all);
            return true;
        }

        // Split ids across messages
        CPH.SendMessage(header);
        string[] parts = all.Split(new string[] { ", " }, StringSplitOptions.None);
        StringBuilder chunk = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            string piece = parts[i];
            int need = chunk.Length == 0 ? piece.Length : chunk.Length + 2 + piece.Length;
            if (need > MAX_CHAT_CHARS && chunk.Length > 0)
            {
                CPH.SendMessage(chunk.ToString());
                chunk.Length = 0;
            }
            if (chunk.Length > 0) chunk.Append(", ");
            chunk.Append(piece);
        }
        if (chunk.Length > 0)
            CPH.SendMessage(chunk.ToString());

        return true;
    }
}

using System;
using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseHome
    {
        private readonly Plugin Plugin;

        public CaseHome(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void HomeCase(string trimtext, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "HomeStart")
            {
                if (trimtext == "The home contains:")
                {
                    Plugin.Host.EchoText("Scanning Home.");
                    Plugin.ScanStart("Home");
                    return;
                }

                if (trimtext.StartsWith("Your documentation filed with the Estate Holders", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Host.EchoText("Skipping Home");
                    Plugin.GuildCheck(trimtext);
                    return;
                }

                if (Plugin.IsDenied(trimtext))
                {
                    Plugin.Host.EchoText("Skipping Home");
                    Plugin.GuildCheck(trimtext);
                    return;
                }
            }

            if (scanMode == "Home")
                HandleHomeInventory(trimtext, ref lastItem, currentData);
        }

        private void HandleHomeInventory(string trimtext, ref ItemData lastItem, CharacterData currentData)
        {
            // Home list ends when a prompt or roundtime appears
            if (Regex.IsMatch(trimtext, @"\^[^>]*>|[^>]*\>|>|\^\>|^Roundtime:"))
            {
                Plugin.GuildCheck(trimtext);
                return;
            }

            // Attached item (in/on/under/behind furniture)
            if (trimtext.StartsWith("Attached:"))
            {
                string tap = trimtext.Replace("Attached: ", "");
                if (tap[^1] == '.') tap = tap.TrimEnd('.');
                tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");

                lastItem = (lastItem?.parent ?? lastItem)?.AddItem(new ItemData { tap = tap });
                return;
            }

            // Furniture item line with a colon, e.g., "Chair: an old wooden chair"
            int colonIndex = trimtext.IndexOf(":");
            if (colonIndex != -1 && colonIndex + 2 < trimtext.Length)
            {
                string tap = trimtext[(colonIndex + 2)..];
                tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                lastItem = currentData.AddItem(new ItemData { tap = tap, storage = true });
            }
        }
    }
}

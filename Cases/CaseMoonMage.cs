using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseMoonMage
    {
        private readonly Plugin Plugin;

        public CaseMoonMage(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void MoonMageCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "MoonMageStart")
            {
                if (Regex.IsMatch(trimtext, @"^You feel fully prepared to cast your spell\."))
                {
                    Plugin.Host.EchoText("Scanning Shadow Servant.");
                    Plugin.Host.SendText("cast servant");
                    return;
                }

                if (trimtext == "Within the belly of the Shadow Servant you see:")
                {
                    Plugin.ScanStart("MoonMage");
                    return;
                }

                if (Plugin.IsDenied(trimtext))
                {
                    scanMode = null;
                    Plugin.Host.EchoText("Skipping Servant, Piercing Gaze required.");
                    Plugin.Host.EchoText("Scan Complete.");
                    Plugin.Host.SendText("#parse Scan Complete");
                    LoadSave.SaveSettings();
                    return;
                }

                return;
            }

            if (scanMode == "MoonMage")
            {
                if (fullText.StartsWith("Your Servant is holding"))
                {
                    scanMode = null;
                    Plugin.Host.EchoText("Scan Complete.");
                    Plugin.Host.SendText("#parse Scan Complete");
                    LoadSave.SaveSettings();
                    return;
                }

                int spaces = fullText.Length - fullText.TrimStart().Length;
                spaces = spaces >= 4 && spaces <= 14 && spaces % 2 == 0 ? spaces / 2 - 1 : spaces == 15 ? 7 : 1;

                string tap = Regex.Replace(trimtext, @"^(an?|some|several)\s", "");

                if (spaces == 1)
                {
                    lastItem = currentData.AddItem(new ItemData { tap = tap });
                }
                else if (spaces == level)
                {
                    lastItem = lastItem.parent.AddItem(new ItemData { tap = tap });
                }
                else if (spaces == level + 1)
                {
                    lastItem = lastItem.AddItem(new ItemData { tap = tap });
                }
                else
                {
                    for (int i = spaces; i <= level; i++)
                        lastItem = lastItem.parent;

                    lastItem = lastItem.AddItem(new ItemData { tap = tap });
                }

                level = spaces;
            }
        }
    }
}

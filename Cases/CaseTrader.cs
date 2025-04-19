using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseTrader
    {
        private readonly Plugin Plugin;

        public CaseTrader(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void TraderCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "TraderStart")
            {
                if (trimtext.StartsWith("Roundtime:"))
                {
                    Plugin.PauseForRoundtime(trimtext);
                    scanMode = "TraderStart";
                    Plugin.Host.SendText("get my storage book"); // ✅ add this
                    return;
                }

                if (Regex.IsMatch(trimtext, "^You get a.*storage book.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*storage book.*from.+your (.+)\\.");
                    Plugin.bookContainer = match.Success ? match.Groups[1].Value : "";

                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                    {
                        string[] words = Plugin.bookContainer.Split(' ');
                        Plugin.bookContainer = words.Length switch
                        {
                            3 => $"{words[0]} {words[2]}",
                            2 => $"{words[0]} {words[1]}",
                            _ => words[0]
                        };

                        Plugin.Host.EchoText("Scanning Trader Storage.");
                        Plugin.Host.SendText("read my storage book");
                    }

                    return;
                }

                if (trimtext == "in the known realms since 402.") // Start of the list
                {
                    Plugin.ScanStart("Trader");
                    return;
                }

                if (Regex.IsMatch(fullText, "^What were you referring to\\?") || Plugin.IsDenied(trimtext))
                {
                    scanMode = null;
                    Plugin.Host.EchoText("Skipping Trader Storage.");
                    Plugin.Host.EchoText("Scan Complete.");
                    Plugin.Host.SendText("#parse Scan Complete");
                    Plugin.bookContainer = "";
                    LoadSave.SaveSettings();
                    return;
                }
            }

            if (scanMode == "Trader")
            {
                if (fullText.StartsWith("A notation at the bottom indicates"))
                {
                    scanMode = null;
                    Plugin.Host.EchoText("Scan Complete.");
                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my storage book in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my storage book");

                    Plugin.Host.SendText("#parse Scan Complete");
                    Plugin.bookContainer = "";
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

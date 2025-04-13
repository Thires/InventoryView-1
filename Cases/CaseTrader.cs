using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseTrader
    {
        private readonly plugin plugin;

        public CaseTrader(plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void TraderCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "TraderStart")
            {
                if (trimtext.StartsWith("Roundtime:"))
                {
                    plugin.PauseForRoundtime(trimtext);
                    scanMode = "TraderStart";
                    plugin.Host.SendText("get my storage book"); // ✅ add this
                    return;
                }

                if (Regex.IsMatch(trimtext, "^You get a.*storage book.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*storage book.*from.+your (.+)\\.");
                    plugin.bookContainer = match.Success ? match.Groups[1].Value : "";

                    if (!string.IsNullOrEmpty(plugin.bookContainer))
                    {
                        string[] words = plugin.bookContainer.Split(' ');
                        plugin.bookContainer = words.Length switch
                        {
                            3 => $"{words[0]} {words[2]}",
                            2 => $"{words[0]} {words[1]}",
                            _ => words[0]
                        };

                        plugin.Host.EchoText("Scanning Trader Storage.");
                        plugin.Host.SendText("read my storage book");
                    }

                    return;
                }

                if (trimtext == "in the known realms since 402.") // Start of the list
                {
                    plugin.ScanStart("Trader");
                    return;
                }

                if (Regex.IsMatch(fullText, "^What were you referring to\\?") || plugin.IsDenied(trimtext))
                {
                    scanMode = null;
                    plugin.Host.EchoText("Skipping Trader Storage.");
                    plugin.Host.EchoText("Scan Complete.");
                    plugin.Host.SendText("#parse Scan Complete");
                    plugin.bookContainer = "";
                    LoadSave.SaveSettings();
                    return;
                }
            }

            if (scanMode == "Trader")
            {
                if (fullText.StartsWith("A notation at the bottom indicates"))
                {
                    scanMode = null;
                    plugin.Host.EchoText("Scan Complete.");
                    if (!string.IsNullOrEmpty(plugin.bookContainer))
                        plugin.Host.SendText($"put my storage book in my {plugin.bookContainer}");
                    else
                        plugin.Host.SendText("stow my storage book");

                    plugin.Host.SendText("#parse Scan Complete");
                    plugin.bookContainer = "";
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

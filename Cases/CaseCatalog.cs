using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseCatalog
    {
        private readonly Plugin Plugin;

        public CaseCatalog(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void CatalogCase(string trimtext, string fullText, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "CatalogStart")
            {
                if (trimtext.StartsWith("Roundtime:"))
                {
                    Plugin.PauseForRoundtime(trimtext);
                    scanMode = "CatalogStart";
                    Plugin.Host.SendText("get my tool catalog");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^You get a.*tool catalog.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*tool catalog.*from.+your (.+)\\.");
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

                        Plugin.Host.EchoText("Scanning tool catalog.");
                        Plugin.Host.SendText("turn my tool catalog to contents");
                        Plugin.Host.SendText("read my tool catalog");
                    }

                    return;
                }

                if (fullText.StartsWith("   Page -- Tool"))
                {
                    Plugin.ScanStart("Catalog");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^What were you referring to\\?") || Plugin.IsDenied(trimtext))
                {
                    Plugin.Host.EchoText("Skipping Tool Catalog.");
                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my tool catalog in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my tool catalog");

                    Plugin.bookContainer = "";
                    scanMode = "HomeStart";
                    Plugin.Host.SendText("home recall");
                }

                return;
            }

            if (scanMode == "Catalog")
            {
                if (trimtext.StartsWith("Currently stored:"))
                {
                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my tool catalog in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my tool catalog");

                    Plugin.bookContainer = "";
                    scanMode = "HomeStart";
                    Plugin.Host.SendText("home recall");
                }
                else
                {
                    // clean and add line
                    string tap = Regex.Replace(trimtext, @" -- (an?|some|several)", " -- ");
                    lastItem = currentData.AddItem(new ItemData { tap = tap, storage = false });
                }
            }
        }
    }
}

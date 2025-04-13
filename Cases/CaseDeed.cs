using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseDeed
    {
        private readonly plugin plugin;

        public CaseDeed(plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void DeedCase(string trimtext, string fullText, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "DeedStart")
            {
                if (trimtext.StartsWith("Roundtime:"))
                {
                    plugin.PauseForRoundtime(trimtext);
                    scanMode = "DeedStart";
                    plugin.Host.SendText("get my deed register");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^You get a.*deed register.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*deed register.*from.+your (.+)\\.");
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

                        plugin.Host.EchoText("Scanning Deed Register.");
                        plugin.Host.SendText("turn my deed register to contents");
                        plugin.Host.SendText("read my deed register");
                    }

                    return;
                }

                if (Regex.IsMatch(trimtext, @"^\|\s*Page\s*\|\s*Type\s*\|\s*Deed\s*\|\s*Notes\s*\|"))
                {
                    plugin.togglecraft = true;
                    plugin.ScanStart("Deed");
                    return;
                }

                if (fullText.StartsWith("   Page -- [Type]       Deed"))
                {
                    plugin.togglecraft = false;
                    plugin.ScanStart("Deed");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^What were you referring to\\?") || plugin.IsDenied(trimtext))
                {
                    plugin.Host.EchoText("Skipping Deed Register.");

                    if (!string.IsNullOrEmpty(plugin.bookContainer))
                        plugin.Host.SendText($"put my deed register in my {plugin.bookContainer}");
                    else
                        plugin.Host.SendText("stow my deed register");

                    plugin.bookContainer = "";
                    scanMode = "CatalogStart";
                    plugin.Host.SendText("get my tool catalog");
                    return;
                }

                return;
            }

            if (scanMode == "Deed")
            {
                if (Regex.IsMatch(trimtext, @"^Currently [Ss]tored"))
                {
                    if (!string.IsNullOrEmpty(plugin.bookContainer))
                        plugin.Host.SendText($"put my deed register in my {plugin.bookContainer}");
                    else
                        plugin.Host.SendText("stow my deed register");

                    plugin.bookContainer = "";
                    scanMode = "CatalogStart";
                    plugin.Host.SendText("get my tool catalog");
                    return;
                }

                if (plugin.togglecraft)
                {
                    if (!Regex.IsMatch(trimtext, @"\| Page\| Type"))
                    {
                        string[] parts = trimtext.Trim().Split('|');
                        if (parts.Length >= 4)
                        {
                            string pageNumber = parts[1].Trim();
                            string type = parts[2].Trim();
                            string deed = Regex.Replace(parts[3], @"\s(an?|some|several)\s", " ").Trim();
                            string notes = parts[4].Trim();

                            string outputLine = !string.IsNullOrEmpty(notes)
                                ? $"{pageNumber} -- [{type}]     {deed}  ({notes})"
                                : $"{pageNumber} -- [{type}]     {deed}";

                            lastItem = currentData.AddItem(new ItemData { tap = outputLine, storage = false });
                        }
                    }
                }
                else
                {
                    string tap = Regex.Replace(trimtext, @"a deed for\s(an?|some|several)", " ");
                    lastItem = currentData.AddItem(new ItemData { tap = tap, storage = false });
                }
            }
        }

    }
}

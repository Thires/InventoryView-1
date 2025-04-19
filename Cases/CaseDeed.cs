using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseDeed
    {
        private readonly Plugin Plugin;

        public CaseDeed(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void DeedCase(string trimtext, string fullText, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "DeedStart")
            {
                if (trimtext.StartsWith("Roundtime:"))
                {
                    Plugin.PauseForRoundtime(trimtext);
                    scanMode = "DeedStart";
                    Plugin.Host.SendText("get my deed register");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^You get a.*deed register.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*deed register.*from.+your (.+)\\.");
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

                        Plugin.Host.EchoText("Scanning Deed Register.");
                        Plugin.Host.SendText("turn my deed register to contents");
                        Plugin.Host.SendText("read my deed register");
                    }

                    return;
                }

                if (Regex.IsMatch(trimtext, @"^\|\s*Page\s*\|\s*Type\s*\|\s*Deed\s*\|\s*Notes\s*\|"))
                {
                    Plugin.togglecraft = true;
                    Plugin.ScanStart("Deed");
                    return;
                }

                if (fullText.StartsWith("   Page -- [Type]       Deed"))
                {
                    Plugin.togglecraft = false;
                    Plugin.ScanStart("Deed");
                    return;
                }

                if (Regex.IsMatch(trimtext, "^What were you referring to\\?") || Plugin.IsDenied(trimtext))
                {
                    Plugin.Host.EchoText("Skipping Deed Register.");

                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my deed register in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my deed register");

                    Plugin.bookContainer = "";
                    scanMode = "CatalogStart";
                    Plugin.Host.SendText("get my tool catalog");
                    return;
                }

                return;
            }

            if (scanMode == "Deed")
            {
                if (Regex.IsMatch(trimtext, @"^Currently [Ss]tored"))
                {
                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my deed register in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my deed register");

                    Plugin.bookContainer = "";
                    scanMode = "CatalogStart";
                    Plugin.Host.SendText("get my tool catalog");
                    return;
                }

                if (Plugin.togglecraft)
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

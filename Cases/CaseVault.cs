using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseVault
    {
        private readonly Plugin Plugin;

        public CaseVault(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void VaultCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "VaultStart")
            {
                if (Regex.IsMatch(trimtext, "^You get a.*vault book.*from") || trimtext == "You are already holding that.")
                {
                    Match match = Regex.Match(trimtext, "^You get a.*vault book.*from.+your (.+)\\.");
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

                        Plugin.Host.EchoText("Scanning Book Vault.");
                        Plugin.Host.SendText("read my vault book");
                    }

                    return;
                }

                if (trimtext == "Vault Inventory:")
                {
                    Plugin.ScanStart("Vault");
                    return;
                }

                if (Regex.IsMatch(fullText, "^What were you referring to\\?") || Plugin.IsDenied(trimtext))
                {
                    Plugin.Host.EchoText("Skipping Book Vault.");
                    if (!string.IsNullOrEmpty(Plugin.bookContainer))
                        Plugin.Host.SendText($"put my vault book in my {Plugin.bookContainer}");
                    else
                        Plugin.Host.SendText("stow my vault book");

                    Plugin.bookContainer = "";
                    scanMode = "StandardStart";
                    Plugin.Host.SendText("vault standard");
                    return;
                }
            }

            if (scanMode == "Vault")
            {
                if (fullText.StartsWith("The last note in your book indicates that your vault contains"))
                {
                    if (((InventoryViewForm)Plugin.Form).toolStripFamily.Checked)
                    {
                        scanMode = "FamilyStart";
                        if (!string.IsNullOrEmpty(Plugin.bookContainer))
                            Plugin.Host.SendText($"put my vault book in my {Plugin.bookContainer}");
                        else
                            Plugin.Host.SendText("stow my vault book");

                        Plugin.bookContainer = "";
                        Plugin.Host.SendText("vault family");
                    }
                    else
                    {
                        scanMode = "DeedStart";
                        Plugin.Host.EchoText("Skipping Family Vault");
                        if (!string.IsNullOrEmpty(Plugin.bookContainer))
                            Plugin.Host.SendText($"put my vault book in my {Plugin.bookContainer}");
                        else
                            Plugin.Host.SendText("stow my vault book");

                        Plugin.bookContainer = "";
                        Plugin.Host.SendText("get my deed register");
                    }

                    return;
                }

                // Parse vault item
                int spaces = fullText.Length - fullText.TrimStart().Length;
                spaces = spaces >= 4 && spaces <= 14 && spaces % 2 == 0 ? spaces / 2 - 1 : spaces == 15 ? 7 : 1;

                string tap = trimtext;
                if (tap.StartsWith("-")) tap = tap[1..];
                if (tap[^1] == '.') tap = tap.TrimEnd('.');
                tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");

                if (spaces == 1)
                {
                    lastItem = currentData.AddItem(new ItemData { tap = tap, storage = true });
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

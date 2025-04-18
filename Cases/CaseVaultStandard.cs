using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseVaultStandard
    {
        private readonly Plugin plugin;

        public CaseVaultStandard(Plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void StandardVaultCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "StandardStart")
            {
                if (Regex.IsMatch(fullText, @"^You flag down a local you know works with the Estate Holders' Council and send \w+ to the nearest carousel\.") ||
                    trimtext == "You are already holding that.")
                {
                    Plugin.Host.EchoText("Scanning Standard Vault.");
                    return;
                }

                else if (trimtext == "Vault Inventory:")
                {
                    plugin.ScanStart("Standard");
                    return;
                }

                if (Plugin.IsDenied(trimtext))
                {
                    if (((InventoryViewForm)Plugin.Form).toolStripFamily.Checked)
                    {
                        Plugin.Host.EchoText("Skipping Standard Vault.");
                        scanMode = "FamilyStart";
                        Plugin.Host.SendText("vault family");
                    }
                    else
                    {
                        Plugin.Host.EchoText("Skipping Family Vault");
                        scanMode = "DeedStart";
                        Plugin.Host.SendText("get my deed register");
                    }
                }

                return;
            }

            if (scanMode == "Standard")
            {
                if (trimtext.StartsWith("The last note indicates that your vault contains"))
                {
                    if (((InventoryViewForm)Plugin.Form).toolStripFamily.Checked)
                        plugin.ScanMode = "FamilyStart";
                    else
                    {
                        plugin.ScanMode = "DeedStart";
                    }
                }
                else
                {
                    // Determine level of indentation
                    int spaces = fullText.Length - fullText.TrimStart().Length;
                    spaces = spaces >= 5 && spaces <= 25 && spaces % 5 == 0 ? spaces / 5 : 1;

                    string tap = trimtext;
                    if (tap[^1] == '.') tap = tap.TrimEnd('.');
                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                    tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some|several)\s", ") ");

                    // Build item tree
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
                        {
                            lastItem = lastItem.parent;
                        }
                        lastItem = lastItem.AddItem(new ItemData { tap = tap });
                    }

                    level = spaces;
                }
            }
        }
    }
}

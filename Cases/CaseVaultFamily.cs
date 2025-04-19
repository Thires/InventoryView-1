using System.Text.RegularExpressions;

namespace InventoryView.Cases
{
    public class CaseVaultFamily
    {
        private readonly Plugin Plugin;

        public CaseVaultFamily(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void FamilyVaultCase(string trimtext, string fullText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (scanMode == "FamilyStart")
            {
                if (fullText.StartsWith("Roundtime:"))
                {
                    Plugin.PauseForRoundtime(trimtext);
                    scanMode = "FamilyStart";
                    Plugin.Host.SendText("vault family");
                    return;
                }

                if (Regex.IsMatch(trimtext, "You flag down an urchin and direct (him|her) to the nearest carousel"))
                {
                    Plugin.Host.EchoText("Scanning Family Vault.");
                    return;
                }

                if (trimtext == "Vault Inventory:")
                {
                    Plugin.ScanStart("FamilyVault");
                    return;
                }

                if (Plugin.IsDenied(trimtext))
                {
                    if (fullText.StartsWith("Roundtime:"))
                    {
                        Plugin.PauseForRoundtime(trimtext);
                    }
                    Plugin.Host.EchoText("Skipping Family Vault.");
                    scanMode = "DeedStart";
                    Plugin.Host.SendText("get my deed register");
                    return;
                }
            }
            else if (scanMode == "FamilyVault")
            {
                if (fullText.StartsWith("The last note indicates that your vault contains"))
                {
                    scanMode = "DeedStart";
                    Plugin.Host.SendText("get my deed register");
                    return;
                }

                int spaces = fullText.Length - fullText.TrimStart().Length;
                spaces = spaces >= 5 && spaces <= 20 && spaces % 5 == 0 ? spaces / 5 : 1;

                string tap = trimtext;
                tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some|several)\s", ") ");
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


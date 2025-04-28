using System.Text.RegularExpressions;
using System.Threading;

namespace InventoryView.Cases
{
    public class CaseFamilyVault
    {
        private readonly Plugin Plugin;

        public CaseFamilyVault(Plugin PluginInstance)
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

                    Plugin.Host.EchoText("Setting Family vault to False");
                    ((InventoryViewForm)Plugin.Form).toolStripFamily.Checked = false;
                    Thread.Sleep(2000);
                    Plugin.Host.EchoText("Scan Complete.");
                    Plugin.Host.SendText("#parse Scan Complete");
                    LoadSave.SaveSettings();
                    scanMode = null;
                    return;
                }
            }
            else if (scanMode == "FamilyVault")
            {
                if (fullText.StartsWith("The last note indicates that your vault contains"))
                {
                    Plugin.Host.EchoText("Setting Family vault to False");
                    ((InventoryViewForm)Plugin.Form).toolStripFamily.Checked = false;
                    Thread.Sleep(2000);
                    scanMode = null;
                    Plugin.Host.EchoText("Scan Complete.");
                    Plugin.Host.SendText("#parse Scan Complete");
                    LoadSave.SaveSettings();
                    return;
                }

                int spaces = fullText.Length - fullText.TrimStart().Length;
                spaces = spaces >= 5 && spaces <= 20 && spaces % 5 == 0 ? spaces / 5 : 1;

                string tap = CleanTap(trimtext);

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

        private static string CleanTap(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            text = text.Trim();
            if (text.EndsWith("."))
                text = text.TrimEnd('.');

            text = Regex.Replace(text, @"^(an?|some|several)\s+", "");
            text = Regex.Replace(text, @"\)\s{1,4}(an?|some|several)\s", ") ");

            return text;
        }

    }
}


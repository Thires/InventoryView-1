using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System;

namespace InventoryView.Cases
{
    public class CaseInVault
    {
        private readonly Plugin plugin;
        private static readonly List<string> surfacesEncountered = new();
        private static List<string> SurfacesEncountered => surfacesEncountered;
        private readonly string[] surfaces = {
            "jewelry case", "ammunition box", "bottom drawer", "middle drawer",
            "top drawer", "shoe tree", "weapon rack", "steel wire rack",
            "small shelf", "large shelf", "brass hook"
        };
        private string currentSurface = "";

        public CaseInVault(Plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void InVaultCase(string trimtext, ref string scanMode, ref ItemData lastItem)
        {
            if (Regex.IsMatch(trimtext, "\"^You rummage through a(?: secure)? vault but there is nothing in there\\."))
            {
                scanMode = null;

                Thread.Sleep(500);

                if (((InventoryViewForm)Plugin.Form).toolStripFamily.Checked)
                {
                    scanMode = null;
                    Plugin.FinishFamilyVault();
                }
                else
                {
                    Plugin.Host.SendText("close vault");
                    Thread.Sleep(500);
                    Plugin.Host.SendText("go door");
                    Thread.Sleep(500);
                    Plugin.Host.SendText("go arch");
                    Thread.Sleep(2000);

                    scanMode = "DeedStart";
                    Plugin.Host.SendText("get my deed register");
                }
                return;
            }

            var match = Regex.Match(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\.", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string vaultInv = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(vaultInv))
                {
                    if (!plugin.InFamVault)
                    {
                        Plugin.Host.EchoText("Vault scanning");
                        plugin.ScanStart("InVault");
                    }

                    if (Regex.Match(vaultInv, @"\band\s(?:a|an|some|several)\b").Success)
                        vaultInv = Regex.Replace(vaultInv, @"\band\s(a|an|some|several)\s", ", $1 ");

                    var items = vaultInv.Split(',').Select(x => x.Trim()).ToList();

                    if (Regex.IsMatch(items[^1], @"\band\s(?:a|an|some|several)\s\b"))
                    {
                        string[] lastItemSplit = Regex.Split(items[^1], @"\band\s");
                        items.RemoveAt(items.Count - 1); // Remove the last item

                        // Combine the first part of the split with the rest and add them back
                        items[^1] += " " + lastItemSplit[0];
                        items.AddRange(lastItemSplit.Skip(1)); // Add the other parts
                    }

                    foreach (var item in items)
                    {
                        var tap = Plugin.CleanTapText(item);

                        bool isSurface = surfaces.Any(surface =>
                            tap.Equals(surface, StringComparison.OrdinalIgnoreCase) ||
                            tap.Equals($"a {surface}", StringComparison.OrdinalIgnoreCase) ||
                            tap.Equals($"an {surface}", StringComparison.OrdinalIgnoreCase) ||
                            tap.Equals($"some {surface}", StringComparison.OrdinalIgnoreCase) ||
                            tap.Equals($"several {surface}", StringComparison.OrdinalIgnoreCase)
                        );

                        if (isSurface)
                        {
                            SurfacesEncountered.Add(tap);
                        }
                        else
                        {
                            lastItem = plugin.currentData.AddItem(new ItemData { tap = tap });
                        }
                    }


                    if (SurfacesEncountered.Count > 0)
                        scanMode = "Surface";
                    else
                        FinishVault(ref scanMode);
                }
            }
        }

        public void SurfaceCase(string trimtext, ref string scanMode)
        {
            if (SurfacesEncountered.Count == 0)
            {
                FinishVault(ref scanMode);
                return;
            }

            currentSurface = SurfacesEncountered[0];
            if (currentSurface == "steel wire rack")
                currentSurface = "wire rack";

            if (!trimtext.StartsWith("You rummage"))
                Plugin.Host.SendText($"rummage {currentSurface}");

            scanMode = "SurfaceRummage";
        }

        public void SurfaceRummageCase(string trimtext, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (currentSurface == "wire rack")
                currentSurface = "steel wire rack";

            if (!trimtext.StartsWith("You rummage"))
                return;

            if (RummageCheck(trimtext, currentSurface, out var resultText))
            {
                SurfaceRummage(SurfacesEncountered[0], resultText, ref lastItem, currentData);
                SurfacesEncountered.RemoveAt(0);

                Thread.Sleep(100);
                scanMode = "Surface";
                return;
            }

            if (SurfacesEncountered.Count == 0)
            {
                FinishVault(ref scanMode);
            }
        }

        private static void SurfaceRummage(string surfaceType, string rummageText, ref ItemData lastItem, CharacterData currentData)
        {
            lastItem = currentData.AddItem(new ItemData() { tap = surfaceType, storage = true });

            if (Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Success)
            {
                string itemsMatch = Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Groups[1].Value;

                List<string> items = new(itemsMatch.Split(','));

                // Check if the last item contains "and" conjunction or articles
                if (Regex.IsMatch(items[^1], @"\band\s(?:a|an|some|several)\b"))
                {
                    string[] lastItemSplit = Regex.Split(items[^1], @"\band\s(?:a|an|some|several)\b");

                    // Remove the last item and handle the split
                    items.RemoveAt(items.Count - 1);

                    // Combine the last two items if necessary
                    if (!Regex.IsMatch(lastItemSplit[0], @"\b(?:a|an|some|several)\b"))
                    {
                        items[^1] += " " + lastItemSplit[0];
                        Array.Copy(lastItemSplit, 1, lastItemSplit, 0, lastItemSplit.Length - 1);
                    }

                    items.AddRange(lastItemSplit);
                }

                // Process each item in the list
                foreach (var item in items)
                {
                    string tap = Plugin.CleanTapText(item);
                    lastItem.AddItem(new ItemData { tap = tap });
                }
            }
        }

        private static bool RummageCheck(string text, string surface, out string resultText)
        {
            resultText = null;
            if (Regex.IsMatch(text, $"^You rummage (through|around on) (a|an) {Regex.Escape(surface)}"))
            {
                resultText = text;
                return true;
            }
            return false;
        }

        private static void FinishVault(ref string scanMode)
        {
            Thread.Sleep(500);

            if (((InventoryViewForm)Plugin.Form).toolStripFamily.Checked)
            {
                scanMode = null;
                Plugin.FinishFamilyVault();
            }
            else
            {
                Plugin.Host.SendText("close vault");
                Thread.Sleep(500);
                Plugin.Host.SendText("go door");
                Thread.Sleep(500);
                Plugin.Host.SendText("go arch");
                Thread.Sleep(2000);
                scanMode = "DeedStart";
                Plugin.Host.SendText("get my deed register");
            }
        }
    }
}

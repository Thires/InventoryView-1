using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace InventoryView.Cases
{
    public class CaseInVault
    {
        private readonly plugin plugin;

        public CaseInVault(plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void InVaultCase(string trimtext, string fullText, ref string scanMode, ref ItemData lastItem, CharacterData currentData, List<string> surfaces, List<string> SurfacesEncountered)
        {
            if (scanMode == "InVault")
            {
                if (Regex.IsMatch(trimtext, "You rummage through a secure vault but there is nothing in there\\."))
                {
                    scanMode = null;
                    Thread.Sleep(500);
                    plugin.Host.SendText("close vault");
                    Thread.Sleep(500);
                    plugin.Host.SendText("go door");
                    Thread.Sleep(500);
                    plugin.Host.SendText("go arch");
                    Thread.Sleep(2000);
                    scanMode = "DeedStart";
                    plugin.Host.SendText("get my deed register");
                    return;
                }

                var match = Regex.Match(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\.");
                if (match.Success)
                {
                    string vaultInv = match.Groups[1].Value;

                    if (!string.IsNullOrWhiteSpace(vaultInv))
                    {
                        plugin.ScanStart("InVault");

                        if (Regex.Match(vaultInv, @"\\b\\sand\\s(?:a|an|some|several)\\s\\b").Success)
                            vaultInv = Regex.Replace(vaultInv, @"\\b\\sand\\s(a|an|some|several)\\s\\b", ", $1 ");

                        List<string> items = new(vaultInv.Split(','));

                        foreach (string itemText in items)
                        {
                            string tap = itemText.Trim();
                            if (surfaces.Contains(tap))
                            {
                                tap = Regex.Replace(tap, @"^(an?|some|several)\\s", "");
                                SurfacesEncountered.Add(tap);
                            }
                            else
                            {
                                tap = Regex.Replace(tap, @"^(an?|some|several)\\s", "");
                                lastItem = currentData.AddItem(new ItemData { tap = tap });
                            }
                        }
                        scanMode = "Surface";
                    }
                }
            }
            else if (scanMode == "Surface")
            {
                if (SurfacesEncountered.Count == 0)
                    return;
                plugin.currentSurface = SurfacesEncountered[0];
                if (plugin.currentSurface == "steel wire rack")
                    plugin.currentSurface = "wire rack";
                if (!trimtext.StartsWith("You rummage"))
                    plugin.Host.SendText("rummage " + plugin.currentSurface);
                scanMode = "SurfaceRummage";
            }
            else if (scanMode == "SurfaceRummage")
            {
                if (plugin.currentSurface == "wire rack")
                    plugin.currentSurface = "steel wire rack";

                for (int i = 0; i < SurfacesEncountered.Count; i++)
                {
                    string surface = SurfacesEncountered[i];

                    plugin.Host.EchoText($"[DEBUG] SurfaceRummage check: '{plugin.currentSurface}', incoming: '{trimtext}'");

                    if (!trimtext.StartsWith("You rummage"))
                        break;

                    //if (plugin.RummageCheck(trimtext, plugin.currentSurface, out _))
                    //{
                    //    plugin.SurfaceRummage(surface, trimtext);
                    //    SurfacesEncountered.RemoveAt(i);
                    //    Thread.Sleep(100);
                    //    scanMode = "Surface";
                    //    break;
                    //}
                }

                if (SurfacesEncountered.Count == 0)
                {
                    if (plugin.InFamVault)
                    {
                        plugin.InFamVault = false;
                        scanMode = null;
                        plugin.Host.EchoText("Scan Complete.");
                        plugin.Host.SendText("#parse Scan Complete");
                        LoadSave.SaveSettings();
                    }
                    else
                    {
                        Thread.Sleep(500);
                        plugin.Host.SendText("close vault");
                        Thread.Sleep(500);
                        plugin.Host.SendText("go door");
                        Thread.Sleep(500);
                        plugin.Host.SendText("go arch");
                        Thread.Sleep(2000);

                        scanMode = "DeedStart";
                        plugin.Host.SendText("get my deed register");
                    }
                }
            }

            else if (scanMode == "InFamilyCheck")
            {
                if (!plugin.InFamVault)
                {
                    plugin.InFamVault = true;
                    plugin.ScanStart("FamilyVault");
                    plugin.Host.SendText("turn vault");
                    plugin.Host.SendText("open vault");
                    Thread.Sleep(6000);
                    plugin.Host.SendText("rummage vault");
                    scanMode = "InVault";
                }
            }
        }

        //private static bool RummageCheck(string trimtext, string currentSurface, out string resultText)
        //{
        //    resultText = null;

        //    if (Regex.IsMatch(trimtext, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(currentSurface)} but there is nothing in there\\."))
        //    {
        //        resultText = trimtext;
        //        return true;
        //    }
        //    else if (Regex.IsMatch(trimtext, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(currentSurface)} and see (.+\\.?)"))
        //    {
        //        resultText = trimtext;
        //        return true;
        //    }

        //    return false;
        //}


        //private void SurfaceRummage(string surfaceType, string rummageText)
        //{
        //    lastItem = currentData.AddItem(new ItemData() { tap = surfaceType, storage = true });
        //    if (Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Success)
        //    {
        //        string itemsMatch = Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Groups[1].Value;

        //        List<string> items = new(itemsMatch.Split(','));

        //        if (Regex.IsMatch(items[^1], @"\band\s(?:a|an|some|several)\b"))
        //        {
        //            string[] lastItemSplit = Regex.Split(items[^1], @"\band\s(?:an?|some|several)\b");
        //            items.RemoveAt(items.Count - 1);

        //            // Combine the last two items if the first part doesn't end with an article
        //            if (!Regex.IsMatch(lastItemSplit[0], @"\b(?:an?|some|several)\b"))
        //            {
        //                items[^1] += " " + lastItemSplit[0];
        //                Array.Copy(lastItemSplit, 1, lastItemSplit, 0, lastItemSplit.Length - 1);
        //            }
        //            items.AddRange(lastItemSplit);
        //        }

        //        foreach (string itemText in items)
        //        {
        //            string tap = itemText.Trim();

        //            if (tap[^1] == '.')
        //                tap = tap.TrimEnd('.');
        //            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
        //            lastItem = (lastItem.parent ?? lastItem).AddItem(new ItemData() { tap = tap });
        //        }
        //    }
        //}
    }
}


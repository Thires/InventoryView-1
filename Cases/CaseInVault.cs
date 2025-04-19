using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace InventoryView.Cases
{
    public class CaseInVault
    {
        private readonly Plugin Plugin;

        public CaseInVault(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void InVaultCase(string trimtext, ref string scanMode, ref ItemData lastItem, CharacterData currentData, List<string> surfaces, List<string> SurfacesEncountered)
        {
                         if (Regex.IsMatch(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\."))
                        {
                            string vaultInv = Regex.Match(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\.").Groups[1].Value;

                            if (Regex.Match(vaultInv, @"\b\sand\s(?:a|an|some|several)\s\b").Success)
                                vaultInv = Regex.Replace(vaultInv, @"\b\sand\s(a|an|some|several)\s\b", ", $1 ");

                            List<string> items = new(vaultInv.Split(','));

                            if (Regex.IsMatch(items[^1], @"\b\s(?:a|an|some|several)\s\b"))
                            {
                                // Split the last item by "and" and articles
                                string[] lastItemSplit = Regex.Split(items[^1], @"\b^\s(?:a|an|some|several)\s\b");

                                items.RemoveAt(items.Count - 1);

                                if (Regex.IsMatch(lastItemSplit[0], @"\band\s(?:a|an|some|several)\s\b"))
                                {
                                    items[^1] += " " + lastItemSplit[0];
                                    Array.Copy(lastItemSplit, 1, lastItemSplit, 0, lastItemSplit.Length - 1);
                                }

                                items.AddRange(lastItemSplit);
                            }

                            foreach (string itemText in items)
                            {
                                string tap = itemText.Trim();
                                if (surfaces.Contains(tap))
                                {
                                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                                    SurfacesEncountered.Add(tap);
                                    
                                }
                                else
                                {
                                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                                    lastItem = currentData.AddItem(new ItemData() { tap = tap });
                                }
                            }                        
                        
                        if (Regex.IsMatch(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\."))
                        {
                            vaultInv = Regex.Match(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\.").Groups[1].Value;

                            if (Regex.Match(vaultInv, @"\b\sand\s(?:a|an|some|several)\s\b").Success)
                                _ = Regex.Replace(vaultInv, @"\b\sand\s(a|an|some|several)\s\b", ", $1 ");

                            //List<string> items = new(vaultInv.Split(','));

                            if (Regex.IsMatch(items[^1], @"\b\s(?:a|an|some|several)\s\b"))
                            {
                                // Split the last item by "and" and articles
                                string[] lastItemSplit = Regex.Split(items[^1], @"\b^\s(?:a|an|some|several)\s\b");

                                items.RemoveAt(items.Count - 1);

                                if (Regex.IsMatch(lastItemSplit[0], @"\band\s(?:a|an|some|several)\s\b"))
                                {
                                    items[^1] += " " + lastItemSplit[0];
                                    Array.Copy(lastItemSplit, 1, lastItemSplit, 0, lastItemSplit.Length - 1);
                                }

                                items.AddRange(lastItemSplit);
                            }

                            foreach (string itemText in items)
                            {
                                string tap = itemText.Trim();
                                if (surfaces.Contains(tap))
                                {
                                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                                    SurfacesEncountered.Add(tap);
                                    
                                }
                                else
                                {
                                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                                    lastItem = currentData.AddItem(new ItemData() { tap = tap });
                                }
                            }
                        scanMode = "Surface";
                    }
                }
            
            else if (scanMode == "Surface")
            {
                if (SurfacesEncountered.Count == 0)
                    return;
                Plugin.currentSurface = SurfacesEncountered[0];
                if (Plugin.currentSurface == "steel wire rack")
                    Plugin.currentSurface = "wire rack";
                if (!trimtext.StartsWith("You rummage"))
                    Plugin.Host.SendText("rummage " + Plugin.currentSurface);
                scanMode = "SurfaceRummage";
            }
            else if (scanMode == "SurfaceRummage")
            {
                if (Plugin.currentSurface == "wire rack")
                    Plugin.currentSurface = "steel wire rack";

                for (int i = 0; i < SurfacesEncountered.Count; i++)
                {
                    _ = SurfacesEncountered[i];

                    if (!trimtext.StartsWith("You rummage"))
                        return;
                            if (Plugin.RummageCheck(trimtext, Plugin.currentSurface, out _))
                            {
                                Plugin.SurfaceRummage(SurfacesEncountered[i], trimtext);
                                SurfacesEncountered.RemoveAt(i); // Remove the surface

                                Thread.Sleep(100);

                                scanMode = "Surface";
                                return;
                            }
                }

                if (SurfacesEncountered.Count == 0)
                {
                    if (Plugin.InFamVault)
                    {
                        Plugin.InFamVault = false;
                        scanMode = null;
                        Plugin.Host.EchoText("Scan Complete.");
                        Plugin.Host.SendText("#parse Scan Complete");
                        LoadSave.SaveSettings();
                    }
                    else
                    {
                        Thread.Sleep(500);
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

            else if (scanMode == "InFamilyCheck")
            {
                if (!Plugin.InFamVault)
                {
                    Plugin.InFamVault = true;
                    Plugin.ScanStart("FamilyVault");
                    Plugin.Host.SendText("turn vault");
                    Plugin.Host.SendText("open vault");
                    Thread.Sleep(6000);
                    Plugin.Host.SendText("rummage vault");
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


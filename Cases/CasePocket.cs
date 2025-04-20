using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


namespace InventoryView.Cases
{
    public class CasePocket
    {
        private readonly Plugin Plugin;

        public CasePocket(Plugin PluginInstance)
        {
            Plugin = PluginInstance;
        }

        public void PocketStart(string trimtext, ref string scanMode)
        {
            if (Regex.IsMatch(trimtext, "^You tap a pocket inside your .+\\."))
            {
                Match match = Regex.Match(trimtext, "^You tap a pocket inside your (.+)\\.");
                Plugin.pocketContainerShort = match.Groups[1].Value.Trim();

                if (Plugin.processedPockets.Contains(Plugin.pocketContainerShort))
                {
                    AfterPocket(ref scanMode);
                    Plugin.pocketScanStarted = false;
                    return;
                }

                Plugin.processedPockets.Add(Plugin.pocketContainerShort);
                Plugin.Host.SendText($"tap my {Plugin.pocketContainerShort}");
                scanMode = "PocketStart";
                return;
            }

            if (scanMode == "PocketStart" &&
                Regex.IsMatch(trimtext, "^You tap (?:an?|some|several)? ?(.+?) that you are"))
            {
                Match match = Regex.Match(trimtext, "^You tap (?:an?|some|several)? ?(.+?) that you are");
                if (match.Success)
                {
                    Plugin.pocketContainer = match.Groups[1].Value.Trim();
                    Plugin.Host.SendText("rummage pocket");
                    scanMode = "Pocket";
                    return;
                }
            }

            if (Plugin.IsDenied(trimtext) || trimtext.StartsWith("What were you referring"))
            {
                Plugin.Host.EchoText("Skipping current pocket.");
                AfterPocket(ref scanMode);
                Plugin.pocketScanStarted = false;
            }
        }

        public void PocketCase(string trimtext, ref string scanMode, ref ItemData lastItem)
        {
            if (Plugin.handledCurrentPocket)
                return;

            if (Regex.IsMatch(trimtext, "^You rummage through a pocket and see (.+)\\.$"))
            {
                string pocketInv = Regex.Match(trimtext, "^You rummage through a pocket and see (.+)\\.").Groups[1].Value;
                pocketInv = Regex.Replace(pocketInv, @"\band\s(a|an|some|several)\s", ", $1 ");

                if (!((InventoryViewForm)Plugin.Form).toolStripPockets.Checked &&
                    Plugin.currentData != null &&
                    !Plugin.currentData.source.StartsWith("Pocket in"))
                {
                    Plugin.ScanStart("Pocket");
                }

                if (!((InventoryViewForm)Plugin.Form).toolStripPockets.Checked)
                {
                    if (Plugin.currentData == null || Plugin.currentData.source != $"Pocket in {Plugin.pocketContainer}")
                    {
                        Plugin.pocketContainer = Plugin.pocketContainer.Trim();
                        Plugin.ScanStart("Pocket");
                    }
                }
                else
                {
                    // Get the container item or add it if missing
                    var containerItem = Plugin.currentData.items
                        .FirstOrDefault(i => i.tap.Equals(Plugin.pocketContainer, StringComparison.OrdinalIgnoreCase));

                    containerItem ??= Plugin.currentData.AddItem(new ItemData { tap = Plugin.pocketContainer });

                    // Add the "hidden pocket" item to the container
                    lastItem = containerItem.AddItem(new ItemData { tap = "hidden pocket" });

                    // Add the items inside the pocket to the hidden pocket
                    foreach (string itemText in pocketInv.Split(','))
                    {
                        string tap = Plugin.CleanTapText(itemText.Trim());
                        lastItem.AddItem(new ItemData { tap = tap });
                    }
                }

                // Add the items to the main inventory (only if pockets are unchecked)
                if (!((InventoryViewForm)Plugin.Form).toolStripPockets.Checked)
                {
                    foreach (string itemText in pocketInv.Split(','))
                    {
                        string tap = Plugin.CleanTapText(itemText.Trim());
                        Plugin.currentData.AddItem(new ItemData { tap = tap });
                    }
                }

                Plugin.handledCurrentPocket = true;

                if (!Plugin.closedContainers.Contains(Plugin.pocketContainerShort))
                {
                    Plugin.closedContainers.Add(Plugin.pocketContainerShort);
                    Plugin.Host.SendText($"close my {Plugin.pocketContainerShort}");
                }

                Plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                Plugin.Host.SendText("tap pocket");
            }
            else if (Regex.IsMatch(trimtext, "^You close your .+\\.$"))
            {
                Plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                Plugin.Host.SendText("tap pocket");
            }
            else if (Plugin.IsDenied(trimtext))
            {
                AfterPocket(ref scanMode);
                Plugin.pocketScanStarted = false;
                ReopenContainers();
            }
        }


        private void ReopenContainers()
        {
            if (Plugin.closedContainers.Count > 0)
            {
                Plugin.Host.EchoText("Reopening closed pocket containers");
                foreach (var container in Plugin.closedContainers)
                {
                    Plugin.Host.SendText($"open my {container}");
                    Thread.Sleep(500);
                }
                Plugin.closedContainers.Clear();
            }
        }

        private void AfterPocket(ref string scanMode)
        {
            ReopenContainers();

            if (Plugin.Host.get_Variable("roomname").Contains("Carousel Chamber"))
            {
                scanMode = "InVault";
                Plugin.Host.EchoText("Rummaging Vault.");
                Plugin.Host.SendText("open vault");
                Plugin.Host.SendText("rummage vault");
            }
            else
            {
                scanMode = "VaultStart";
                Plugin.Host.SendText("get my vault book");
            }
        }
    }
}

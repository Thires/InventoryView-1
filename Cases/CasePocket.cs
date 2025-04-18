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
        private readonly Plugin plugin;

        public CasePocket(Plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void PocketStart(string trimtext, ref string scanMode)
        {
            if (Regex.IsMatch(trimtext, "^You tap a pocket inside your .+\\."))
            {
                Match match = Regex.Match(trimtext, "^You tap a pocket inside your (.+)\\.");
                plugin.pocketContainerShort = match.Groups[1].Value.Trim();

                if (plugin.processedPockets.Contains(plugin.pocketContainerShort))
                {
                    AfterPocket(ref scanMode);
                    plugin.pocketScanStarted = false;
                    return;
                }

                plugin.processedPockets.Add(plugin.pocketContainerShort);
                Plugin.Host.SendText($"tap my {plugin.pocketContainerShort}");
                scanMode = "PocketStart";
                return;
            }

            if (scanMode == "PocketStart" &&
                Regex.IsMatch(trimtext, "^You tap (?:an?|some|several)? ?(.+?) that you are"))
            {
                Match match = Regex.Match(trimtext, "^You tap (?:an?|some|several)? ?(.+?) that you are");
                if (match.Success)
                {
                    plugin.pocketContainer = match.Groups[1].Value.Trim();
                    Plugin.Host.SendText("rummage pocket");
                    scanMode = "Pocket";
                    return;
                }
            }

            if (Plugin.IsDenied(trimtext) || trimtext.StartsWith("What were you referring"))
            {
                Plugin.Host.EchoText("Skipping current pocket.");
                AfterPocket(ref scanMode);
                plugin.pocketScanStarted = false;
            }
        }

        public void PocketCase(string trimtext, ref string scanMode, ref ItemData lastItem, CharacterData currentData)
        {
            if (plugin.handledCurrentPocket)
                return;

            if (Regex.IsMatch(trimtext, "^You rummage through a pocket and see (.+)\\."))
            {
                string pocketInv = Regex.Match(trimtext, "^You rummage through a pocket and see (.+)\\.").Groups[1].Value;
                pocketInv = Regex.Replace(pocketInv, @"\band\s(a|an|some|several)\s", ", $1 ");

                if (!((InventoryViewForm)Plugin.Form).toolStripPockets.Checked &&
                    plugin.currentData != null &&
                    !plugin.currentData.source.StartsWith("Pocket in"))
                {
                    plugin.ScanStart("Pocket");
                }

                if (!((InventoryViewForm)Plugin.Form).toolStripPockets.Checked)
                {
                    if (plugin.currentData == null || plugin.currentData.source != $"Pocket in {plugin.pocketContainer}")
                    {
                        plugin.pocketContainer = plugin.pocketContainer.Trim();
                        plugin.ScanStart("Pocket");
                    }
                }
                else
                {
                    var containerItem = plugin.currentData.items
                        .FirstOrDefault(i => i.tap.Equals(plugin.pocketContainer, StringComparison.OrdinalIgnoreCase));

                    if (containerItem == null)
                            containerItem = plugin.currentData.AddItem(new ItemData { tap = plugin.pocketContainer });

                    lastItem = containerItem.AddItem(new ItemData { tap = "hidden pocket" });

                    foreach (string itemText in pocketInv.Split(','))
                    {
                        string tap = Plugin.CleanTapText(itemText.Trim());
                        lastItem.AddItem(new ItemData { tap = tap });
                    }
                }

                foreach (string itemText in pocketInv.Split(','))
                {
                    string tap = Plugin.CleanTapText(itemText.Trim());

                        plugin.currentData.AddItem(new ItemData { tap = tap });
                }

                plugin.handledCurrentPocket = true;

                if (!plugin.closedContainers.Contains(plugin.pocketContainerShort))
                {
                    plugin.closedContainers.Add(plugin.pocketContainerShort);
                    Plugin.Host.SendText($"close my {plugin.pocketContainerShort}");
                }

                plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                Plugin.Host.SendText("tap pocket");
            }
            else if (Regex.IsMatch(trimtext, "^You close your .+\\.$"))
            {
                plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                Plugin.Host.SendText("tap pocket");
            }
            else if (Plugin.IsDenied(trimtext))
            {
                AfterPocket(ref scanMode);
                plugin.pocketScanStarted = false;
                ReopenContainers();
            }
        }

        private void ReopenContainers()
        {
            if (plugin.closedContainers.Count > 0)
            {
                Plugin.Host.EchoText("Reopening closed pocket containers");
                foreach (var container in plugin.closedContainers)
                {
                    Plugin.Host.SendText($"open my {container}");
                    Thread.Sleep(500);
                }
                plugin.closedContainers.Clear();
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

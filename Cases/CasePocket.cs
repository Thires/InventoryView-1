using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;


namespace InventoryView.Cases
{
    public class CasePocket
    {
        private readonly plugin plugin;

        public CasePocket(plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void PocketStart(string trimtext, ref string scanMode)
        {
            // short form pocket tap
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

                // full tap
                plugin.Host.SendText($"tap my {plugin.pocketContainerShort}");
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
                    plugin.Host.SendText("rummage pocket");
                    scanMode = "Pocket";
                    return;
                }
            }

            if (plugin.IsDenied(trimtext) || trimtext.StartsWith("What were you referring"))
            {
                plugin.Host.EchoText("Skipping current pocket.");
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
                var containerItem = plugin.currentData.items
                    .FirstOrDefault(i => i.tap.Equals(plugin.pocketContainer, StringComparison.OrdinalIgnoreCase));
                if (containerItem == null)
                {
                    containerItem = plugin.currentData.AddItem(new ItemData { tap = plugin.pocketContainer });
                }

                var pocketNode = containerItem.AddItem(new ItemData { tap = "hidden pocket" });

                foreach (string itemText in pocketInv.Split(','))
                {
                    string tap = plugin.CleanTapText(itemText.Trim());
                    pocketNode.AddItem(new ItemData { tap = tap });
                }
                plugin.handledCurrentPocket = true;

                if (!plugin.closedContainers.Contains(plugin.pocketContainer))
                {
                    plugin.closedContainers.Add(plugin.pocketContainerShort);
                    plugin.Host.SendText($"close my {plugin.pocketContainerShort}");
                }

                plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                plugin.Host.SendText("tap pocket");

            }
            else if (Regex.IsMatch(trimtext, "^You close your .+\\.$"))
            {
                plugin.handledCurrentPocket = false;
                scanMode = "PocketStart";
                plugin.Host.SendText("tap pocket");
            }

            else if (plugin.IsDenied(trimtext))
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
                plugin.Host.EchoText("Reopening closed pocket containers");
                foreach (var container in plugin.closedContainers)
                {
                    plugin.Host.SendText($"open my {container}");
                    Thread.Sleep(500);
                }
                plugin.closedContainers.Clear();
            }
        }

        private void AfterPocket(ref string scanMode)
        {
            ReopenContainers();

            if (plugin.Host.get_Variable("roomname").Contains("Carousel Chamber"))
            {
                scanMode = "InVault";
                plugin.Host.EchoText("Rummaging Vault.");
                plugin.Host.SendText("open vault");
                plugin.Host.SendText("rummage vault");
            }
            else
            {
                scanMode = "VaultStart";
                plugin.Host.SendText("get my vault book");
            }
        }
    }
}

namespace InventoryView.Cases
{
    public class CaseInventory
    {
        private readonly plugin plugin;

        public CaseInventory(plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public void InventoryCase(string trimtext, string originalText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (trimtext.StartsWith("Roundtime:"))
            {
                plugin.PauseForRoundtime(trimtext);
                InventoryEnd(ref scanMode);
                return;
            }

            if (trimtext.StartsWith("[Use"))
                return;

            InventoryItems(trimtext, originalText, ref level, ref lastItem, currentData);
        }

        private static void InventoryEnd(ref string scanMode)
        {
            InventoryViewForm form = (InventoryViewForm)plugin.Form;

            if (form.toolStripPockets.Checked)
            {
                plugin.Host.EchoText("Checking Pocket");
                plugin.Host.SendText("tap pocket");
                scanMode = "PocketStart";
            }
            else
            {
                if (plugin.Host.get_Variable("roomname").Contains("Carousel Chamber"))
                {
                    scanMode = "InVault";
                    plugin.Host.EchoText("Rummaging Vault.");
                    //plugin.ScanStart("InVault");
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

        private static void InventoryItems(string trimtext, string originalText, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            int spaces = originalText.Length - originalText.TrimStart().Length;
            int newlevel = (spaces + 1) / 3;

            string tap = plugin.CleanTapText(trimtext);

            if (newlevel == 1)
            {
                lastItem = currentData.AddItem(new ItemData { tap = tap });
            }
            else if (newlevel == level)
            {
                lastItem = lastItem.parent.AddItem(new ItemData { tap = tap });
            }
            else if (newlevel == level + 1)
            {
                lastItem = lastItem.AddItem(new ItemData { tap = tap });
            }
            else
            {
                for (int i = newlevel; i <= level; i++)
                {
                    lastItem = lastItem.parent;
                }
                lastItem = lastItem.AddItem(new ItemData { tap = tap });
            }

            level = newlevel;
        }
    }
}

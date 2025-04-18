﻿namespace InventoryView.Cases
{
    public class CaseInventory
    {
        private readonly Plugin plugin;

        public CaseInventory(Plugin pluginInstance)
        {
            plugin = pluginInstance;
        }

        public static void InventoryCase(string trimtext, string originalText, ref string scanMode, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            if (trimtext.StartsWith("Roundtime:"))
            {
                Plugin.PauseForRoundtime(trimtext);
                InventoryEnd(ref scanMode);
                return;
            }

            if (trimtext.StartsWith("[Use"))
                return;

            InventoryItems(trimtext, originalText, ref level, ref lastItem, currentData);
        }

        private static void InventoryEnd(ref string scanMode)
        {
            Plugin.Host.EchoText("Checking Pocket");
            Plugin.Host.SendText("tap pocket");
            scanMode = "PocketStart";
        }


        private static void InventoryItems(string trimtext, string originalText, ref int level, ref ItemData lastItem, CharacterData currentData)
        {
            int spaces = originalText.Length - originalText.TrimStart().Length;
            int newlevel = (spaces + 1) / 3;

            string tap = Plugin.CleanTapText(trimtext);

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

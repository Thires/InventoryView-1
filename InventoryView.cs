using System.Collections.Generic;
using GeniePlugin.Interfaces;
using System.Windows.Forms;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System;

namespace InventoryView
{
    public class Class1 : GeniePlugin.Interfaces.IPlugin
    {
        // Genie host.
        private static IHost host;

        // Plugin Form.
        private static Form form;

        //readonly InventoryTextSearch inventorytext = new();

        // This contains all the of the inventory data.
        private static List<CharacterData> characterData = new();

        private static readonly List<string> surfacesEncountered = new();
        private readonly string[] surfaces = { "a jewelry case", "an ammunition box", "a bottom drawer", "a middle drawer", "a top drawer", "a shoe tree", "a weapon rack", "a steel wire rack", "a small shelf", "a large shelf", "a brass hook" };

        // Whether or not InventoryView is currently scanning data, and what state it is in.
        private string ScanMode = null;

        // Keeps track of how many containers deep you are when scanning inventory in containers.
        private int level = 1;

        // The current character & source being scanned.
        private CharacterData currentData = null;

        // The last item tha was scanned.
        private ItemData lastItem = null;

        private bool Debug = false;
        private bool togglecraft = false;
        private bool InFamVault = false;

        private string LastText = "";
	    private string bookContainer;
        private string guild = "";
        private string accountName = "";
        private string currentSurface = "";

        public void Initialize(IHost host)
        {
            Host = host;

            // Create a new instance of the InventoryViewForm class
            Form = new InventoryViewForm();

            // Load inventory from the XML config if available.
            LoadSave.LoadSettings();
        }

        public void Show()
        {
            if (Form == null || Form.IsDisposed)
                Form = new InventoryViewForm();

            Form.Show();
        }

        public void VariableChanged(string variable)
        {
        }

        public string ParseText(string text, string window)
        {
            if (ScanMode != null)
            {
                string trimtext = text.Trim(new char[] { '\n', '\r', ' ' }); // Trims spaces and newlines.
                LastText = trimtext;
                if (trimtext.StartsWith("XML") && trimtext.EndsWith("XML")) return ""; // Skip XML parser lines
                else if (string.IsNullOrEmpty(trimtext)) return ""; // Skip blank lines

                if (((InventoryViewForm)Form).toolStripFamily.Checked)
                    if (Regex.IsMatch(trimtext, "^Account Info for\\s+(.+):"))
                        accountName = Regex.Match(trimtext, "^Account Info for\\s+(.+):").Groups[1].Value;

                if (Regex.IsMatch(trimtext, "Guild: [A-z ]+$"))
                {
                    guild = Regex.Match(trimtext, "Guild: ([A-z ]+)$").Groups[1].Value;
                    Host.set_Variable("guild", guild);
                    if (((InventoryViewForm)Form).toolStripFamily.Checked && Host.get_Variable("roomname").Contains("Family Vault"))
                    {
                        Thread.Sleep(500);
                        ScanMode = "InFamilyCheck";

                    }
                    else
                        Host.SendText("inventory list");
                }

                switch (ScanMode)
                {
                    case "Start":
                        if (trimtext == "You have:") // Text that appears at the beginning of "inventory list"
                        {
                            Host.EchoText("Scanning Inventory.");
                            ScanStart("Inventory");
                        }
                        break;
                    case "Inventory":
                        if (trimtext.StartsWith("Roundtime:")) // text that appears at the end of "inventory list"
                        {
                            // Inventory List has a RT based on the number of items, so grab the number and pause the thread for that length.
                            PauseForRoundtime(trimtext);
                            if (Host.get_Variable("roomname").Contains("Carousel Chamber"))
                            {
                                ScanMode = "InVault";
                                Host.EchoText("Rummaging Vault.");
                                ScanStart("InVault");
                                Host.SendText("open vault");
                                Host.SendText("rummage vault");
                            }
                            else
                            {
                                ScanMode = "VaultStart";
                                Host.SendText("get my vault book");
                            }
                        }
                        else if (trimtext.StartsWith("[Use"))
                            return "";
                        else
                        {
                            // The first level of inventory has a padding of 2 spaces to the left, and each level adds an additional 3 spaces.
                            // 2, 5, 8, 11, 14, 17...
                            int spaces = text.Length - text.TrimStart().Length;
                            int newlevel = (spaces + 1) / 3;
                            string tap = trimtext;
                            // remove the - from the beginning if it exists.
                            if (tap.StartsWith("-")) tap = tap.Remove(0, 1);
                            if (tap[^1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");

                            // The logic below builds a tree of inventory items.
                            if (newlevel == 1) // If the item is in the first level, add to the root item list
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap });
                            }
                            else if (newlevel == level) // If this is the same level as the previous item, add to the previous item's parent's item list.
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (newlevel == level + 1) // If this item is down a level from the previous, add it to the previous item's item list.
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else // Else, if the item is up a level, loop back until you reach the correct level.
                            {
                                for (int i = newlevel; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = newlevel;
                        }
                        break; //end of Inventory
                    case "InVault":
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
                            ScanMode = "Surface";
                            break;
                        }
                        break;
                    case "Surface":
                        currentSurface = SurfacesEncountered[0];
                        if (currentSurface == "steel wire rack")
                            currentSurface = "wire rack";
                        if (!trimtext.StartsWith("You rummage"))
                            Host.SendText("rummage " + currentSurface);
                        ScanMode = "SurfaceRummage";
                        break;
                    case "SurfaceRummage":
                        if (currentSurface == "wire rack")
                            currentSurface = "steel wire rack";
                        for (int i = 0; i < SurfacesEncountered.Count; i++)
                        {
                            trimtext = text;

                            if (!trimtext.StartsWith("You rummage"))
                                break;
                            if (RummageCheck(trimtext, currentSurface, out _))
                            {
                                SurfaceRummage(SurfacesEncountered[i], trimtext);
                                SurfacesEncountered.RemoveAt(i); // Remove the surface

                                Thread.Sleep(100);

                                ScanMode = "Surface";
                                break;
                            }
                        }

                        if (SurfacesEncountered.Count == 0)
                        {
                            if (InFamVault)
                            {
                                InFamVault = false;
                                ScanMode = null;
                                Host.EchoText("Scan Complete.");
                                Host.SendText("#parse InventoryView scan complete");
                                LoadSave.SaveSettings();
                            }
                            else
                            {
                                Thread.Sleep(500);
                                Host.SendText("close vault");
                                Thread.Sleep(500);
                                Host.SendText("go door");
                                Thread.Sleep(500);
                                Host.SendText("go arch");
                                Thread.Sleep(2000);

                                ScanMode = "DeedStart";
                                Host.SendText("get my deed register");
                            }
                        }
                        break;
                    case "InFamilyCheck":
                        if (!InFamVault)
                        {
                            InFamVault = true;
                            ScanStart("FamilyVault");
                            Host.SendText("turn vault");
                            Host.SendText("open vault");
                            Thread.Sleep(6000);
                            Host.SendText("rummage vault");
                            ScanMode = "InVault";
                        }
                        break;
                    case "VaultStart":
                        // Get the vault book & read it.
                        if (Regex.Match(trimtext, "^You get a.*vault book.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*vault book.*from.+your (.+)\\.");
                            bookContainer = match2.Success ? match2.Groups[1].Value : "";

                            if (!string.IsNullOrEmpty(bookContainer))
                            {
                                string[] words = bookContainer.Split(' ');
                                string formattedContainer;

                                if (words.Length == 3)
                                {
                                    formattedContainer = $"{words[0]} {words[2]}";
                                }
                                else if (words.Length == 2)
                                {
                                    formattedContainer = $"{words[0]} {words[1]}";
                                }
                                else
                                {
                                    formattedContainer = words[0];
                                }

                                Host.EchoText("Scanning Book Vault.");
                                Host.SendText("read my vault book");
                                bookContainer = formattedContainer;
                            }
                        }

                        else if (trimtext == "Vault Inventory:") // This text appears at the beginning of the vault list.
                        {
                            ScanStart("Vault");
                        }
                        // If you don't have a vault book or you can't read a vault book, it skips to checking use of vault standard.
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            ScanMode = "StandardStart";
                            Host.EchoText("Skipping Book Vault.");
                            Host.SendText("vault standard");
                        }

                        else if (IsDenied(trimtext))
                        {
                            ScanMode = "StandardStart";
                            Host.EchoText("Skipping Book Vault.");
                            if (bookContainer == "")
                                Host.SendText("stow my vault book");
                            else
                                Host.SendText("put my vault book in my " + bookContainer);
                            bookContainer = "";
                            Host.SendText("vault standard");
                        }
                        break; //end of VaultStart
                    case "Vault":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("The last note in your book indicates that your vault contains"))
                        {
                            if (((InventoryViewForm)Form).toolStripFamily.Checked)
                            {
                                ScanMode = "FamilyStart";
                                if (bookContainer == "")
                                    Host.SendText("stow my vault book");
                                else
                                    Host.SendText("put my vault book in my " + bookContainer);
                                bookContainer = "";
                                Host.SendText("vault family");
                            }
                            else
                            {
                                if (bookContainer == "")
                                    Host.SendText("stow my vault book");
                                else
                                    Host.SendText("put my vault book in my " + bookContainer);
                                bookContainer = "";
                            ScanMode = "DeedStart";
                            Host.EchoText("Skipping Family Vault");
                            Host.SendText("get my deed register");
                            }
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            // Anything greater than 4 levels down shows up at the same level as its parent.
                            int spaces = text.Length - text.TrimStart().Length;
                            spaces = (spaces >= 4 && spaces <= 14 && spaces % 2 == 0) ? spaces / 2 - 1 : (spaces == 15) ? 7 : 1;

                            string tap = trimtext;

                            if (tap.StartsWith("-")) tap = tap.Remove(0, 1);
                            if (tap[^1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            if (spaces == 1)
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                            }
                            else if (spaces == level)
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level + 1)
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else
                            {
                                for (int i = spaces; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = spaces;
                        }
                        break; //end of Vault
                    
                    case "StandardStart":
                        if (Regex.Match(text, @"^You flag down a local you know works with the Estate Holders' Council and send \w+ to the nearest carousel.").Success || trimtext == "You are already holding that.")
                        {
                            Host.EchoText("Scanning Standard Vault.");
                        }

                        else if (trimtext == "Vault Inventory:") // This text appears at the beginning of the vault list.
                        {
                            ScanStart("Standard");
                        }
                        // If you don't have access to vault standard, it skips to checking for family vault.
                        else if (IsDenied(trimtext))
                        {
                            if (((InventoryViewForm)Form).toolStripFamily.Checked)
                            {
                                Host.EchoText("Skipping Standard Vault.");
                                ScanMode = "FamilyStart";
                                Host.SendText("vault family");
                            }
                            else
                            {
                                ScanMode = "DeedStart";
                                Host.EchoText("Skipping Family Vault");
                                Host.SendText("get my deed register");
                            }
                        }
                        break; //end of VaultStandardStart
                    case "Standard":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("The last note indicates that your vault contains"))
                        {
                            if (((InventoryViewForm)Form).toolStripFamily.Checked)
                                ScanMode = "FamilyStart";
                            else
                                ScanMode = "DeedStart";
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            int spaces = text.Length - text.TrimStart().Length;
                            spaces = (spaces >= 5 && spaces <= 25 && spaces % 5 == 0) ? spaces / 5 : 1;

                            string tap = trimtext;
                            if (tap[^1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some|several)\s", ") ");
                            if (spaces == 1)
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                            }
                            else if (spaces == level)
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level + 1)
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else
                            {
                                for (int i = spaces; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = spaces;
                        }
                        break; //end of Standard Vault
                    
                    case "FamilyStart":
                        if (text.StartsWith("Roundtime:"))
                        {
                            PauseForRoundtime(trimtext);
                            ScanMode = "FamilyStart";
                            Host.SendText("vault family");
                        }
                        if (Regex.Match(trimtext, "You flag down an urchin and direct him to the nearest carousel").Success || trimtext == "You flag down an urchin and direct her to the nearest carousel")
                        {
                            Host.EchoText("Scanning Family Vault.");
                        }
                        else if (trimtext == "Vault Inventory:") // This text appears at the beginning of the vault list.
                        {
                            ScanStart("FamilyVault");
                        }
                        // If you don't have access to family vault, it skips to checking your deed register.

                        else if (IsDenied(trimtext))
                        {
                            if (text.StartsWith("Roundtime:"))
                            {
                                PauseForRoundtime(trimtext);
                                ScanMode = "DeedStart";
                                Host.SendText("get my deed register");
                            }
                            else
                            {
                                Host.EchoText("Skipping Family Vault.");
                                ScanMode = "DeedStart";
                                Host.SendText("get my deed register");
                            }
                        }
                        break; //end of VaultFamilyStart
                    case "FamilyVault":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("The last note indicates that your vault contains"))
                        {
                            ScanMode = "DeedStart";
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            int spaces = text.Length - text.TrimStart().Length;
                            spaces = (spaces >= 5 && spaces <= 20 && spaces % 5 == 0) ? spaces / 5 : 1;

                            string tap = trimtext;
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some|several)\s", ") ");
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            if (spaces == 1)
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                            }
                            else if (spaces == level)
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level + 1)
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else
                            {
                                for (int i = spaces; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = spaces;
                        }
                        break; //end of Family Vault
                    
                    case "DeedStart":
                        if (text.StartsWith("Roundtime:"))
                        {
                            PauseForRoundtime(trimtext);
                            ScanMode = "DeedStart";
                            Host.SendText("get my deed register");
                        }
                        // Get the register & read it.
                        if (Regex.Match(trimtext, "^You get a.*deed register.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*deed register.*from.+your (.+)\\.");
                            bookContainer = match2.Success ? match2.Groups[1].Value : "";

                            if (!string.IsNullOrEmpty(bookContainer))
                            {
                                string[] words = bookContainer.Split(' ');
                                string formattedContainer;

                                if (words.Length == 3)
                                {
                                    formattedContainer = $"{words[0]} {words[2]}";
                                }
                                else if (words.Length == 2)
                                {
                                    formattedContainer = $"{words[0]} {words[1]}";
                                }
                                else
                                {
                                    formattedContainer = words[0];
                                }

                                Host.EchoText("Scanning Deed Register.");
                                Host.SendText("turn my deed register to contents");
                                Host.SendText("read my deed register");
                                bookContainer = formattedContainer;
                            }
                        }
                        else if (Regex.IsMatch(trimtext, @"^\|\s*Page\s*\|\s*Type\s*\|\s*Deed\s*\|\s*Notes\s*\|")) // This text appears at the beginning of the deed register list when Toggle Craft is active
                        {
                            togglecraft = true;
                            ScanStart("Deed");
                        }   
                        else if (text.StartsWith("   Page -- [Type]       Deed")) // This text appears at the beginning of the deed register list when Toggle Craft is not active
                        {
                            togglecraft = false;
                            ScanStart("Deed");
                        }
                        // If you don't have a deed register or it is empty, it skips to checking for tool catalog.
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            Host.EchoText("Skipping Deed Register.");
                            ScanMode = "CatalogStart";
                            Host.SendText("get my tool catalog");
                        }
                        else if (IsDenied(trimtext))
                        {
                            Host.EchoText("Skipping Deed Register.");
                            if (bookContainer == "")
                                Host.SendText("stow my deed register");
                            else
                                Host.SendText("put my deed register in my " + bookContainer);
                            ScanMode = "CatalogStart";
                            bookContainer = "";
                            Host.SendText("get my tool catalog");
                        }
                        break;//end if DeedStart
                    case "Deed":
                        if (Regex.IsMatch(trimtext, @"^Currently [S|s]tored"))
                        {
                            if (bookContainer == "")
                                Host.SendText("stow my deed register");
                            else
                                Host.SendText("put my deed register in my " + bookContainer);
                            ScanMode = "CatalogStart";
                            bookContainer = "";
                            Host.SendText("get my tool catalog");
                        }
                        else
                        {
                            if (togglecraft)
                            {
                                if (!Regex.IsMatch(trimtext, @"\| Page\| Type"))
                                {
                                    trimtext = trimtext.Trim();
                                    string outputLine;

                                    string[] parts = trimtext.Split('|');

                                    if (parts.Length >= 4)
                                    {
                                        string pageNumber = parts[1].Trim();
                                        string type = parts[2].Trim();
                                        string deed = Regex.Replace(parts[3], @"\s(an?|some|several)\s", " ").Trim();
                                        string notes = parts[4].Trim();
                                        
                                        if (notes != "")
                                            outputLine = $"{pageNumber} -- [{type}]     {deed}  ({notes})";
                                        else
                                            outputLine = $"{pageNumber} -- [{type}]     {deed}";

                                        lastItem = currentData.AddItem(new ItemData() { tap = outputLine, storage = false });
                                    }
                                }
                            }
                            else
                            {
                                string tap = Regex.Replace(trimtext, @"a deed for\s(an?|some|several)", " ");
                                lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = false });
                            }
                        }
                        break;//end of Deed
                    
                    case "CatalogStart":
                        if (text.StartsWith("Roundtime:"))
                        {
                            PauseForRoundtime(trimtext);
                            ScanMode = "CatalogStart";
                            Host.SendText("get my tool catalog");
                        }
                        // Get the catalog & read it.
                        if (Regex.Match(trimtext, "^You get a.*tool catalog.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*tool catalog.*from.+your (.+)\\.");
                            bookContainer = match2.Success ? match2.Groups[1].Value : "";

                            if (!string.IsNullOrEmpty(bookContainer))
                            {
                                string[] words = bookContainer.Split(' ');
                                string formattedContainer;

                                if (words.Length == 3)
                                {
                                    formattedContainer = $"{words[0]} {words[2]}";
                                }
                                else if (words.Length == 2)
                                {
                                    formattedContainer = $"{words[0]} {words[1]}";
                                }
                                else
                                {
                                    formattedContainer = words[0];
                                }

                                Host.EchoText("Scanning tool catalog.");
                                Host.SendText("turn my tool catalog to contents");
                                Host.SendText("read my tool catalog");
                                bookContainer = formattedContainer;
                            }
                        }
                        else if (text.StartsWith("   Page -- Tool")) // This text appears at the beginning of the tool catalog.
                        {
                            ScanStart("Catalog");
                        }
                        // If you don't have a tool catalog or it is empty, it skips to checking your house.
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            Host.EchoText("Skipping Tool Catalog.");
                            ScanMode = "HomeStart";
                            Host.SendText("home recall");
                        }
                        else if (IsDenied(trimtext))
                        {
                            Host.EchoText("Skipping Tool Catalog.");
                            if (bookContainer == "")
                                Host.SendText("stow my tool catalog");
                            else
                                Host.SendText("put my tool catalog in my " + bookContainer);
                            ScanMode = "HomeStart";
                            bookContainer = "";
                            Host.SendText("home recall");
                        }
                        break; //end of CatalogStart 
                    case "Catalog":
                        if (trimtext.StartsWith("Currently stored:"))
                        {
                            if (bookContainer == "")
                                Host.SendText("stow my tool catalog");
                            else
                                Host.SendText("put my tool catalog in my " + bookContainer);
                            ScanMode = "HomeStart";
                            bookContainer = "";
                            Host.SendText("home recall");
                        }
                        else
                        {
                            _ = Regex.Replace(trimtext, @" -- (an?|some|several)", " -- ");
                            lastItem = currentData.AddItem(new ItemData() { tap = trimtext, storage = false });
                        }
                        break;//end of Catalog

                    case "HomeStart":
                        if (trimtext == "The home contains:") // This text appears at the beginning of the home list.
                        {
                            Host.EchoText("Scanning Home.");
                            ScanStart("Home");
                        }
                        // This text appears if you don't have a home, skips and saves the results.
                        else if (trimtext.StartsWith("Your documentation filed with the Estate Holders"))
                        {
                            Host.EchoText("Skipping Home.");
                            GuildCheck(trimtext);
                        }
                        else if (IsDenied(trimtext))
                        {
                            GuildCheck(trimtext);
                        }
                        break; //end of HomeStart
                    case "Home":
                        if (Regex.IsMatch(trimtext, @"\^[^>]*>|[^>]*\>|>|\^\>|^Roundtime:")) // There is no text after the home list, so watch for the next >
                        {
                            GuildCheck(trimtext);
                        }
                        else if (trimtext.StartsWith("Attached:")) // If the item is attached, it is in/on/under/behind a piece of furniture.
                        {
                            string tap = trimtext.Replace("Attached: ", "");
                            if (tap[^1] == '.')
                                tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            lastItem = (lastItem.parent ?? lastItem).AddItem(new ItemData() { tap = tap });
                        }
                        else // Otherwise, it is a piece of furniture.
                        {
                            string tap = trimtext[(trimtext.IndexOf(":") + 2)..];
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                            lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                        }
                        break; //end of Home
                   
                    case "TraderStart":
                        // Get the storage book & read it.
                        if (Regex.Match(trimtext, "^You get a.*storage book.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*storage book.*from.+your (.+)\\.");
                            bookContainer = match2.Success ? match2.Groups[1].Value : "";

                            if (!string.IsNullOrEmpty(bookContainer))
                            {
                                string[] words = bookContainer.Split(' ');
                                string formattedContainer;

                                if (words.Length == 3)
                                {
                                    formattedContainer = $"{words[0]} {words[2]}";
                                }
                                else if (words.Length == 2)
                                {
                                    formattedContainer = $"{words[0]} {words[1]}";
                                }
                                else
                                {
                                    formattedContainer = words[0];
                                }

                                Host.EchoText("Scanning Trader Storage.");
                                Host.SendText("read my storage book");
                                bookContainer = formattedContainer;
                            }
                        }
                        else if (trimtext == "in the known realms since 402.") // This text appears at the beginning of the storage book list.
                        {
                            ScanStart("Trader");
                        }
                        // If you don't have a storage book or you can't read a storage book, it skips to checking your house.
                        else if (IsDenied(trimtext))
                        {
                            ScanMode = null;
                            Host.EchoText("Skipping Trader Storage.");
                            Host.EchoText("Scan Complete.");
                            Host.SendText("#parse InventoryView scan complete");
                            LoadSave.SaveSettings();
                        }
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            ScanMode = null;
                            Host.EchoText("Skipping Trader Storage.");
                            Host.EchoText("Scan Complete.");
                            Host.SendText("#parse InventoryView scan complete");
                            bookContainer = "";
                            LoadSave.SaveSettings();
                        }
                        break; // end of trader start
                    case "Trader":
                        // This text indicates the end of the storage box inventory list.
                        if (text.StartsWith("A notation at the bottom indicates"))
                        {
                            ScanMode = null;
                            Host.EchoText("Scan Complete.");   
                            if (bookContainer == "")
                                Host.SendText("stow my storage book");
                            else
                                Host.SendText("put my storage book in my " + bookContainer);
                                Host.SendText("#parse InventoryView scan complete");
                                bookContainer = "";
                            LoadSave.SaveSettings();
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            int spaces = text.Length - text.TrimStart().Length;
                            spaces = (spaces >= 4 && spaces <= 14 && spaces % 2 == 0) ? spaces / 2 - 1 : (spaces == 15) ? 7 : 1;

                            string tap = trimtext;
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");

                            // The logic below builds a tree of inventory items.
                            if (spaces == 1) // If the item is in the first level, add to the root item list
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level) // If this is the same level as the previous item, add to the previous item's parent's item list.
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level + 1) // If this item is down a level from the previous, add it to the previous item's item list.
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else // Else, if the item is up a level, loop back until you reach the correct level.
                            {
                                for (int i = spaces; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = spaces;
                        }
                    break; //end of Trader
                    
                    case "MoonMageStart":
                        if (Regex.Match(trimtext, "^You feel fully prepared to cast your spell\\.").Success)
                        {
                            Host.EchoText("Scanning Shadow Servant.");
                            Host.SendText("cast servant");
                        }
                        else if (trimtext == "Within the belly of the Shadow Servant you see:") // Shadow Servant start of the list.
                        {
                            ScanStart("MoonMage");
                        }
                        else if (IsDenied(trimtext))
                        {
                            ScanMode = null;
                            Host.EchoText("Skipping Servant, Piercing Gaze required.");
                            Host.EchoText("Scan Complete.");
                            Host.SendText("#parse InventoryView scan complete");
                            LoadSave.SaveSettings();
                        }
                        break;
                    case "MoonMage":
                        if (text.StartsWith("Your Servant is holding"))
                        {
                            ScanMode = null;
                            Host.EchoText("Scan Complete.");
                            Host.SendText("#parse InventoryView scan complete");
                            LoadSave.SaveSettings();
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            int spaces = text.Length - text.TrimStart().Length;
                            spaces = (spaces >= 4 && spaces <= 14 && spaces % 2 == 0) ? spaces / 2 - 1 : (spaces == 15) ? 7 : 1;

                            string tap = trimtext;
                            tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");

                            if (spaces == 1)
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level)
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (spaces == level + 1)
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else
                            {
                                for (int i = spaces; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = spaces;
                        }
                        break; // end of moon mage

                    default:
                        ScanMode = null;
                        break;
                }
            }

            return text;
        }

        void ScanStart(string mode)
        {
            ScanMode = mode;
            if (mode == "FamilyVault")
            {
                // Find the index of the CharacterData with the specified source
                int sourceIndex = CharacterData.FindIndex(cd => cd.name == "Family Vault(s)" && cd.source == accountName);

                if (sourceIndex != -1)
                {
                    CharacterData.RemoveAt(sourceIndex);
                }

                // Create a new CharacterData
                currentData = new CharacterData() { name = "Family Vault(s)", source = accountName };

                // Insert the new CharacterData in alphabetical order
                CharacterData.Add(currentData);
                CharacterData = CharacterData.OrderBy(cd => cd.source).ToList();

                level = 1;
            }
            else
            {
                if (mode == "InVault")
                    mode = "Vault";
                if (mode == "Standard")
                    mode = "Vault";
                if (mode == "Trader")
                    mode = "Trader Storage";
                if (mode == "Catalog")
                    mode = "Tool Catalog";
                if (mode == "MoonMage")
                    mode = "Shadow Servant";
                currentData = new CharacterData() { name = Host.get_Variable("charactername"), source = mode };
                CharacterData.Add(currentData);
                level = 1;
            }
        }

        private static void PauseForRoundtime(string text)
        {
            Match match = Regex.Match(text, @"^(Roundtime:|\.\.\.wait)\s{1,3}(\d{1,3})\s{1,3}(secs?|seconds?)\.$");
            int roundtime = int.Parse(match.Groups[2].Value);
            Host.EchoText($"Pausing {roundtime} seconds for RT.");
            Thread.Sleep(roundtime * 1000);
        }

        private void GuildCheck(string text)
        {
            if (text.StartsWith("Roundtime:"))
            {
                PauseForRoundtime(text);
            }

            switch (guild)
            {
                case "Trader":
                    ScanMode = "TraderStart";
                    Host.SendText("get my storage book");
                    break;

                case "Moon Mage":
                    string shadow = Host.get_Variable("roomobjs");
                    if (shadow.Contains("Shadow Servant"))
                    {
                        ScanMode = "MoonMageStart";
                        Host.SendText("prep pg 5");
                    }
                    else
                    {
                        ScanMode = null;
                        Host.EchoText("Scan Complete.");
                        Host.SendText("#parse InventoryView scan complete");
                        LoadSave.SaveSettings();
                    }
                    break;

                default:
                    ScanMode = null;
                    Host.EchoText("Scan Complete.");
                    Host.SendText("#parse InventoryView scan complete");
                    LoadSave.SaveSettings();
                    break;
            }
        }

        private static bool RummageCheck(string trimtext, string currentSurface, out string resultText)
        {
            resultText = null;

            if (Regex.IsMatch(trimtext, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(currentSurface)} but there is nothing in there\\."))
            {
                resultText = trimtext;
                return true;
            }
            else if (Regex.IsMatch(trimtext, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(currentSurface)} and see (.+\\.?)"))
            {
                resultText = trimtext;
                return true;
            }

            return false;
        }


        private void SurfaceRummage(string surfaceType, string rummageText)
        {
            lastItem = currentData.AddItem(new ItemData() { tap = surfaceType, storage = true });
            if (Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Success)
            {
                string itemsMatch = Regex.Match(rummageText, $"^You rummage(?: through| around on) (?:a|an) {Regex.Escape(surfaceType)} and see (.+\\.?)").Groups[1].Value;

                List<string> items = new(itemsMatch.Split(','));

                if (Regex.IsMatch(items[^1], @"\band\s(?:a|an|some|several)\b"))
                {
                    string[] lastItemSplit = Regex.Split(items[^1], @"\band\s(?:an?|some|several)\b");
                    items.RemoveAt(items.Count - 1);

                    // Combine the last two items if the first part doesn't end with an article
                    if (!Regex.IsMatch(lastItemSplit[0], @"\b(?:an?|some|several)\b"))
                    {
                        items[^1] += " " + lastItemSplit[0];
                        Array.Copy(lastItemSplit, 1, lastItemSplit, 0, lastItemSplit.Length - 1);
                    }
                    items.AddRange(lastItemSplit);
                }

                foreach (string itemText in items)
                {
                    string tap = itemText.Trim();

                    if (tap[^1] == '.')
                        tap = tap.TrimEnd('.');
                    tap = Regex.Replace(tap, @"^(an?|some|several)\s", "");
                    lastItem = (lastItem.parent ?? lastItem).AddItem(new ItemData() { tap = tap });
                }
            }
        }

        static bool IsDenied(string text)
        {
            var deniedPatterns = new List<string>
            {
                "^A Dwarven attendant steps in front of you, barring your path\\.  \"Wait a bit, \\S+\\.  You were just in there not too long ago\\.\"",
                "^The script that the vault book is written in is unfamiliar to you\\.  You are unable to read it\\.",
                "^The storage book is filled with complex lists of inventory that make little sense to you\\.",
                "^The vault book is filled with blank pages pre-printed with branch office letterhead\\.",
                "^This storage book doesn't seem to belong to you\\.",
                "^You can't access [A-z ]+ vault at this time[A-z \\.]+",
                "^You currently do not have access to VAULT STANDARD or VAULT FAMILY\\.  You will need to use VAULT PAY CONVERT to convert an urchin runner for this purpose\\.",
                "^You currently do not have a vault rented\\.",
                "^You currently do not have access to VAULT \\w+\\.  You will need to use VAULT PAY CONVERT to convert an urchin runner for this purpose\\.",
                "^You currently have no contract with the representative of the local Traders' Guild for this service\\.",
                "^You have no idea how to cast that spell\\.",
                "^You haven't documented any stored tools in the catalog\\.  You could note \\d+ in total\\.",
                "^You haven't stored any deeds in this register\\.  It can hold \\d+ deeds in total\\.",
                "^You haven't stored any deeds in this register\\.",
                "^You have no arrangements with the local Traders' Guild representative for urchin runners\\.",
                "^You look around, but cannot find a nearby urchin to send to effect the transfer\\.",
                "^You shouldn't do that to somebody else's deed book\\.",
                "^You shouldn't do that while inside of a home\\.  Step outside if you need to check something\\.",
                "^You shouldn't read somebody else's \\w+ \\w+\\.",
                "^Now may not be the best time for that\\.",
                "^\\[You don't have access to advanced vault urchins because you don't have a subscription\\.  To sign up for one, please visit https\\://www\\.play\\.net/dr/signup/subscribe\\.asp \\.\\]"
            };

            foreach (var pattern in deniedPatterns)
	        {
		        if (Regex.IsMatch(text, pattern))
		        {
			        return true;
		        }
	        }

	        return false;
        }

        public string ParseInput(string text)
        {
            if (text.ToLower().StartsWith("/inventoryview ") || text.ToLower().StartsWith("/iv "))
            {
                var SplitText = text.Split(' ');
                if (SplitText.Length == 1 || SplitText[1].ToLower() == "help")
                {
                    Help();
                }
                else if (SplitText[1].ToLower() == "scan")
                {
                    if (Host.get_Variable("connected") == "0")
                    {
                        Host.EchoText("You must be connected to the server to do a scan.");
                    }
                    else
                    {
                        LoadSave.LoadSettings();
                        ScanMode = "Start";
                        while (CharacterData.Where(tbl => tbl.name == Host.get_Variable("charactername")).Any())
                        {
                            CharacterData.Remove(CharacterData.Where(tbl => tbl.name == Host.get_Variable("charactername")).First());
                        }
                        if (((InventoryViewForm)Form).toolStripFamily.Checked)
                            Host.SendText("played");
                        Host.SendText("info");
                    }
                }
                else if (SplitText[1].ToLower() == "open")
                {
                    Show();
                }
                else if (SplitText[1].ToLower() == "debug")
                {
                    Debug = !Debug;
                    Host.EchoText("InventoryView Debug Mode " + (Debug ? "ON" : "OFF"));
                }
                else if (SplitText[1].ToLower() == "lasttext")
                {
                    Debug = !Debug;
                    Host.EchoText("InventoryView Debug Last Text: " + LastText);
                }
                else if (SplitText[1].ToLower() == "search" && SplitText.Length > 2)
                {
                    string searchText = SplitText[2];
                    if (SplitText.Length > 3)
                    {
                        searchText += " " + SplitText[3];
                    }

                    if (searchText.Length < 3)
                    {
                        Host.EchoText("Search text should be one or two words and larger than 2 characters.");
                        return text;
                    }

                    string style = "line";
                    InventoryTextSearch.PerformSearch(searchText, style);
                }
                else if (SplitText[1].ToLower() == "path" && SplitText.Length > 1)
                {
                    if (SplitText.Length < 3)
                    {
                        Host.EchoText("Search text should be full tap for the 'path' command.");
                        return text;
                    }

                    string searchText = string.Join(" ", SplitText, 2, SplitText.Length - 2);
                    string style = "path";
                    InventoryTextSearch.PerformSearch(searchText, style);
                }
                return string.Empty;
            }
            return text;
        }

        public static void Help()
        {
            Host.EchoText("Inventory View plugin options:");
            Host.EchoText("/InventoryView scan  -- scan the items on the current character.");
            Host.EchoText("/InventoryView open  -- open the InventoryView Window to see items.");
            Host.EchoText("/InventoryView search keyword -- Will search xml for matches from command line.");
            Host.EchoText("/InvenotryView path tap -- Will show the path from command line.");
            Host.EchoText("All of these can also be done using /IV as well.");
        }

        public void ParseXML(string xml)
        {

        }

        public void ParentClosing()
        {

        }

        public string Name
        {
            get { return "Inventory View"; }
        }

        public string Version
        {
            get { return "2.2.24"; }
        }

        public string Description
        {
            get { return "Stores your character inventory and allows you to search items across characters."; }
        }

        public string Author
        {
            get { return "Etherian <EtherianDR@gmail.com>"; }
        }

        private bool _enabled = true;
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public static Form Form { get => form; set => form = value; }
        public static IHost Host { get => host; set => host = value; }
        public static List<CharacterData> CharacterData { get => characterData; set => characterData = value; }

        public static List<string> SurfacesEncountered => surfacesEncountered;
    }
}
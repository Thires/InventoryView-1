using System;
using System.Collections.Generic;
using GeniePlugin.Interfaces;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace InventoryView
{
    public class Class1 : GeniePlugin.Interfaces.IPlugin
    {
        // Genie host.
        public static IHost _host;

    // Plugin Form
    public static Form _form;

        // This contains all the of the inventory data.
        public static List<CharacterData> characterData = new List<CharacterData>();

        // Path to Genie config.
        private static string basePath = Application.StartupPath;

        // Whether or not InventoryView is currently scanning data, and what state it is in.
        private string ScanMode = null;

        // Keeps track of how many containers deep you are when scanning inventory in containers.
        private int level = 1;

        // The current character & source being scanned.
        private CharacterData currentData = null;

        // The last item tha was scanned.
        private ItemData lastItem = null;

        private bool Debug = false;
        private string LastText = "";
	    private string bookContainer;

        public void Initialize(IHost host)
        {
            _host = host;

            basePath = _host.get_Variable("PluginPath");

            // Create a new instance of the InventoryViewForm class
            _form = new InventoryViewForm();

            // Load inventory from the XML config if available.
            LoadSettings(initial: true);
        }


        public void Show()
        {
            if (_form == null || _form.IsDisposed)
                _form = new InventoryViewForm();

            _form.Show();
        }

        public void VariableChanged(string variable)
        {
        }

        public string ParseText(string text, string window)
        {
            if (ScanMode != null) // If a scan isn't in progress, do nothing here.
            {
                string trimtext = text.Trim(new char[] { '\n', '\r', ' ' }); // Trims spaces and newlines.
                LastText = trimtext;
                if (trimtext.StartsWith("XML") && trimtext.EndsWith("XML")) return ""; // Skip XML parser lines
                else if (string.IsNullOrEmpty(trimtext)) return ""; // Skip blank lines
                switch (ScanMode)
                {
                    case "Start":
                        if (trimtext == "You have:") // Text that appears at the beginning of "inventory list"
                        {
                            _host.EchoText("Scanning Inventory.");
                            ScanStart("Inventory");
                        }
                        break;
                    case "Inventory":
                        if (trimtext.StartsWith("Roundtime:")) // text that appears at the end of "inventory list"
                        {
                            // Inventory List has a RT based on the number of items, so grab the number and pause the thread for that length.
                            PauseForRoundtime(trimtext);
                            ScanMode = "VaultStart";
                            _host.SendText("get my vault book");
                        }
                        else if (trimtext.StartsWith("[Use"))
                            return "";
                        else
                        {
                            // The first level of inventory has a padding of 2 spaces to the left, and each level adds an additional 3 spaces.
                            // 2, 5, 8, 11, 14, etc..
                            int spaces = text.Length - text.TrimStart().Length;
                            int newlevel = (spaces + 1) / 3;
                            string tap = trimtext;
                            // remove the - from the beginning if it exists.
                            if (tap.StartsWith("-")) tap = tap.Remove(0, 1);
                            if (tap[tap.Length - 1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");

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
                    case "VaultStart":
                        // Get the vault book & read it.
                        if (Regex.Match(trimtext, "^You get a.*vault book.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*vault book.*from.+your (.+)\\.");
                            bookContainer = string.Format("{0}", match2.Groups[1]);
                            _host.EchoText("Scanning Book Vault.");
                            _host.SendText("read my vault book");
                        }
                        else if (trimtext == "Vault Inventory:") // This text appears at the beginning of the vault list.
                        {
                            ScanStart("Vault");
                        }
                        // If you don't have a vault book or you can't read a vault book, it skips to checking use of vault standard.
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            ScanMode = "StandardStart";
                            _host.EchoText("Skipping Book Vault.");
                            _host.SendText("vault standard");
                        }

                        else if (IsDenied(trimtext))
                        {
                            ScanMode = "StandardStart";
                            _host.EchoText("Skipping Book Vault.");
                            if (bookContainer == "")
                                _host.SendText("stow my vault book");
                            else
                                _host.SendText("put my vault book in my " + bookContainer);
                            bookContainer = "";
                            _host.SendText("vault standard");
                        }
                        break; //end of VaultStart
                    case "Vault":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("The last note in your book indicates that your vault contains"))
                        {
                            ScanMode = "FamilyStart";
                            if (bookContainer == "")
                                _host.SendText("stow my vault book");
                            else
                                _host.SendText("put my vault book in my " + bookContainer);
                            bookContainer = "";
                            _host.SendText("vault family");
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            // Anything greater than 4 levels down shows up at the same level as its parent.
                            int spaces = text.Length - text.TrimStart().Length;
                            int newlevel = 1;
                            if (spaces > 4)
                                newlevel += (spaces - 4) / 2;
                            string tap = trimtext;
                            if (tap.StartsWith("-")) tap = tap.Remove(0, 1);
                            if (tap[tap.Length - 1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
                            if (newlevel == 1)
                            {
                                lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                            }
                            else if (newlevel == level)
                            {
                                lastItem = lastItem.parent.AddItem(new ItemData() { tap = tap });
                            }
                            else if (newlevel == level + 1)
                            {
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            else
                            {
                                for (int i = newlevel; i <= level; i++)
                                {
                                    lastItem = lastItem.parent;
                                }
                                lastItem = lastItem.AddItem(new ItemData() { tap = tap });
                            }
                            level = newlevel;
                        }
                        break; //end of Vault
                    case "StandardStart":
                        if (Regex.Match(text, @"^You flag down a local you know works with the Estate Holders' Council and send \w+ to the nearest carousel.").Success || trimtext == "You are already holding that.")
                        {
                            _host.EchoText("Scanning Standard Vault.");
                        }

                        else if (trimtext == "Vault Inventory:") // This text appears at the beginning of the vault list.
                        {
                            ScanStart("Standard");
                        }
                        // If you don't have access to vault standard, it skips to checking for family vault.
                        else if (IsDenied(trimtext))
                        {
                            _host.EchoText("Skipping Standard Vault.");
                            ScanMode = "FamilyStart";
                            _host.SendText("vault family");
                        }
                        break; //end of VaultStandardStart
                    case "Standard":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("The last note indicates that your vault contains"))
                        {
                            ScanMode = "FamilyStart";
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            // Anything greater than 4 levels down shows up at the same level as its parent.
                            int spaces = text.Length - text.TrimStart().Length;
                            switch (spaces)
                            {
                                case 5:
                                    spaces = 1;
                                    break;
                                case 10:
                                    spaces = 2;
                                    break;
                                case 15:
                                    spaces = 3;
                                    break;
                                case 20:
                                    spaces = 4;
                                    break;
                                default:
                                    spaces = 1;
                                    break;
                            }

                            string tap = trimtext;
                            if (tap[tap.Length - 1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
                            tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some)\s", ") ");
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
                            _host.SendText("vault family");
                        }
                        if (Regex.Match(trimtext, "You flag down an urchin and direct him to the nearest carousel").Success || trimtext == "You flag down an urchin and direct her to the nearest carousel")
                        {
                            _host.EchoText("Scanning Family Vault.");
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
                                this.ScanMode = "DeedStart";
                                _host.SendText("get my deed register");
                            }
                            else
                            {
                                _host.EchoText("Skipping Family Vault.");
                                ScanMode = "DeedStart";
                                _host.SendText("get my deed register");
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
                            // Anything greater than 4 levels down shows up at the same level as its parent.
                            int spaces = text.Length - text.TrimStart().Length;
                            switch (spaces)
                            {
                                case 5:
                                    spaces = 1;
                                    break;
                                case 10:
                                    spaces = 2;
                                    break;
                                case 15:
                                    spaces = 3;
                                    break;
                                case 20:
                                    spaces = 4;
                                    break;
                                default:
                                    spaces = 1;
                                    break;
                            }
                            string tap = trimtext;
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
                            tap = Regex.Replace(tap, @"\)\s{1,4}(an?|some)\s", ") ");
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
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
                            _host.SendText("get my deed register");
                        }
                        // Get the register & read it.
                        Match match1 = Regex.Match(trimtext, @"^You get a.*deed register.*from");
                        if (match1.Success || trimtext == "You are already holding that.")
                        {
                            Match match2 = Regex.Match(trimtext, "^You get a.*deed register.*from.+your (.+)\\.");
                            bookContainer = string.Format("{0}", match2.Groups[1]);
                            _host.EchoText("Scanning Deed Register.");
                            _host.SendText("turn my deed register to contents");
                            _host.SendText("read my deed register");
                        }

                        else if (text.StartsWith("   Page -- [Type]       Deed")) // This text appears at the beginning of the deed register list.
                        {
                            ScanStart("Deed");
                        }
                        // If you don't have a deed register or it is empty, it skips to checking your house.
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            _host.EchoText("Skipping Deed Register.");
                            ScanMode = "HomeStart";
                            _host.SendText("home recall");
                        }
                        else if (IsDenied(trimtext))
                        {
                            _host.EchoText("Skipping Deed Register.");
                            if (bookContainer == "")
                                _host.SendText("stow my deed register");
                            else
                                _host.SendText("put my deed register in my " + bookContainer);
                            ScanMode = "HomeStart";
                            bookContainer = "";
                            _host.SendText("home recall");
                        }
                        break;//end if DeedStart
                    case "Deed":
                        if (trimtext.StartsWith("Currently stored"))
                        {
                            if (bookContainer == "")
                                _host.SendText("stow my deed register");
                            else
                                _host.SendText("put my deed register in my " + bookContainer);
                            ScanMode = "HomeStart";
                            bookContainer = "";
                            _host.SendText("home recall");
                        }
                        else
                        {
                            string tap = Regex.Replace(trimtext, @"a deed for\s(an?|some)", " ");

                            if (tap[tap.Length - 1] == '.')
                                tap = tap.TrimEnd('.');

                            lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = false });
                        }
                        break;//end of Deed
                    case "HomeStart":
                        if (trimtext == "The home contains:") // This text appears at the beginning of the home list.
                        {
                            _host.EchoText("Scanning Home.");
                            ScanStart("Home");
                        }
                        // This text appears if you don't have a home, skips and saves the results.
                        else if (trimtext.StartsWith("Your documentation filed with the Estate Holders"))
                        {
                            _host.EchoText("Skipping Home.");
                            if (_host.get_Variable("guild") == "Trader")
                            {
                                ScanMode = "TraderStart";
                                _host.SendText("get my storage book");
                            }
                            else
                            {
                                ScanMode = null;
                                _host.EchoText("Scan Complete.");
                                _host.SendText("#parse InventoryView scan complete");
                                SaveSettings();
                            }
                        }
                        else if (IsDenied(trimtext))
                        {
                            if (_host.get_Variable("guild") == "Trader")
                            {
                                ScanMode = "TraderStart";
                                _host.SendText("get my storage book");
                            }
                            else
                            {
                                ScanMode = null;
                                _host.EchoText("Scan Complete.");
                                _host.SendText("#parse InventoryView scan complete");
                                SaveSettings();
                            }
                        }
                        break; //end of HomeStart
                    case "Home":
                        if (Regex.IsMatch(trimtext, @"\^[^>]*>|[^>]*\>|>|\^\>")) // There is no text after the home list, so watch for the next >
                        {
                            if (_host.get_Variable("guild") == "Trader")
                            {
                                ScanMode = "TraderStart";
                                _host.SendText("get my storage book");
                            }
                            else
                            {
                                ScanMode = null;
                                _host.EchoText("Scan Complete.");
                                _host.SendText("#parse InventoryView scan complete");
                                SaveSettings();
                            }
                        }
                        else if (trimtext.StartsWith("Attached:")) // If the item is attached, it is in/on/under/behind a piece of furniture.
                        {
                            string tap = trimtext.Replace("Attached: ", "");
                            if (tap[tap.Length - 1] == '.')
                                tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
                            lastItem = (lastItem.parent != null ? lastItem.parent : lastItem).AddItem(new ItemData() { tap = tap });
                        }
                        else // Otherwise, it is a piece of furniture.
                        {
                            string tap = trimtext.Substring(trimtext.IndexOf(":") + 2);
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");
                            lastItem = currentData.AddItem(new ItemData() { tap = tap, storage = true });
                        }
                        break; //end of Home
                    case "TraderStart":
                        // Get the storage book & read it.
                        if (Regex.Match(trimtext, "^You get a.*storage book.*from").Success || trimtext == "You are already holding that.")
                        {
                            Match match3 = Regex.Match(trimtext, "^You get a.*storage book.*from.+your (.+)\\.");
                            bookContainer = string.Format("{0}", match3.Groups[1]);
                            _host.EchoText("Scanning Trader Storage.");
                            _host.SendText("read my storage book");
                        }
                        else if (trimtext == "in the known realms since 402.") // This text appears at the beginning of the storage book list.
                        {
                            ScanStart("Trader");
                        }
                        // If you don't have a storage book or you can't read a storage book, it skips to checking your house.
                        else if (IsDenied(trimtext))
                        {
                            ScanMode = null;
                            _host.EchoText("Skipping Trader Storage.");
                            _host.EchoText("Scan Complete.");
                            _host.SendText("#parse InventoryView scan complete");
                            SaveSettings();
                        }
                        else if (Regex.IsMatch(text, "^What were you referring to\\?"))
                        {
                            ScanMode = null;
                            _host.EchoText("Skipping Trader Storage.");
                            _host.EchoText("Scan Complete.");
                            if (bookContainer == "")
                                _host.SendText("stow my storage book");
                            else
                                _host.SendText("put my storage book in my " + bookContainer);
                            _host.SendText("#parse InventoryView scan complete");
                            bookContainer = "";
                            SaveSettings();
                        }
                        break; // end of trader start
                    case "Trader":
                        // This text indicates the end of the vault inventory list.
                        if (text.StartsWith("A notation at the bottom indicates"))
                        {
                            ScanMode = null;
                            _host.EchoText("Scan Complete.");
                            if (bookContainer == "")
                                _host.SendText("stow my storage book");
                            else
                                _host.SendText("put my storage book in my " + bookContainer);
                                _host.SendText("#parse InventoryView scan complete");
                                bookContainer = "";
                            SaveSettings();
                        }
                        else
                        {
                            // Determine how many levels down an item is based on the number of spaces before it.
                            // Anything greater than 4 levels down shows up at the same level as its parent
                            int spaces = text.Length - text.TrimStart().Length;
                            int newlevel = (spaces + 1) / 3;
                            switch (spaces)
                            {
                                case 4:
                                    newlevel = 1;
                                    break;
                                case 6:
                                    newlevel = 2;
                                    break;
                                case 8:
                                    newlevel = 3;
                                    break;
                                case 12:
                                    newlevel = 4;
                                    break;
                                default:
                                    newlevel = 1;
                                    break;
                            }
                            string tap = trimtext;
                            // remove the - from the beginning if it exists.
                            if (tap.StartsWith("-")) tap = tap.Remove(0, 1);
                            if (tap[tap.Length - 1] == '.') tap = tap.TrimEnd('.');
                            tap = Regex.Replace(tap, @"^(an?|some)\s", "");

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
                    break; //end of Trader
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
            if (mode == "Standard")
                mode = "Vault";
            if (mode == "FamilyVault")
                mode = "Family Vault";
            if (mode == "Trader")
                mode = "Trader Storage";
            currentData = new CharacterData() { name = _host.get_Variable("charactername"), source = mode};
            characterData.Add(currentData);
            level = 1;
        }

        private void PauseForRoundtime(string text)
        {
            Match match = Regex.Match(text, "^Roundtime:\\s{1,3}(\\d{1,3})\\s{1,3}secs?\\.$");
            int roundtime = int.Parse(match.Groups[1].Value);
            _host.EchoText($"Pausing {roundtime} seconds for RT.");
            Thread.Sleep(roundtime * 1000);
        }

        bool IsDenied(string text)
        {
            return text == "You currently do not have access to VAULT STANDARD or VAULT FAMILY.  You will need to use VAULT PAY CONVERT to convert an urchin runner for this purpose."
                || text == "You currently have no contract with the representative of the local Traders' Guild for this service."
                || text == "You have no arrangements with the local Traders' Guild representative for urchin runners."
                || text == "Now may not be the best time for that."
                || text == "You look around, but cannot find a nearby urchin to send to effect the transfer."
                || text == "You can't access the family vault at this time."
                || text == "You can't access your vault at this time.  Rent is due."
                || text == "You can't access your vault at this time."
                || text == "You currently do not have a vault rented."
                || text == "The script that the vault book is written in is unfamiliar to you.  You are unable to read it."
                || text == "The vault book is filled with blank pages pre-printed with branch office letterhead.  An advertisement touting the services of Rundmolen Bros. Storage Co. is pasted on the inside cover."
                || text == "You haven't stored any deeds in this register."
                || text == "You shouldn't do that to somebody eles's deed book."
                || text == "You shouldn't read somebody else's deed book."
                || text == "The storage book is filled with complex lists of inventory that make little sense to you."
                || Regex.IsMatch(text, "^You shouldn't do that while inside of a home\\.  Step outside if you need to check something\\.")
                || Regex.IsMatch(text, "^\\[You don't have access to advanced vault urchins because you don't have a subscription\\.")
                || Regex.IsMatch(text, "^You haven't stored any deeds in this register\\.  It can hold \\d+ deeds in total\\.");
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
                    if (_host.get_Variable("connected") == "0")
                    {
                        _host.EchoText("You must be connected to the server to do a scan.");
                    }
                    else
                    {
                        LoadSettings();
                        ScanMode = "Start";
                        while (characterData.Where(tbl => tbl.name == _host.get_Variable("charactername")).Count() > 0)
                        {
                            characterData.Remove(characterData.Where(tbl => tbl.name == _host.get_Variable("charactername")).First());
                        }
                        _host.SendText("inventory list");
                    }
                }
                else if (SplitText[1].ToLower() == "open")
                {
                    Show();
                }
                else if (SplitText[1].ToLower() == "debug")
                {
                    Debug = !Debug;
                    _host.EchoText("InventoryView Debug Mode " + (Debug ? "ON" : "OFF"));
                }
                else if (SplitText[1].ToLower() == "lasttext")
                {
                    Debug = !Debug;
                    _host.EchoText("InventoryView Debug Last Text: " + LastText);
                }
                else
                    Help();

                return string.Empty;
            }
            return text;
        }

        public void Help()
        {
            _host.EchoText("Inventory View plugin options:");
            _host.EchoText("/InventoryView scan  -- scan the items on the current character.");
            _host.EchoText("/InventoryView open  -- open the InventoryView Window to see items.");
        }

        public static void RemoveParents(List<ItemData> iList)
        {
            foreach (var iData in iList)
            {
                iData.parent = null;
                RemoveParents(iData.items);
            }
        }

        public static void AddParents(List<ItemData> iList, ItemData parent)
        {
            foreach (var iData in iList)
            {
                iData.parent = parent;
                AddParents(iData.items, iData);
            }
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
            get { return "2.1.6"; }
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

        public static void LoadSettings(bool initial = false)
        {
            string configFile = Path.Combine(basePath, "InventoryView.xml");
            if (File.Exists(configFile))
            {
                try
                {
                    File.SetLastWriteTime(configFile, DateTime.Now);

                    // Load the XML file into an XmlDocument
                    XmlDocument doc = new XmlDocument();
                    doc.Load(configFile);

                    // Get the CheckBoxState element
                    XmlNode checkBoxStateNode = doc.DocumentElement.SelectSingleNode("CheckBoxState");

                    // Restore the state of the CheckBox from the value of the CheckBoxState element
                    if (checkBoxStateNode != null)
                    {
                        ((InventoryViewForm)_form).chkMultilineTabs.Checked = bool.Parse(checkBoxStateNode.InnerText);
                    }

                    using (Stream stream = File.Open(configFile, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<CharacterData>));
                        characterData = (List<CharacterData>)serializer.Deserialize(stream);
                    }
                    foreach (var cData in characterData)
                    {
                        AddParents(cData.items, null);
                    }
                    //if (!initial)
                    //    _host.EchoText("Inventory data loaded.");
                }
                catch (IOException ex)
                {
                    _host.EchoText("Error reading InventoryView file: " + ex.Message);
                }
            }
        }


        public static void SaveSettings()
        {
            string configFile = Path.Combine(basePath, "InventoryView.xml");
            try
            {
                // Can't serialize a class with circular references, so I have to remove the parent links first.
                foreach (var cData in characterData)
                {
                    RemoveParents(cData.items);
                }

                // Create a new XmlSerializer for the CharacterData type
                XmlSerializer serializer = new XmlSerializer(typeof(List<CharacterData>));

                // Serialize the characterData list to a StringWriter
                StringWriter writer = new StringWriter();
                serializer.Serialize(writer, characterData);

                // Load the serialized characterData XML into an XmlDocument
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(writer.ToString());

                // Create a new CheckBoxState element and set its value to the state of the checkBox1 control
                XmlElement checkBoxStateElement = doc.CreateElement("CheckBoxState");
                checkBoxStateElement.InnerText = ((InventoryViewForm)_form).chkMultilineTabs.Checked.ToString();

                // Append the CheckBoxState element to the root element of the XmlDocument
                doc.DocumentElement.AppendChild(checkBoxStateElement);

                // Save the changes to the XML file
                doc.Save(configFile);

                // ..and add them back again afterwards.
                foreach (var cData in characterData)
                {
                    AddParents(cData.items, null);
                }
            }
            catch (IOException ex)
            {
                _host.EchoText("Error writing to InventoryView file: " + ex.Message);
            }
        }

    }
}
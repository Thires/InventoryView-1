using System.Collections.Generic;
using GeniePlugin.Interfaces;
using System.Windows.Forms;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System;
using InventoryView.Cases;

namespace InventoryView
{
    public class Plugin : GeniePlugin.Interfaces.IPlugin
    {
        // Genie host.
        private static IHost host;
        public static IHost Host { get => host; set => host = value; }

        // Plugin Form.
        internal static Form form;
        public static Form Form { get => form; set => form = value; }

        private Dictionary<string, (bool Archived, string TabColor, string TabTextColor)> _preservedProperties;


        // This contains all the of the inventory data.
        private static List<CharacterData> characterData = new();
        public static List<CharacterData> CharacterData { get => characterData; set => characterData = value; }

        private static readonly List<string> surfacesEncountered = new();
        public static List<string> SurfacesEncountered => surfacesEncountered;
        private readonly string[] surfaces = { "a jewelry case", "an ammunition box", "a bottom drawer", "a middle drawer", "a top drawer", "a shoe tree", "a weapon rack", "a steel wire rack", "a small shelf", "a large shelf", "a brass hook" };

        // Whether or not InventoryView is currently scanning data, and what state it is in.
        internal string ScanMode = null;

        // Keeps track of how many containers deep you are when scanning inventory in containers.
        private int level = 1;

        // The current character & source being scanned.
        internal CharacterData currentData = null;

        // The last item tha was scanned.
        private ItemData lastItem = null;

        private bool Debug = false;
        internal bool togglecraft = false;
        internal bool InFamVault = false;

        internal string LastText = "";
        internal string bookContainer;
        internal string pocketContainer;
        internal bool pocketScanStarted;
        internal string pocketContainerShort;
        internal bool handledCurrentPocket;
        internal HashSet<string> processedPockets = new();


        internal string guild = "";
        internal string accountName = "";
        internal string currentSurface = "";
        internal List<string> closedContainers = new();

        public CaseCatalog catalog;
        public CaseDeed deed;
        public CaseHome home;
        //public CaseInVault inVault;
        public CaseInventory inventory;
        public CaseMoonMage moonmage;
        public CasePocket pocket;
        public CaseTrader trader;
        public CaseVault vault;
        //public CaseVaultFamily familyvault;
        public CaseVaultStandard standard;

        public void Initialize(IHost host)
        {
            Host = host;

            // Create a new instance of the InventoryViewForm class
            Form = new InventoryViewForm();

            // Load inventory from the XML config if available.
            LoadSave.LoadSettings();

            catalog = new CaseCatalog(this);
            deed = new CaseDeed(this);
            
            home = new CaseHome(this);
            inventory = new CaseInventory(this);
            //inVault = new CaseInVault(this);
            moonmage = new CaseMoonMage(this);
            pocket = new CasePocket(this);
            trader = new CaseTrader(this);
            vault = new CaseVault(this);
            //familyvault = new CaseVaultFamily(this);
            standard = new CaseVaultStandard(this);
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
                        inventory.InventoryCase(trimtext, text, ref ScanMode, ref level, ref lastItem, currentData);
                        break;

                    case "PocketStart":
                        pocket.PocketStart(trimtext, ref ScanMode);
                        break;

                    case "Pocket":
                        pocket.PocketCase(trimtext, ref ScanMode, ref lastItem, currentData);
                        break;

                    case "InVault":
                        if (Regex.IsMatch(trimtext, "You rummage through a secure vault but there is nothing in there\\."))
                        {
                            ScanMode = null;
                            Thread.Sleep(500);
                            Host.SendText("close vault");
                            Thread.Sleep(500);
                            Host.SendText("go door");
                            Thread.Sleep(500);
                            Host.SendText("go arch");
                            Thread.Sleep(2000);

                            ScanMode = "DeedStart";
                            Host.SendText("get my deed register");
                            break;
                        }

                        var match = Regex.Match(trimtext, "^You rummage through a(?: secure)? vault and see (.+)\\.");
                        if (match.Success)
                        {
                            string vaultInv = match.Groups[1].Value;

                            if (!string.IsNullOrWhiteSpace(vaultInv))
                            {
                                // now call ScanStart after content is confirmed
                                ScanStart("InVault");

                                if (Regex.Match(vaultInv, @"\b\sand\s(?:a|an|some|several)\s\b").Success)
                                    vaultInv = Regex.Replace(vaultInv, @"\b\sand\s(a|an|some|several)\s\b", ", $1 ");

                                List<string> items = new(vaultInv.Split(','));

                                if (Regex.IsMatch(items[^1], @"\b\s(?:a|an|some|several)\s\b"))
                                {
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
                                        lastItem = currentData.AddItem(new ItemData { tap = tap });
                                    }
                                }

                                ScanMode = "Surface";
                            }
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
                                Host.SendText("#parse Scan Complete");
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
                    case "Vault":
                        vault.VaultCase(trimtext, text, ref ScanMode, ref level, ref lastItem, currentData);
                        break;


                    case "StandardStart":
                    case "Standard":
                        standard.StandardVaultCase(trimtext, text, ref ScanMode, ref level, ref lastItem, currentData);
                        break;


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
                    case "Deed":
                        deed.DeedCase(trimtext, text, ref ScanMode, ref lastItem, currentData);
                        break;

                    case "CatalogStart":
                    case "Catalog":
                        catalog.CatalogCase(trimtext, text, ref ScanMode, ref lastItem, currentData);
                        break;

                    case "HomeStart":
                    case "Home":
                        home.HomeCase(trimtext, text, ref ScanMode, ref lastItem, currentData);
                        break;

                    case "TraderStart":
                    case "Trader":
                        trader.TraderCase(trimtext, text, ref ScanMode, ref level, ref lastItem, currentData);
                        break;

                    case "MoonMageStart":
                    case "MoonMage":
                        moonmage.MoonMageCase(trimtext, text, ref ScanMode, ref level, ref lastItem, currentData);
                        break;

                    default:
                        ScanMode = null;
                        break;
                }
            }

            return text;
        }

        internal void ScanStart(string mode)
        {
            ScanMode = mode;
            if (!mode.StartsWith("Pocket in") && mode != "Pocket")
            {
                processedPockets.Clear();
                pocketScanStarted = false;
                handledCurrentPocket = false;
                pocketContainer = null;
                pocketContainerShort = null;
            }

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
                if (!((InventoryViewForm)Form).toolStripPockets.Checked)
                {
                    if (mode == "Pocket")
                        mode = $"Pocket in {pocketContainer}";

                }
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

                        currentData = new CharacterData() 
        { 
            name = Host.get_Variable("charactername"), 
            source = mode 
        };

        // Apply preserved properties if available
        if (_preservedProperties != null && _preservedProperties.TryGetValue(mode, out var props))
        {
            currentData.Archived = props.Archived;
            currentData.TabColor = props.TabColor;
            currentData.TabTextColor = props.TabTextColor;
        }

                CharacterData.Add(currentData);
                level = 1;
            }
        }

        internal static void PauseForRoundtime(string text)
        {
            Match match = Regex.Match(text, @"^(Roundtime:|\.\.\.wait)\s{1,3}(\d{1,3})\s{1,3}(secs?|seconds?|Irenos?)\.$");
            int roundtime = int.Parse(match.Groups[2].Value);
            Host.EchoText($"Pausing {roundtime} seconds for RT.");
            Thread.Sleep(roundtime * 1000);
        }

        internal void GuildCheck(string text)
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
                        Host.SendText("#parse Scan Complete");
                        LoadSave.SaveSettings();
                    }
                    break;

                default:
                    ScanMode = null;
                    Host.EchoText("Scan Complete.");
                    Host.SendText("#parse Scan Complete");
                    LoadSave.SaveSettings();
                    break;
            }
        }

        internal static bool RummageCheck(string trimtext, string currentSurface, out string resultText)
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


        internal void SurfaceRummage(string surfaceType, string rummageText)
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

        internal static string CleanTapText(string text)
        {
            string tap = text.Trim();
            if (tap.StartsWith("-")) tap = tap[1..];
            if (tap[^1] == '.') tap = tap.TrimEnd('.');
            return Regex.Replace(tap, @"^(an?|some|several)\s", "");
        }

        internal static bool IsDenied(string text)
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
                "^\\[You don't have access to advanced vault urchins because you don't have a subscription\\.  To sign up for one, please visit https\\://www\\.play\\.net/dr/signup/subscribe\\.asp \\.\\]",
                "^While it's closed?",
                "^There is nothing in there\\.",
                "^I don't know what you are referring to\\.",
                "^You rummage through a pocket but there is nothing in there\\.",
                "^You rummage through a (?!pocket$).*$",
                "^In the keyblank pocket you see",
                "^You tap [A-z ]+ keyblank pocket",
                "^You tap [A-z ]+ pocket that you are wearing\\.",
                "^I could not find what you were referring to\\."
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

                        var characterName = Host.get_Variable("charactername");
                        var existingEntries = CharacterData.Where(tbl => tbl.name == characterName).ToList();
                        _preservedProperties = new Dictionary<string, (bool, string, string)>();
                        foreach (var entry in existingEntries)
                        {
                            _preservedProperties[entry.source] = (entry.Archived, entry.TabColor, entry.TabTextColor);
                        }

                        // Remove existing entries for the character
                        CharacterData.RemoveAll(tbl => tbl.name == characterName);


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
            Host.EchoText("Shift + Right click on a tab will open colors for it");
        }

        public void ParseXML(string xml)
        {

        }

        public void ParentClosing()
        {
            LoadSave.SaveSettings();
        }

        public string Name
        {
            get { return "Inventory View"; }
        }

        public string Version
        {
            get { return "3.0.1c"; }
        }

        public string Description
        {
            get { return "Stores your character inventory and allows you to search items across characters."; }
        }

        public string Author
        {
            get { return "Created by Etherian <EtherianDR@gmail.com>\nModified by Thires"; }
        }

        private bool _enabled = true;

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }
    }
}
![image](https://github.com/Thires/InventoryView-1/assets/28072996/d1157e37-da27-4813-8772-85c3bf60c4d1)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/6c7c23c3-3a91-44cc-b735-007ac6f55861)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/d121b19b-460c-4f7d-88fa-370725763b58)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/44e1de3b-4dce-420c-898c-ccba3ae6e33d)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/ee41bca4-0f47-42c0-aabf-b120feaa0c91)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/02487956-5d2b-4aa5-9db2-b58ad6f5d1b7)


# InventoryView
* Added family vault
* Made it so you don't need vault book, will use vault standard
* Can still use vault book
* Will return books/register back to containers they are taken from
* Made search not case sensitive
* Removed a/an/some/several
* Made a second window where all searches will show up
* Added search count
* Added right click options for second window
* Added matches for when using vault book and vault is empty
* Fixed browser when using wiki lookup
* Made change to handle if someone is using ShowLinks, works fine now
* Fixed to handle new updated registry and sorting
* Added tabs for characters
* Fixed a parsing issue for some getting stuck on house
* Added dark mode and multiline tabs
* Added handling for tool catalogs
* Added tooltips when double clicking a found item
* Made it handle both toggle craft modes
* Added text searching for matches and full paths of items
* Added Shadow Servant inventory using percing gaze
* Added check for guild

This plugin will record the items that each of your characters has in their inventory, vault (if you have a vault book), and home (if you have one) and make them searchable in a form.

Installation instructions:
1. Download InventoryView.zip
2. Install InventoryView.dll in either the Genie Plugins folder (%appdata%\Genie Client 3\Plugins) or your Genie.exe directory, whichever you keep config files in.
3. Either restart any open instances of Genie, or type:  #plugin load InventoryView.dll  ..in each open instance of Genie.
4. Go to the Plugins menu and Inventory View.
5. Click the Scan Inventory button to record the inventory of the currently logged in character.

Inventory View form:
The Inventory View form contains a tree list of each character that you've done an inventory scan. Each character splits into the inventory, vault and home as applicable and shows your containers and items in a tree structure that can be expanded or collapsed.

The buttons at the top allow you to search your inventory across all characters, highlights any items that match, and navigate through the results.
You can also select an item and click Wiki Lookup to open Elanthipedia to the entry for that item, if an exact match was found. If no match was found it will do a search for the item.

If you are running multiple instances of Genie you will need to click the Reload File button on each instance of Genie after you have done an inventory scan on all of them. This will ensure that the inventory of each character is up-to-date in each instance of Genie.

When you scan the items of a character it will first check the inventory, then the vaults (if a vault book is found and readable), and the home (if you have one).
At the end it will send "Scan Complete" to the screen. If for some reason the process gets stuck you won't see that text.

/InventoryView command  (/iv command for short)
/InventoryView scan  -- scan the items on the current character.
/InventoryView open  -- open the InventoryView Window to see items.
/InventoryView search keyword -- Will search xml for matches from command line.
/InvenotryView path tap -- Will show the path from command line.

Along with sending "Scan Complete" to the screen, the phrase "InventoryView scan complete" is sent to the parser at the end allowing you to do an inventory scan from a login or other script if desired.

send /InventoryView scan
waitforre ^InventoryView scan complete

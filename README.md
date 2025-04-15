![image](https://github.com/user-attachments/assets/f5b3dc45-4851-4da5-bd9b-db1798b1c809)

![image](https://github.com/user-attachments/assets/694acae7-affc-4d1c-9140-c926c3710295)

![image](https://github.com/user-attachments/assets/9df3230a-4d4c-478a-9f50-3f9e92145259)

![image](https://github.com/user-attachments/assets/6c21cbab-bba1-4a0b-a470-57e9d79ef876)

![image](https://github.com/user-attachments/assets/c3c76aec-95d5-4b2b-9d98-673edd532053)

![image](https://github.com/user-attachments/assets/3c4caaa4-5b26-4cc0-a3fe-19c8eb50e3e1)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/02487956-5d2b-4aa5-9db2-b58ad6f5d1b7)


# InventoryView
Compact version
* added menus for commmands and options
* added pockets for containers that have hidden pockets, they will show in the contianer
* added ability to archive tabs
* added ability to color tabs
* various others changes
*
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
* Added Shadow Servant inventory using Piercing Gaze
* Added check for guild
* Added rummaging of vault and/or family vault, vault, both need to be inside the vault. Family vault is stand alone, just be inside of it and it will just do it. Both are only top level grabs
* Added checkbox for on top

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

![image](https://github.com/Thires/InventoryView-1/assets/28072996/aca75652-99ee-4b13-ab9e-85ecd78c3585)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/c8052f2d-c849-43b2-8753-ce84024c5d84)

![image](https://github.com/Thires/InventoryView-1/assets/28072996/4ceb2f9a-97ae-46d5-bb6d-e2e6aaf8104e)


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

This plugin will record the items that each of your characters has in their inventory, vault (if you have a vault book), and home (if you have one) and make them searchable in a form.

Installation instructions:
1. Download InventoryView.zip
2. Install InventoryView.dll in either the Genie Plugins folder (%appdata%\Genie Client 3\Plugins) or your Genie.exe directory, whichever you keep config files in.
3. Either restart any open instances of Genie, or type:  #plugin load InventoryView.dll  ..in each open instance of Genie.
3a. Until this plugin is approved, you may get a warning saying you are installing an unapproved plugin the first time you load it.
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

Along with sending "Scan Complete" to the screen, the phrase "InventoryView scan complete" is sent to the parser at the end allowing you to do an inventory scan from a login or other script if desired.

send /InventoryView scan
waitforre ^InventoryView scan complete

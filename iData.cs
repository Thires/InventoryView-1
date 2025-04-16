using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace InventoryView
{
    [Serializable]
    public class CharacterData
    {
        public string name;
        public string source;
        public string Circle { get; set; }
        public string Guild { get; set; }
        public List<ItemData> items = new();

        public bool Archived { get; set; }
        public string TabColor { get; set; }
        public string TabTextColor { get; set; }

        public ItemData AddItem(ItemData newItem)
        {
            items.Add(newItem);
            return newItem;
        }

        public ItemData AddItem(string tap, bool storage = false)
        {
            ItemData newItem = new() { tap = tap, storage = storage };
            items.Add(newItem);
            return newItem;
        }
    }

    [Serializable]
    public class ItemData
    {
        public bool storage;
        public string tap;
        public ItemData parent;
        //public List<ItemData> items = new();
        public List<ItemData> items { get; set; } = new List<ItemData>();

        public ItemData AddItem(ItemData newItem)
        {
            newItem.parent = this;
            items.Add(newItem);
            return newItem;
        }

        public ItemData AddItem(string tap, bool storage = false)
        {
            ItemData newItem = new() { tap = tap, storage = storage, parent = this };
            items.Add(newItem);
            return newItem;
        }
    }

    public class MatchedItemInfo
    {
        public string CharacterName { get; set; }
        public string FullPath { get; set; }
    }

    public class TreeViewSearchMatches
    {
        public TreeView TreeView { get; set; }
        public List<TreeNode> SearchMatches { get; set; } = new List<TreeNode>();
        public TreeNode CurrentMatch { get; set; }
    }

    public class ExportData
    {
        public string Character { get; set; }
        public string Tap { get; set; }
        public List<string> Path { get; set; } = new List<string>();
    }

    public class TitledColorDialog : ColorDialog
    {
        public string DialogTitle { get; set; } = "Choose Color";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int WM_SETTEXT = 0x000C;

        protected override IntPtr HookProc(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            if (msg == 0x0110) // WM_INITDIALOG
            {
                SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, DialogTitle);
            }
            return base.HookProc(hWnd, msg, wparam, lparam);
        }
    }

    public class LoadSave
    {
        private static string basePath = Application.StartupPath;

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

        public static void SaveSettings()
        {
            basePath = plugin.Host.get_Variable("PluginPath");
            string configFile = Path.Combine(basePath, "InventoryView.xml");

            try
            {
                // Can't serialize a class with circular references, so I have to remove the parent links first.
                foreach (var cData in plugin.CharacterData)
                {
                    RemoveParents(cData.items);
                }

                // Create a new XmlSerializer for the CharacterData type
                XmlSerializer serializer = new(typeof(List<CharacterData>));

                // Serialize the characterData list to a StringWriter
                StringWriter stringWriter = new();
                serializer.Serialize(stringWriter, plugin.CharacterData);

                // Create a new XmlDocument
                XmlDocument doc = new();

                // If the configFile exists, load the existing XML data into the XmlDocument
                if (File.Exists(configFile))
                    doc.Load(configFile);
                else
                {
                    // Otherwise, create a new root element for the XmlDocument with a custom name
                    XmlElement rootElement = doc.CreateElement("Root");
                    doc.AppendChild(rootElement);
                }

                // Remove any existing ArrayOfCharacterData elements from ArrayOfCharacterData element.
                XmlNode arrayOfCharacterDataNode = doc.DocumentElement.SelectSingleNode("ArrayOfCharacterData");
                if (arrayOfCharacterDataNode != null)
                {
                    XmlNodeList arrayOfCharacterDataNodes = arrayOfCharacterDataNode.SelectNodes("ArrayOfCharacterData");
                    foreach (XmlNode innerArrayOfCharacterDataNode in arrayOfCharacterDataNodes)
                    {
                        innerArrayOfCharacterDataNode.ParentNode.RemoveChild(innerArrayOfCharacterDataNode);
                    }
                }

                // If characterData list is not empty, import serialized character data into XmlDocument.
                if (plugin.CharacterData.Count > 0)
                {
                    // Create a new ArrayOfCharacterData element and append it to the root element if it doesn't exist
                    if (arrayOfCharacterDataNode == null)
                    {
                        arrayOfCharacterDataNode = doc.CreateElement("ArrayOfCharacterData");
                        doc.DocumentElement.AppendChild(arrayOfCharacterDataNode);
                    }

                    // Remove XML declaration from serialized character data.
                    string characterDataXml = stringWriter.ToString();
                    if (characterDataXml.StartsWith("<?xml"))
                    {
                        int endOfDeclaration = characterDataXml.IndexOf("?>") + 2;
                        characterDataXml = characterDataXml[endOfDeclaration..];
                    }

                    // Set InnerXml property of ArrayOfCharacterData element to serialized character data.
                    arrayOfCharacterDataNode.InnerXml += characterDataXml;
                }

                // Get CheckBoxState element.
                XmlNode multilinetabsNode = doc.SelectSingleNode("/Root/MultilineTabs");

                // If CheckBoxState element exists, update its value.
                if (multilinetabsNode != null)
                    multilinetabsNode.InnerText = ((InventoryViewForm)plugin.Form).toolStripMultilineTabs.Checked.ToString();
                else
                {
                    // Otherwise, create new CheckBoxState element and set its value to state of checkBox1 control.
                    XmlElement multilineTabsElemnt = doc.CreateElement("MultilineTabs");
                    multilineTabsElemnt.InnerText = ((InventoryViewForm)plugin.Form).toolStripMultilineTabs.Checked.ToString();

                    // Append CheckBoxState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(multilineTabsElemnt);
                }

                // Get DarkModeState element.
                XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");

                // If DarkModeState element exists, update its value.
                if (darkModeNode != null)
                    darkModeNode.InnerText = ((InventoryViewForm)plugin.Form).toolStripDarkMode.Checked.ToString();
                else
                {
                    // Otherwise, create new DarkModeState element and set its value to state of chkDarkMode control.
                    XmlElement darkModeElement = doc.CreateElement("DarkMode");
                    darkModeElement.InnerText = ((InventoryViewForm)plugin.Form).toolStripDarkMode.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(darkModeElement);
                }

                XmlNode familyNode = doc.SelectSingleNode("/Root/Family");

                // If FamilyModeState element exists, update its value.
                if (familyNode != null)
                    familyNode.InnerText = ((InventoryViewForm)plugin.Form).toolStripFamily.Checked.ToString();
                else
                {
                    XmlElement familyElement = doc.CreateElement("Family");
                    familyElement.InnerText = ((InventoryViewForm)plugin.Form).toolStripFamily.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(familyElement);
                }

                XmlNode alwaysTopNode = doc.SelectSingleNode("/Root/AlwaysTop");

                // If AlwaysTopState element exists, update its value.
                if (alwaysTopNode != null)
                    alwaysTopNode.InnerText = ((InventoryViewForm)plugin.Form).toolStripAlwaysTop.Checked.ToString();
                else
                {
                    // Otherwise, create new DarkModeState element and set its value to state of chkDarkMode control.
                    XmlElement alwaysTopElement = doc.CreateElement("AlwaysTop");
                    alwaysTopElement.InnerText = ((InventoryViewForm)plugin.Form).toolStripAlwaysTop.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(alwaysTopElement);
                }

                XmlNode pocketsNode = doc.SelectSingleNode("/Root/Pockets");

                if (pocketsNode != null)
                    pocketsNode.InnerText = ((InventoryViewForm)plugin.Form).toolStripPockets.Checked.ToString();
                else
                {
                    XmlElement pocketsElement = doc.CreateElement("Pockets");
                    pocketsElement.InnerText = ((InventoryViewForm)plugin.Form).toolStripPockets.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(pocketsElement);
                }

                //XmlNode filterNode = doc.SelectSingleNode("/Root/Filter");

                //if (filterNode != null)
                //    filterNode.InnerText = ((InventoryViewForm)plugin.Form).currentFilter;
                //else
                //{
                //    XmlElement filterElement = doc.CreateElement("Filter");
                //    filterElement.InnerText = ((InventoryViewForm)plugin.Form).currentFilter;

                //    // Append DarkModeState element to root element of XmlDocument.
                //    doc.DocumentElement.AppendChild(filterElement);
                //}

                foreach (var character in plugin.CharacterData)
                {
                    XmlNode characterNode = doc.SelectSingleNode($"/Root/ArrayOfCharacterData/CharacterData[name='{character.name}']");
                    if (characterNode != null)
                    {
                        XmlNode archivedNode = characterNode.SelectSingleNode("Archived");
                        if (archivedNode == null)
                        {
                            archivedNode = doc.CreateElement("Archived");
                            characterNode.AppendChild(archivedNode);
                        }
                        archivedNode.InnerText = character.Archived.ToString();

                        XmlNode colorNode = characterNode.SelectSingleNode("TabColor");
                        if (colorNode == null)
                        {
                            colorNode = doc.CreateElement("TabColor");
                            characterNode.AppendChild(colorNode);
                        }
                        colorNode.InnerText = character.TabColor.ToString();

                        XmlNode colorTextNode = characterNode.SelectSingleNode("TabTextColor");
                        if (colorTextNode == null)
                        {
                            colorTextNode = doc.CreateElement("TabTextColor");
                            characterNode.AppendChild(colorTextNode);
                        }
                        colorTextNode.InnerText = character.TabTextColor.ToString();
                    }

                }

                // Save changes to XML file.
                using (var writer = XmlWriter.Create(configFile, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
                {
                    doc.Save(writer);
                }

                // ..and add them back again afterwards.
                foreach (var cData in plugin.CharacterData)
                {
                    AddParents(cData.items, null);
                }
            }
            catch (IOException ex)
            {
                plugin.Host.EchoText("Error writing to InventoryView file: " + ex.Message);
            }
        }

        public static void LoadSettings()
        {
            basePath = plugin.Host.get_Variable("PluginPath");
            string configFile = Path.Combine(basePath, "InventoryView.xml");
            try
            {
                if (File.Exists(configFile))
                {
                    // Load the XML data into an XmlDocument
                    XmlDocument doc = new();
                    doc.Load(configFile);

                    // Check if the XML file starts with the <Root> element
                    XmlNode rootNode = doc.SelectSingleNode("/Root");
                    if (rootNode == null)
                    {
                        // Create a new root element with a custom name
                        XmlElement newRootNode = doc.CreateElement("Root");

                        // Create a new ArrayOfCharacterData element and append it to the new root element
                        XmlElement arrayOfCharacterDataElement = doc.CreateElement("ArrayOfCharacterData");
                        arrayOfCharacterDataElement.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                        arrayOfCharacterDataElement.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
                        newRootNode.AppendChild(arrayOfCharacterDataElement);

                        // Append the old XML data to the ArrayOfCharacterData element
                        arrayOfCharacterDataElement.InnerXml = doc.DocumentElement.OuterXml;

                        // Create a new CheckBoxState element and set its value to False
                        XmlElement multilineTabsElement = doc.CreateElement("MultilineTabs");
                        multilineTabsElement.InnerText = "False";
                        newRootNode.AppendChild(multilineTabsElement);

                        // Create a new DarkModeState element and set its value to False
                        XmlElement darkModeElement = doc.CreateElement("DarkMode");
                        darkModeElement.InnerText = "False";
                        newRootNode.AppendChild(darkModeElement);

                        XmlElement familyElement = doc.CreateElement("Family");
                        familyElement.InnerText = "False";
                        newRootNode.AppendChild(familyElement);

                        // Create a new AlwaysTop element and set its value to False
                        XmlElement alwaysTopElement = doc.CreateElement("AlwaysTop");
                        alwaysTopElement.InnerText = "False";
                        newRootNode.AppendChild(alwaysTopElement);

                        XmlElement pocketsElement = doc.CreateElement("Pockets");
                        pocketsElement.InnerText = "False";
                        newRootNode.AppendChild(pocketsElement);

                        XmlElement archiveElement = doc.CreateElement("Archived");
                        archiveElement.InnerText = "False";
                        newRootNode.AppendChild(archiveElement);

                        //XmlElement filterElement = doc.CreateElement("Filter");
                        //filterElement.InnerText = ("Filter Tabs");
                        //newRootNode.AppendChild(filterElement);

                        // Replace the old root node with the new root node
                        doc.ReplaceChild(newRootNode, doc.DocumentElement);

                        // Save changes to XML file
                        doc.Save(configFile);
                    }
                    else
                    {
                    }
                    // Select all CharacterData elements from the XmlDocument
                    XmlNodeList characterDataNodes = doc.SelectNodes("/Root/ArrayOfCharacterData/ArrayOfCharacterData/CharacterData");

                    // Clear the existing characterData list
                    plugin.CharacterData.Clear();

                    // Iterate over each CharacterData element and deserialize it into a CharacterData object
                    foreach (XmlNode characterDataNode in characterDataNodes)
                    {
                        // Create a new XmlSerializer for the CharacterData type
                        XmlSerializer serializer = new(typeof(CharacterData));

                        // Deserialize the CharacterData element into a CharacterData object
                        using StringReader reader = new(characterDataNode.OuterXml);
                        CharacterData cData = (CharacterData)serializer.Deserialize(reader);

                        XmlNode archivedNode = characterDataNode.SelectSingleNode("Archived");
                        if (archivedNode != null)
                        {
                            cData.Archived = bool.Parse(archivedNode.InnerText);
                        }

                        XmlNode colorNode = characterDataNode.SelectSingleNode("TabColor");
                        if (colorNode != null)
                        {
                            cData.TabColor = colorNode.InnerText;
                        }

                        XmlNode colorTextNode = characterDataNode.SelectSingleNode("TabTextColor");
                        if (colorTextNode != null)
                        {
                            cData.TabTextColor = colorTextNode.InnerText;
                        }

                        // Add the deserialized CharacterData object to the characterData list
                        plugin.CharacterData.Add(cData);
                    }
                    // ..and add them back again afterwards.
                    foreach (var cData in plugin.CharacterData)
                    {
                        AddParents(cData.items, null);
                    }

                    // Load CheckBox state.
                    XmlNode multilineTabsNode = doc.SelectSingleNode("/Root/MultilineTabs");
                    if (multilineTabsNode != null)
                        ((InventoryViewForm)plugin.Form).toolStripMultilineTabs.Checked = bool.Parse(multilineTabsNode.InnerText);

                    // Load DarkMode state
                    XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");
                    if (darkModeNode != null)
                        ((InventoryViewForm)plugin.Form).toolStripDarkMode.Checked = bool.Parse(darkModeNode.InnerText);

                    // Load Family Vault state
                    XmlNode familyNode = doc.SelectSingleNode("/Root/Family");
                    if (familyNode != null)
                        ((InventoryViewForm)plugin.Form).toolStripFamily.Checked = bool.Parse(familyNode.InnerText);

                    // Load Always On Top state
                    XmlNode alwaystopNode = doc.SelectSingleNode("/Root/AlwaysTop");
                    if (alwaystopNode != null)
                        ((InventoryViewForm)plugin.Form).toolStripAlwaysTop.Checked = bool.Parse(alwaystopNode.InnerText);

                    XmlNode pocketsNode = doc.SelectSingleNode("/Root/Pockets");
                    if (pocketsNode != null)
                        ((InventoryViewForm)plugin.Form).toolStripPockets.Checked = bool.Parse(pocketsNode.InnerText);

                    //XmlNode filterNode = doc.SelectSingleNode("/Root/Filter");
                    //if (filterNode != null)
                    //    ((InventoryViewForm)plugin.Form).currentFilter = filterNode.InnerText;


                }
                else
                    plugin.Host.EchoText("File does not exist");
            }
            catch (IOException ex)
            {
                plugin.Host.EchoText("Error reading from InventoryView file: " + ex.Message);
            }
        }
    }
}

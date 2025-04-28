using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Windows.Forms;
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
        public List<ItemData> items = new();
        //public List<ItemData> items { get; set; } = new List<ItemData>();

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
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
            basePath = Plugin.Host.get_Variable("PluginPath");
            string configFile = Path.Combine(basePath, "InventoryView.xml");

            try
            {
                // Can't serialize a class with circular references, so I have to remove the parent links first.
                foreach (var cData in Plugin.CharacterData)
                {
                    RemoveParents(cData.items);
                }

                // Create a new XmlSerializer for the CharacterData type
                XmlSerializer serializer = new(typeof(List<CharacterData>));

                // Serialize the characterData list to a StringWriter
                StringWriter stringWriter = new();
                serializer.Serialize(stringWriter, Plugin.CharacterData);

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
                if (Plugin.CharacterData.Count > 0)
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

                SaveConfigOptions(doc);

                foreach (var character in Plugin.CharacterData)
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
                foreach (var cData in Plugin.CharacterData)
                {
                    AddParents(cData.items, null);
                }
            }
            catch (IOException ex)
            {
                Plugin.Host.EchoText("Error writing to InventoryView file: " + ex.Message);
            }
        }

        private static void SaveConfigOptions(XmlDocument doc)
        {
            // MultilineTabs
            XmlNode multilineTabsNode = doc.SelectSingleNode("/Root/MultilineTabs");
            if (multilineTabsNode != null)
                multilineTabsNode.InnerText = ((InventoryViewForm)Plugin.Form).toolStripMultilineTabs.Checked.ToString();
            else
            {
                XmlElement multilineTabsElement = doc.CreateElement("MultilineTabs");
                multilineTabsElement.InnerText = ((InventoryViewForm)Plugin.Form).toolStripMultilineTabs.Checked.ToString();
                doc.DocumentElement.AppendChild(multilineTabsElement);
            }

            // DarkMode
            XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");
            if (darkModeNode != null)
                darkModeNode.InnerText = ((InventoryViewForm)Plugin.Form).toolStripDarkMode.Checked.ToString();
            else
            {
                XmlElement darkModeElement = doc.CreateElement("DarkMode");
                darkModeElement.InnerText = ((InventoryViewForm)Plugin.Form).toolStripDarkMode.Checked.ToString();
                doc.DocumentElement.AppendChild(darkModeElement);
            }

            // Family
            XmlNode familyNode = doc.SelectSingleNode("/Root/Family");
            if (familyNode != null)
                familyNode.InnerText = ((InventoryViewForm)Plugin.Form).toolStripFamily.Checked.ToString();
            else
            {
                XmlElement familyElement = doc.CreateElement("Family");
                familyElement.InnerText = ((InventoryViewForm)Plugin.Form).toolStripFamily.Checked.ToString();
                doc.DocumentElement.AppendChild(familyElement);
            }

            // AlwaysTop
            XmlNode alwaysTopNode = doc.SelectSingleNode("/Root/AlwaysTop");
            if (alwaysTopNode != null)
                alwaysTopNode.InnerText = ((InventoryViewForm)Plugin.Form).toolStripAlwaysTop.Checked.ToString();
            else
            {
                XmlElement alwaysTopElement = doc.CreateElement("AlwaysTop");
                alwaysTopElement.InnerText = ((InventoryViewForm)Plugin.Form).toolStripAlwaysTop.Checked.ToString();
                doc.DocumentElement.AppendChild(alwaysTopElement);
            }

            // Pockets
            XmlNode pocketsNode = doc.SelectSingleNode("/Root/Pockets");
            if (pocketsNode != null)
                pocketsNode.InnerText = ((InventoryViewForm)Plugin.Form).toolStripPockets.Checked.ToString();
            else
            {
                XmlElement pocketsElement = doc.CreateElement("Pockets");
                pocketsElement.InnerText = ((InventoryViewForm)Plugin.Form).toolStripPockets.Checked.ToString();
                doc.DocumentElement.AppendChild(pocketsElement);
            }

            // Precise matching logic
            XmlNode preciseNode = doc.SelectSingleNode("/Root/Precise");
            if (preciseNode != null)
                preciseNode.InnerText = ((InventoryViewForm)Plugin.Form).cbPrecise.Checked.ToString();
            else
            {
                XmlElement preciseElement = doc.CreateElement("Precise");
                preciseElement.InnerText = ((InventoryViewForm)Plugin.Form).cbPrecise.Checked.ToString();
                doc.DocumentElement.AppendChild(preciseElement);
            }
        }

        public static void LoadSettings()
        {
            basePath = Plugin.Host.get_Variable("PluginPath");
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

                        XmlElement preciseElement = doc.CreateElement("Precise");
                        preciseElement.InnerText = "False";
                        newRootNode.AppendChild(preciseElement);

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
                    Plugin.CharacterData.Clear();

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
                        Plugin.CharacterData.Add(cData);
                    }
                    // ..and add them back again afterwards.
                    foreach (var cData in Plugin.CharacterData)
                    {
                        AddParents(cData.items, null);
                    }

                    // Load CheckBox state.
                    XmlNode multilineTabsNode = doc.SelectSingleNode("/Root/MultilineTabs");
                    if (multilineTabsNode != null)
                        ((InventoryViewForm)Plugin.Form).toolStripMultilineTabs.Checked = bool.Parse(multilineTabsNode.InnerText);

                    // Load DarkMode state
                    XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");
                    if (darkModeNode != null)
                        ((InventoryViewForm)Plugin.Form).toolStripDarkMode.Checked = bool.Parse(darkModeNode.InnerText);

                    // Load Family Vault state
                    XmlNode familyNode = doc.SelectSingleNode("/Root/Family");
                    if (familyNode != null)
                        ((InventoryViewForm)Plugin.Form).toolStripFamily.Checked = bool.Parse(familyNode.InnerText);

                    // Load Always On Top state
                    XmlNode alwaystopNode = doc.SelectSingleNode("/Root/AlwaysTop");
                    if (alwaystopNode != null)
                        ((InventoryViewForm)Plugin.Form).toolStripAlwaysTop.Checked = bool.Parse(alwaystopNode.InnerText);

                    XmlNode pocketsNode = doc.SelectSingleNode("/Root/Pockets");
                    if (pocketsNode != null)
                        ((InventoryViewForm)Plugin.Form).toolStripPockets.Checked = bool.Parse(pocketsNode.InnerText);

                    XmlNode preciseNode = doc.SelectSingleNode("/Root/Precise");
                    if (preciseNode != null)
                        ((InventoryViewForm)Plugin.Form).cbPrecise.Checked = bool.Parse(preciseNode.InnerText);
                }
                else
                    Plugin.Host.EchoText("File does not exist");
            }
            catch (IOException ex)
            {
                Plugin.Host.EchoText("Error reading from InventoryView file: " + ex.Message);
            }
        }
    }
}

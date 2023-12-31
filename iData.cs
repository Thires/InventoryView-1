using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Windows.Forms;

namespace InventoryView
{
    [Serializable]
    public class CharacterData
    {
        public string name;
        public string source;
        public string Circle { get; set; }
        public string Guild { get; set; }
        public List<ItemData> items = new List<ItemData>();

        public ItemData AddItem(ItemData newItem)
        {
            items.Add(newItem);
            return newItem;
        }

        public ItemData AddItem(string tap, bool storage = false)
        {
            ItemData newItem = new ItemData() { tap = tap, storage = storage };
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
        public List<ItemData> items = new List<ItemData>();

        public ItemData AddItem(ItemData newItem)
        {
            newItem.parent = this;
            items.Add(newItem);
            return newItem;
        }

        public ItemData AddItem(string tap, bool storage = false)
        {
            ItemData newItem = new ItemData() { tap = tap, storage = storage, parent = this };
            items.Add(newItem);
            return newItem;
        }
    }

    public class MatchedItemInfo
    {
        public string CharacterName { get; set; }
        public string FullPath { get; set; }
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
            basePath = Class1._host.get_Variable("PluginPath");
            string configFile = Path.Combine(basePath, "InventoryView.xml");
            try
            {
                // Can't serialize a class with circular references, so I have to remove the parent links first.
                foreach (var cData in Class1.characterData)
                {
                    RemoveParents(cData.items);
                }

                // Create a new XmlSerializer for the CharacterData type
                XmlSerializer serializer = new XmlSerializer(typeof(List<CharacterData>));

                // Serialize the characterData list to a StringWriter
                StringWriter stringWriter = new StringWriter();
                serializer.Serialize(stringWriter, Class1.characterData);

                // Create a new XmlDocument
                XmlDocument doc = new XmlDocument();

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
                if (Class1.characterData.Count > 0)
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
                        characterDataXml = characterDataXml.Substring(endOfDeclaration);
                    }

                    // Set InnerXml property of ArrayOfCharacterData element to serialized character data.
                    arrayOfCharacterDataNode.InnerXml += characterDataXml;
                }

                // Get CheckBoxState element.
                XmlNode multilinetabsNode = doc.SelectSingleNode("/Root/MultilineTabs");

                // If CheckBoxState element exists, update its value.
                if (multilinetabsNode != null)
                    multilinetabsNode.InnerText = ((InventoryViewForm)Class1._form).chkMultilineTabs.Checked.ToString();
                else
                {
                    // Otherwise, create new CheckBoxState element and set its value to state of checkBox1 control.
                    XmlElement multilineTabsElemnt = doc.CreateElement("MultilineTabs");
                    multilineTabsElemnt.InnerText = ((InventoryViewForm)Class1._form).chkMultilineTabs.Checked.ToString();

                    // Append CheckBoxState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(multilineTabsElemnt);
                }

                // Get DarkModeState element.
                XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");

                // If DarkModeState element exists, update its value.
                if (darkModeNode != null)
                    darkModeNode.InnerText = ((InventoryViewForm)Class1._form).chkDarkMode.Checked.ToString();
                else
                {
                    // Otherwise, create new DarkModeState element and set its value to state of chkDarkMode control.
                    XmlElement darkModeElement = doc.CreateElement("DarkMode");
                    darkModeElement.InnerText = ((InventoryViewForm)Class1._form).chkDarkMode.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(darkModeElement);
                }

                XmlNode familyNode = doc.SelectSingleNode("/Root/Family");

                // If DarkModeState element exists, update its value.
                if (familyNode != null)
                    familyNode.InnerText = ((InventoryViewForm)Class1._form).chkFamily.Checked.ToString();
                else
                {
                    // Otherwise, create new DarkModeState element and set its value to state of chkDarkMode control.
                    XmlElement familyElement = doc.CreateElement("Family");
                    familyElement.InnerText = ((InventoryViewForm)Class1._form).chkFamily.Checked.ToString();

                    // Append DarkModeState element to root element of XmlDocument.
                    doc.DocumentElement.AppendChild(familyElement);
                }


                // Save changes to XML file.
                using (var writer = XmlWriter.Create(configFile, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
                {
                    doc.Save(writer);
                }

                // ..and add them back again afterwards.
                foreach (var cData in Class1.characterData)
                {
                    AddParents(cData.items, null);
                }
            }
            catch (IOException ex)
            {
                Class1._host.EchoText("Error writing to InventoryView file: " + ex.Message);
            }
        }
        public static void LoadSettings()
        {
            basePath = Class1._host.get_Variable("PluginPath");
            string configFile = Path.Combine(basePath, "InventoryView.xml");
            try
            {
                if (File.Exists(configFile))
                {
                    // Load the XML data into an XmlDocument
                    XmlDocument doc = new XmlDocument();
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

                        // Create a new DarkModeState element and set its value to False
                        XmlElement familyElement = doc.CreateElement("Family");
                        familyElement.InnerText = "False";
                        newRootNode.AppendChild(familyElement);

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
                    Class1.characterData.Clear();

                    // Iterate over each CharacterData element and deserialize it into a CharacterData object
                    foreach (XmlNode characterDataNode in characterDataNodes)
                    {
                        // Create a new XmlSerializer for the CharacterData type
                        XmlSerializer serializer = new XmlSerializer(typeof(CharacterData));

                        // Deserialize the CharacterData element into a CharacterData object
                        using (StringReader reader = new StringReader(characterDataNode.OuterXml))
                        {
                            CharacterData cData = (CharacterData)serializer.Deserialize(reader);

                            // Add the deserialized CharacterData object to the characterData list
                            Class1.characterData.Add(cData);
                        }
                    }
                    // ..and add them back again afterwards.
                    foreach (var cData in Class1.characterData)
                    {
                        AddParents(cData.items, null);
                    }

                    // Load CheckBox state.
                    XmlNode multilineTabsNode = doc.SelectSingleNode("/Root/MultilineTabs");
                    if (multilineTabsNode != null)
                        ((InventoryViewForm)Class1._form).chkMultilineTabs.Checked = bool.Parse(multilineTabsNode.InnerText);

                    // Load DarkMode state.
                    XmlNode darkModeNode = doc.SelectSingleNode("/Root/DarkMode");
                    if (darkModeNode != null)
                        ((InventoryViewForm)Class1._form).chkDarkMode.Checked = bool.Parse(darkModeNode.InnerText);

                    // Load Family Vault state.
                    XmlNode familyNode = doc.SelectSingleNode("/Root/Family");
                    if (familyNode != null)
                        ((InventoryViewForm)Class1._form).chkFamily.Checked = bool.Parse(familyNode.InnerText);
                }
                else
                    Class1._host.EchoText("File does not exist");
            }
            catch (IOException ex)
            {
                Class1._host.EchoText("Error reading from InventoryView file: " + ex.Message);
            }
        }


    }
}

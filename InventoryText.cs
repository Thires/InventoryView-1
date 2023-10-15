using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;

namespace InventoryView
{
    internal class InventoryText
    {
        private static string basePath = Application.StartupPath;
        
        public void PerformSearch(string searchText, string style)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                Class1._host.EchoText("Provide a search word.");
                return;
            }

            var matchingItems = SearchXmlData(searchText, style);

            if (matchingItems.Count > 0)
            {
                if (style == "path") Class1._host.EchoText($"\nFull path for '{searchText}':\n");
                else Class1._host.EchoText($"\nFound {matchingItems.Count} items matching '{searchText}':\n");

                foreach (var item in matchingItems)
                {
                    //Class1._host.EchoText(item);
                    if (style == "path") Class1._host.EchoText(item);
                    else Class1._host.SendText("#link {" + item + "} {#put /iv path " + Regex.Replace(item, @"\w+ - ", "") + "}");
                }
            }
            else
            {
                Class1._host.EchoText($"No matches found for '{searchText}'.");
            }
        }

        public List<string> SearchXmlData(string searchText, string style)
        {
            basePath = Class1._host.get_Variable("PluginPath");
            var matchingItems = new List<string>();
            string xmlPath = Path.Combine(basePath, "InventoryView.xml");

            if (!File.Exists(xmlPath))
            {
                Class1._host.EchoText("InventoryView.xml not found.");
                return matchingItems;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);

            Regex regex = new Regex(searchText, RegexOptions.IgnoreCase);

            // Navigate to the CharacterData elements
            XmlNodeList characterDataElements = doc.GetElementsByTagName("CharacterData");

            foreach (XmlNode characterDataNode in characterDataElements)
            {
                string characterName = characterDataNode.SelectSingleNode("name")?.InnerText;
                if (characterName == null)
                    continue;

                // Get the items section for this character
                XmlNode itemsNodes = characterDataNode.SelectSingleNode("items");

                if (itemsNodes == null)
                    continue;

                XmlNodeList itemNodes = itemsNodes.SelectNodes(".//ItemData");

                foreach (XmlNode itemNode in itemNodes)
                {
                    string itemText = itemNode.SelectSingleNode("tap")?.InnerText;
                    if (itemText == null)
                        continue;

                    // Search for the searchText using a case-insensitive regex pattern
                    if (regex.IsMatch(itemText))
                    {
                        string fullPath = GetItemPath(itemNode);
                        string single = Regex.Replace(itemText.TrimEnd('.'), @"\(\d+\)\s|\s\(closed\)", "");

                        if (style == "path") matchingItems.Add($"{characterName}\n{fullPath}\n");
                        else matchingItems.Add($"{characterName} - {single}"); // Single tap line
                    }
                }
            }
            return matchingItems;
        }

        private string GetItemPath(XmlNode itemNode)
        {
            var path = new List<string>();
            var current = itemNode;

            while (current != null)
            {
                string source = current.SelectSingleNode("source")?.InnerText;
                string tap = current.SelectSingleNode("tap")?.InnerText;

                if (!string.IsNullOrEmpty(source))
                {
                    path.Insert(0, source);
                }

                if (!string.IsNullOrEmpty(tap))
                {
                    path.Insert(0, Regex.Replace(tap.TrimEnd('.'), @"\(\d+\)\s", ""));
                }

                current = current.ParentNode;
            }
            return string.Join(Environment.NewLine, path.Select((part, index) => new string('-', index + 1) + " " + part));
        }
    }
}

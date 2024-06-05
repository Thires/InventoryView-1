using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;

namespace InventoryView
{
    internal class InventoryTextSearch
    {
        private static string basePath = Application.StartupPath;

        public static void PerformSearch(string searchText, string style)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                Class1.Host.EchoText("Provide a tap to use.");
                return;
            }

            var matchingItems = SearchXmlData(searchText, style);

            if (matchingItems.Count > 0)
            {
                if (style == "path") Class1.Host.EchoText($"\nFull path for '{searchText}':\n");
                else Class1.Host.EchoText($"\nFound {matchingItems.Count} items matching '{searchText}':\n");

                foreach (var item in matchingItems)
                {
                    if (style == "path") Class1.Host.EchoText(item);
                    else Class1.Host.SendText("#link {" + item + "} {#put /iv path " + Regex.Replace(item, @"\w+ - ", "") + "}");
                }
            }
            else
            {
                Class1.Host.EchoText($"No matches found for '{searchText}'.");
            }
        }

        public static List<string> SearchXmlData(string searchText, string style)
        {
            basePath = Class1.Host.get_Variable("PluginPath");
            var matchingItems = new List<string>();
            string xmlPath = Path.Combine(basePath, "InventoryView.xml");

            if (!File.Exists(xmlPath))
            {
                Class1.Host.EchoText("InventoryView.xml not found.");
                return matchingItems;
            }

            XmlDocument doc = new();
            doc.Load(xmlPath);

            Regex regex = new(searchText, RegexOptions.IgnoreCase);

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
                        string single = Regex.Replace(itemText.TrimEnd('.'), @"\(\d+\)\s|\s\(closed\)", "");
                        string fullPath = GetItemPath(itemNode);

                        if (style == "path")
                        {
                            if (single.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingItems.Add($"{characterName}\n{fullPath}\n");
                            }
                        }
                        else
                        {
                            matchingItems.Add($"{characterName} - {single}"); // Single tap line
                        }
                    }
                }
            }
            return matchingItems;
        }

        private static string GetItemPath(XmlNode itemNode)
        {
            var path = new List<string>();
            var current = itemNode;

            while (current != null)
            {
                string source = current.SelectSingleNode("source")?.InnerText;
                string tap = current.SelectSingleNode("tap")?.InnerText;
                string tapText = !string.IsNullOrEmpty(tap) ? Regex.Replace(tap.TrimEnd('.'), @"\(\d+\)\s", "") : string.Empty;

                if (!string.IsNullOrEmpty(source))
                {
                    path.Insert(0, source);
                }

                if (!string.IsNullOrEmpty(tap))
                {
                    path.Insert(0, tapText);
                }

                current = current.ParentNode;

                // Check if it's the final <tap> node within <items> and it's a single-word item
                if (current != null && current.Name == "items" && !current.PreviousSibling.HasChildNodes && !tapText.Contains(' '))
                {
                    break;
                }
            }

            return string.Join(Environment.NewLine, path.Select((part, index) => new string('-', index + 1) + " " + part));
        }

    }
}

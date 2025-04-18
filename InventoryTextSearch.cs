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
                Plugin.Host.EchoText("Provide a tap to use.");
                return;
            }

            // Split the search text into individual words
            var searchWords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var matchingItems = SearchXmlData(searchWords, style);

            if (matchingItems.Count > 0)
            {
                if (style == "path") Plugin.Host.EchoText($"\nFull path for '{searchText}':\n");
                else Plugin.Host.EchoText($"\nFound {matchingItems.Count} items matching '{searchText}':\n");

                foreach (var item in matchingItems)
                {
                    if (style == "path") Plugin.Host.EchoText(item);
                    else Plugin.Host.SendText("#link {" + item + "} {#put /iv path " + Regex.Replace(item, @"\w+ - ", "") + "}");
                }
            }
            else
            {
                Plugin.Host.EchoText($"No matches found for '{searchText}'.");
            }
        }

        public static List<string> SearchXmlData(string[] searchWords, string style)
        {
            basePath = Plugin.Host.get_Variable("PluginPath");
            var matchingItems = new List<string>();
            string xmlPath = Path.Combine(basePath, "InventoryView.xml");

            if (!File.Exists(xmlPath))
            {
                Plugin.Host.EchoText("InventoryView.xml not found.");
                return matchingItems;
            }

            XmlDocument doc = new();
            doc.Load(xmlPath);

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

                    // Check if all search words are present in the item text
                    bool allWordsMatch = searchWords.All(word => itemText.Contains(word, StringComparison.OrdinalIgnoreCase));

                    if (allWordsMatch)
                    {
                        string single = Regex.Replace(itemText.TrimEnd('.'), @"\(\d+\)\s|\s\(closed\)", "");
                        string fullPath = GetItemPath(itemNode);

                        if (style == "path")
                        {
                            matchingItems.Add($"{characterName}\n{fullPath}\n");
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

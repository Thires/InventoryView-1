﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace InventoryView
{
    public class InventoryViewForm : Form
    {
        private readonly List<TreeNode> searchMatches = new();
        private IContainer components;
        private TreeView tv;
        private ContextMenuStrip listBox_Menu;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem wikiToolStripMenuItem;
        private ToolStripMenuItem copyAllToolStripMenuItem;
        private bool clickSearch = false;
        private ToolStripMenuItem copySelectedToolStripMenuItem;
        // Create a new list to store the search matches for each TreeView control
        private readonly List<InventoryViewForm.TreeViewSearchMatches> treeViewSearchMatchesList = new();
        // Create a list to store the hidden tab pages and their original positions
        private readonly List<(TabPage tabPage, int index)> hiddenTabPages = new();
        private TabControl tabControl1;
        private ListBox lblMatches;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel panel1;
        private Button btnFindPrev;
        private TextBox txtSearch;
        private Label lblFound;
        private Label lblSearch;
        private Button btnSearch;
        private Button btnExport;
        private Button btnExpand;
        private Button btnReload;
        private Button btnCollapse;
        private Button btnScan;
        private Button btnWiki;
        private Button btnFindNext;
        private Button btnReset;
        private SplitContainer splitContainer1;
        private ComboBox cboCharacters;
        private Button btnRemoveCharacter;

        private Label infolabel;
        internal CheckBox chkMultilineTabs;
        internal CheckBox chkDarkMode;
        internal CheckBox chkFamily;
        private static string basePath = Application.StartupPath;
        internal CheckBox chkAlwaysTop;
        private Button button1;
        private readonly Dictionary<string, List<MatchedItemInfo>> matchedItemsDictionary = new();

        public InventoryViewForm()
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void InventoryViewForm_Load(object sender, EventArgs e)
        {
            BindData();
            basePath = Class1.Host.get_Variable("PluginPath");

            // Load the character data
            LoadSave.LoadSettings();

            // Get a list of distinct character names from the characterData list
            List<string> characterNames = Class1.CharacterData.Select(c => c.name).Distinct().ToList();

            // Sort the character names
            characterNames.Sort();

            // Add the character names to the cboCharacters control
            cboCharacters.Items.Clear();
            cboCharacters.Items.AddRange(characterNames.ToArray());

            lblMatches.MouseDoubleClick += LblMatches_MouseDoubleClick;
            InitializeTooltipTimer();

            chkAlwaysTop.CheckedChanged += ChkAlwaysTop_CheckedChanged;

            if (tabControl1.SelectedTab?.Controls.Count > 0 && tabControl1.SelectedTab.Controls[0] is TreeView tv)
            {
                // Expand root nodes for the selected tab
                foreach (TreeNode rootNode in tv.Nodes)
                {
                    rootNode.Expand();
                }
            }
        }
       
        private void BindData()
        {
            // Clear existing data
            tabControl1.TabPages.Clear();

            // Get a distinct character list
            var characters = GetDistinctCharacters();

            // Create a new tab page for each character
            foreach (var character in characters)
            {
                // Create a new tab page
                var tabPage = new TabPage(character);
                tabControl1.TabPages.Add(tabPage);

                // Create a new TreeView control
                var tv = new TreeView
                {
                    Dock = DockStyle.Fill
                };

                if (chkDarkMode.Checked)
                {
                    tv.ForeColor = Color.White;
                    tv.BackColor = Color.Black;
                }
                else
                {
                    tv.ForeColor = Color.Black;
                    tv.BackColor = Color.White;
                }

                tabPage.Controls.Add(tv);

                // Clear existing nodes
                tv.Nodes.Clear();

                // Create a new ContextMenuStrip for the TreeView control
                var contextMenuStrip = CreateTreeViewContextMenuStrip(tv);
                tv.ContextMenuStrip = contextMenuStrip;

                // Add the character's data to the TreeView and get the total item count
                int totalCount = AddCharacterDataToTreeView(tv, character);

                // Update the tab page text with the total item count
                tabPage.Text = $"{character} (T: {totalCount})";

                foreach (TreeNode rootNode in tv.Nodes)
                {
                    rootNode.Expand();
                }
            }
        }

        private static List<string> GetDistinctCharacters()
        {
            var characters = Class1.CharacterData.Select(tbl => tbl.name).Distinct().ToList();
            characters.Sort();
            return characters;
        }

        private static int AddCharacterDataToTreeView(TreeView tv, string character)
        {
            int totalCount = 0;

            TreeNode charNode = tv.Nodes.Add(character);
            foreach (var source in Class1.CharacterData.Where(tbl => tbl.name == character))
            {
                TreeNode sourceNode = charNode.Nodes.Add(source.source);
                sourceNode.ToolTipText = sourceNode.FullPath;

                totalCount += PopulateTree(sourceNode, source.items);
            }

            return totalCount;
        }

        private static int PopulateTree(TreeNode treeNode, List<ItemData> itemList)
        {
            int totalCount = 0;

            foreach (ItemData itemData in itemList)
            {
                TreeNode treeNode1 = treeNode.Nodes.Add(itemData.tap);
                treeNode1.ToolTipText = treeNode1.FullPath;

                if (!itemData.tap.EndsWith("."))
                {
                    totalCount++;
                }

                if (itemData.items.Count > 0)
                    totalCount += PopulateTree(treeNode1, itemData.items);
            }

            return totalCount;
        }

        private static ContextMenuStrip CreateTreeViewContextMenuStrip(TreeView tv)
        {
            var contextMenuStrip = new ContextMenuStrip();
            var wikiLookupToolStripMenuItem = new ToolStripMenuItem("Wiki Lookup");
            wikiLookupToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode == null)
                {
                    int num = (int)MessageBox.Show("Select an item to lookup.");
                }
                else
                    OpenWikiPage(tv.SelectedNode.Text);
            };
            contextMenuStrip.Items.Add(wikiLookupToolStripMenuItem);

            // Add the "Copy Text" option
            var copyTextToolStripMenuItem = new ToolStripMenuItem("Copy Text");
            copyTextToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode != null)
                    Clipboard.SetText(Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s|^(an?|some|several)\s", ""));
            };
            contextMenuStrip.Items.Add(copyTextToolStripMenuItem);

            // Add the "Copy Branch" option
            var copyBranchToolStripMenuItem = new ToolStripMenuItem("Copy Branch");
            copyBranchToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode != null)
                {
                    List<string> branchText = new()
                    {
                        Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s|^(an?|some|several)\s", "")
                    };
                    CopyBranchText(tv.SelectedNode.Nodes, branchText, 1);
                    Clipboard.SetText(string.Join("\r\n", branchText.ToArray()));
                }
            };
            contextMenuStrip.Items.Add(copyBranchToolStripMenuItem);

            return contextMenuStrip;
        }

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            if (customTooltip != null && !customTooltip.IsDisposed)
            {
                customTooltip.Close();
                tooltipTimer.Stop(); // Stop the timer
            }
            // Reset the found count value
            lblFound.Text = "Found: 0";
            ClearMatchedItemPaths();
            // Clear existing search matches
            searchMatches.Clear();
            treeViewSearchMatchesList.Clear();
            lblMatches.Items.Clear();

            if (!string.IsNullOrEmpty(txtSearch.Text))
            {
                // Clear hiddenTabPages
                hiddenTabPages.Clear();

                // Call BindData to make sure all tabs are visible
                BindData();

                // Save the currently selected tab page
                var selectedTab = tabControl1.SelectedTab;

                // Iterate over each visible and hidden tab page
                var allTabPages = tabControl1.TabPages.Cast<TabPage>().Concat(hiddenTabPages.Select(x => x.tabPage)).ToList();

                // Clear the treeViewSearchMatchesList
                treeViewSearchMatchesList.Clear();

                foreach (var tabPage in allTabPages)
                {
                    // Reset the search count for the tab page
                    tabPage.Text = tabPage.Text.Split(' ')[0];

                    // Select the tab page if it's visible
                    if (tabControl1.TabPages.Contains(tabPage))
                        tabControl1.SelectedTab = tabPage;

                    // Get the TreeView control on the tab page
                    var tv = tabPage.Controls[0] as TreeView;

                    // Create a new TreeViewSearchMatches object for the TreeView control
                    var treeViewSearchMatches = new TreeViewSearchMatches() { TreeView = tv };
                    treeViewSearchMatchesList.Add(treeViewSearchMatches);

                    // Reset the search count and total count
                    int searchCount = 0;
                    int totalCount = 0;

                    // Search the TreeView
                    tv.CollapseAll();
                    SearchTree(tv, tv.Nodes, treeViewSearchMatches.SearchMatches, ref searchCount, ref totalCount);

                    // Update the tab page text with the search count if greater than zero
                    if (searchCount > 0)
                        tabPage.Text = new StringBuilder(tabPage.Text.Split(' ')[0]).Append($" (M: {searchCount})").ToString();
                    else
                    {
                        tabPage.Text = new StringBuilder(tabPage.Text.Split(' ')[0]).Append($" (T: {totalCount})").ToString();

                        // Hide the tab page if no matching items were found and it's not already hidden
                        if (tabControl1.TabPages.Contains(tabPage) && !hiddenTabPages.Any(x => x.tabPage == tabPage))
                        {
                            hiddenTabPages.Add((tabPage, tabControl1.TabPages.IndexOf(tabPage)));
                            tabControl1.TabPages.Remove(tabPage);
                        }
                    }
                }

                // Restore the originally selected tab page
                if (tabControl1.TabPages.Contains(selectedTab))
                    tabControl1.SelectedTab = selectedTab;
                else if (tabControl1.TabPages.Count > 0)
                    tabControl1.SelectedIndex = 0;

                btnFindNext.Visible = btnFindPrev.Visible = btnReset.Visible = treeViewSearchMatchesList.Any(x => x.SearchMatches.Count > 0);
                lblFound.Text = "Found: " + treeViewSearchMatchesList.Sum(x => x.SearchMatches.Count).ToString();

                if (!treeViewSearchMatchesList.Any(x => x.SearchMatches.Count > 0))
                {
                    BindData();
                    // Set focus back to the txtSearch control
                    txtSearch.Focus();
                    return;
                }
            }
            // Set clickSearch to true to indicate that a search has been performed
            clickSearch = true;

            // Set focus back to the txtSearch control
            txtSearch.Focus();
        }

        private void ClearMatchedItemPaths()
        {
            foreach (var matchedItem in matchedItemsDictionary.Values)
            {
                matchedItem.Clear();
            }
        }

        private bool SearchTree(TreeView treeView, TreeNodeCollection nodes, List<TreeNode> searchMatches, ref int searchCount, ref int totalCount)
        {
            bool isMatchFound = false;

            foreach (TreeNode node in nodes)
            {
                totalCount++;

                // Reset the node's background color and foreground color based on the dark mode
                node.BackColor = chkDarkMode.Checked ? Color.Black : Color.White;
                node.ForeColor = chkDarkMode.Checked ? Color.White : Color.Black;

                // Search the node's child nodes and update the match status
                if (SearchTree(treeView, node.Nodes, searchMatches, ref searchCount, ref totalCount))
                {
                    // Expand the node if a match was found in its child nodes
                    node.Expand();
                    isMatchFound = true;
                }
                else
                {
                    // Collapse the node if no match was found in its child nodes
                    node.Collapse();
                }

                // Check if the node's text contains the search text
                if (node.Text.Contains(txtSearch.Text, StringComparison.OrdinalIgnoreCase))
                {
                    // Highlight the node and add it to the list of search matches
                    node.BackColor = chkDarkMode.Checked ? Color.LightBlue : Color.Yellow;
                    node.ForeColor = Color.Black;
                    searchMatches.Add(node);
                    searchCount++;
                    isMatchFound = true;

                    // Add the node's text to the lblMatches ListBox control
                    if (tabControl1.SelectedTab.Controls[0] == treeView)
                    {
                        // Get the character name from the tab page's Text property
                        string characterName = tabControl1.SelectedTab.Text;

                        if (!lblMatches.Items.Contains(" --- " + characterName + " --- "))
                        {
                            // Add a space after the last item if this is not the first character
                            if (lblMatches.Items.Count > 0 && lblMatches.Items[^1].ToString() != " ")
                            {
                                lblMatches.Items.Add(" ");
                            }

                            // Add the character name to the ListBox
                            lblMatches.Items.Add(" --- " + characterName + " --- ");
                        }

                        // Add the node's text to the ListBox
                        string nodeText = Regex.Replace(node.Text.TrimEnd('.'), @"\(\d+\)\s|^(an?|some|several)\s", "");
                        lblMatches.Items.Add(nodeText);

                        if (!matchedItemsDictionary.ContainsKey(nodeText))
                        {
                            matchedItemsDictionary[nodeText] = new List<MatchedItemInfo>();
                        }

                        matchedItemsDictionary[nodeText].Add(new MatchedItemInfo
                        {
                            FullPath = Regex.Replace(node.FullPath.TrimEnd('.'), @"\(\d+\)\s|^(an?|some|several)\s", "")
                        });
                    }
                }
                else
                {
                    // Change the color of non-matching nodes
                    node.ForeColor = Color.LightGray;

                    // Check if any child node is a matching node and update its color
                    foreach (TreeNode childNode in node.Nodes)
                    {
                        if (childNode.BackColor == Color.LightBlue || childNode.BackColor == Color.Yellow)
                        {
                            childNode.ForeColor = Color.Black;
                            break;
                        }
                    }
                }
            }

            return isMatchFound;
        }

        private void LblMatches_MouseDown(object sender, MouseEventArgs e)
        {
            // Close the custom tooltip if it's open
            if (customTooltip != null && !customTooltip.IsDisposed)
            {
                customTooltip.Close();
                tooltipTimer.Stop();
            }
        }

        private void InventoryViewForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (customTooltip != null && !customTooltip.IsDisposed)
            {
                customTooltip.Close();
                tooltipTimer.Stop();
            }
        }

        private Form customTooltip = null;
        private readonly Timer tooltipTimer = new();

        private void InitializeTooltipTimer()
        {
            // Set the interval for the timer
            tooltipTimer.Interval = 5000;

            // Tick event of the timer
            tooltipTimer.Tick += (sender, e) =>
            {
                if (customTooltip != null && !customTooltip.IsDisposed)
                {
                    customTooltip.Close();
                }

                tooltipTimer.Stop(); // Stop the timer
            };
        }

        private void LblMatches_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            int selectedIndex = listBox.SelectedIndex;

            if (selectedIndex >= 0 && selectedIndex < listBox.Items.Count)
            {
                string selectedItemText = listBox.Items[selectedIndex].ToString();

                if (matchedItemsDictionary.ContainsKey(selectedItemText))
                {
                    List<MatchedItemInfo> matchedItems = matchedItemsDictionary[selectedItemText];

                    // Close the old tooltip if it's open
                    if (customTooltip != null && !customTooltip.IsDisposed)
                    {
                        customTooltip.Close();
                        tooltipTimer.Stop();
                    }

                    // Create a new custom tooltip form
                    customTooltip = new Form
                    {
                        // Set the form's properties
                        FormBorderStyle = FormBorderStyle.FixedSingle,
                        MaximizeBox = false,
                        MinimizeBox = false,
                        ControlBox = false,
                        AutoScroll = true,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        //MaximumSize = new Size(int.MaxValue, 420),
                        TopMost = true // Keep the form on top of all other windows
                    };

                    // Create a label to display the tooltip text
#pragma warning disable CA1416 // Validate platform compatibility
                    Label label = new()
                    {
                        AutoSize = true,
                        Text = string.Join(Environment.NewLine + Environment.NewLine, matchedItems.Select(item => FormatPath(item.FullPath))),
                        ForeColor = Color.Black,
                        BackColor = Color.Beige,
                        Font = new Font("System", 10, FontStyle.Bold), // Set the desired font and size
                    };
#pragma warning restore CA1416 // Validate platform compatibility

                    // Add the label to the form
                    customTooltip.Controls.Add(label);

                    // Show the custom tooltip
                    tooltipTimer.Start(); // Start or restart the timer
                    tooltipTimer.Tick += (s, args) =>
                    {
                        customTooltip.Close();
                        tooltipTimer.Stop(); // Stop the timer
                    };

                    // Calculate the tooltip position near the click location
                    Point screenClickLocation = listBox.PointToScreen(e.Location);
                    customTooltip.StartPosition = FormStartPosition.CenterParent;
                    customTooltip.Location = new Point(screenClickLocation.X + 10, screenClickLocation.Y + 10);
                    customTooltip.Show();
                }
            }
        }

        // Add this method to format the path with hyphen indentation
        private static string FormatPath(string fullPath)
        {
            string[] parts = fullPath.Split('\\');
            if (parts.Length == 0)
            {
                return fullPath;
            }

            string itemName = parts[^1];
            string indentation = new('-', parts.Length - 1);

            if (parts.Length > 1)
            {
                string parentPath = FormatPath(string.Join("\\", parts.Take(parts.Length - 1)));
                return $"{parentPath}{Environment.NewLine}{indentation} {itemName}";
            }

            return $"{indentation} {itemName}";
        }

        private void BtnExpand_Click(object sender, EventArgs e)
        {
            // Expand all nodes in all TreeView controls
            SetTreeViewNodeState(true);
        }

        private void BtnCollapse_Click(object sender, EventArgs e)
        {
            // Collapse all nodes in all TreeView controls
            SetTreeViewNodeState(false);
        }

        private void SetTreeViewNodeState(bool isExpanded)
        {
            // Iterate over each tab page
            foreach (TabPage tabPage in tabControl1.TabPages)
            {
                // Get the TreeView control on the tab page
                var tv = tabPage.Controls[0] as TreeView;

                // Expand or collapse all nodes in the TreeView
                if (isExpanded)
                    tv.ExpandAll();
                else
                    tv.CollapseAll();
            }
        }

        private void BtnWiki_Click(object sender, EventArgs e)
        {
            // Get the TreeView control on the currently selected tab page
            var tv = tabControl1.SelectedTab.Controls[0] as TreeView;

            if (tv.SelectedNode == null)
            {
                MessageBox.Show("Select an item to lookup.");
            }
            else
            {
                try
                {
                    OpenWikiPage(tv.SelectedNode.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while opening the wiki page: {ex.Message}");
                }
            }
        }

        private void Listbox_Wiki_Click(object sender, EventArgs e)
        {
            if (lblMatches.SelectedItem == null)
            {
                MessageBox.Show("Select an item to lookup.");
            }
            else
            {
                string selectedItem = (string)lblMatches.SelectedItem;
                if (selectedItem == " " || selectedItem.StartsWith(" --- "))
                {
                    MessageBox.Show("Select a valid item to lookup.");
                }
                else
                {
                    OpenWikiPage(selectedItem);
                }
            }
        }

        private static void OpenWikiPage(string text)
        {
            if (Class1.Host.InterfaceVersion == 4)
                Class1.Host.SendText(string.Format("#browser https://elanthipedia.play.net/index.php?search={0}", Uri.EscapeDataString(Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)|^(an?|some|several)\s", ""))));
            else
                Process.Start(new ProcessStartInfo(string.Format("https://elanthipedia.play.net/index.php?search={0}", Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)|(^an?|some|several)\s", ""))) { UseShellExecute = true });
        }

        private void BtnFindNext_Click(object sender, EventArgs e)
        {
            // Find the next search match
            FindSearchMatch(true);
        }

        private void BtnFindPrev_Click(object sender, EventArgs e)
        {
            // Find the previous search match
            FindSearchMatch(false);
        }

        private void FindSearchMatch(bool isNext)
        {
            // Get the TreeView control on the currently selected tab page
            var tv = tabControl1.SelectedTab.Controls[0] as TreeView;

            // Get the TreeViewSearchMatches object for the TreeView control
            var treeViewSearchMatches = treeViewSearchMatchesList.FirstOrDefault(x => x.TreeView == tv);
            if (treeViewSearchMatches == null)
                return;

            if (treeViewSearchMatches.CurrentMatch == null)
            {
                // Set the current match to the first or last match in the list
                treeViewSearchMatches.CurrentMatch = isNext ? treeViewSearchMatches.SearchMatches.First<TreeNode>() : treeViewSearchMatches.SearchMatches.Last<TreeNode>();
            }
            else
            {
                // Reset the current match's background color
                if (chkDarkMode.Checked)
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.LightBlue;
                else
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.Yellow;

                // Get the index of the current match
                int index = treeViewSearchMatches.SearchMatches.IndexOf(treeViewSearchMatches.CurrentMatch) + (isNext ? 1 : -1);
                if (index == treeViewSearchMatches.SearchMatches.Count || index == -1)
                {
                    // Get the index of the currently selected tab page
                    int tabIndex = tabControl1.SelectedIndex;

                    // Find the next or previous tab page that has search matches
                    while (true)
                    {
                        tabIndex += isNext ? 1 : -1;
                        if (tabIndex < 0 || tabIndex >= tabControl1.TabPages.Count)
                        {
                            // Wrap around to the first or last tab page
                            tabIndex = isNext ? 0 : tabControl1.TabPages.Count - 1;
                        }

                        // Select the next or previous tab page
                        tabControl1.SelectedIndex = tabIndex;

                        // Get the TreeView control on the next or previous tab page
                        tv = tabControl1.SelectedTab.Controls[0] as TreeView;

                        // Get the TreeViewSearchMatches object for the TreeView control
                        treeViewSearchMatches = treeViewSearchMatchesList.FirstOrDefault(x => x.TreeView == tv);
                        if (treeViewSearchMatches != null && treeViewSearchMatches.SearchMatches.Count > 0)
                        {
                            // Set the current match to the first or last match in the list
                            index = isNext ? 0 : treeViewSearchMatches.SearchMatches.Count - 1;
                            break;
                        }
                    }
                }
                if (treeViewSearchMatches != null)
                    treeViewSearchMatches.CurrentMatch = treeViewSearchMatches.SearchMatches[index];
            }

            if (treeViewSearchMatches != null)
            {
                // Ensure that the current match is visible and highlight it
                treeViewSearchMatches.CurrentMatch.EnsureVisible();
                if (chkDarkMode.Checked)
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.LightBlue;
                else
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.Yellow;
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            Class1.Host.SendText("/InventoryView scan");
            Close();
        }

        private void BtnRemoveCharacter_Click(object sender, EventArgs e)
        {
            string characterName = cboCharacters.Text; // The name of the selected character

            if (!string.IsNullOrEmpty(characterName))
            {
                // Display a confirmation message
                DialogResult result = MessageBox.Show($"Are you sure you want to remove the character '{characterName}'?", "Confirm Remove", MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Load the XML document
                        XmlDocument doc = new();
                        string xmlPath = Path.Combine(basePath, "InventoryView.xml");
                        doc.Load(xmlPath);

                        // Find all CharacterData elements with the specified name element value
                        XmlNodeList characterNodes = doc.SelectNodes($"/Root/ArrayOfCharacterData/ArrayOfCharacterData/CharacterData[name='{characterName}']");

                        if (characterNodes.Count > 0)
                        {
                            // Remove all matching CharacterData elements from their parent
                            foreach (XmlNode characterNode in characterNodes)
                            {
                                characterNode.ParentNode.RemoveChild(characterNode);
                            }

                            // Save the modified XML document
                            doc.Save(xmlPath);

                            // Update the cboCharacters combobox
                            cboCharacters.Items.Remove(characterName);
                            cboCharacters.SelectedIndex = -1;
                            ReloadData();
                        }
                        else
                        {
                            Class1.Host.EchoText($"Could not find any CharacterData elements with a name element value of '{characterName}' in the XML file.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle the exception here
                        Class1.Host.EchoText($"An exception occurred: {ex.Message}");
                    }
                }
            }
            else
            {
                Class1.Host.EchoText("Please select a character name.");
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (customTooltip != null && !customTooltip.IsDisposed)
            {
                customTooltip.Close();
                tooltipTimer.Stop();
            }

            ClearMatchedItemPaths();
            // Reload the data
            ReloadData();

            // Reset the search controls
            ResetSearchControls();
        }

        private void BtnReload_Click(object sender, EventArgs e)
        {
            // Reload the data
            ReloadData();
        }

        private void ReloadData()
        {
            LoadSave.LoadSettings();
            //Class1._host.EchoText("Inventory reloaded.");
            BindData();
            UpdateCboCharacters();
        }

        private void UpdateCboCharacters()
        {
            // Clear the cboCharacters combobox
            cboCharacters.Items.Clear();

            try
            {
                // Load the XML document
                XmlDocument doc = new();
                string xmlPath = Path.Combine(basePath, "InventoryView.xml");
                doc.Load(xmlPath);

                // Find all CharacterData elements
                XmlNodeList characterNodes = doc.SelectNodes("/Root/ArrayOfCharacterData/ArrayOfCharacterData/CharacterData");

                // Create a list to store the character names
                List<string> characterNames = new();

                // Add the character names to the list
                foreach (XmlNode characterNode in characterNodes)
                {
                    string characterName = characterNode["name"].InnerText;
                    if (!characterNames.Contains(characterName))
                    {
                        characterNames.Add(characterName);
                    }
                }

                // Sort the character names in alphabetical order
                characterNames.Sort();

                // Add the sorted character names to the cboCharacters combobox
                foreach (string characterName in characterNames)
                {
                    cboCharacters.Items.Add(characterName);
                }

                // Select the first item in the cboCharacters combobox
                if (cboCharacters.Items.Count > 0)
                {
                    cboCharacters.SelectedIndex = -1;
                }
                if (cboCharacters.Items.Count == 0)
                {
                    cboCharacters.Text = "";
                }
            }
            catch (Exception ex)
            {
                // Handle the exception here
                MessageBox.Show($"An exception occurred: {ex.Message}");
            }
        }

        private void ResetSearchControls()
        {
            btnFindNext.Visible = btnFindPrev.Visible = btnReset.Visible = clickSearch = false;
            lblMatches.Items.Clear();
            lblFound.Text = "Found: 0";
            searchMatches.Clear();
            txtSearch.Text = "";
            // Set clickSearch to true to indicate that a search has been performed
            clickSearch = false;
            // Set focus back to the txtSearch control
            txtSearch.Focus();
        }

        private void ExportBranchToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> branchText = new()
            {
                Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s|(an?|some|several)\s", "")
            };
            CopyBranchText(tv.SelectedNode.Nodes, branchText, 1);
            Clipboard.SetText(string.Join("\r\n", branchText.ToArray()));
        }

        private static void CopyBranchText(TreeNodeCollection nodes, List<string> branchText, int level)
        {
            foreach (TreeNode node in nodes)
            {
                branchText.Add(new string('\t', level) + Regex.Replace(node.Text, @"\(\d+\)\s|^(an?|some|several)\s", ""));
                CopyBranchText(node.Nodes, branchText, level + 1);
            }
        }

        private void ListBox_Copy_Click(object sender, EventArgs e)
        {
            if (lblMatches.SelectedItem == null)
            {
                _ = (int)MessageBox.Show("Select an item to copy.");
            }
            else
            {
                StringBuilder txt = new();
                foreach (object row in lblMatches.SelectedItems)
                {
                    txt.Append(row.ToString());
                    txt.AppendLine();
                }
                txt.Remove(txt.Length - 1, 1);
                Clipboard.SetData(System.Windows.Forms.DataFormats.Text, txt.ToString());
            }
        }

        public void ListBox_Copy_All_Click(Object sender, EventArgs e)
        {
            if (clickSearch == false)
            {
                _ = (int)MessageBox.Show("Must search first to copy all.");
            }
            else
            {
                StringBuilder buffer = new();

                for (int i = 0; i < lblMatches.Items.Count; i++)
                {
                    buffer.Append(lblMatches.Items[i].ToString());
                    buffer.Append('\n');
                }
                Clipboard.SetText(buffer.ToString());
            }
        }

        public void ListBox_Copy_All_Selected_Click(Object sender, EventArgs e)
        {
            if (lblMatches.SelectedItem == null)
            {
                _ = (int)MessageBox.Show("Select items to copy.");
            }
            else
            {
                StringBuilder buffer = new();

                for (int i = 0; i < lblMatches.SelectedItems.Count; i++)
                {
                    buffer.Append(lblMatches.SelectedItems[i].ToString());
                    buffer.Append('\n');
                }
                Clipboard.SetText(buffer.ToString());
            }
        }

        private void Tv_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            Point point = new(e.X, e.Y);
            TreeNode nodeAt = tv.GetNodeAt(point);
            if (nodeAt == null)
                return;
            tv.SelectedNode = nodeAt;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV file|*.csv",
                Title = "Save the CSV file"
            };
            _ = (int)saveFileDialog.ShowDialog();
            if (!(saveFileDialog.FileName != ""))
                return;
            using (StreamWriter text = File.CreateText(saveFileDialog.FileName))
            {
                List<InventoryViewForm.ExportData> list = new();

                // Get the TreeView control on the currently selected tab page
                var tv = tabControl1.SelectedTab.Controls[0] as TreeView;

                // Add the TreeView's data to the list
                ExportBranch(tv.Nodes, list, 1);

                text.WriteLine("Character,Tap,Path");
                foreach (InventoryViewForm.ExportData exportData in list)
                {
                    if (exportData.Path.Count >= 1)
                    {
                        if (exportData.Path.Count == 3)
                        {
                            if (((IEnumerable<string>)new string[2]
                            {
                        "Vault",
                        "Home"
                            }).Contains<string>(exportData.Path[1]))
                                continue;
                        }
                        text.WriteLine(string.Format("{0},{1},{2}", (object)CleanCSV(exportData.Character), (object)CleanCSV(exportData.Tap), (object)CleanCSV(string.Join("\\", (IEnumerable<string>)exportData.Path))));
                    }
                }
            }
            _ = (int)MessageBox.Show("Export Complete.");
        }

        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV file|*.csv",
                Title = "Save the CSV file"
            };
            _ = (int)saveFileDialog.ShowDialog();
            if (!(saveFileDialog.FileName != ""))
                return;
            using (StreamWriter text = File.CreateText(saveFileDialog.FileName))
            {
                text.WriteLine("Character,Tap,Path");

                // Iterate through each tab page
                foreach (TabPage tabPage in tabControl1.TabPages)
                {
                    // Get the TreeView control on the current tab page
                    var tv = tabPage.Controls[0] as TreeView;

                    // Add the TreeView's data to the list
                    List<InventoryViewForm.ExportData> list = new();
                    ExportBranch(tv.Nodes, list, 1);

                    // Write data from the current tab page to the CSV file
                    foreach (InventoryViewForm.ExportData exportData in list)
                    {
                        if (exportData.Path.Count >= 1)
                        {
                            if (exportData.Path.Count == 3)
                            {
                                if (((IEnumerable<string>)new string[2] { "Vault", "Home" }).Contains<string>(exportData.Path[1]))
                                    continue;
                            }
                            text.WriteLine(string.Format("{0},{1},{2}", (object)CleanCSV(exportData.Character), (object)CleanCSV(exportData.Tap), (object)CleanCSV(string.Join("\\", (IEnumerable<string>)exportData.Path))));
                        }
                    }
                }
            }
            _ = (int)MessageBox.Show("Export Complete.");
        }

        private static string CleanCSV(string data)
        {
            if (!data.Contains(','))
                return data;
            return !data.Contains('"') ? string.Format("\"{0}\"", (object)data) : string.Format("\"{0}\"", (object)data.Replace("\"", "\"\""));
        }

        private static void ExportBranch(
          TreeNodeCollection nodes,
          List<InventoryViewForm.ExportData> list,
          int level)
        {
            foreach (TreeNode node in nodes)
            {
                InventoryViewForm.ExportData exportData = new()
                {
                    Tap = node.Text
                };
                TreeNode treeNode = node;
                while (treeNode.Parent != null)
                {
                    treeNode = treeNode.Parent;
                    if (treeNode.Parent != null)
                        exportData.Path.Insert(0, treeNode.Text);
                }
                exportData.Character = treeNode.Text;
                list.Add(exportData);
                ExportBranch(node.Nodes, list, level + 1);
            }
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

        public void ChkMultiLineTabs_CheckedChanged(object sender, EventArgs e)
        {
            tabControl1.Multiline = chkMultilineTabs.Checked;
            chkMultilineTabs.Enabled = false;
            LoadSave.SaveSettings();
            chkMultilineTabs.Enabled = true;
        }

        public void ChkFamily_CheckedChanged(object sender, EventArgs e)
        {
            chkFamily.Enabled = false;
            LoadSave.SaveSettings();
            chkFamily.Enabled = true;
        }

        public void ChkAlwaysTop_CheckedChanged(object sender, EventArgs e)
        {
            chkAlwaysTop.Enabled = false;
            LoadSave.SaveSettings();
            chkAlwaysTop.Enabled = true;
            this.TopMost = chkAlwaysTop.Checked;
        }

        public void ChkDarkMode_CheckedChanged(object sender, EventArgs e)
        {
            // Define the color values for dark mode and light mode
            Color darkModeForeColor = Color.White;
            Color darkModeBackColor = Color.Black;
            Color lightModeForeColor = Color.Black;
            Color lightModeBackColor = Color.White;

            Color foreColor, backColor;
            if (chkDarkMode.Checked)
            {
                foreColor = darkModeForeColor;
                backColor = darkModeBackColor;
            }
            else
            {
                foreColor = lightModeForeColor;
                backColor = lightModeBackColor;
            }

            // List of controls to update
            Control[] controlsToUpdate = new Control[]
            {
        this,
        this.lblMatches,
        this.panel1,
        this.splitContainer1,
        this.splitContainer1.Panel1,
        this.splitContainer1.Panel2,
        this.tabControl1,
        this.tableLayoutPanel1
            };

            foreach (Control control in controlsToUpdate)
            {
                if (control.ForeColor != foreColor)
                    control.ForeColor = foreColor;

                if (control.BackColor != backColor)
                    control.BackColor = backColor;
            }

            // Loop through all the TabPage controls in the tabControl1 control
            foreach (TabPage tabPage in tabControl1.TabPages)
            {
                // Get the TreeView control on the tab page
                TreeView tv = tabPage.Controls[0] as TreeView;

                // Update the colors of the TreeView control
                if (tv.ForeColor != foreColor)
                    tv.ForeColor = foreColor;

                if (tv.BackColor != backColor)
                    tv.BackColor = backColor;

                // Loop through all the nodes in the TreeView control
                UpdateNodeColors(tv.Nodes, foreColor, backColor);
            }

            LoadSave.SaveSettings();
        }

        private void UpdateNodeColors(TreeNodeCollection nodes, Color foreColor, Color backColor)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.BackColor == Color.LightBlue || node.BackColor == Color.Yellow)
                {
                    // Set the ForeColor of matching nodes to black and the BackColor to LightBlue or Yellow
                    node.ForeColor = Color.Black;
                    node.BackColor = chkDarkMode.Checked ? Color.LightBlue : Color.Yellow;
                }
                else
                {
                    // Set the ForeColor and BackColor of non-matching nodes to foreColor and backColor, respectively
                    node.ForeColor = foreColor;
                    node.BackColor = backColor;
                }

                // Recursively update the colors of child nodes
                UpdateNodeColors(node.Nodes, foreColor, backColor);
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();
            tv = new TreeView();
            listBox_Menu = new ContextMenuStrip(components);
            copyToolStripMenuItem = new ToolStripMenuItem();
            wikiToolStripMenuItem = new ToolStripMenuItem();
            copyAllToolStripMenuItem = new ToolStripMenuItem();
            copySelectedToolStripMenuItem = new ToolStripMenuItem();
            tabControl1 = new TabControl();
            lblMatches = new ListBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            panel1 = new Panel();
            chkAlwaysTop = new CheckBox();
            chkFamily = new CheckBox();
            chkDarkMode = new CheckBox();
            chkMultilineTabs = new CheckBox();
            infolabel = new Label();
            cboCharacters = new ComboBox();
            btnRemoveCharacter = new Button();
            btnFindPrev = new Button();
            txtSearch = new TextBox();
            lblFound = new Label();
            lblSearch = new Label();
            btnSearch = new Button();
            btnExport = new Button();
            btnExpand = new Button();
            btnReload = new Button();
            btnCollapse = new Button();
            btnScan = new Button();
            btnWiki = new Button();
            btnFindNext = new Button();
            btnReset = new Button();
            splitContainer1 = new SplitContainer();
            button1 = new Button();
            listBox_Menu.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            panel1.SuspendLayout();
            ((ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // tv
            // 
            tv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tv.Location = new Point(2, 2);
            tv.Margin = new Padding(4, 3, 4, 3);
            tv.Name = "tv";
            tv.ShowNodeToolTips = true;
            tv.Size = new Size(5, 32);
            tv.TabIndex = 10;
            tv.Visible = false;
            tv.MouseUp += Tv_MouseUp;
            // 
            // listBox_Menu
            // 
            listBox_Menu.Items.AddRange(new ToolStripItem[] { copyToolStripMenuItem, wikiToolStripMenuItem, copyAllToolStripMenuItem, copySelectedToolStripMenuItem });
            listBox_Menu.Name = "listBox_Menu";
            listBox_Menu.Size = new Size(167, 92);
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new Size(166, 22);
            copyToolStripMenuItem.Text = "Copy Selected";
            copyToolStripMenuItem.Click += ListBox_Copy_Click;
            // 
            // wikiToolStripMenuItem
            // 
            wikiToolStripMenuItem.Name = "wikiToolStripMenuItem";
            wikiToolStripMenuItem.Size = new Size(166, 22);
            wikiToolStripMenuItem.Text = "Wiki Selected";
            wikiToolStripMenuItem.Click += Listbox_Wiki_Click;
            // 
            // copyAllToolStripMenuItem
            // 
            copyAllToolStripMenuItem.Name = "copyAllToolStripMenuItem";
            copyAllToolStripMenuItem.Size = new Size(166, 22);
            copyAllToolStripMenuItem.Text = "Copy All";
            copyAllToolStripMenuItem.Click += ListBox_Copy_All_Click;
            // 
            // copySelectedToolStripMenuItem
            // 
            copySelectedToolStripMenuItem.Name = "copySelectedToolStripMenuItem";
            copySelectedToolStripMenuItem.Size = new Size(166, 22);
            copySelectedToolStripMenuItem.Text = "Copy All Selected";
            copySelectedToolStripMenuItem.Click += ListBox_Copy_All_Selected_Click;
            // 
            // tabControl1
            // 
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Margin = new Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(654, 472);
            tabControl1.TabIndex = 16;
            // 
            // lblMatches
            // 
            lblMatches.AllowDrop = true;
            lblMatches.ContextMenuStrip = listBox_Menu;
            lblMatches.Dock = DockStyle.Fill;
            lblMatches.FormattingEnabled = true;
            lblMatches.HorizontalScrollbar = true;
            lblMatches.ItemHeight = 15;
            lblMatches.Location = new Point(0, 0);
            lblMatches.Margin = new Padding(4, 3, 4, 3);
            lblMatches.Name = "lblMatches";
            lblMatches.SelectionMode = SelectionMode.MultiExtended;
            lblMatches.Size = new Size(530, 472);
            lblMatches.TabIndex = 17;
            lblMatches.MouseDoubleClick += LblMatches_MouseDoubleClick;
            lblMatches.MouseDown += LblMatches_MouseDown;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel1.Controls.Add(panel1, 0, 0);
            tableLayoutPanel1.Controls.Add(splitContainer1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(4, 3, 4, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.Size = new Size(1184, 552);
            tableLayoutPanel1.TabIndex = 18;
            // 
            // panel1
            // 
            panel1.Controls.Add(button1);
            panel1.Controls.Add(chkAlwaysTop);
            panel1.Controls.Add(chkFamily);
            panel1.Controls.Add(chkDarkMode);
            panel1.Controls.Add(chkMultilineTabs);
            panel1.Controls.Add(infolabel);
            panel1.Controls.Add(cboCharacters);
            panel1.Controls.Add(btnRemoveCharacter);
            panel1.Controls.Add(btnFindPrev);
            panel1.Controls.Add(txtSearch);
            panel1.Controls.Add(lblFound);
            panel1.Controls.Add(lblSearch);
            panel1.Controls.Add(btnSearch);
            panel1.Controls.Add(btnExport);
            panel1.Controls.Add(btnExpand);
            panel1.Controls.Add(btnReload);
            panel1.Controls.Add(btnCollapse);
            panel1.Controls.Add(btnScan);
            panel1.Controls.Add(btnWiki);
            panel1.Controls.Add(btnFindNext);
            panel1.Controls.Add(btnReset);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(4, 3);
            panel1.Margin = new Padding(4, 3, 4, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(1189, 68);
            panel1.TabIndex = 0;
            // 
            // chkAlwaysTop
            // 
            chkAlwaysTop.AutoSize = true;
            chkAlwaysTop.Location = new Point(388, 0);
            chkAlwaysTop.Name = "chkAlwaysTop";
            chkAlwaysTop.Size = new Size(85, 19);
            chkAlwaysTop.TabIndex = 20;
            chkAlwaysTop.Text = "Always Top";
            chkAlwaysTop.UseVisualStyleBackColor = true;
            chkAlwaysTop.CheckedChanged += ChkAlwaysTop_CheckedChanged;
            // 
            // chkFamily
            // 
            chkFamily.AutoSize = true;
            chkFamily.Location = new Point(92, 0);
            chkFamily.Margin = new Padding(4, 3, 4, 3);
            chkFamily.Name = "chkFamily";
            chkFamily.Size = new Size(292, 19);
            chkFamily.TabIndex = 19;
            chkFamily.Text = "Family Vault - Must have runners or be inside vault";
            chkFamily.UseVisualStyleBackColor = true;
            chkFamily.CheckedChanged += ChkFamily_CheckedChanged;
            // 
            // chkDarkMode
            // 
            chkDarkMode.AutoSize = true;
            chkDarkMode.Location = new Point(2, 0);
            chkDarkMode.Margin = new Padding(4, 3, 4, 3);
            chkDarkMode.Name = "chkDarkMode";
            chkDarkMode.Size = new Size(84, 19);
            chkDarkMode.TabIndex = 18;
            chkDarkMode.Text = "Dark Mode";
            chkDarkMode.UseVisualStyleBackColor = true;
            chkDarkMode.CheckedChanged += ChkDarkMode_CheckedChanged;
            // 
            // chkMultilineTabs
            // 
            chkMultilineTabs.AutoSize = true;
            chkMultilineTabs.Location = new Point(2, 46);
            chkMultilineTabs.Margin = new Padding(4, 3, 4, 3);
            chkMultilineTabs.Name = "chkMultilineTabs";
            chkMultilineTabs.Size = new Size(99, 19);
            chkMultilineTabs.TabIndex = 17;
            chkMultilineTabs.Text = "Multiline Tabs";
            chkMultilineTabs.UseVisualStyleBackColor = true;
            chkMultilineTabs.CheckedChanged += ChkMultiLineTabs_CheckedChanged;
            // 
            // infolabel
            // 
            infolabel.AutoSize = true;
            infolabel.Location = new Point(117, 48);
            infolabel.Margin = new Padding(4, 0, 4, 0);
            infolabel.Name = "infolabel";
            infolabel.Size = new Size(222, 15);
            infolabel.TabIndex = 16;
            infolabel.Text = "T = Total Item Count | M = Total Matches";
            // 
            // cboCharacters
            // 
            cboCharacters.FormattingEnabled = true;
            cboCharacters.Location = new Point(1066, 6);
            cboCharacters.Margin = new Padding(4, 3, 4, 3);
            cboCharacters.Name = "cboCharacters";
            cboCharacters.Size = new Size(109, 23);
            cboCharacters.TabIndex = 15;
            // 
            // btnRemoveCharacter
            // 
            btnRemoveCharacter.AutoSize = true;
            btnRemoveCharacter.ForeColor = Color.Black;
            btnRemoveCharacter.Location = new Point(1066, 37);
            btnRemoveCharacter.Margin = new Padding(4, 3, 4, 3);
            btnRemoveCharacter.Name = "btnRemoveCharacter";
            btnRemoveCharacter.Size = new Size(110, 29);
            btnRemoveCharacter.TabIndex = 14;
            btnRemoveCharacter.Text = "Remove";
            btnRemoveCharacter.UseVisualStyleBackColor = true;
            btnRemoveCharacter.Click += BtnRemoveCharacter_Click;
            // 
            // btnFindPrev
            // 
            btnFindPrev.AutoSize = true;
            btnFindPrev.ForeColor = Color.Black;
            btnFindPrev.Location = new Point(475, 6);
            btnFindPrev.Margin = new Padding(4, 3, 4, 3);
            btnFindPrev.Name = "btnFindPrev";
            btnFindPrev.Size = new Size(88, 29);
            btnFindPrev.TabIndex = 3;
            btnFindPrev.Text = "Find Prev";
            btnFindPrev.UseVisualStyleBackColor = true;
            btnFindPrev.Visible = false;
            btnFindPrev.Click += BtnFindPrev_Click;
            // 
            // txtSearch
            // 
            txtSearch.Location = new Point(68, 22);
            txtSearch.Margin = new Padding(4, 3, 4, 3);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(305, 23);
            txtSearch.TabIndex = 1;
            // 
            // lblFound
            // 
            lblFound.AutoSize = true;
            lblFound.Location = new Point(380, 48);
            lblFound.Margin = new Padding(4, 0, 4, 0);
            lblFound.Name = "lblFound";
            lblFound.Size = new Size(53, 15);
            lblFound.TabIndex = 0;
            lblFound.Text = "Found: 0";
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = true;
            lblSearch.Location = new Point(9, 25);
            lblSearch.Margin = new Padding(4, 0, 4, 0);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(45, 15);
            lblSearch.TabIndex = 0;
            lblSearch.Text = "Search:";
            // 
            // btnSearch
            // 
            btnSearch.AutoSize = true;
            btnSearch.ForeColor = Color.Black;
            btnSearch.Location = new Point(380, 18);
            btnSearch.Margin = new Padding(4, 3, 4, 3);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new Size(88, 29);
            btnSearch.TabIndex = 2;
            btnSearch.Text = "Search";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += BtnSearch_Click;
            // 
            // btnExport
            // 
            btnExport.AutoSize = true;
            btnExport.ForeColor = Color.Black;
            btnExport.Location = new Point(853, 6);
            btnExport.Margin = new Padding(4, 3, 4, 3);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(94, 29);
            btnExport.TabIndex = 13;
            btnExport.Text = "Export Current";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += BtnExport_Click;
            // 
            // btnExpand
            // 
            btnExpand.AutoSize = true;
            btnExpand.ForeColor = Color.Black;
            btnExpand.Location = new Point(664, 6);
            btnExpand.Margin = new Padding(4, 3, 4, 3);
            btnExpand.Name = "btnExpand";
            btnExpand.Size = new Size(88, 29);
            btnExpand.TabIndex = 6;
            btnExpand.Text = "Expand All";
            btnExpand.UseVisualStyleBackColor = true;
            btnExpand.Click += BtnExpand_Click;
            // 
            // btnReload
            // 
            btnReload.AutoSize = true;
            btnReload.ForeColor = Color.Black;
            btnReload.Location = new Point(946, 37);
            btnReload.Margin = new Padding(4, 3, 4, 3);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(113, 29);
            btnReload.TabIndex = 12;
            btnReload.Text = "Reload File";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += BtnReload_Click;
            // 
            // btnCollapse
            // 
            btnCollapse.AutoSize = true;
            btnCollapse.ForeColor = Color.Black;
            btnCollapse.Location = new Point(664, 37);
            btnCollapse.Margin = new Padding(4, 3, 4, 3);
            btnCollapse.Name = "btnCollapse";
            btnCollapse.Size = new Size(92, 29);
            btnCollapse.TabIndex = 7;
            btnCollapse.Text = "Collapse All";
            btnCollapse.UseVisualStyleBackColor = true;
            btnCollapse.Click += BtnCollapse_Click;
            // 
            // btnScan
            // 
            btnScan.AutoSize = true;
            btnScan.ForeColor = Color.Black;
            btnScan.Location = new Point(946, 6);
            btnScan.Margin = new Padding(4, 3, 4, 3);
            btnScan.Name = "btnScan";
            btnScan.Size = new Size(113, 29);
            btnScan.TabIndex = 11;
            btnScan.Text = "Scan Inventory";
            btnScan.UseVisualStyleBackColor = true;
            btnScan.Click += BtnScan_Click;
            // 
            // btnWiki
            // 
            btnWiki.AutoSize = true;
            btnWiki.ForeColor = Color.Black;
            btnWiki.Location = new Point(758, 22);
            btnWiki.Margin = new Padding(4, 3, 4, 3);
            btnWiki.Name = "btnWiki";
            btnWiki.Size = new Size(97, 29);
            btnWiki.TabIndex = 8;
            btnWiki.Text = "Wiki Lookup";
            btnWiki.UseVisualStyleBackColor = true;
            btnWiki.Click += BtnWiki_Click;
            // 
            // btnFindNext
            // 
            btnFindNext.AutoSize = true;
            btnFindNext.ForeColor = Color.Black;
            btnFindNext.Location = new Point(475, 37);
            btnFindNext.Margin = new Padding(4, 3, 4, 3);
            btnFindNext.Name = "btnFindNext";
            btnFindNext.Size = new Size(88, 29);
            btnFindNext.TabIndex = 4;
            btnFindNext.Text = "Find Next";
            btnFindNext.UseVisualStyleBackColor = true;
            btnFindNext.Visible = false;
            btnFindNext.Click += BtnFindNext_Click;
            // 
            // btnReset
            // 
            btnReset.AutoSize = true;
            btnReset.ForeColor = Color.Black;
            btnReset.Location = new Point(569, 22);
            btnReset.Margin = new Padding(4, 3, 4, 3);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(88, 29);
            btnReset.TabIndex = 5;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Visible = false;
            btnReset.Click += BtnReset_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(4, 77);
            splitContainer1.Margin = new Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(lblMatches);
            splitContainer1.Size = new Size(1189, 472);
            splitContainer1.SplitterDistance = 654;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 19;
            // 
            // button1
            // 
            button1.AutoSize = true;
            button1.ForeColor = Color.Black;
            button1.Location = new Point(853, 37);
            button1.Margin = new Padding(4, 3, 4, 3);
            button1.Name = "button1";
            button1.Size = new Size(88, 29);
            button1.TabIndex = 21;
            button1.Text = "Export All";
            button1.UseVisualStyleBackColor = true;
            button1.Click += BtnExportAll_Click;
            // 
            // InventoryViewForm
            // 
            AcceptButton = btnSearch;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            ClientSize = new Size(1184, 552);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(tv);
            Margin = new Padding(4, 3, 4, 3);
            Name = "InventoryViewForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Inventory View";
            FormClosed += InventoryViewForm_FormClosed;
            Load += InventoryViewForm_Load;
            listBox_Menu.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
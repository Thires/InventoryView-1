﻿using GeniePlugin.Interfaces;
using System;
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
using System.Runtime.InteropServices;


namespace InventoryView
{
    public class InventoryViewForm : Form
    {
        private readonly List<TreeNode> searchMatches = new();
        private IContainer components;
        private TreeView tv;

        private MenuStrip menuStrip;
        private ContextMenuStrip listBox_Menu;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem wikiToolStripMenuItem;
        private ToolStripMenuItem copyAllToolStripMenuItem;
        private ToolStripMenuItem optionsToolStripMenuItem;
        internal ToolStripMenuItem toolStripDarkMode;
        internal ToolStripMenuItem toolStripFamily;
        internal ToolStripMenuItem toolStripPockets;
        internal ToolStripMenuItem toolStripMultilineTabs;
        private ToolStripMenuItem commandsToolStripMenuItem;
        internal ToolStripMenuItem toolStripScan;
        internal ToolStripMenuItem toolStripReload;
        internal ToolStripMenuItem toolStripWiki;
        private ToolStripMenuItem toolStripExport;
        private ToolStripContainer toolStripContainer1;
        internal ToolStripMenuItem toolStripAlwaysTop;
        private ToolStripMenuItem toolStripExportAll;
        private ToolStripMenuItem copySelectedToolStripMenuItem;
        private ToolStripMenuItem filtarAlltoolStripMenuItem;
        private ToolStripMenuItem filterActivetoolStripMenuItem;
        private ToolStripMenuItem filterArchivedtoolStripMenuItem;
        private ContextMenuStrip tabContextMenuStrip;
        private ToolStripMenuItem resetTabColorsMenuItem;
        private ToolStripMenuItem filterToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem colorTabToolStrip;
        private ToolStripMenuItem resetSingleTabStripMenu;

        private bool clickSearch = false;

        // Create a new list to store the search matches for each TreeView control
        private readonly List<TreeViewSearchMatches> treeViewSearchMatchesList = new();
        // Create a list to store the hidden tab pages and their original positions
        private readonly List<(TabPage tabPage, int index)> hiddenTabPages = new();
        internal TabControl tabControl1;
        private ListBox lblMatches;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel panel1;
        private TextBox txtSearch;

        private Label infolabel;
        private Label lblFound;
        private Label lblSearch;
        private Label label1;

        private Button btnRemoveCharacter;
        private Button btnFindPrev;
        private Button btnExpand;
        private Button btnCollapse;
        private Button btnFindNext;
        private Button btnReset;

        private SplitContainer splitContainer1;
        private ComboBox cboCharacters;

        private Form customTooltip = null;
        private readonly Timer tooltipTimer = new();
        internal static Form form;

        private static string basePath = Application.StartupPath;
        private string currentFilter;

        private readonly Dictionary<string, List<MatchedItemInfo>> matchedItemsDictionary = new();

        public InventoryViewForm()
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void InventoryViewForm_Load(object sender, EventArgs e)
        {
            BindData();
            basePath = plugin.Host.get_Variable("PluginPath");

            // Load the character data
            LoadSave.LoadSettings();

            // Get a list of distinct character names from the characterData list
            List<string> characterNames = plugin.CharacterData.Select(c => c.name).Distinct().ToList();

            // Sort the character names
            characterNames.Sort();

            // Add the character names to the cboCharacters control
            cboCharacters.Items.Clear();
            cboCharacters.Items.AddRange(characterNames.ToArray());

            lblMatches.MouseDoubleClick += LblMatches_MouseDoubleClick;
            InitializeTooltipTimer();

            toolStripAlwaysTop.CheckedChanged += ToolStripAlwaysTop_CheckedChanged;

            tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl1.DrawItem += tabControl1_DrawItem;

            // In InventoryViewForm_Load or in a helper method:
            tabContextMenuStrip = new ContextMenuStrip();
            ToolStripMenuItem toggleArchiveMenuItem = new ToolStripMenuItem("Toggle Archive");
            toggleArchiveMenuItem.Click += ToggleArchiveMenuItem_Click;
            tabContextMenuStrip.Items.Add(toggleArchiveMenuItem);

            // Attach a MouseUp event to the TabControl if not already attached.
            tabControl1.MouseUp += TabControl1_MouseUp;
            resetTabColorsMenuItem.Click += ResetAllTabColors_Click;

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
            tabControl1.TabPages.Clear();

            // Use the global currentFilter.
            string filter = currentFilter;

            // Get a distinct character list.
            var characters = GetDistinctCharacters();

            // Filter the character list
            if (filter == "Active Tabs")
            {
                characters = characters.Where(ch => plugin.CharacterData
                    .FirstOrDefault(c => c.name == ch && !c.Archived) != null).ToList(); // Only active characters
            }
            else if (filter == "Archived Tabs")
            {
                characters = characters.Where(ch => plugin.CharacterData
                    .FirstOrDefault(c => c.name == ch && c.Archived) != null).ToList(); // Only archived characters
            }

            // For each character, create a tab
            foreach (var character in characters)
            {
                // Determine archive state
                bool isArchived = plugin.CharacterData.FirstOrDefault(c => c.name == character)?.Archived ?? false; // If no character found, assume not archived

                // Create the tab page text
                var tabPage = new TabPage(character + (isArchived ? " (Archived)" : " (Acivated)"));
                tabPage.Tag = character; // store name

                ApplyTabColor(tabPage, character);

                tabControl1.TabPages.Add(tabPage);

                // Create a new TreeView for inventory.
                var tv = new TreeView { Dock = DockStyle.Fill };
                if (toolStripDarkMode.Checked)
                {
                    tv.ForeColor = SystemColors.Control;
                    tv.BackColor = SystemColors.ControlText;
                }
                else
                {
                    tv.ForeColor = SystemColors.ControlText;
                    tv.BackColor = SystemColors.Control;
                }
                tabPage.Controls.Add(tv);
                tv.Nodes.Clear();

                // Create a new ContextMenuStrip for the TreeView control
                var contextMenuStrip = CreateTreeViewContextMenuStrip(tv);
                tv.ContextMenuStrip = contextMenuStrip;

                int totalCount = AddCharacterDataToTreeView(tv, character);
                tabPage.Text = $"{character} (T: {totalCount})";

                foreach (TreeNode rootNode in tv.Nodes)
                {
                    rootNode.Expand();
                }
            }
        }

        private static List<string> GetDistinctCharacters()
        {
            var characters = plugin.CharacterData.Select(tbl => tbl.name).Distinct().ToList();
            characters.Sort();
            return characters;
        }

        private static int AddCharacterDataToTreeView(TreeView tv, string character)
        {
            int totalCount = 0;

            TreeNode charNode = tv.Nodes.Add(character);
            foreach (var source in plugin.CharacterData.Where(tbl => tbl.name == character))
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

            // Adds the "Copy Text" option
            var copyTextToolStripMenuItem = new ToolStripMenuItem("Copy Text");
            copyTextToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode != null)
                    Clipboard.SetText(Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s|^(an?|some|several)\s|^\d+\s--\s(an?|some|several)\s", ""));
            };
            contextMenuStrip.Items.Add(copyTextToolStripMenuItem);

            // Adds the "Copy Branch" option
            var copyBranchToolStripMenuItem = new ToolStripMenuItem("Copy Branch");
            copyBranchToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode != null)
                {
                    List<string> branchText = new()
                    {
                        Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s|^(an?|some|several)\s|^\d+\s--\s(an?|some|several)\s", "")
                    };
                    CopyBranchText(tv.SelectedNode.Nodes, branchText, 1);
                    Clipboard.SetText(string.Join("\r\n", branchText.ToArray()));
                }
            };
            contextMenuStrip.Items.Add(copyBranchToolStripMenuItem);

            return contextMenuStrip;
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

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
                    // Split the search text into individual words
                    var searchWords = txtSearch.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

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
                        SearchTree(tv, tv.Nodes, treeViewSearchMatches.SearchMatches, ref searchCount, ref totalCount, searchWords);

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
        }

        private void ClearMatchedItemPaths()
        {
            foreach (var matchedItem in matchedItemsDictionary.Values)
            {
                matchedItem.Clear();
            }
        }

        private bool SearchTree(TreeView treeView, TreeNodeCollection nodes, List<TreeNode> searchMatches, ref int searchCount, ref int totalCount, string[] searchWords)
        {
            bool isMatchFound = false;

            foreach (TreeNode node in nodes)
            {
                totalCount++;

                // Reset the node's background color and foreground color based on the dark mode
                node.BackColor = toolStripDarkMode.Checked ? SystemColors.ControlText : SystemColors.Control;
                node.ForeColor = toolStripDarkMode.Checked ? SystemColors.Control : SystemColors.ControlText;

                // Search the node's child nodes and update the match status
                if (SearchTree(treeView, node.Nodes, searchMatches, ref searchCount, ref totalCount, searchWords))
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

                // Check if the node's text contains all search words
                bool allWordsMatch = searchWords.All(word => node.Text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);

                if (allWordsMatch)
                {
                    // Highlight the node and add it to the list of search matches
                    node.BackColor = toolStripDarkMode.Checked ? Color.LightBlue : Color.Yellow;
                    node.ForeColor = SystemColors.ControlText;
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
                            childNode.ForeColor = SystemColors.ControlText;
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

        public static void HandleTabControlMouseUp(TabControl tabControl, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Check if Shift key was pressed using Control.ModifierKeys
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    for (int i = 0; i < tabControl.TabPages.Count; i++)
                    {
                        Rectangle tabRect = tabControl.GetTabRect(i);
                        if (tabRect.Contains(e.Location))
                        {
                            tabControl.SelectedIndex = i;
                            ChangeTabColor(tabControl);
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < tabControl.TabPages.Count; i++)
                    {
                        Rectangle tabRect = tabControl.GetTabRect(i);
                        if (tabRect.Contains(e.Location))
                        {
                            tabControl.SelectedIndex = i;
                            ToggleArchiveForSelectedTab(tabControl);  // Use ToggleArchiveForSelectedTab for archive toggling
                            break;
                        }
                    }
                }
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

        private void InitializeTooltipTimer()
        {
            // Set the interval for the timer
            tooltipTimer.Interval = 6000;

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
                        ControlBox = true,
                        AutoScroll = true,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        MaximumSize = new Size(int.MaxValue, 600),
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
                    customTooltip.StartPosition = FormStartPosition.CenterScreen;
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

        private void Wiki_Click(object sender, EventArgs e)
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
            if (plugin.Host.InterfaceVersion == 4)
                plugin.Host.SendText(string.Format("#browser https://elanthipedia.play.net/index.php?search={0}", Uri.EscapeDataString(Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)|^(an?|some|several)\s|^\d+\s--\s(an?|some|several)\s", ""))));
            else
                Process.Start(new ProcessStartInfo(string.Format("https://elanthipedia.play.net/index.php?search={0}", Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)|(^an?|some|several)\s|^\d+\s--\s(an?|some|several)\s", ""))) { UseShellExecute = true });
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
                if (toolStripDarkMode.Checked)
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
                if (toolStripDarkMode.Checked)
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.LightBlue;
                else
                    treeViewSearchMatches.CurrentMatch.BackColor = Color.Yellow;
            }
        }

        private void Scan_Click(object sender, EventArgs e)
        {
            plugin.Host.SendText("/InventoryView scan");
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
                            plugin.Host.EchoText($"Could not find any CharacterData elements with a name element value of '{characterName}' in the XML file.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle the exception here
                        plugin.Host.EchoText($"An exception occurred: {ex.Message}");
                    }
                }
            }
            else
            {
                plugin.Host.EchoText("Please select a character name.");
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

        private void Reload_Click(object sender, EventArgs e)
        {
            // Reload the data
            ReloadData();
        }

        private void ReloadData()
        {
            LoadSave.LoadSettings();
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

        private void Export_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV file|*.csv",
                Title = "Save the CSV file"
            };
            _ = (int)saveFileDialog.ShowDialog();
            if (!(saveFileDialog.FileName != ""))
                return;
            using (StreamWriter text = System.IO.File.CreateText(saveFileDialog.FileName))
            {
                List<ExportData> list = new();

                // Get the TreeView control on the currently selected tab page
                var tv = tabControl1.SelectedTab.Controls[0] as TreeView;

                // Add the TreeView's data to the list
                ExportBranch(tv.Nodes, list, 1);

                text.WriteLine("Character,Tap,Path");
                foreach (ExportData exportData in list)
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

        private void ExportAll_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "CSV file|*.csv",
                Title = "Save the CSV file"
            };
            _ = (int)saveFileDialog.ShowDialog();
            if (!(saveFileDialog.FileName != ""))
                return;
            using (StreamWriter text = System.IO.File.CreateText(saveFileDialog.FileName))
            {
                text.WriteLine("Character,Tap,Path");

                // Iterate through each tab page
                foreach (TabPage tabPage in tabControl1.TabPages)
                {
                    // Get the TreeView control on the current tab page
                    var tv = tabPage.Controls[0] as TreeView;

                    // Add the TreeView's data to the list
                    List<ExportData> list = new();
                    ExportBranch(tv.Nodes, list, 1);

                    // Write data from the current tab page to the CSV file
                    foreach (ExportData exportData in list)
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
          List<ExportData> list,
          int level)
        {
            foreach (TreeNode node in nodes)
            {
                ExportData exportData = new()
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

        public void MultiLineTabs_CheckedChanged(object sender, EventArgs e)
        {
            tabControl1.Multiline = toolStripMultilineTabs.Checked;
            toolStripMultilineTabs.Enabled = false;
            LoadSave.SaveSettings();
            toolStripMultilineTabs.Enabled = true;
        }

        public void Family_CheckedChanged(object sender, EventArgs e)
        {
            toolStripFamily.Enabled = false;
            LoadSave.SaveSettings();
            toolStripFamily.Enabled = true;
            if (toolStripFamily.Checked)
                plugin.Host.EchoText("To use family vault, be inside the vault or have runners");
        }

        public void Pockets_CheckedChanged(object sender, EventArgs e)
        {
            toolStripPockets.Enabled = false;
            LoadSave.SaveSettings();
            toolStripPockets.Enabled = true;
        }

        public void DarkMode_CheckedChanged(object sender, EventArgs e)
        {
            // Define the color values for dark mode and light mode
            Color darkModeForeColor = SystemColors.Control;
            Color darkModeBackColor = SystemColors.ControlText;
            Color lightModeForeColor = SystemColors.ControlText;
            Color lightModeBackColor = SystemColors.Control;

            Color foreColor, backColor;
            if (toolStripDarkMode.Checked)
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
        this.menuStrip,
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

            ToolStripItem[] toolStripItemsToUpdate = new ToolStripItem[]
            {
                optionsToolStripMenuItem,
                commandsToolStripMenuItem,
                filterToolStripMenuItem,
                filtarAlltoolStripMenuItem,
                filterActivetoolStripMenuItem,
                filterArchivedtoolStripMenuItem,
                toolStripDarkMode,
                toolStripFamily,
                toolStripMultilineTabs,
                toolStripPockets,
                toolStripScan,
                toolStripReload,
                toolStripWiki,
                toolStripExport
            };

            foreach (ToolStripItem toolStripItem in toolStripItemsToUpdate)
            {
                UpdateToolStripItemColors(toolStripItem, foreColor, backColor);
            }

            LoadSave.SaveSettings();
        }

        // Toggles the archive state for the currently selected tab.
        public static void ToggleArchiveForSelectedTab(TabControl tabControl)
        {
            if (tabControl.SelectedTab != null)
            {
                // Retrieve the character name from the TabPage's Tag (if you stored it as a string)
                string characterName = tabControl.SelectedTab.Tag as string;
                if (string.IsNullOrEmpty(characterName))
                    return;

                var entries = plugin.CharacterData
                    .Where(c => c.name.Equals(characterName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (entries.Count > 0)
                {
                    bool currentlyArchived = entries.All(c => c.Archived);
                    bool newArchiveState = !currentlyArchived;
                    foreach (var entry in entries)
                    {
                        entry.Archived = newArchiveState;
                    }
                    tabControl.SelectedTab.Text = characterName + (newArchiveState ? " (Archived)" : " (Activated)");
                }
                tabControl.Invalidate();
                LoadSave.SaveSettings();
            }
        }

        private void UpdateNodeColors(TreeNodeCollection nodes, Color foreColor, Color backColor)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.BackColor == Color.LightBlue || node.BackColor == Color.Yellow)
                {
                    // Set the ForeColor of matching nodes to black and the BackColor to LightBlue or Yellow
                    node.ForeColor = SystemColors.ControlText;
                    node.BackColor = toolStripDarkMode.Checked ? Color.LightBlue : Color.Yellow;
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

        private static void UpdateToolStripItemColors(ToolStripItem toolStripItem, Color foreColor, Color backColor)
        {
            if (toolStripItem is ToolStripMenuItem menuItem)
            {
                menuItem.ForeColor = foreColor;
                menuItem.BackColor = backColor;

                // Update sub-menu items recursively
                foreach (ToolStripItem subItem in menuItem.DropDownItems)
                {
                    UpdateToolStripItemColors(subItem, foreColor, backColor);
                }
            }
            else
            {
                toolStripItem.ForeColor = foreColor;
                toolStripItem.BackColor = backColor;
            }
        }

        private void ToolStripAlwaysTop_CheckedChanged(object sender, EventArgs e)
        {
            toolStripAlwaysTop.Enabled = false;
            LoadSave.SaveSettings();
            toolStripAlwaysTop.Enabled = true;
            this.TopMost = toolStripAlwaysTop.Checked;
        }

        private void ApplyTabColor(TabPage tabPage, string characterName)
        {
            var character = plugin.CharacterData.FirstOrDefault(c => c.name == characterName);

            if (character != null)
            {
                // Apply TabColor (Background Color) from stored character data
                if (!string.IsNullOrEmpty(character.TabColor))
                {
                    try
                    {
                        var color = ColorTranslator.FromHtml(character.TabColor);  // Convert hex string to color
                        tabPage.BackColor = color;  // Apply the color to the tab's background
                    }
                    catch
                    {
                        // Handle invalid color string (fallback to default color)
                        tabPage.BackColor = Color.White;
                    }
                }

                // Apply TabTextColor (Text Color)
                if (!string.IsNullOrEmpty(character.TabTextColor))
                {
                    try
                    {
                        var textColor = ColorTranslator.FromHtml(character.TabTextColor);  // Convert hex string to color
                        tabPage.ForeColor = textColor;  // Apply the color to the tab's text
                    }
                    catch
                    {
                        // Handle invalid color string (fallback to default text color)
                        tabPage.ForeColor = Color.Black;
                    }
                }
            }
        }

        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage page = tabControl1.TabPages[e.Index];
            Color backColor = page.BackColor;
            Color textColor = Color.Black; // Default fallback

            // Get text color from character data
            var character = plugin.CharacterData.FirstOrDefault(c => c.name == page.Tag?.ToString());
            if (character != null && !string.IsNullOrEmpty(character.TabTextColor))
            {
                try { textColor = ColorTranslator.FromHtml(character.TabTextColor); }
                catch { textColor = SystemColors.ControlText; }
            }

            using (SolidBrush brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            //if (backColor.GetBrightness() < 0.5)
            //{
            //    textColor = Color.White;  // Use white text for dark background
            //}

            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tabControl1.Font,
                e.Bounds,
                textColor,  // Use the custom text color
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }

        public static void ChangeTabColor(TabControl tabControl)
        {
            // First dialog: Background color
            using (var bgDialog = new TitledColorDialog { DialogTitle = "Select Background Color" })
            {
                if (bgDialog.ShowDialog() == DialogResult.OK)
                {
                    Color bgColor = bgDialog.Color;

                    // Second dialog: Text color with contrast suggestion
                    using (var textDialog = new TitledColorDialog
                    {
                        DialogTitle = "Select Text Color",
                        Color = GetContrastColor(bgColor)  // Auto-suggest contrast
                    })
                    {
                        if (textDialog.ShowDialog() == DialogResult.OK)
                        {
                            ApplyColorsToTab(tabControl, bgColor, textDialog.Color);
                        }
                    }
                }
            }
        }

        private static void ApplyColorsToTab(TabControl tabControl, Color bgColor, Color textColor)
        {
            if (tabControl.SelectedTab != null)
            {
                string characterName = tabControl.SelectedTab.Tag as string;
                var character = plugin.CharacterData.FirstOrDefault(c => c.name == characterName);
                if (character != null)
                {
                    character.TabColor = ColorTranslator.ToHtml(bgColor);
                    character.TabTextColor = ColorTranslator.ToHtml(textColor);

                    tabControl.SelectedTab.BackColor = bgColor;
                    tabControl.SelectedTab.ForeColor = textColor;
                    tabControl.Invalidate();
                    LoadSave.SaveSettings();
                }
            }
        }

        private static Color GetContrastColor(Color bgColor)
        {
            // WCAG 2.0 contrast calculation
            double luminance = (0.2126 * bgColor.R + 0.7152 * bgColor.G + 0.0722 * bgColor.B) / 255;
            return luminance > 0.4 ? Color.Black : Color.White;
        }


        public static void DrawTab(DrawItemEventArgs e, TabControl tabControl)
        {
            TabPage page = tabControl.TabPages[e.Index];
            Color baseColor = Color.White;  // Default to white if no color is set

            // Retrieve the color from TabPage's Tag (if it's set)
            if (page.Tag is string tabColorString && !string.IsNullOrEmpty(tabColorString))
            {
                try
                {
                    baseColor = ColorTranslator.FromHtml(tabColorString);  // Convert the string to a Color
                }
                catch
                {
                    baseColor = Color.White; // In case of an error in conversion, fallback to white
                }
            }

            using (SolidBrush brush = new SolidBrush(baseColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);  // Fill the background with the color
            }

            bool isSelected = (tabControl.SelectedIndex == e.Index);
            TextRenderer.DrawText(e.Graphics, page.Text, tabControl.Font, e.Bounds, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        public static void UpdateFilterMenuItems(string currentFilter, ToolStripMenuItem filterMenuItem)
        {
            if (currentFilter == "All Tabs")
            {
                filterMenuItem.Text = "Filter All Tabs";
            }
            else if (currentFilter == "Active Tabs")
            {
                filterMenuItem.Text = "Filter Active Tabs";
            }
            else if (currentFilter == "Archived Tabs")
            {
                filterMenuItem.Text = "Filter Archived Tabs";
            }
        }

        private void ResetAllTabColors()
        {
            foreach (TabPage tabPage in tabControl1.TabPages)
            {
                tabPage.BackColor = SystemColors.Control;

                // Optionally reset text color as well
                tabPage.ForeColor = SystemColors.ControlText;

                // You can also clear any custom color stored in the tab's tag if needed
                var characterName = tabPage.Tag as string;
                if (!string.IsNullOrEmpty(characterName))
                {
                    var entries = plugin.CharacterData
                        .Where(c => c.name.Equals(characterName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var entry in entries)
                    {
                        entry.TabColor = string.Empty;  // Clear the saved color
                        entry.TabTextColor = string.Empty;
                    }
                }
            }

            tabControl1.Invalidate();  // Redraw the tabs

            // Optionally save the changes
            LoadSave.SaveSettings();  // Ensure settings are saved after reset
        }

        private void ResetSingleSelectedTabColor()
        {
            if (tabControl1.SelectedTab != null)
            {
                // Reset the background color and text color to the default (Control color)
                tabControl1.SelectedTab.BackColor = SystemColors.Control;
                tabControl1.SelectedTab.ForeColor = SystemColors.ControlText;

                // Optionally clear the custom color stored in the tab's tag
                var characterName = tabControl1.SelectedTab.Tag as string;
                if (!string.IsNullOrEmpty(characterName))
                {
                    var entries = plugin.CharacterData
                        .Where(c => c.name.Equals(characterName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var entry in entries)
                    {
                        entry.TabColor = string.Empty;  // Clear the saved color
                        entry.TabTextColor = string.Empty;  // Clear the saved text color (if applicable)
                    }
                }

                // Redraw the selected tab
                tabControl1.SelectedTab.Invalidate();

                // Optionally save the changes
                LoadSave.SaveSettings();  // Ensure settings are saved after reset
            }
        }


        private void filtarAlltoolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentFilter = "All Tabs";
            UpdateFilterMenuItems(currentFilter, filterToolStripMenuItem);
            BindData();
        }

        private void filterActivetoolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentFilter = "Active Tabs";
            UpdateFilterMenuItems(currentFilter, filterToolStripMenuItem);
            BindData();
        }

        private void filterArchivedtoolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentFilter = "Archived Tabs";
            UpdateFilterMenuItems(currentFilter, filterToolStripMenuItem);
            BindData();
        }

        private void TabControl1_MouseUp(object sender, MouseEventArgs e)
        {
            HandleTabControlMouseUp(tabControl1, e);
        }

        private void ToggleArchiveMenuItem_Click(object sender, EventArgs e)
        {
            ToggleArchiveForSelectedTab(tabControl1);
        }

        private void ResetAllTabColors_Click(object sender, EventArgs e)
        {
            ResetAllTabColors();  // Call the method to reset all tab colors
        }


        private void colorTabToolStrip_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < tabControl1.TabPages.Count; i++)
            {
                ChangeTabColor(tabControl1);
                break;
            }
        }

        private void ResetSelectedTabColorButton_Click(object sender, EventArgs e)
        {
            ResetSingleSelectedTabColor();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
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
            label1 = new Label();
            infolabel = new Label();
            cboCharacters = new ComboBox();
            btnRemoveCharacter = new Button();
            btnFindPrev = new Button();
            txtSearch = new TextBox();
            lblFound = new Label();
            lblSearch = new Label();
            btnExpand = new Button();
            btnCollapse = new Button();
            btnFindNext = new Button();
            btnReset = new Button();
            splitContainer1 = new SplitContainer();
            menuStrip = new MenuStrip();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            toolStripDarkMode = new ToolStripMenuItem();
            toolStripFamily = new ToolStripMenuItem();
            toolStripMultilineTabs = new ToolStripMenuItem();
            toolStripPockets = new ToolStripMenuItem();
            toolStripAlwaysTop = new ToolStripMenuItem();
            commandsToolStripMenuItem = new ToolStripMenuItem();
            toolStripScan = new ToolStripMenuItem();
            toolStripReload = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripWiki = new ToolStripMenuItem();
            toolStripExport = new ToolStripMenuItem();
            toolStripExportAll = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            resetTabColorsMenuItem = new ToolStripMenuItem();
            resetSingleTabStripMenu = new ToolStripMenuItem();
            colorTabToolStrip = new ToolStripMenuItem();
            filterToolStripMenuItem = new ToolStripMenuItem();
            filtarAlltoolStripMenuItem = new ToolStripMenuItem();
            filterActivetoolStripMenuItem = new ToolStripMenuItem();
            filterArchivedtoolStripMenuItem = new ToolStripMenuItem();
            toolStripContainer1 = new ToolStripContainer();
            listBox_Menu.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            panel1.SuspendLayout();
            ((ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            menuStrip.SuspendLayout();
            toolStripContainer1.ContentPanel.SuspendLayout();
            toolStripContainer1.TopToolStripPanel.SuspendLayout();
            toolStripContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // tv
            // 
            tv.BorderStyle = BorderStyle.None;
            tv.Font = new Font("Segoe UI Semibold", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            tv.Location = new Point(0, 0);
            tv.Margin = new Padding(4, 3, 4, 3);
            tv.Name = "tv";
            tv.ShowNodeToolTips = true;
            tv.Size = new Size(11, 30);
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
            tabControl1.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            tabControl1.Location = new Point(0, 0);
            tabControl1.Margin = new Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(726, 271);
            tabControl1.TabIndex = 16;
            // 
            // lblMatches
            // 
            lblMatches.AllowDrop = true;
            lblMatches.BackColor = SystemColors.Control;
            lblMatches.ContextMenuStrip = listBox_Menu;
            lblMatches.Dock = DockStyle.Fill;
            lblMatches.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            lblMatches.FormattingEnabled = true;
            lblMatches.HorizontalScrollbar = true;
            lblMatches.ItemHeight = 17;
            lblMatches.Location = new Point(0, 0);
            lblMatches.Margin = new Padding(4, 3, 4, 3);
            lblMatches.Name = "lblMatches";
            lblMatches.SelectionMode = SelectionMode.MultiExtended;
            lblMatches.Size = new Size(726, 279);
            lblMatches.TabIndex = 17;
            lblMatches.MouseDoubleClick += LblMatches_MouseDoubleClick;
            lblMatches.MouseDown += LblMatches_MouseDown;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel1.Controls.Add(panel1, 0, 0);
            tableLayoutPanel1.Controls.Add(splitContainer1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(4, 3, 4, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 79F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new Size(734, 641);
            tableLayoutPanel1.TabIndex = 18;
            // 
            // panel1
            // 
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel1.Controls.Add(label1);
            panel1.Controls.Add(infolabel);
            panel1.Controls.Add(cboCharacters);
            panel1.Controls.Add(btnRemoveCharacter);
            panel1.Controls.Add(btnFindPrev);
            panel1.Controls.Add(txtSearch);
            panel1.Controls.Add(lblFound);
            panel1.Controls.Add(lblSearch);
            panel1.Controls.Add(btnExpand);
            panel1.Controls.Add(btnCollapse);
            panel1.Controls.Add(btnFindNext);
            panel1.Controls.Add(btnReset);
            panel1.Location = new Point(4, 3);
            panel1.Margin = new Padding(4, 3, 4, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(726, 73);
            panel1.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(1, 1);
            label1.Name = "label1";
            label1.Size = new Size(200, 15);
            label1.TabIndex = 17;
            label1.Text = "Right click tabs to archive or activate";
            // 
            // infolabel
            // 
            infolabel.AutoSize = true;
            infolabel.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            infolabel.Location = new Point(2, 51);
            infolabel.Margin = new Padding(4, 0, 4, 0);
            infolabel.Name = "infolabel";
            infolabel.Size = new Size(245, 17);
            infolabel.TabIndex = 16;
            infolabel.Text = "T = Total Item Count  M = Total Matches";
            // 
            // cboCharacters
            // 
            cboCharacters.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cboCharacters.FormattingEnabled = true;
            cboCharacters.Location = new Point(607, 7);
            cboCharacters.Margin = new Padding(4, 3, 4, 3);
            cboCharacters.Name = "cboCharacters";
            cboCharacters.Size = new Size(109, 23);
            cboCharacters.TabIndex = 15;
            // 
            // btnRemoveCharacter
            // 
            btnRemoveCharacter.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnRemoveCharacter.AutoSize = true;
            btnRemoveCharacter.ForeColor = SystemColors.ControlText;
            btnRemoveCharacter.Location = new Point(607, 41);
            btnRemoveCharacter.Margin = new Padding(4, 3, 4, 3);
            btnRemoveCharacter.MinimumSize = new Size(109, 25);
            btnRemoveCharacter.Name = "btnRemoveCharacter";
            btnRemoveCharacter.Size = new Size(109, 25);
            btnRemoveCharacter.TabIndex = 14;
            btnRemoveCharacter.Text = "Remove";
            btnRemoveCharacter.UseVisualStyleBackColor = true;
            btnRemoveCharacter.Click += BtnRemoveCharacter_Click;
            // 
            // btnFindPrev
            // 
            btnFindPrev.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnFindPrev.AutoSize = true;
            btnFindPrev.ForeColor = SystemColors.ControlText;
            btnFindPrev.Location = new Point(417, 7);
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
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.Location = new Point(47, 26);
            txtSearch.Margin = new Padding(4, 3, 4, 3);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(266, 23);
            txtSearch.TabIndex = 1;
            txtSearch.KeyDown += TxtSearch_KeyDown;
            // 
            // lblFound
            // 
            lblFound.AutoSize = true;
            lblFound.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            lblFound.Location = new Point(261, 50);
            lblFound.Margin = new Padding(4, 0, 4, 0);
            lblFound.Name = "lblFound";
            lblFound.Size = new Size(58, 17);
            lblFound.TabIndex = 0;
            lblFound.Text = "Found: 0";
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = true;
            lblSearch.Location = new Point(2, 30);
            lblSearch.Margin = new Padding(4, 0, 4, 0);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(45, 15);
            lblSearch.TabIndex = 0;
            lblSearch.Text = "Search:";
            // 
            // btnExpand
            // 
            btnExpand.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExpand.AutoSize = true;
            btnExpand.ForeColor = SystemColors.ControlText;
            btnExpand.Location = new Point(511, 7);
            btnExpand.Margin = new Padding(4, 3, 4, 3);
            btnExpand.Name = "btnExpand";
            btnExpand.Size = new Size(88, 29);
            btnExpand.TabIndex = 6;
            btnExpand.Text = "Expand All";
            btnExpand.UseVisualStyleBackColor = true;
            btnExpand.Click += BtnExpand_Click;
            // 
            // btnCollapse
            // 
            btnCollapse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCollapse.AutoSize = true;
            btnCollapse.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnCollapse.ForeColor = SystemColors.ControlText;
            btnCollapse.Location = new Point(511, 38);
            btnCollapse.Margin = new Padding(4, 3, 4, 3);
            btnCollapse.Name = "btnCollapse";
            btnCollapse.Size = new Size(88, 29);
            btnCollapse.TabIndex = 7;
            btnCollapse.Text = "Collapse All";
            btnCollapse.UseVisualStyleBackColor = true;
            btnCollapse.Click += BtnCollapse_Click;
            // 
            // btnFindNext
            // 
            btnFindNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnFindNext.AutoSize = true;
            btnFindNext.ForeColor = SystemColors.ControlText;
            btnFindNext.Location = new Point(417, 38);
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
            btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReset.AutoSize = true;
            btnReset.ForeColor = SystemColors.ControlText;
            btnReset.Location = new Point(327, 24);
            btnReset.Margin = new Padding(4, 3, 4, 3);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(75, 25);
            btnReset.TabIndex = 5;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Visible = false;
            btnReset.Click += BtnReset_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(4, 82);
            splitContainer1.Margin = new Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tabControl1);
            splitContainer1.Panel1.Controls.Add(tv);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(lblMatches);
            splitContainer1.Size = new Size(726, 556);
            splitContainer1.SplitterDistance = 271;
            splitContainer1.SplitterWidth = 6;
            splitContainer1.TabIndex = 19;
            // 
            // menuStrip
            // 
            menuStrip.BackColor = SystemColors.Control;
            menuStrip.Dock = DockStyle.None;
            menuStrip.GripMargin = new Padding(0);
            menuStrip.Items.AddRange(new ToolStripItem[] { optionsToolStripMenuItem, commandsToolStripMenuItem, filterToolStripMenuItem });
            menuStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Size = new Size(736, 24);
            menuStrip.TabIndex = 19;
            menuStrip.Text = "menuStrip1";
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.BackColor = SystemColors.Control;
            optionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripDarkMode, toolStripFamily, toolStripMultilineTabs, toolStripPockets, toolStripAlwaysTop });
            optionsToolStripMenuItem.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            optionsToolStripMenuItem.ForeColor = SystemColors.ControlText;
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new Size(61, 20);
            optionsToolStripMenuItem.Text = "Options";
            // 
            // toolStripDarkMode
            // 
            toolStripDarkMode.BackColor = SystemColors.Control;
            toolStripDarkMode.CheckOnClick = true;
            toolStripDarkMode.ForeColor = SystemColors.ControlText;
            toolStripDarkMode.Name = "toolStripDarkMode";
            toolStripDarkMode.Size = new Size(199, 22);
            toolStripDarkMode.Text = "Dark Mode";
            toolStripDarkMode.CheckedChanged += DarkMode_CheckedChanged;
            // 
            // toolStripFamily
            // 
            toolStripFamily.BackColor = SystemColors.Control;
            toolStripFamily.CheckOnClick = true;
            toolStripFamily.ForeColor = SystemColors.ControlText;
            toolStripFamily.Name = "toolStripFamily";
            toolStripFamily.Size = new Size(199, 22);
            toolStripFamily.Text = "Family Vault";
            toolStripFamily.CheckedChanged += Family_CheckedChanged;
            // 
            // toolStripMultilineTabs
            // 
            toolStripMultilineTabs.BackColor = SystemColors.Control;
            toolStripMultilineTabs.CheckOnClick = true;
            toolStripMultilineTabs.ForeColor = SystemColors.ControlText;
            toolStripMultilineTabs.Name = "toolStripMultilineTabs";
            toolStripMultilineTabs.Size = new Size(199, 22);
            toolStripMultilineTabs.Text = "Multiline Tabs";
            toolStripMultilineTabs.CheckedChanged += MultiLineTabs_CheckedChanged;
            // 
            // toolStripPockets
            // 
            toolStripPockets.BackColor = SystemColors.Control;
            toolStripPockets.Checked = true;
            toolStripPockets.CheckOnClick = true;
            toolStripPockets.CheckState = CheckState.Checked;
            toolStripPockets.ForeColor = SystemColors.ControlText;
            toolStripPockets.Name = "toolStripPockets";
            toolStripPockets.Size = new Size(199, 22);
            toolStripPockets.Text = "Pocket inside Container";
            toolStripPockets.CheckedChanged += Pockets_CheckedChanged;
            // 
            // toolStripAlwaysTop
            // 
            toolStripAlwaysTop.CheckOnClick = true;
            toolStripAlwaysTop.Name = "toolStripAlwaysTop";
            toolStripAlwaysTop.Size = new Size(199, 22);
            toolStripAlwaysTop.Text = "Always On top";
            toolStripAlwaysTop.CheckedChanged += ToolStripAlwaysTop_CheckedChanged;
            // 
            // commandsToolStripMenuItem
            // 
            commandsToolStripMenuItem.BackColor = SystemColors.Control;
            commandsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripScan, toolStripReload, toolStripSeparator1, toolStripWiki, toolStripExport, toolStripExportAll, toolStripSeparator2, resetTabColorsMenuItem, resetSingleTabStripMenu, colorTabToolStrip });
            commandsToolStripMenuItem.ForeColor = SystemColors.ControlText;
            commandsToolStripMenuItem.Name = "commandsToolStripMenuItem";
            commandsToolStripMenuItem.Size = new Size(81, 20);
            commandsToolStripMenuItem.Text = "Commands";
            // 
            // toolStripScan
            // 
            toolStripScan.BackColor = SystemColors.Control;
            toolStripScan.ForeColor = SystemColors.ControlText;
            toolStripScan.Name = "toolStripScan";
            toolStripScan.Size = new Size(221, 22);
            toolStripScan.Text = "Scan Character";
            toolStripScan.Click += Scan_Click;
            // 
            // toolStripReload
            // 
            toolStripReload.BackColor = SystemColors.Control;
            toolStripReload.ForeColor = SystemColors.ControlText;
            toolStripReload.Name = "toolStripReload";
            toolStripReload.Size = new Size(221, 22);
            toolStripReload.Text = "Reload File";
            toolStripReload.Click += Reload_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(218, 6);
            // 
            // toolStripWiki
            // 
            toolStripWiki.BackColor = SystemColors.Control;
            toolStripWiki.ForeColor = SystemColors.ControlText;
            toolStripWiki.Name = "toolStripWiki";
            toolStripWiki.Size = new Size(221, 22);
            toolStripWiki.Text = "Wiki Lookup";
            toolStripWiki.Click += Wiki_Click;
            // 
            // toolStripExport
            // 
            toolStripExport.Name = "toolStripExport";
            toolStripExport.Size = new Size(221, 22);
            toolStripExport.Text = "Export Current";
            toolStripExport.Click += Export_Click;
            // 
            // toolStripExportAll
            // 
            toolStripExportAll.Name = "toolStripExportAll";
            toolStripExportAll.Size = new Size(221, 22);
            toolStripExportAll.Text = "Export All";
            toolStripExportAll.Click += ExportAll_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(218, 6);
            // 
            // resetTabColorsMenuItem
            // 
            resetTabColorsMenuItem.Name = "resetTabColorsMenuItem";
            resetTabColorsMenuItem.Size = new Size(221, 22);
            resetTabColorsMenuItem.Text = "Reset All Tab Colors";
            resetTabColorsMenuItem.Click += ResetAllTabColors_Click;
            // 
            // resetSingleTabStripMenu
            // 
            resetSingleTabStripMenu.Name = "resetSingleTabStripMenu";
            resetSingleTabStripMenu.Size = new Size(221, 22);
            resetSingleTabStripMenu.Text = "Reset Selected Tab Colors";
            resetSingleTabStripMenu.Click += ResetSelectedTabColorButton_Click;
            // 
            // colorTabToolStrip
            // 
            colorTabToolStrip.Name = "colorTabToolStrip";
            colorTabToolStrip.Size = new Size(221, 22);
            colorTabToolStrip.Text = "Change Selected Tab Colors";
            colorTabToolStrip.Click += colorTabToolStrip_Click;
            // 
            // filterToolStripMenuItem
            // 
            filterToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { filtarAlltoolStripMenuItem, filterActivetoolStripMenuItem, filterArchivedtoolStripMenuItem });
            filterToolStripMenuItem.Name = "filterToolStripMenuItem";
            filterToolStripMenuItem.Size = new Size(72, 20);
            filterToolStripMenuItem.Text = "Filter Tabs";
            // 
            // filtarAlltoolStripMenuItem
            // 
            filtarAlltoolStripMenuItem.Name = "filtarAlltoolStripMenuItem";
            filtarAlltoolStripMenuItem.Size = new Size(121, 22);
            filtarAlltoolStripMenuItem.Text = "All";
            filtarAlltoolStripMenuItem.Click += filtarAlltoolStripMenuItem_Click;
            // 
            // filterActivetoolStripMenuItem
            // 
            filterActivetoolStripMenuItem.Name = "filterActivetoolStripMenuItem";
            filterActivetoolStripMenuItem.Size = new Size(121, 22);
            filterActivetoolStripMenuItem.Text = "Active";
            filterActivetoolStripMenuItem.Click += filterActivetoolStripMenuItem_Click;
            // 
            // filterArchivedtoolStripMenuItem
            // 
            filterArchivedtoolStripMenuItem.Name = "filterArchivedtoolStripMenuItem";
            filterArchivedtoolStripMenuItem.Size = new Size(121, 22);
            filterArchivedtoolStripMenuItem.Text = "Archived";
            filterArchivedtoolStripMenuItem.Click += filterArchivedtoolStripMenuItem_Click;
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.ContentPanel
            // 
            toolStripContainer1.ContentPanel.BorderStyle = BorderStyle.FixedSingle;
            toolStripContainer1.ContentPanel.Controls.Add(tableLayoutPanel1);
            toolStripContainer1.ContentPanel.Size = new Size(736, 643);
            toolStripContainer1.Dock = DockStyle.Fill;
            toolStripContainer1.Location = new Point(0, 0);
            toolStripContainer1.Name = "toolStripContainer1";
            toolStripContainer1.Size = new Size(736, 667);
            toolStripContainer1.TabIndex = 19;
            toolStripContainer1.Text = "toolStripContainer1";
            // 
            // toolStripContainer1.TopToolStripPanel
            // 
            toolStripContainer1.TopToolStripPanel.Controls.Add(menuStrip);
            // 
            // InventoryViewForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(736, 667);
            Controls.Add(toolStripContainer1);
            MainMenuStrip = menuStrip;
            Margin = new Padding(4, 3, 4, 3);
            MinimumSize = new Size(746, 693);
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
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            toolStripContainer1.ContentPanel.ResumeLayout(false);
            toolStripContainer1.ContentPanel.PerformLayout();
            toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.PerformLayout();
            toolStripContainer1.ResumeLayout(false);
            toolStripContainer1.PerformLayout();
            ResumeLayout(false);
        }
    }
}
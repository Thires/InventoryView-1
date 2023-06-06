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

namespace InventoryView
{
    public class InventoryViewForm : Form
    {
        private readonly List<TreeNode> searchMatches = new List<TreeNode>();
        private TreeNode currentMatch;
        private IContainer components;
        private TreeView tv;
        private readonly ToolTip toolTip = new ToolTip();
        private ContextMenuStrip listBox_Menu;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem wikiToolStripMenuItem;
        private ToolStripMenuItem copyAllToolStripMenuItem;
        private bool clickSearch = false;
        private ToolStripMenuItem copySelectedToolStripMenuItem;
        //private readonly ListBox listBox = new ListBox();

        // Create a new list to store the search matches for each TreeView control
        private readonly List<InventoryViewForm.TreeViewSearchMatches> treeViewSearchMatchesList = new List<InventoryViewForm.TreeViewSearchMatches>();
        // Create a list to store the hidden tab pages and their original positions
        private readonly List<(TabPage tabPage, int index)> hiddenTabPages = new List<(TabPage tabPage, int index)>();
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
        private CheckedListBox chkCharacters;

        private List<TreeNode> removedNodes = new List<TreeNode>();
        private static string basePath = Application.StartupPath;

        public InventoryViewForm() => InitializeComponent();

        private void InventoryViewForm_Load(object sender, EventArgs e)
        {
            BindData();
            basePath = Class1._host.get_Variable("PluginPath");

            // Load the character data
            Class1.LoadSettings();

            // Get a list of distinct character names from the characterData list
            List<string> characterNames = Class1.characterData.Select(c => c.name).Distinct().ToList();

            // Sort the character names
            characterNames.Sort();

            // Add the character names to the cboCharacters control
            cboCharacters.Items.Clear();
            cboCharacters.Items.AddRange(characterNames.ToArray());
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
                var tv = new TreeView();
                tv.Dock = DockStyle.Fill;
                tabPage.Controls.Add(tv);

                // Clear existing nodes
                tv.Nodes.Clear();

                // Create a new ContextMenuStrip for the TreeView control
                var contextMenuStrip = CreateTreeViewContextMenuStrip(tv);
                tv.ContextMenuStrip = contextMenuStrip;

                // Add the character's data to the TreeView and get the total item count
                int totalCount = AddCharacterDataToTreeView(tv, character);

                // Update the tab page text with the total item count
                tabPage.Text = $"{character} (Total: {totalCount})";
            }
        }

        private List<string> GetDistinctCharacters()
        {
            var characters = Class1.characterData.Select(tbl => tbl.name).Distinct().ToList();
            characters.Sort();
            return characters;
        }

        private int AddCharacterDataToTreeView(TreeView tv, string character)
        {
            int totalCount = 0;

            TreeNode charNode = tv.Nodes.Add(character);
            foreach (var source in Class1.characterData.Where(tbl => tbl.name == character))
            {
                TreeNode sourceNode = charNode.Nodes.Add(source.source);
                sourceNode.ToolTipText = sourceNode.FullPath;

                totalCount += PopulateTree(sourceNode, source.items);
            }

            return totalCount;
        }

        private int PopulateTree(TreeNode treeNode, List<ItemData> itemList)
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

                if (itemData.items.Count<ItemData>() > 0)
                    totalCount += PopulateTree(treeNode1, itemData.items);
            }

            return totalCount;
        }

        private ContextMenuStrip CreateTreeViewContextMenuStrip(TreeView tv)
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
                    Clipboard.SetText(Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s", ""));
            };
            contextMenuStrip.Items.Add(copyTextToolStripMenuItem);

            // Add the "Copy Branch" option
            var copyBranchToolStripMenuItem = new ToolStripMenuItem("Copy Branch");
            copyBranchToolStripMenuItem.Click += (sender, e) =>
            {
                if (tv.SelectedNode != null)
                {
                    List<string> branchText = new List<string>();
                    branchText.Add(Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s", ""));
                    CopyBranchText(tv.SelectedNode.Nodes, branchText, 1);
                    Clipboard.SetText(string.Join("\r\n", branchText.ToArray()));
                }
            };
            contextMenuStrip.Items.Add(copyBranchToolStripMenuItem);

            return contextMenuStrip;
        }

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            // Reset the found count value
            lblFound.Text = "Found: 0";

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
                        tabPage.Text = $"{tabPage.Text.Split(' ')[0]} (Matches: {searchCount})";
                    else
                    {
                        tabPage.Text = $"{tabPage.Text.Split(' ')[0]} (Total: {totalCount})";

                        // Remove the tab page from the TabControl if no matching items were found and it's not already hidden
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
                    // Reload the data if no matching items were found

                    // Call BindData to make sure all tabs are visible and collapsed
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

        private bool SearchTree(TreeView treeView, TreeNodeCollection nodes, List<TreeNode> searchMatches, ref int searchCount, ref int totalCount)
        {
            bool isMatchFound = false;
            foreach (TreeNode node in nodes)
            {
                totalCount++;

                // Reset the node's background color
                node.BackColor = Color.White;

                // Reset the node's foreground color
                node.ForeColor = treeView.ForeColor;

                // Search the node's child nodes
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
                if (node.Text.Contains(txtSearch.Text))
                {
                    // Highlight the node and add it to the list of search matches
                    node.BackColor = Color.Yellow;
                    searchMatches.Add(node);
                    ++searchCount;
                    isMatchFound = true;

                    // Add the node's text to the lblMatches ListBox control
                    if (tabControl1.SelectedTab.Controls[0] == treeView)
                    {
                        node.Text = Regex.Replace(node.Text, @"\(\d+\)\s", "");
                        if (node.Text[node.Text.Length - 1] == '.')
                            node.Text = node.Text.TrimEnd('.');
                        lblMatches.Items.Add(node.Text);
                    }
                }
                else
                {
                    // Change the color of non-matching nodes
                    node.ForeColor = Color.LightGray;
                }
            }
            return isMatchFound;
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
                int num = (int)MessageBox.Show("Select an item to lookup.");
            }
            else
                OpenWikiPage((string)lblMatches.SelectedItem);
        }

        private void OpenWikiPage(string text)
        {
            if (Class1._host.InterfaceVersion == 4)
                Class1._host.SendText(string.Format("#browser https://elanthipedia.play.net/index.php?search={0}", Uri.EscapeDataString(Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)", ""))));
            else
                Process.Start(new ProcessStartInfo(string.Format("https://elanthipedia.play.net/index.php?search={0}", Regex.Replace(text, @"\(\d+\)\s|\s\(closed\)", ""))) { UseShellExecute = true });
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
                treeViewSearchMatches.CurrentMatch.BackColor = Color.LightBlue;
            }
        }


        private void BtnScan_Click(object sender, EventArgs e)
        {
            Class1._host.SendText("/InventoryView scan");
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
                        XmlDocument doc = new XmlDocument();
                        string xmlPath = Path.Combine(basePath, "InventoryView.xml");
                        doc.Load(xmlPath);

                        // Find all CharacterData elements with the specified name element value
                        XmlNodeList characterNodes = doc.SelectNodes($"/ArrayOfCharacterData/CharacterData[name='{characterName}']");

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
                            Class1._host.EchoText($"Could not find any CharacterData elements with a name element value of '{characterName}' in the XML file.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle the exception here
                        Class1._host.EchoText($"An exception occurred: {ex.Message}");
                    }
                }
            }
            else
            {
                Class1._host.EchoText("Please select a character name.");
            }
        }


        private void BtnReset_Click(object sender, EventArgs e)
        {
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
            Class1.LoadSettings();
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
                XmlDocument doc = new XmlDocument();
                string xmlPath = Path.Combine(basePath, "InventoryView.xml");
                doc.Load(xmlPath);

                // Find all CharacterData elements
                XmlNodeList characterNodes = doc.SelectNodes("/ArrayOfCharacterData/CharacterData");

                // Create a list to store the character names
                List<string> characterNames = new List<string>();

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
            currentMatch = null;
            txtSearch.Text = "";
            // Set clickSearch to true to indicate that a search has been performed
            clickSearch = false;
            // Set focus back to the txtSearch control
            txtSearch.Focus();
        }


        private void ExportBranchToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> branchText = new List<string>();
            branchText.Add(Regex.Replace(tv.SelectedNode.Text, @"\(\d+\)\s", ""));
            CopyBranchText(tv.SelectedNode.Nodes, branchText, 1);
            Clipboard.SetText(string.Join("\r\n", branchText.ToArray()));
        }

        private void CopyBranchText(TreeNodeCollection nodes, List<string> branchText, int level)
        {
            foreach (TreeNode node in nodes)
            {
                branchText.Add(new string('\t', level) + Regex.Replace(node.Text, @"\(\d+\)\s", ""));
                CopyBranchText(node.Nodes, branchText, level + 1);
            }
        }

        private void ListBox_Copy_Click(object sender, EventArgs e)
        {
            if (lblMatches.SelectedItem == null)
            {
                int num = (int)MessageBox.Show("Select an item to copy.");
            }
            else
            {
                StringBuilder txt = new StringBuilder();
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
                int num = (int)MessageBox.Show("Must search first to copy all.");
            }
            else
            {
                StringBuilder buffer = new StringBuilder();

                for (int i = 0; i < lblMatches.Items.Count; i++)
                {
                    buffer.Append(lblMatches.Items[i].ToString());
                    buffer.Append("\n");
                }
                Clipboard.SetText(buffer.ToString());
            }
        }

        public void ListBox_Copy_All_Selected_Click(Object sender, EventArgs e)
        {
            if (lblMatches.SelectedItem == null)
            {
                int num = (int)MessageBox.Show("Select items to copy.");
            }
            else
            {
                StringBuilder buffer = new StringBuilder();

                for (int i = 0; i < lblMatches.SelectedItems.Count; i++)
                {
                    buffer.Append(lblMatches.SelectedItems[i].ToString());
                    buffer.Append("\n");
                }
                Clipboard.SetText(buffer.ToString());
            }
        }

        private void Lb1_MouseDown(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control || (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.ShiftKey || e.Button == MouseButtons.Left))
            {
                lblMatches.SelectionMode = SelectionMode.MultiExtended;
            }
            else if (e.Button == MouseButtons.Left)
            {
                lblMatches.SelectionMode = SelectionMode.One;
            }
        }

        private void Lb1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (lblMatches.SelectedItem != null)
            {
                if (Control.ModifierKeys == Keys.Control || (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.ShiftKey || e.Button == MouseButtons.Right))
                {
                    lblMatches.SelectionMode = SelectionMode.MultiExtended;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    lblMatches.SelectionMode = SelectionMode.One;
                }
            }
        }

        private void Tv_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            Point point = new Point(e.X, e.Y);
            TreeNode nodeAt = tv.GetNodeAt(point);
            if (nodeAt == null)
                return;
            tv.SelectedNode = nodeAt;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV file|*.csv";
            saveFileDialog.Title = "Save the CSV file";
            int num1 = (int)saveFileDialog.ShowDialog();
            if (!(saveFileDialog.FileName != ""))
                return;
            using (StreamWriter text = File.CreateText(saveFileDialog.FileName))
            {
                List<InventoryViewForm.ExportData> list = new List<InventoryViewForm.ExportData>();

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
            int num2 = (int)MessageBox.Show("Export Complete.");
        }

        private string CleanCSV(string data)
        {
            if (!data.Contains(","))
                return data;
            return !data.Contains("\"") ? string.Format("\"{0}\"", (object)data) : string.Format("\"{0}\"", (object)data.Replace("\"", "\"\""));
        }

        private void ExportBranch(
          TreeNodeCollection nodes,
          List<InventoryViewForm.ExportData> list,
          int level)
        {
            foreach (TreeNode node in nodes)
            {
                InventoryViewForm.ExportData exportData = new InventoryViewForm.ExportData()
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tv = new System.Windows.Forms.TreeView();
            this.listBox_Menu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wikiToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copySelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.lblMatches = new System.Windows.Forms.ListBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.cboCharacters = new System.Windows.Forms.ComboBox();
            this.btnRemoveCharacter = new System.Windows.Forms.Button();
            this.btnFindPrev = new System.Windows.Forms.Button();
            this.chkCharacters = new System.Windows.Forms.CheckedListBox();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lblFound = new System.Windows.Forms.Label();
            this.lblSearch = new System.Windows.Forms.Label();
            this.btnSearch = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnExpand = new System.Windows.Forms.Button();
            this.btnReload = new System.Windows.Forms.Button();
            this.btnCollapse = new System.Windows.Forms.Button();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnWiki = new System.Windows.Forms.Button();
            this.btnFindNext = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox_Menu.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tv
            // 
            this.tv.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tv.Location = new System.Drawing.Point(2, 2);
            this.tv.Name = "tv";
            this.tv.ShowNodeToolTips = true;
            this.tv.Size = new System.Drawing.Size(5, 28);
            this.tv.TabIndex = 10;
            this.tv.Visible = false;
            this.tv.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Tv_MouseUp);
            // 
            // listBox_Menu
            // 
            this.listBox_Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem,
            this.wikiToolStripMenuItem,
            this.copyAllToolStripMenuItem,
            this.copySelectedToolStripMenuItem});
            this.listBox_Menu.Name = "listBox_Menu";
            this.listBox_Menu.Size = new System.Drawing.Size(167, 92);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.copyToolStripMenuItem.Text = "Copy Selected";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.ListBox_Copy_Click);
            // 
            // wikiToolStripMenuItem
            // 
            this.wikiToolStripMenuItem.Name = "wikiToolStripMenuItem";
            this.wikiToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.wikiToolStripMenuItem.Text = "Wiki Selected";
            this.wikiToolStripMenuItem.Click += new System.EventHandler(this.Listbox_Wiki_Click);
            // 
            // copyAllToolStripMenuItem
            // 
            this.copyAllToolStripMenuItem.Name = "copyAllToolStripMenuItem";
            this.copyAllToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.copyAllToolStripMenuItem.Text = "Copy All";
            this.copyAllToolStripMenuItem.Click += new System.EventHandler(this.ListBox_Copy_All_Click);
            // 
            // copySelectedToolStripMenuItem
            // 
            this.copySelectedToolStripMenuItem.Name = "copySelectedToolStripMenuItem";
            this.copySelectedToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.copySelectedToolStripMenuItem.Text = "Copy All Selected";
            this.copySelectedToolStripMenuItem.Click += new System.EventHandler(this.ListBox_Copy_All_Selected_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(590, 408);
            this.tabControl1.TabIndex = 16;
            // 
            // lblMatches
            // 
            this.lblMatches.AllowDrop = true;
            this.lblMatches.ContextMenuStrip = this.listBox_Menu;
            this.lblMatches.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMatches.FormattingEnabled = true;
            this.lblMatches.Location = new System.Drawing.Point(0, 0);
            this.lblMatches.Name = "lblMatches";
            this.lblMatches.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lblMatches.Size = new System.Drawing.Size(425, 408);
            this.lblMatches.TabIndex = 17;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.splitContainer1, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1015, 478);
            this.tableLayoutPanel1.TabIndex = 18;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.cboCharacters);
            this.panel1.Controls.Add(this.btnRemoveCharacter);
            this.panel1.Controls.Add(this.btnFindPrev);
            this.panel1.Controls.Add(this.chkCharacters);
            this.panel1.Controls.Add(this.txtSearch);
            this.panel1.Controls.Add(this.lblFound);
            this.panel1.Controls.Add(this.lblSearch);
            this.panel1.Controls.Add(this.btnSearch);
            this.panel1.Controls.Add(this.btnExport);
            this.panel1.Controls.Add(this.btnExpand);
            this.panel1.Controls.Add(this.btnReload);
            this.panel1.Controls.Add(this.btnCollapse);
            this.panel1.Controls.Add(this.btnScan);
            this.panel1.Controls.Add(this.btnWiki);
            this.panel1.Controls.Add(this.btnFindNext);
            this.panel1.Controls.Add(this.btnReset);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1019, 58);
            this.panel1.TabIndex = 0;
            // 
            // cboCharacters
            // 
            this.cboCharacters.FormattingEnabled = true;
            this.cboCharacters.Location = new System.Drawing.Point(914, 5);
            this.cboCharacters.Name = "cboCharacters";
            this.cboCharacters.Size = new System.Drawing.Size(94, 21);
            this.cboCharacters.TabIndex = 15;
            // 
            // btnRemoveCharacter
            // 
            this.btnRemoveCharacter.AutoSize = true;
            this.btnRemoveCharacter.Location = new System.Drawing.Point(914, 32);
            this.btnRemoveCharacter.Name = "btnRemoveCharacter";
            this.btnRemoveCharacter.Size = new System.Drawing.Size(94, 23);
            this.btnRemoveCharacter.TabIndex = 14;
            this.btnRemoveCharacter.Text = "Remove";
            this.btnRemoveCharacter.UseVisualStyleBackColor = true;
            this.btnRemoveCharacter.Click += new System.EventHandler(this.BtnRemoveCharacter_Click);
            // 
            // btnFindPrev
            // 
            this.btnFindPrev.AutoSize = true;
            this.btnFindPrev.Location = new System.Drawing.Point(407, 5);
            this.btnFindPrev.Name = "btnFindPrev";
            this.btnFindPrev.Size = new System.Drawing.Size(75, 23);
            this.btnFindPrev.TabIndex = 3;
            this.btnFindPrev.Text = "Find Prev";
            this.btnFindPrev.UseVisualStyleBackColor = true;
            this.btnFindPrev.Visible = false;
            this.btnFindPrev.Click += new System.EventHandler(this.BtnFindPrev_Click);
            // 
            // chkCharacters
            // 
            this.chkCharacters.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkCharacters.FormattingEnabled = true;
            this.chkCharacters.Location = new System.Drawing.Point(-18, 39);
            this.chkCharacters.Name = "chkCharacters";
            this.chkCharacters.Size = new System.Drawing.Size(136, 19);
            this.chkCharacters.TabIndex = 9;
            this.chkCharacters.Visible = false;
            // 
            // txtSearch
            // 
            this.txtSearch.Location = new System.Drawing.Point(58, 19);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(262, 20);
            this.txtSearch.TabIndex = 1;
            // 
            // lblFound
            // 
            this.lblFound.AutoSize = true;
            this.lblFound.Location = new System.Drawing.Point(326, 42);
            this.lblFound.Name = "lblFound";
            this.lblFound.Size = new System.Drawing.Size(49, 13);
            this.lblFound.TabIndex = 0;
            this.lblFound.Text = "Found: 0";
            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new System.Drawing.Point(8, 22);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(44, 13);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.Text = "Search:";
            // 
            // btnSearch
            // 
            this.btnSearch.AutoSize = true;
            this.btnSearch.Location = new System.Drawing.Point(326, 16);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(75, 23);
            this.btnSearch.TabIndex = 2;
            this.btnSearch.Text = "Search";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.BtnSearch_Click);
            // 
            // btnExport
            // 
            this.btnExport.AutoSize = true;
            this.btnExport.Location = new System.Drawing.Point(731, 19);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 23);
            this.btnExport.TabIndex = 13;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.BtnExport_Click);
            // 
            // btnExpand
            // 
            this.btnExpand.AutoSize = true;
            this.btnExpand.Location = new System.Drawing.Point(569, 5);
            this.btnExpand.Name = "btnExpand";
            this.btnExpand.Size = new System.Drawing.Size(75, 23);
            this.btnExpand.TabIndex = 6;
            this.btnExpand.Text = "Expand All";
            this.btnExpand.UseVisualStyleBackColor = true;
            this.btnExpand.Click += new System.EventHandler(this.BtnExpand_Click);
            // 
            // btnReload
            // 
            this.btnReload.AutoSize = true;
            this.btnReload.Location = new System.Drawing.Point(811, 32);
            this.btnReload.Name = "btnReload";
            this.btnReload.Size = new System.Drawing.Size(97, 23);
            this.btnReload.TabIndex = 12;
            this.btnReload.Text = "Reload File";
            this.btnReload.UseVisualStyleBackColor = true;
            this.btnReload.Click += new System.EventHandler(this.BtnReload_Click);
            // 
            // btnCollapse
            // 
            this.btnCollapse.AutoSize = true;
            this.btnCollapse.Location = new System.Drawing.Point(569, 32);
            this.btnCollapse.Name = "btnCollapse";
            this.btnCollapse.Size = new System.Drawing.Size(75, 23);
            this.btnCollapse.TabIndex = 7;
            this.btnCollapse.Text = "Collapse All";
            this.btnCollapse.UseVisualStyleBackColor = true;
            this.btnCollapse.Click += new System.EventHandler(this.BtnCollapse_Click);
            // 
            // btnScan
            // 
            this.btnScan.AutoSize = true;
            this.btnScan.Location = new System.Drawing.Point(811, 5);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(97, 23);
            this.btnScan.TabIndex = 11;
            this.btnScan.Text = "Scan Inventory";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.BtnScan_Click);
            // 
            // btnWiki
            // 
            this.btnWiki.AutoSize = true;
            this.btnWiki.Location = new System.Drawing.Point(650, 19);
            this.btnWiki.Name = "btnWiki";
            this.btnWiki.Size = new System.Drawing.Size(77, 23);
            this.btnWiki.TabIndex = 8;
            this.btnWiki.Text = "Wiki Lookup";
            this.btnWiki.UseVisualStyleBackColor = true;
            this.btnWiki.Click += new System.EventHandler(this.BtnWiki_Click);
            // 
            // btnFindNext
            // 
            this.btnFindNext.AutoSize = true;
            this.btnFindNext.Location = new System.Drawing.Point(407, 32);
            this.btnFindNext.Name = "btnFindNext";
            this.btnFindNext.Size = new System.Drawing.Size(75, 23);
            this.btnFindNext.TabIndex = 4;
            this.btnFindNext.Text = "Find Next";
            this.btnFindNext.UseVisualStyleBackColor = true;
            this.btnFindNext.Visible = false;
            this.btnFindNext.Click += new System.EventHandler(this.BtnFindNext_Click);
            // 
            // btnReset
            // 
            this.btnReset.AutoSize = true;
            this.btnReset.Location = new System.Drawing.Point(488, 19);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 5;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Visible = false;
            this.btnReset.Click += new System.EventHandler(this.BtnReset_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(3, 67);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lblMatches);
            this.splitContainer1.Size = new System.Drawing.Size(1019, 408);
            this.splitContainer1.SplitterDistance = 590;
            this.splitContainer1.TabIndex = 19;
            // 
            // InventoryViewForm
            // 
            this.AcceptButton = this.btnSearch;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(1015, 478);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.tv);
            this.Name = "InventoryViewForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Inventory View";
            this.Load += new System.EventHandler(this.InventoryViewForm_Load);
            this.listBox_Menu.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}
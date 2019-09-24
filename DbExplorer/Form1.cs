using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.IO.Compression;
using DbExplorer.Class;
using DbExplorer.Forms;
using Microsoft.Win32;
using DbExplorer.Properties;
using System.Reflection;

// Provider=MSDASQL.1;Persist Security Info=True;Extended Properties="DSN=CFS_ODBC_Access;SERVER=HAWPABXCC601;PORT=1972;DATABASE=CCMS_STAT;AUTHENTICATION METHOD=0;UID=bluedb;QUERY TIMEOUT=1";;Initial Catalog=(Default)
namespace DbExplorer
{
    public partial class Form1 : System.Windows.Forms.Form
    {
        private NodeManager nodeManager;
        private TreeNode currentNode;
        private Color currentNodeColor;
        private bool ctrlIsDown;
        private List<TreeNode> history;
        private int historyIndex;
        private bool selectingHistory;
        private NodeData prevData;
        private Dictionary<string, int> scrollPositions;
        private ScrollFilter filter;
        private List<string> cleanupFiles = new List<string>();
        private bool rowsSelected;

        public Form1()
        {
            history = new List<TreeNode>();
            filter = new ScrollFilter();
            scrollPositions = new Dictionary<string, int>();
            InitializeComponent();
            if (Settings.Default.Environments == null)
                Settings.Default.Environments = new System.Collections.Specialized.StringCollection();
            if (Settings.Default.Colours == null)
                Settings.Default.Colours = new System.Collections.Specialized.StringCollection();
            zoom(0);
            rebuildDropDown();
            positionScreen();
            toolStripStatusLabel1.Text = toolStripStatusLabel2.Text = toolStripStatusLabel3.Text = "";
            tabControl1.Visible = false;
            this.MouseWheel += new MouseEventHandler(Form1_MouseWheel);
            this.KeyPreview = true;
            this.KeyDown += new KeyEventHandler(Form1_KeyDownUp);
            this.KeyUp += new KeyEventHandler(Form1_KeyDownUp);
        }

        void Form1_KeyDownUp(object sender, KeyEventArgs e)
        {
            bool ctrlWasDown = ctrlIsDown;
            ctrlIsDown = e.Control;
            if (ctrlWasDown && !ctrlIsDown)
            {
                zoomDataGridView();
            }
        }

        void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ctrlIsDown)
            {
                if (e.Delta < 0)
                    zoom(-1);
                else if (e.Delta > 0)
                    zoom(+1);
            }
        }

        private void zoomDataGridView()
        {
            dataGridView1.Font = getZoomedFont();
        }

        private void zoom(int direction)
        {
            float zoom = Settings.Default.Zoom * (direction == 0 ? 1.0f : (1 + (direction * 0.2f)));
            if (zoom == 0.0f)
                zoom = 1.0f;
            else if (zoom < 0.5f)
                zoom = 0.5f;
            else if (zoom > 5.0f)
                zoom = 5.0f;
            Settings.Default.Zoom = zoom;

            Font font = getZoomedFont();

            treeView1.Font = font;
            propertyGrid1.Font = font;
            //dataGridView1.Font = font;
            textBox1.Font = font;
            textBox2.Font = font;

            if (direction == 0)
                zoomDataGridView();
        }

        private Font getZoomedFont()
        {
            float newSize = 10.0f * Settings.Default.Zoom;
            return new Font(treeView1.Font.FontFamily, newSize);
        }

        private void addServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string server = InputDialog.ShowDialog("Server name:", "Add Server");
            if (string.IsNullOrWhiteSpace(server))
                return;

            server = server.ToUpper();
            if (!setEnvironment(server)) return;

            if (!Settings.Default.Environments.Contains(server))
            {
                List<string> environments = Settings.Default.Environments.Cast<string>().ToList();
                environments.Add(server);
                environments.Sort();
                Settings.Default.Environments.Clear();
                Settings.Default.Environments.AddRange(environments.ToArray());
                rebuildDropDown();
            }
        }

        private void rebuildDropDown()
        {
            for (int i = toolStripDropDownButton1.DropDownItems.Count - 1; i >= 0; i--)
            {
                ToolStripItem item = toolStripDropDownButton1.DropDownItems[i];
                if (item.Tag == null)
                    toolStripDropDownButton1.DropDownItems.Remove(item);
            }
            foreach (string environment in Settings.Default.Environments)
            {
                ToolStripItem item = toolStripDropDownButton1.DropDownItems.Add(environment);
                item.Click += new EventHandler(item_Click);
            }
        }

        void item_Click(object sender, EventArgs e)
        {
            string environment = ((ToolStripItem)sender).Text;
            if (!setEnvironment(environment)) return;
            this.Text = string.Format("{0} Explorer", environment);
        }

        private bool setEnvironment(string environment)
        {
            try
            {
                string path = string.Format(@"{0}\Xml\DbExplorer.xml", Path.GetDirectoryName(Application.ExecutablePath));
                nodeManager = new NodeManager(treeView1, path, environment);
                nodeManager.Start();
                toolStripDropDownButton1.Text = environment;
                resetHistory();
                showBackgroundColour(environment);
                showVersion();
                tabControl1.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something bad happened: " + ex.Message);
                return false;
            }

        }

        private void showVersion()
        {
            if (nodeManager != null)
            {
                toolStripStatusLabel2.Text = string.Format("Version {0}", nodeManager.Version);
            }
        }

        private void saveScrollPosition(string nodeName)
        {
            int i = getPropertyGridScroll().Value;
            if (scrollPositions.ContainsKey(nodeName))
                scrollPositions[nodeName] = i;
            else
            scrollPositions.Add(nodeName, i);
        }

        private void restoreScrollPosition(string nodeName)
        {
            try
            {
                if (scrollPositions.ContainsKey(nodeName))
                    getPropertyGridScroll().Value = scrollPositions[nodeName];
            }
            catch { }
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                NodeData node = (NodeData)treeView1.SelectedNode.Tag;
                saveScrollPosition(node.NodeName);
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (currentNode != null)
            {
                currentNode.BackColor = treeView1.BackColor;
                //currentNode.ForeColor = currentNodeColor;
            }
            showData();
            addHistory(e.Node);
        }

        private void resetHistory()
        {
            history.Clear();
            historyIndex = 0;
            updateHistoryButtons();
        }

        private void addHistory(TreeNode node)
        {
            if (selectingHistory)
                return;
            history.Add(node);
            historyIndex = history.Count - 1;
            updateHistoryButtons();
        }

        private void updateHistoryButtons()
        {
            historyBackBtn.Enabled = (historyIndex > 0);
            historyForwardBtn.Enabled = (historyIndex < history.Count - 1);
        }

        private void goBack()
        {
            if (historyIndex <= 0)
                return;
            historyIndex--;
            TreeNode node = history[historyIndex];
            selectingHistory = true;
            selectNodeFromHistory(treeView1.Nodes);
            selectingHistory = false;
            updateHistoryButtons();
        }

        private void goForward()
        {
            if (historyIndex <= 0)
                return;
            historyIndex++;
            selectingHistory = true;
            selectNodeFromHistory(treeView1.Nodes);
            selectingHistory = false;
            updateHistoryButtons();
        }

        private bool selectNodeFromHistory(TreeNodeCollection nodes)
        {
            foreach (TreeNode test in nodes)
            {
                if (test == history[historyIndex])
                {
                    treeView1.SelectedNode = test;
                    return true;
                }
                else
                {
                    bool found = selectNodeFromHistory(test.Nodes);
                    if (found) return true;
                }
            }
            return false;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            showData();
        }

        private void showData()
        {
            if ((treeView1.SelectedNode == null) || (treeView1.SelectedNode.Tag == null))
                return;
            NodeData data = (NodeData)treeView1.SelectedNode.Tag;
            toolStripStatusLabel1.Text = data.NodeName;
            string keys = string.Join("; ", data.Keys);
            toolStripStatusLabel3.Text = (keys.Length < 100 ? keys : keys.Substring(0, 100));

            int page = 0;
            int.TryParse((string)tabControl1.TabPages[tabControl1.SelectedIndex].Tag, out page);
            if (page == 1)
                showDataProperties(data);
            else if (page == 2)
                showDataSql(data);
            else if (page == 3)
                showDataChildKeys(data);
        }

        private void showDataProperties(NodeData data)
        {
            List<DataRow> rows = new List<DataRow>();

            nodeManager.Get(rows, data);
            int mode = data.Mode;

            if ((mode == 1) && (rows.Count > 0)) // properties
            {
                propertyGrid1.Visible = true;
                dataGridView1.Visible = false;
                panel1.Visible = false;
                panel4.Visible = false;
                propertyGrid1.Dock = DockStyle.Fill;
                showDataPropertiesInspector(rows, data);
                restoreScrollPosition(data.NodeName);
            }
            else if ((mode == 2) && (rows.Count > 0)) // grid
            {
                dataGridView1.DataSource = null; // Need to null it out or same table with different cols screws up the col order
                propertyGrid1.Visible = false;
                dataGridView1.Visible = true;
                panel1.Visible = false;
                panel4.Visible = false;
                dataGridView1.DataSource = rows[0].Table;
                dataGridView1.Dock = DockStyle.Fill;
                dataGridView1.DataSource = rows[0].Table;
                dataGridView1.AutoResizeColumns();
            }
            else if (mode == 3) // search
            {
                Control focus = configSearch(data);
                propertyGrid1.Visible = false;
                dataGridView1.Visible = false;
                panel1.Visible = true;
                panel4.Visible = false;
                panel1.Dock = DockStyle.Fill;
                if (focus != null) // This doesn't work. Figure it out one day.
                {
                    if (focus is TextBox)
                        (focus as TextBox).SelectAll();
                    else
                        focus.Select();
                }
            }
            else if ((mode == 4) && (rows.Count > 0)) // gridtranspose
            {
                dataGridView1.Visible = true;
                dataGridView1.Dock = DockStyle.Fill;
                populateGrid(dataGridView1, rows);
                dataGridView1.AutoResizeColumns();
            }
            else if ((mode == 5) && (rows.Count > 0) && (rows[0].Table.Columns.Count >= 3)) // browser
            {
                propertyGrid1.Visible = false;
                dataGridView1.Visible = false;
                panel4.Visible = true;
                panel4.Dock = DockStyle.Fill;
                DataRow row = rows[0];
                int dataType = 0;
                int.TryParse(row[0].ToString(), out dataType);
                if (row[2] is DBNull)
                    webBrowser1.DocumentText = "No view to display";
                else if (dataType == 1)
                    handleInlineData(row[1], row[2], webBrowser1);
                else if (dataType == 2)
                    handleFileRef(row[1], (string)row[2], webBrowser1);
                else if (dataType == 3)
                {
                    int count = row.Table.Columns.Count;
                    List<string> paths = new List<string>();
                    for (int i=3; i<count; i++)
                        paths.Add((string)row[i]);
                    handleRelativeFileRef(row[1], (string)row[2], paths, webBrowser1);
                }
            }
            else
            {
                propertyGrid1.Visible = false;
                dataGridView1.Visible = false;
                panel1.Visible = false;
                panel4.Visible = false;
            }
        }

        private void handleInlineData(object mimeType, object fileData, WebBrowser browser)
        {
            byte[] decompressed;
            string tempFile = Path.GetTempFileName();
            if (fileData is string)
            {
                File.WriteAllText(tempFile, (string)fileData);
            }
            else if (fileData is byte[])
            {
                byte[] byteData = (byte[])fileData;
                bool zipped = (byteData[0] == 0x1f) && (byteData[1] == 0x8b);
                decompressed = (zipped ? decompress(byteData) : byteData);
                File.WriteAllBytes(tempFile, decompressed);
            }
            cleanupFiles.Add(tempFile);
            browser.Navigate(tempFile);
        }

        private void handleFileRef(object mimeType, string fileRef, WebBrowser browser)
        {
            if (string.IsNullOrEmpty(fileRef))
                browser.DocumentText = "No file to view";
            else if (string.IsNullOrEmpty(fileRef) || !File.Exists(fileRef))
                browser.DocumentText = string.Format("File '{0}' not found", Path.GetFullPath(fileRef));
            else
                browser.Navigate(fileRef);
        }

        private void handleRelativeFileRef(object mimeType, string fileRef, List<string> paths, WebBrowser browser)
        {
            if (string.IsNullOrEmpty(fileRef))
                browser.DocumentText = "No file to view";
            else
            {
                foreach (string path in paths)
                    if (File.Exists(path + fileRef))
                    {
                        browser.Navigate(path + fileRef);
                        return;
                    }
                browser.DocumentText = string.Format("File '{0}' not found at any and of:\n  {1}", fileRef, string.Join("\n  ", paths));
            }
        }

        private byte[] decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        private bool objectToBool(object obj)
        {
            if (obj is byte)
                return (byte)obj == 1;
            else if (obj is bool)
                return (bool)obj;
            else return obj.ToString() == "1";
        }

        private void populateGrid(DataGridView grid, List<DataRow> rows)
        {
            if (rows.Count == 0)
                return;

            grid.Rows.Clear();
            grid.Columns.Clear();

            int i = 0;
            grid.Columns.Add("Column", "Column");
            foreach (DataRow row in rows)
            {
                string name = string.Format("{0}: {1}", ++i, row.Table.TableName);
                grid.Columns.Add(name, name);
            }

            foreach (DataColumn col in rows[0].Table.Columns)
            {
                string[] newRow = new string[] { col.ColumnName, "", "" };
                grid.Rows.Add(newRow);
            }

            int colNumber = 1;
            foreach (DataRow row in rows)
            {
                foreach (DataColumn col in row.Table.Columns)
                {
                    int rowNumber = findRow(grid, 0, col.ColumnName);
                    if (rowNumber >= 0)
                        grid.Rows[rowNumber].Cells[colNumber].Value = row[col];
                }
                colNumber++;
            }
        }

        private int findRow(DataGridView grid, int column, string value)
        {
            foreach (DataGridViewRow row in grid.Rows)
                if (value.Equals(row.Cells[column].Value))
                    return row.Index;
            return -1;
        }

        private Control configSearch(NodeData data)
        {
            panel1.Controls.Clear();
            buttonAccept.Tag = data;
            XmlNodeList nodes = nodeManager.InputItems(data);
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                XmlNode node = nodes[i];
                XmlElement element = node as XmlElement;
                Panel panel = new Panel();
                panel.Height = 40;
                panel.Dock = DockStyle.Top;
                panel.TabIndex = i;
                Label label = new Label();
                label.Bounds = new Rectangle(0, 10, 200, 28);
                label.Text = element.GetAttribute("prompt");
                label.Dock = DockStyle.Left;
                label.TextAlign = ContentAlignment.TopRight;
                label.Padding = new Padding(3, 3, 20, 3);
                TextBoxWithPaste textBox = new TextBoxWithPaste();
                textBox.Bounds = new Rectangle(300, 10, 200, 28);
                textBox.Text = element.GetAttribute("default");
                textBox.Dock = DockStyle.Fill;
                textBox.MinimumSize = new System.Drawing.Size(200, 28);
                textBox.Text = element.GetAttribute("value");
                if (data.KeyCount - data.KeyCountOriginal > i)
                {
                    textBox.Text = data.Keys[i + data.KeyCountOriginal];
                }
                textBox.Pasting += new EventHandler(textBox_Pasting);
                Label comment = new Label();
                comment.Bounds = new Rectangle(510, 10, 500, 28);
                comment.Text = element.GetAttribute("comment");
                comment.Dock = DockStyle.Right;
                comment.Padding = new Padding(20, 3, 3, 3);
                panel.Controls.Add(textBox);
                panel.Controls.Add(label);
                panel.Controls.Add(comment);
                panel1.Controls.Add(panel);
            }
            return (nodes.Count == 0 ? null : panel1.Controls[panel1.Controls.Count - 1].Controls[0]);
        }

        void textBox_Pasting(object sender, EventArgs e)
        {
            string text = Clipboard.GetText();
            if (text.IndexOf("\n") > 0)
            {
                text = string.Join(",", text.Replace("\r", "").Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            text = text.Replace("\n", ",");
            TextBoxWithPaste textBox = (TextBoxWithPaste)sender;
            textBox.Paste(text);
        }

        private ScrollBar getPropertyGridScroll()
        {
            var pgv = FindControl(propertyGrid1.Controls, "PropertyGridView");
            Type type = pgv.GetType();
            FieldInfo f = FindField(type, "scrollBar");
            return (ScrollBar)f.GetValue(pgv);
        }

        private void showDataPropertiesInspector(List<DataRow> rows, NodeData data)
        {
            if (rows.Count == 0)
                propertyGrid1.SelectedObject = null;
            else if (rows.Count == 1)
                propertyGrid1.SelectedObject = new DataRowWrapper(rows[0], rows[0].Table.TableName);
            else
            {
                List<DataRowWrapper> wrappers = new List<DataRowWrapper>();
                foreach (DataRow row in rows)
                {
                    wrappers.Add(new DataRowWrapper(row, row.Table.TableName));
                }
                propertyGrid1.SelectedObject = new ListWrapper(wrappers);
            }
            propertyGrid1.ExpandAllGridItems();
        }

        private void showDataSql(NodeData data)
        {
            textBox1.Text = string.Join("\r\n", nodeManager.SqlGet(data).ToArray());
            List<string> childrenSql = nodeManager.SqlChildren(data);
            textBox2.Text = string.Join("\r\n", childrenSql.ToArray());
            toolStripButton1.Enabled = (childrenSql.Count == 1);
        }

        private void showDataChildKeys(NodeData data)
        {
            DataGridView grid = dataGridView2;
            grid.Rows.Clear();
            grid.Columns.Clear();

            int i = 0;
            foreach (TreeNode node in treeView1.SelectedNode.Nodes)
            {
                NodeData child = (NodeData)node.Tag;
                if ((child != null) && (child.Keys != null) && (child.Keys.Count > i))
                    i = child.Keys.Count;
            }

            for (int j = 0; j < i; j++)
            {
                string col = string.Format("Column {0}", j + 1);
                grid.Columns.Add(col, col);
            }

            foreach (TreeNode node in treeView1.SelectedNode.Nodes)
            {
                NodeData child = (NodeData)node.Tag;
                if ((child != null) && (child.Keys != null))
                    grid.Rows.Add(child.Keys.ToArray());
            }
        }

        private void showDataInput(NodeData data)
        {
            ;
        }

        private void copyToolStripButton_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textBox1.Text);
        }

        private void copyToolStripButton1_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textBox2.Text);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            collapseNodes(treeView1.Nodes);
        }

        private void collapseNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count > 0)
                    collapseNodes(node.Nodes);
                node.Collapse();
            }

        }

        private void reloadConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (nodeManager != null)
                nodeManager.ReloadConfig();
            resetHistory();
            showVersion();

            if (treeView1.SelectedNode != null)
            {
                NodeData data = (NodeData)treeView1.SelectedNode.Tag;
                if (data.Loaded)
                {
                    data.Loaded = false;
                    if (treeView1.SelectedNode.IsExpanded)
                    {
                        treeView1.SelectedNode.Collapse();
                        treeView1.SelectedNode.Expand();
                    }
                }
            }
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            NodeData data = (NodeData)e.Node.Tag;
            if (!data.Loaded || ((Control.ModifierKeys & Keys.Shift) == Keys.Shift))
            {
                this.Cursor = Cursors.WaitCursor;
                nodeManager.AddNodes(e.Node.Nodes, data);
                this.Cursor = Cursors.Default;
                int page = 0;
                int.TryParse((string)tabControl1.TabPages[tabControl1.SelectedIndex].Tag, out page);
                if (page == 3)
                    showData();
            }
        }

        private void treeView1_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            NodeData data = (NodeData)e.Node.Tag;
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                data.Forget();
            }
        }

        private void positionScreen()
        {
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.WindowsDefaultBounds;

            if (Settings.Default.WindowPosition != Rectangle.Empty &&
                IsVisibleOnAnyScreen(Settings.Default.WindowPosition))
            {
                this.StartPosition = FormStartPosition.Manual;
                this.DesktopBounds = Settings.Default.WindowPosition;
                this.WindowState = Settings.Default.WindowState;
            }
            else
            {
                this.StartPosition = FormStartPosition.WindowsDefaultLocation;
                this.Size = Settings.Default.WindowPosition.Size;
            }
        }
        private bool IsVisibleOnAnyScreen(Rectangle rect)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(rect))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            switch (this.WindowState)
            {
                case FormWindowState.Normal:
                case FormWindowState.Maximized:
                    Settings.Default.WindowState = this.WindowState;
                    break;

                default:
                    Settings.Default.WindowState = FormWindowState.Normal;
                    break;
            }

            if (this.WindowState == FormWindowState.Normal)
                Settings.Default.WindowPosition = this.DesktopBounds;
            Settings.Default.Save();
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(toolStripStatusLabel1.Text);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Requesting alternate SQL:\n{0}", textBox2.Text);
            NodeData data = (NodeData)treeView1.SelectedNode.Tag;
            nodeManager.AddNodes(treeView1.SelectedNode.Nodes, data, textBox2.Text);
        }

        private void treeView1_Validating(object sender, CancelEventArgs e)
        {
            currentNode = treeView1.SelectedNode;
            if (currentNode != null)
            {
                currentNodeColor = currentNode.ForeColor;
                currentNode.BackColor = Color.LightBlue;
                //currentNode.ForeColor = Color.White;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // It's the default button, so don't process if Enter pressed when tab not visible
            if (tabControl1.SelectedTab != tabPage1)
                return;

            Cursor.Current = Cursors.WaitCursor;
            NodeData data = (NodeData)buttonAccept.Tag;

            try
            {
                // If it's not a search node, then it's just a refresh of the current node
                if (data == null)
                {
                    showData();
                }
                else
                {
                    data.KeysResetCountToOriginal();
                    addTextBoxes(panel1, data.Keys);
                    data.KeysResetCountToContent();
                    nodeManager.AddNodes(treeView1.SelectedNode.Nodes, data);
                    treeView1.SelectedNode.Expand();
                }
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

        }

        private void addTextBoxes(Control control, List<string> values)
        {
            for (int i = control.Controls.Count - 1; i >= 0; i--)
            {
                Control child = control.Controls[i];
                if (child is TextBox)
                    values.Add((child as TextBox).Text);
                else
                    addTextBoxes(child, values);
            }
        }

        private void copyChildKeysToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null)
                return;

            try
            {
                StringBuilder copy = new StringBuilder();
                foreach (TreeNode treeNode in treeView1.SelectedNode.Nodes)
                {
                    NodeData data = treeNode.Tag as NodeData;
                    if (data != null)
                    {
                        copy.AppendLine(string.Join("\t", data.Keys.ToArray()));
                    }
                }
                Clipboard.SetText(copy.ToString());
            }
            catch { } // Tidy this later. If there are no child nodes it tends to crash otherwise.
        }

        private void historyBackBtn_Click(object sender, EventArgs e)
        {
            goBack();
        }

        private void historyForwardBtn_Click(object sender, EventArgs e)
        {
            goForward();
        }

        private static Control FindControl(Control.ControlCollection controls, string name)
        {
            foreach (Control c in controls)
            {
                if (c.Text == name)
                    return c;
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string method)
        {
            foreach (MethodInfo mi in type.GetMethods())
            {
                if (method == mi.Name)
                    return mi;
            }

            return null;
        }

        private static FieldInfo FindField(Type type, string field)
        {
            FieldInfo f = type.GetField(field,
               BindingFlags.Instance | BindingFlags.NonPublic);

            return f;
        }

        private void cutToolStripButton_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView2.SelectedRows)
            {
                dataGridView2.Rows.Remove(row);
            }
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(dataGridView2.GetClipboardContent());
        }

        private void toolStripSplitButton1_ButtonClick(object sender, EventArgs e)
        {
            copyWithCommas();
        }

        private void toolStripDropDownButton2_Click(object sender, EventArgs e)
        {
            copyWithCommas();
        }

        private void copyWithCommaSeparatorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            copyWithCommas();
        }

        private void copyWithCommas()
        {
            if (dataGridView2.SelectedCells.Count == 0)
                dataGridView2.SelectAll();

            List<string> cells = new List<string>();
            foreach (DataGridViewCell cell in dataGridView2.SelectedCells)
            {
                cells.Add(cell.Value.ToString());
            }

            Clipboard.SetText(string.Join(", ", cells.ToArray()));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            doCleanupFiles();
        }

        private void doCleanupFiles()
        {
            foreach (string fileName in cleanupFiles)
                try { File.Delete(fileName); }
                catch { }
        }

        private void dataGridView1_RowStateChanged(object sender, DataGridViewRowStateChangedEventArgs e)
        {
            if (e.StateChanged == DataGridViewElementStates.Selected)
            {
                dataGridView1.ClipboardCopyMode = (dataGridView1.SelectedRows.Count > 0 ? DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText : DataGridViewClipboardCopyMode.EnableWithoutHeaderText);
            }
        }

        private void treeView1_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Delete) && (treeView1.SelectedNode != null))
            {
                treeView1.SelectedNode.Remove();
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form form = new RemoveServers();
            form.ShowDialog(this);
            rebuildDropDown();
        }

        private string getBackgroundColour(String environment)
        {
            char[] delimiters = { '=' };

            List<string> colours = Settings.Default.Colours.Cast<string>().ToList();

            foreach (string item in colours)
            {
                string[] test = item.Split(delimiters);
                if (nodeManager.Environment == test[0])
                    return item;
            }
            return "";
        }

        private void selectBackgroundColour(Color colour)
        {
            if (nodeManager == null)
            {
                return;
            }
            string current = getBackgroundColour(nodeManager.Environment);
            if (current != "")
            {
                Settings.Default.Colours.Remove(current);
            }
            Settings.Default.Colours.Add(string.Format("{0}={1}", nodeManager.Environment, colour.ToArgb()));
            showBackgroundColour(nodeManager.Environment);
        }

        private void showBackgroundColour(string environment)
        {
            char[] delimiters = { '=' };
            string current = getBackgroundColour(environment);
            if (current != "")
            {
                string[] test = current.Split(delimiters);
                int i = 0;
                int.TryParse(test[1], out i);
                Color colour = (i == 0) ? SystemColors.Window : Color.FromArgb(i);
                treeView1.BackColor = colour;
            }
        }

        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectBackgroundColour(Color.Beige);
        }

        private void aquaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectBackgroundColour(Color.Aqua);
        }

        private void coralToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectBackgroundColour(Color.Coral);
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if ((textBox != null) && e.Control && (e.KeyCode == Keys.A))
            {
                textBox.SelectAll();
            }
        }

        private void standardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectBackgroundColour(SystemColors.Window);
        }

        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Documentation form = new Documentation();
            form.Show();
        }


    }
}

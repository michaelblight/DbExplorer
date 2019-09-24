using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Data.SqlClient;
using System.Data;
using System.Drawing;
using DbExplorer.Properties;

namespace DbExplorer.Class
{
    public class NodeManager
    {
        public TreeView TreeView { get; private set; }
        public string Environment { get; private set; }
        public string ConfigSource { get; private set; }
        public string Version { get; private set; }
        private XmlDocument configXml;
        private SqlConnection connection;
        private int version = 1;

        public NodeManager(TreeView TreeView, string ConfigSource, string Environment)
        {
            this.TreeView = TreeView;
            this.ConfigSource = ConfigSource;
            this.Environment = Environment;
            /*if (ReloadConfig())
            {
                string connectionString = string.Format("server={0};Integrated Security=true;", Environment);
                connection = new SqlConnection(connectionString);
                connection.Open();
                NodeData data = new NodeData("Root", true, true, false);
                AddNodes(this.TreeView.Nodes, data);
            }*/
        }

        public bool ReloadConfig()
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                string path = string.Format(@"{0}\Xml\DbExplorer.xml", Path.GetDirectoryName(Application.ExecutablePath));
                xml.Load(ConfigSource);
                expandReferences(xml);
                configXml = xml;
                this.Version = xml.DocumentElement.GetAttribute("version");
                if (this.Version == "")
                    this.Version = "details missing";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        public void Start()
        {
            if (ReloadConfig())
            {
                string connectionString = string.Format("server={0};Integrated Security=true;Connection Timeout=5;", this.Environment);
                connection = new SqlConnection(connectionString);
                connection.Open();
                NodeData data = new NodeData("Root", true, true, false, 0);
                AddNodes(this.TreeView.Nodes, data);
            }
            if (this.TreeView.Nodes.Count > 0)
                this.TreeView.SelectedNode = this.TreeView.Nodes[0];
        }

        private void expandReferences(XmlDocument xml)
        {
            includeReferencesOnceOnly(xml, new List<string>());
        }

        private void includeReferencesOnceOnly(XmlDocument xml, List<string> included)
        {
            const string includeName = "includeUrl";
            XmlNodeList nodes = xml.SelectNodes("//*[@includeUrl]");
            if (nodes.Count == 0)
                return;

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                XmlElement element = nodes[i] as XmlElement;
                string includeUrl = element.GetAttribute(includeName).ToLower();
                element.RemoveAttribute(includeName);
                if (included.Contains(includeUrl))
                    continue;
                included.Add(includeUrl);
                XmlDocument includeXml = openXmlUrl(includeUrl);
                if (includeXml != null)
                {
                    moveChildNodes(includeXml.DocumentElement, xml.DocumentElement);
                }
                else
                {
                    if (element.GetAttribute("errorOnMissingFile") != "false")
                        MessageBox.Show(string.Format("Could not load file '{0}'", includeUrl));
                    else
                        element.ParentNode.RemoveChild(element);
                }
                element.SetAttribute("includedUrl", includeUrl);
            }

            includeReferencesOnceOnly(xml, included); // Those includes may have includes
        }

        public XmlDocument openXmlUrl(string url)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                string path = string.Format(@"{0}\{1}", Settings.Default.ConfigPath1, url); 
                if (!File.Exists(path))
                    path = string.Format(@"{0}\Xml\{1}", Path.GetDirectoryName(Application.ExecutablePath), url);
                Console.WriteLine("Opening file '{0}'", url);
                xml.Load(path);
                return xml;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void moveChildNodes(XmlElement from, XmlElement to)
        {
            foreach (XmlNode node in from.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                    continue;
                XmlNode move = to.OwnerDocument.ImportNode(element, true);
                to.AppendChild(move);
            }
        }

        public List<string> SqlGet(NodeData Data)
        {
            List<string> result = new List<string>();
            XmlNode node = findNodeGet(Data.NodeName);
            if (node != null)
                sqlToList(result, Data, node.ChildNodes);
            return result;
        }

        public List<string> SqlChildren(NodeData Data)
        {
            List<string> result = new List<string>();
            XmlNode node = findNodeChildren(Data.NodeName);
            if (node != null)
                sqlToList(result, Data, node.ChildNodes);
            return result;
        }

        public void Get(List<DataRow> List, NodeData Data)
        {
            Data.Mode = 1; // Standard
            XmlElement node = findNodeGet(Data.NodeName) as XmlElement;
            string view = (node == null ? "" : node.GetAttribute("view"));
            if (node == null)
            {
                Data.Mode = 1; // Standard
            }
            else if (string.IsNullOrEmpty(view))
            {
                Data.Mode = 1; // Standard
                get(List, Data, node.ChildNodes, true);
            }
            else if (view.Equals("grid", StringComparison.InvariantCultureIgnoreCase))
            {
                Data.Mode = 2; // Grid
                get(List, Data, node.ChildNodes, false);
            }
            else if (view.Equals("gridtranspose", StringComparison.InvariantCultureIgnoreCase))
            {
                Data.Mode = 4; // Gridtranspose
                get(List, Data, node.ChildNodes, false);
            }
            else if (view.Equals("search", StringComparison.InvariantCultureIgnoreCase))
            {
                Data.Mode = 3; // Search
            }
            else if (view.Equals("browser", StringComparison.InvariantCultureIgnoreCase))
            {
                Data.Mode = 5; // Browser
                get(List, Data, node.ChildNodes, true);
            }
        }

        public XmlNodeList InputItems(NodeData Data)
        {
            XmlElement node = findNodeGet(Data.NodeName) as XmlElement;
            return node.SelectNodes("input");
        }

        public void AddNodes(TreeNodeCollection treeNodeCollection, NodeData data)
        {
            List<TreeNode> treeNodes = new List<TreeNode>();
            XmlElement node = findNodeChildren(data.NodeName) as XmlElement;    // First look for <children>
            if (node == null)
                node = findNodeGet(data.NodeName) as XmlElement;                // Then look for <get> (might be a search node)
            if (node != null)
                addNodes(treeNodes, data, node.ChildNodes);
            treeNodeCollection.Clear();
            treeNodeCollection.AddRange(treeNodes.ToArray());
            data.Loaded = true;

            foreach (TreeNode treeNode in treeNodes)
            {
                NodeData nodeData = treeNode.Tag as NodeData;
                if (nodeData.Expand)
                    treeNode.Expand();
            }
        }

        private void addNodes(List<TreeNode> treeNodes, NodeData data, XmlNodeList nodes)
        {
            foreach (XmlNode node in nodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                {
                    continue;
                }
                else if (element.Name.Equals("node", StringComparison.InvariantCultureIgnoreCase))
                {
                    addNodeConstant(treeNodes, element, data);
                }
                else if (element.Name.Equals("query", StringComparison.InvariantCultureIgnoreCase))
                {
                    bool ignoreErrors = (element.GetAttribute("ignoreErrors") == "true");
                    addNodesSql(treeNodes, element, data, ignoreErrors);
                }
                else if (element.Name.Equals("queryRef", StringComparison.InvariantCultureIgnoreCase))
                {
                    bool cascadeKeys = (element.GetAttribute("cascadeKeys") != "false");
                    bool expand = (element.GetAttribute("expand") == "true");
                    int style = getStyle(element.GetAttribute("nodeStyle"), 0);
                    List<string> keys = createNewKeys(data.Keys, cascadeKeys);
                    addParameters(element, keys, data);
                    NodeData newData = new NodeData(data.NodeName, data.HasChildren, cascadeKeys, expand, style, keys);
                    //addParameters(element, newData, data);
                    addNodesRef(treeNodes, element, newData);
                }
                data.KeysResetCount();
            }
        }

        private void addNodesRef(List<TreeNode> treeNodes, XmlElement element, NodeData data)
        {
            XmlNodeList nodes = findNodeRef(element.GetAttribute("xpath"));
            addNodes(treeNodes, data, nodes);
        }

        public void AddNodes(TreeNodeCollection treeNodeCollection, NodeData data, string altSql)
        {
            List<TreeNode> nodes = new List<TreeNode>();
            XmlNode node = findNodeChildren(data.NodeName);
            if (node.FirstChild == null)
                return;
            XmlElement element = node.FirstChild as XmlElement;
            if (!element.Name.Equals("query", StringComparison.InvariantCultureIgnoreCase))
                return;
            bool ignoreErrors = (element.GetAttribute("ignoreErrors") == "true");
            addNodesSql(nodes, element, data, altSql, ignoreErrors);
            treeNodeCollection.Clear();
            treeNodeCollection.AddRange(nodes.ToArray());
            data.Loaded = true;
        }

        private void sqlToList(List<string> result, NodeData Data, XmlNodeList nodes)
        {
            if (version == 1)
            {
                foreach (XmlNode node in nodes)
                {
                    XmlElement element = node as XmlElement;
                    if (element == null)
                        continue;
                    else if (element.Name.Equals("query", StringComparison.InvariantCultureIgnoreCase))
                        result.Add(expandParams(textOf(element), Data) + "\r\n");
                    else if (element.Name.Equals("queryRef", StringComparison.InvariantCultureIgnoreCase))
                        sqlToListRef(result, element, Data);
                    else
                        result.Add(textOf(element));
                }
            }
            else
            {
            }
            //Console.WriteLine("Node {0} sqlToList - {1} results found", Data.NodeName, result.Count);
        }

        private void sqlToListRef(List<string> result, XmlElement element, NodeData data)
        {
            XmlNodeList nodes = findNodeRef(element.GetAttribute("xpath"));
            sqlToList(result, data, nodes);
        }

        private void get(List<DataRow> list, NodeData data, XmlNodeList nodes, bool firstOnly)
        {
            foreach (XmlNode node in nodes)
            {
                XmlElement element = node as XmlElement;
                bool ignoreErrors = (element.GetAttribute("ignoreErrors") == "true");
                if (element == null)
                    continue;
                else if (element.Name.Equals("query", StringComparison.InvariantCultureIgnoreCase))
                    getNodeSql(list, element, data, firstOnly, ignoreErrors);
                else if (element.Name.Equals("queryRef", StringComparison.InvariantCultureIgnoreCase))
                    getRef(list, element, data, firstOnly);
            }
        }

        private void getRef(List<DataRow> list, XmlElement element, NodeData data, bool firstOnly)
        {
            XmlNodeList nodes = findNodeRef(element.GetAttribute("xpath"));
            get(list, data, nodes, firstOnly);
        }

        private void getNodeSql(List<DataRow> list, XmlElement element, NodeData Data, bool firstOnly, bool ignoreErrors)
        {
            string sql = expandParams(textOf(element), Data);
            string name = expandParams(element.GetAttribute("name"), Data);
            Console.WriteLine("Get SQL: {0}", sql);
            try
            {
                using (SqlDataAdapter da = new SqlDataAdapter(sql, connection))
                {
                    DataTable dt = new DataTable();
                    dt.TableName = name;
                    da.Fill(dt);
                    //Console.WriteLine("Name = {0}, Rows = {1}, Sql:\r\n{2}", name, dt.Rows.Count, sql);
                    Console.WriteLine("Get SQL returned {0} rows", dt.Rows.Count);
                    if (dt.Rows.Count > 0)
                    {
                        if (firstOnly)
                        {
                            list.Add(dt.Rows[0]);
                        }
                        else
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                list.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ignoreErrors)
                    MessageBox.Show(string.Format("{0}\n\n{1}", ex.Message, sql));
            }
            finally
            {
            }
            //Console.WriteLine("Node {0} getNodeSql - {1} results included", Data.NodeName, list.Count);
        }

        //private void addParameters(XmlElement element, NodeData data, NodeData fromData)
        private void addParameters(XmlElement element, List<string> keys, NodeData fromData)
        {
            if (element == null)
                return;

            foreach (XmlElement param in element.SelectNodes("param"))
            {
                string value = textOf(param);
                keys.Add(expandParams(value, fromData));
            }
        }

        private List<string> createNewKeys(List<string> oldKeys, bool cascadeKeys)
        {
            List<string> keys;
            if (!cascadeKeys)
                keys = new List<string>();
            else if (oldKeys == null)
                keys = new List<string>();
            else
                keys = new List<string>(oldKeys);
            return keys;
        }

        private void addNodeConstant(List<TreeNode> treeNodes, XmlElement element, NodeData data)
        {
            string id = element.GetAttribute("id");
            XmlElement node = findNodeChildren(id) as XmlElement;
            bool hasChildren = (node != null) && (node.FirstChild != null);
            bool cascadeKeys = (element.GetAttribute("cascadeKeys") != "false");
            bool expand = (element.GetAttribute("expand") == "true");
            int style = getStyle(element.GetAttribute("nodeStyle"), 1);
            //Console.WriteLine("addNodeConstant {0} children={1}", id, hasChildren);
            TreeNode newNode = new TreeNode(textOf(element));

            List<string> keys = createNewKeys(data.Keys, data.CascadeKeys);

            //addParameters(element, data);
            addParameters(element, keys, data);

            NodeData newData = new NodeData(id, hasChildren, cascadeKeys, expand, style, keys);
            //addParameters(element, newData, data);
            newNode.Tag = newData;
            setStyle(this.TreeView, newNode, newData);
            Console.WriteLine("Added constant node with id {0} and keys '{1}', original length {2} cascade {3} style {4}", id, string.Join(", ", keys.ToArray()), newData.KeyCountOriginal, newData.CascadeKeys, newData.Style);
            if (hasChildren)
                newNode.Nodes.Add("Please wait...");
            treeNodes.Add(newNode);
        }

        private int getStyle(String style, int defaultValue)
        {
            int i = defaultValue;
            i += checkStyle(style, "static", 1, defaultValue);
            i += checkStyle(style, "bold", 2, defaultValue);
            i += checkStyle(style, "up", 4, defaultValue);
            return i;
        }

        private int checkStyle(String style, String check, int value, int defaultValue)
        {
            if ((defaultValue & value) > 0)
                return 0; // Already set (probably through defaultValue)
            else if (style.IndexOf(check) >= 0)
                return value;
            else
                return 0;
        }

        private void setStyle(TreeView treeView, TreeNode node, NodeData data)
        {
            Font nodeFont;
            node.ForeColor = Color.Blue;
            if ((data.Style & 1) > 0)
            {
                node.ForeColor = Color.Black;
            }
            if ((data.Style & 2) > 0)
            {
                nodeFont = new Font(treeView.Font, FontStyle.Bold);
                node.NodeFont = nodeFont;
            }
            if ((data.Style & 4) > 0)
            {
                nodeFont = new Font(treeView.Font, FontStyle.Italic);
                node.NodeFont = nodeFont;
            }
        }

        private void addNodesSql(List<TreeNode> nodes, XmlElement element, NodeData data, bool ignoreErrors)
        {
            addNodesSql(nodes, element, data, null, ignoreErrors);
        }

        private void addNodesSql(List<TreeNode> nodes, XmlElement element, NodeData data, string altSql, bool ignoreErrors)
        {
            //Console.WriteLine(element.OuterXml);
            string sql = (altSql == null ? expandParams(textOf(element), data) : altSql);
            Console.WriteLine("Executing SQL:\n{0}", sql);
            bool showTitle = (element.GetAttribute("showTitle") == "true");
            try
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.HasRows)
                        Console.WriteLine("No rows returned");
                    while (reader.Read())
                    {
                        string id = reader.GetValue(0).ToString();
                        List<string> keys = new List<string>();
                        for (int i=0; i < reader.FieldCount; i++)
                        {
                            keys.Add(reader.GetValue(i).ToString());
                        }
                        XmlNode childrenNode = findNodeChildren(id);
                        bool hasChildren = (childrenNode != null) && (childrenNode.FirstChild != null);
                        bool cascadeKeys = (element.GetAttribute("cascadeKeys") != "false");
                        bool expand = false;
                        int style = getStyle(element.GetAttribute("nodeStyle"), 0);
                        //bool expand = (element.GetAttribute("expand") == "true");
                        string name = reader.GetValue(reader.FieldCount - 1).ToString();

                        XmlElement node = findNode(id) as XmlElement;
                        if (node != null)
                        {
                            if (showTitle)
                            {
                                string title = node.GetAttribute("title");
                                if (!string.IsNullOrEmpty(title))
                                    name = "(" + title + ") " + name;
                            }
                        }
                        addParameters(element, keys, data);
                        TreeNode newNode = new TreeNode(name);
                        NodeData newData = new NodeData(id, hasChildren, cascadeKeys, expand, style, keys);
                        //addParameters(element, newData, data);
                        newNode.Tag = newData;
                        setStyle(this.TreeView, newNode, newData);
                        nodes.Add(newNode);
                        Console.WriteLine("Added sql node with id {0} and keys {1}, original length {2} cascade {3}", id, string.Join(", ", keys.ToArray()), newData.KeyCountOriginal, newData.CascadeKeys);
                        if (hasChildren)
                            newNode.Nodes.Add("Please wait...");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ignoreErrors)
                    MessageBox.Show(string.Format("{0}\n\n{1}", ex.Message, sql));
            }
            finally
            {
            }
        }

        private string expandParams(string text, NodeData data)
        {
            if (data == null)
                return text;
            for (int i = 0; i < data.Keys.Count; i++)
            {
                // First replace {0}, {1}, {2} etc.
                string key = data.Keys[i];
                string keyRef = "{" + i.ToString() + "}";
                text = text.Replace(keyRef, key);
                // Now replace {n}, {n-1}, {n-2}, etc.
                key = data.Keys[data.Keys.Count - 1 - i];
                keyRef = "{n" + (i == 0 ? "" : "-" + i.ToString()) + "}";
                text = text.Replace(keyRef, key);
                //Console.WriteLine("Replaced item {0} named {1} with {2}", i, key, keyRef);
            }
            return text;
        }

        private XmlNode findNode(string id)
        {
            return configXml.SelectSingleNode(string.Format(@"/DbExplorer/{0}", id));
        }

        private XmlNode findNodeGet(string id)
        {
            return configXml.SelectSingleNode(string.Format(@"/DbExplorer/{0}/get", id));
        }

        private XmlNode findNodeChildren(string id)
        {
            return configXml.SelectSingleNode(string.Format(@"/DbExplorer/{0}/children", id));
        }

        private XmlNodeList findNodeSearch(string id)
        {
            return configXml.SelectNodes(string.Format(@"/DbExplorer/{0}/search/*", id));
        }

        private XmlNodeList findNodeRef(string id)
        {
            return configXml.SelectNodes(string.Format(@"{0}/*", id));
        }

        private string textOf(XmlElement element)
        {
            StringBuilder sb = new StringBuilder();
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Text ||
                    child.NodeType == XmlNodeType.CDATA)
                {
                    sb.Append(child.Value);
                }
            }
            //foreach (XmlNode node in element.SelectNodes("text()"))
            //    sb.Append(node.Value);
            return sb.ToString().Trim();
        }
    }
}

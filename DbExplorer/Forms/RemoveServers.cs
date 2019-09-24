using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DbExplorer.Properties;

namespace DbExplorer.Forms
{

    public partial class RemoveServers : Form
    {

        private List<string> environments;

        public RemoveServers()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (String item in checkedListBox1.CheckedItems)
            {
                environments.Remove(item);
            }
            Settings.Default.Environments.Clear();
            Settings.Default.Environments.AddRange(environments.ToArray());
            buildList();
        }

        private void RemoveServers_Load(object sender, EventArgs e)
        {
            buildList();
        }

        private void buildList()
        {
            checkedListBox1.Items.Clear();
            environments = Settings.Default.Environments.Cast<string>().ToList();
            foreach (String environment in environments)
            {
                checkedListBox1.Items.Add(environment);
            }
        }
    }
}

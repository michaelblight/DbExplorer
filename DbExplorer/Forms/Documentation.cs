using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace DbExplorer.Forms
{
    public partial class Documentation : Form
    {
        public Documentation()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Documentation_Load(object sender, EventArgs e)
        {
            string doc = string.Format(@"{0}\Documentation.html", Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase));
            webBrowser1.Navigate(doc);
        }
    }
}

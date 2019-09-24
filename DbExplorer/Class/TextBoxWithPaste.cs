using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DbExplorer.Class
{
    public class TextBoxWithPaste : TextBox
    {
        public event EventHandler Pasting;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x302 && Clipboard.ContainsText())
            {
                if (this.Pasting != null)
                    this.Pasting(this, new EventArgs());
                return;
            }
            base.WndProc(ref m);
        }
    }
}

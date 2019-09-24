using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DbExplorer.Forms
{
    public static class InputDialog
    {
        public static string ShowDialog(string text, string caption)
        {
            System.Windows.Forms.Form prompt = new System.Windows.Forms.Form();
            prompt.Width = 500;
            prompt.Height = 160;
            prompt.Text = caption;
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text };
            TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            Button confirmation = new Button() { Text = "&OK", Left = 350, Width = 100, Top = 80 };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.ActiveControl = textBox;
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            return textBox.Text;
        }
    }
}

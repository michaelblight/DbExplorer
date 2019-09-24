using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbExplorer.Class
{
    public class NodeData
    {
        public int Mode { get; set; }
        public string NodeName { get; private set; }
        public List<string> Keys { get; private set; }
        public bool CascadeKeys { get; private set; }
        public bool HasChildren { get; private set; }
        public bool Expand { get; private set; }
        public int Style { get; private set; }
        public int KeyCount { get; private set; }
        public int KeyCountOriginal { get; private set; }
        public bool Loaded { get; set; }

        public NodeData(string NodeName, bool HasChildren, bool CascadeKeys, bool Expand, int Style)
        {
            this.NodeName = NodeName;
            this.Keys = new List<string>();
            this.HasChildren = HasChildren;
            this.CascadeKeys = CascadeKeys;
            this.Expand = Expand;
            this.Loaded = false;
            this.KeyCount = 0;
            this.KeyCountOriginal = 0;
            this.Style = Style;
        }

        public NodeData(string NodeName, bool HasChildren, bool CascadeKeys, bool Expand, int Style, List<string> Keys)
            : this(NodeName, HasChildren, CascadeKeys, Expand, Style)
        {
            if (Keys != null)
            {
                this.Keys.AddRange(Keys);
                KeysInitialised();
                //Console.WriteLine("Added {0} length {1} count {2}", string.Join(", ", Keys.ToArray()), this.Keys.Count, this.KeyCountOriginal);
            }
            Console.WriteLine("NodeData created for {0} with cascade {1} and keys {2}", this.NodeName, this.CascadeKeys, string.Join(", ", this.Keys.ToArray()));
        }

        public void Forget()
        {
            Loaded = false;
        }

        public void KeysResetCount()
        {
            removeExcessKeys(this.KeyCount);
        }

        public void KeysResetCountToOriginal()
        {
            removeExcessKeys(this.KeyCountOriginal);
        }

        public void KeysResetCountToContent()
        {
            this.KeyCount = this.Keys.Count;
        }

        public void KeysInitialised()
        {
            this.KeyCount = this.Keys.Count;
            this.KeyCountOriginal = this.Keys.Count;
        }

        private void removeExcessKeys(int newCount)
        {
            int excess = this.Keys.Count - newCount;
            if (excess > 0)
            {
                Console.WriteLine("{0} had {1} keys, now has {2}: removing {3} excess keys, cascade {4}\n{5}",
                    this.NodeName, newCount, this.Keys.Count, excess, this.CascadeKeys, string.Join(", ", this.Keys.ToArray()));
                this.Keys.RemoveRange(newCount, excess);
            }
        }
    }
}

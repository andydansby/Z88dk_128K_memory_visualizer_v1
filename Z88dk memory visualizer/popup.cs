using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Z88dk_memory_visualizer
{
    public partial class popup : Form
    {
        public popup()
        {
            InitializeComponent();
        }
    }
}



/*
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace Z88dk_memory_visualizer
{
    public partial class visual_memory : Form
    {
        private Dictionary<string, List<string>> _public;
        private Dictionary<string, List<string>> _local;
        private Dictionary<string, Dictionary<string, ModuleInfo>> _modules;

        private Dictionary<string, ListView> _moduleViews = new Dictionary<string, ListView>();
        private Dictionary<string, ListView> _symbolViews = new Dictionary<string, ListView>();

        public visual_memory(Dictionary<string, List<string>> publicData, Dictionary<string, List<string>> localData)
        {
            InitializeComponent();
            _public = publicData;
            _local = localData;
            BuildModules();
            BuildTabs();
            PopulateModules();
        }




        public class ModuleInfo
        {
            public string Name { get; set; }  // source file name e.g. ram0.c
            public int StartAddr { get; set; }  // lowest address in this module
            public int EndAddr { get; set; }  // highest address in this module
            public int Size { get; set; }  // EndAddr - StartAddr
            public int SymbolCount { get; set; }  // total public + local symbols
            public Color ModuleColor { get; set; }  // assigned colour for this module
            public List<string> Lines { get; set; }  // raw lines belonging to this module

            public ModuleInfo()
            {
                Lines = new List<string>();
            }
        }

        private void BuildModules()
        {
            _modules = new Dictionary<string, Dictionary<string, ModuleInfo>>();

            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };

            // predefined colours to cycle through for each module
            Color[] palette = new Color[]
    {
        Color.FromArgb(0,   229, 255),  // cyan
        Color.FromArgb(255, 107,  53),  // orange
        Color.FromArgb(57,  255,  20),  // green
        Color.FromArgb(255, 215,   0),  // gold
        Color.FromArgb(255,  99, 132),  // pink
        Color.FromArgb(153, 102, 255),  // purple
        Color.FromArgb(255, 159,  64),  // amber
        Color.FromArgb(100, 200, 255),  // light blue
        Color.FromArgb(255, 100, 100),  // red
        Color.FromArgb(100, 255, 100),  // light green
    };

            foreach (string sec in sections)
            {
                var moduleDict = new Dictionary<string, ModuleInfo>();
                int colorIndex = 0;

                // process both public and local lines
                List<string> allLines = new List<string>();
                allLines.AddRange(_public[sec]);
                allLines.AddRange(_local[sec]);

                foreach (string line in allLines)
                {
                    string symbol, address, section, sourceFile;
                    ParseLine(line, out symbol, out address, out section, out sourceFile);

                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    // create module entry if it doesn't exist yet
                    if (!moduleDict.ContainsKey(sourceFile))
                    {
                        moduleDict[sourceFile] = new ModuleInfo
                        {
                            Name = sourceFile,
                            StartAddr = int.MaxValue,
                            EndAddr = int.MinValue,
                            ModuleColor = palette[colorIndex % palette.Length]
                        };
                        colorIndex++;
                    }

                    var mod = moduleDict[sourceFile];
                    mod.Lines.Add(line);
                    mod.SymbolCount++;

                    int addr = ExtractAddress(line);
                    if (addr < mod.StartAddr) mod.StartAddr = addr;
                    if (addr > mod.EndAddr) mod.EndAddr = addr;
                }

                // calculate size for each module
                foreach (var mod in moduleDict.Values)
                {
                    mod.Size = mod.EndAddr - mod.StartAddr;
                    if (mod.StartAddr == int.MaxValue) mod.StartAddr = 0;
                    if (mod.EndAddr == int.MinValue) mod.EndAddr = 0;
                }

                _modules[sec] = moduleDict;
            }
        }


        private void ParseLine(string line, out string symbol, out string address, out string section, out string sourceFile)
        {
            symbol = "";
            address = "";
            section = "";
            sourceFile = "";

            // symbol name is everything before the =
            int eqPos = line.IndexOf('=');
            if (eqPos < 0) return;
            symbol = line.Substring(0, eqPos).Trim();

            // address is the $XXXX part
            int dollarPos = line.IndexOf('$');
            if (dollarPos >= 0)
                address = "$" + line.Substring(dollarPos + 1, 4).Trim();

            // split on comma for the rest
            string[] parts = line.Split(',');
            if (parts.Length < 5) return;

            section = parts[4].Trim();
            sourceFile = parts.Length > 5 ? parts[5].Trim() : "";

            // strip line number from source file e.g. ball_sprite.asm:131
            int colon = sourceFile.LastIndexOf(':');
            if (colon > 0) sourceFile = sourceFile.Substring(0, colon);
        }

        private int ExtractAddress(string line)
        {
            // finds the $XXXX portion of the line
            int dollarPos = line.IndexOf('$');
            if (dollarPos < 0) return 0;

            string hex = line.Substring(dollarPos + 1, 4);
            int addr;
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out addr))
                return addr;

            return 0;
        }

        private void BuildTabs()
        {
            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };
            foreach (string sec in sections)
                BuildTab(sec);
        }

        private void BuildTab(string sec)
        {
            TabPage page = new TabPage(sec);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = (int)(this.ClientSize.Height * 0.50);
            page.Controls.Add(split);

            // --- Module list in top panel ---
            ListView lvModules = new ListView();
            lvModules.View = View.Details;
            lvModules.FullRowSelect = true;
            lvModules.GridLines = true;
            lvModules.Scrollable = true;
            lvModules.Dock = DockStyle.Fill;
            lvModules.Columns.Add("Module", 150);
            lvModules.Columns.Add("Start Addr", 80);
            lvModules.Columns.Add("End Addr", 80);
            lvModules.Columns.Add("Symbols", 60);
            lvModules.Columns.Add("Size", 60);
            lvModules.Columns.Add("Size Bar", 200);
            split.Panel1.Controls.Add(lvModules);

            // --- Symbol drill-down in bottom panel ---
            ListView lvSymbols = new ListView();
            lvSymbols.View = View.Details;
            lvSymbols.FullRowSelect = true;
            lvSymbols.GridLines = true;
            lvSymbols.Scrollable = true;
            lvSymbols.Dock = DockStyle.Fill;
            lvSymbols.Columns.Add("Symbol", 300);
            lvSymbols.Columns.Add("Address", 80);
            lvSymbols.Columns.Add("Scope", 80);
            lvSymbols.Columns.Add("Line", 60);
            split.Panel2.Controls.Add(lvSymbols);

            _moduleViews[sec] = lvModules;
            _symbolViews[sec] = lvSymbols;

            tabControl1.TabPages.Add(page);
        }

        private void PopulateModules()
        {
            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };

            foreach (string sec in sections)
            {
                _moduleViews[sec].Items.Clear();

                var modules = _modules[sec].Values.OrderBy(m => m.StartAddr).ToList();

                int maxSize = modules.Count > 0 ? modules.Max(m => m.Size) : 1;
                if (maxSize == 0) maxSize = 1;

                foreach (var mod in modules)
                {
                    ListViewItem item = new ListViewItem(mod.Name);
                    item.SubItems.Add("$" + mod.StartAddr.ToString("X4"));
                    item.SubItems.Add("$" + mod.EndAddr.ToString("X4"));
                    item.SubItems.Add(mod.SymbolCount.ToString());
                    item.SubItems.Add(mod.Size.ToString());
                    item.SubItems.Add("");
                    item.ForeColor = mod.ModuleColor;
                    item.Tag = mod;
                    _moduleViews[sec].Items.Add(item);
                }

                int barColumnIndex = 5;
                int localMaxSize = maxSize;

                _moduleViews[sec].OwnerDraw = true;

                _moduleViews[sec].DrawColumnHeader += (s, e) => e.DrawDefault = true;

                _moduleViews[sec].DrawItem += (s, e) => { };

                _moduleViews[sec].DrawSubItem += (s, e) =>
                {
                    if (e.ColumnIndex != barColumnIndex)
                    {
                        // draw background
                        e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);

                        // draw text in the module's colour
                        using (var brush = new SolidBrush(e.Item.ForeColor))
                        {
                            e.Graphics.DrawString(
                                e.SubItem.Text,
                                e.Item.Font,
                                brush,
                                e.Bounds.X + 2,
                                e.Bounds.Y + 2);
                        }
                        return;
                    }

                    // draw size bar column
                    var mod = e.Item.Tag as ModuleInfo;
                    if (mod == null) return;

                    int barMaxWidth = e.Bounds.Width - 4;
                    int barWidth = (int)((double)mod.Size / localMaxSize * barMaxWidth);
                    if (barWidth < 2) barWidth = 2;

                    e.Graphics.FillRectangle(Brushes.Black, e.Bounds);
                    using (var brush = new SolidBrush(mod.ModuleColor))
                    {
                        e.Graphics.FillRectangle(brush,
                            e.Bounds.X + 2,
                            e.Bounds.Y + 2,
                            barWidth,
                            e.Bounds.Height - 4);
                    }
                };
            }
        }





    }

    

}
*/




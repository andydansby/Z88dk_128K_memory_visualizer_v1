using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

using System.Diagnostics;

namespace Z88dk_memory_visualizer
{
    public partial class Form1 : Form
    {
        private string _mapPath;
        private string[] _mapLines;
        private Dictionary<string, List<string>> _sections;
        private Dictionary<string, List<string>> _public;
        private Dictionary<string, List<string>> _local;

        private Dictionary<string, ListView> _publicViews = new Dictionary<string, ListView>();
        private Dictionary<string, ListView> _localViews = new Dictionary<string, ListView>();

        private Dictionary<string, Color> _fileColors = new Dictionary<string, Color>();


        //private ListView _publicListView;
        //private ListView _localListView;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };
            foreach (string s in sections)
                BuildTab(s);
        }


        private void BuildTab(string sectionName)
        {
            TabPage page = new TabPage(sectionName);

            // SplitContainer fills the whole tab
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = (int)(this.ClientSize.Height * 0.60);
            page.Controls.Add(split);

            // --- Public label + ListView in top panel ---
            Label lblPublic = new Label();
            lblPublic.Text = "Public Symbols";
            lblPublic.Dock = DockStyle.Top;
            lblPublic.Height = 20;
            split.Panel1.Controls.Add(lblPublic);

            ListView lvPublic = new ListView();
            lvPublic.View = View.Details;
            lvPublic.FullRowSelect = true;
            lvPublic.GridLines = true;
            lvPublic.Scrollable = true;
            lvPublic.Dock = DockStyle.Fill;
            lvPublic.Columns.Add("Symbol", 200);
            lvPublic.Columns.Add("Address", 70);
            lvPublic.Columns.Add("Size", 50);
            lvPublic.Columns.Add("Section", 120);
            lvPublic.Columns.Add("Source File", 200);
            split.Panel1.Controls.Add(lvPublic);

            // --- Local label + ListView in bottom panel ---
            Label lblLocal = new Label();
            lblLocal.Text = "Local Symbols";
            lblLocal.Dock = DockStyle.Top;
            lblLocal.Height = 20;
            split.Panel2.Controls.Add(lblLocal);

            ListView lvLocal = new ListView();
            lvLocal.View = View.Details;
            lvLocal.FullRowSelect = true;
            lvLocal.GridLines = true;
            lvLocal.Scrollable = true;
            lvLocal.Dock = DockStyle.Fill;
            lvLocal.Columns.Add("Symbol", 200);
            lvLocal.Columns.Add("Address", 70);
            lvLocal.Columns.Add("Size", 50);
            lvLocal.Columns.Add("Section", 120);
            lvLocal.Columns.Add("Source File", 200);
            split.Panel2.Controls.Add(lvLocal);

            // store references and add tab
            _publicViews[sectionName] = lvPublic;
            _localViews[sectionName] = lvLocal;
            tabControl1.TabPages.Add(page);
        }
        
        
        
        private void LoadMapFile(string path)
        {
            _mapPath = path;
            _mapLines = File.ReadAllLines(path);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;

            openFileDialog1.Filter = "Map files (*.map)|*.map|All files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.Cursor = Cursors.WaitCursor;

                try
                {
                    LoadMapFile(openFileDialog1.FileName);
                    InitSections();
                    SiftLines();
                    PopulateViews();
                }
                finally
                {
                    this.Cursor = Cursors.Default;
                    button2.Enabled = true;
                }
            }
        }

        private void InitSections()
        {
            _public = new Dictionary<string, List<string>>
            {
                { "CONTENDED",   new List<string>() },
                { "UNCONTENDED", new List<string>() },
                { "RAM0",        new List<string>() },
                { "RAM1",        new List<string>() },
                { "RAM3",        new List<string>() },
                { "RAM4",        new List<string>() },
                { "RAM6",        new List<string>() },
                { "LIBRARY",     new List<string>() }
            };

                    _local = new Dictionary<string, List<string>>
            {
                { "CONTENDED",   new List<string>() },
                { "UNCONTENDED", new List<string>() },
                { "RAM0",        new List<string>() },
                { "RAM1",        new List<string>() },
                { "RAM3",        new List<string>() },
                { "RAM4",        new List<string>() },
                { "RAM6",        new List<string>() },
                { "LIBRARY",     new List<string>() }
            };
        }

        private void SiftLines()
        {
            _public = new Dictionary<string, List<string>>();
            _local = new Dictionary<string, List<string>>();

            foreach (string section in new[] { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" })
            {
                _public[section] = new List<string>();
                _local[section] = new List<string>();
            }

            foreach (string line in _mapLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(';');
                if (parts.Length < 2) continue;

                string[] meta = parts[1].Split(',');
                if (meta.Length < 5) continue;

                string visibility = meta[1].Trim();
                string bank = meta[4].Trim();
                int addr = ExtractAddress(line);
                bool isConst = meta[0].Trim() == "const";
                string rawSource = meta.Length > 5 ? meta[5].Trim() : "";

                // skip z88dk system constants (BiFrost, Nirvana, SP1, ESXDOS etc.)
                if (IsSystemConst(rawSource, isConst)) continue;

                // route by address/bank — same for both const and global
                string section = AddressToSection(addr, bank);

                if (section == null) continue;

                if (visibility == "public")
                    _public[section].Add(line);
                else
                    _local[section].Add(line);
            }
        }
        private static bool IsSystemConst(string sourceFile, bool isConst)
        {
            if (!isConst) return false;

            // z88dk system config files - all defc constants, zero bytes
            if (sourceFile.Contains("config_zx_public.inc")) return true;
            if (sourceFile.Contains("config_private.inc")) return true;
            if (sourceFile.Contains("zx_crt.asm")) return true;

            return false;
        }

        private string AddressToSection(int addr, string bank)
        {
            // Paged RAM banks — all share $C000-$FFFF so must use bank name
            if (bank == "BANK_00") return "RAM0";
            if (bank == "BANK_01") return "RAM1";
            if (bank == "BANK_03") return "RAM3";
            if (bank == "BANK_04") return "RAM4";
            if (bank == "BANK_06") return "RAM6";

            // Everything else is determined purely by address
            if (addr >= 0x5DC0 && addr <= 0x7FFF) return "CONTENDED";
            if (addr >= 0x8000 && addr <= 0xBFFF) return "UNCONTENDED";
            if (addr >= 0xC000 && addr <= 0xFFFF) return "UNCONTENDED"; // ramALL etc that land in upper RAM

            return null;
        }

        private string BankToSection(string bank)
        {
            switch (bank)
            {
                case "CONTENDED": return "CONTENDED";
                case "BANK_00": return "RAM0";
                case "BANK_01": return "RAM1";
                case "BANK_03": return "RAM3";
                case "BANK_04": return "RAM4";
                case "BANK_06": return "RAM6";
                case "ramALL": return "UNCONTENDED";
                case "CODE": return "UNCONTENDED";
                case "BSS_UNINITIALIZED": return "UNCONTENDED";
                case "IM2_VECTOR_PLACEMENT": return "UNCONTENDED";
            }

            if (bank.StartsWith("code_") ||
                bank.StartsWith("bss_") ||
                bank.StartsWith("rodata_") ||
                bank.StartsWith("data_") ||
                bank.StartsWith("smc_"))
                return "UNCONTENDED";

            return null;
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

        private List<string> SortByAddress(List<string> lines)
        {
            return lines.OrderBy(line => ExtractAddress(line)).ToList();
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

        private void PopulateViews()
        {
            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };

            foreach (string sec in sections)
            {
                _publicViews[sec].Items.Clear();
                _localViews[sec].Items.Clear();

                // populate public
                var publicSorted = SortByAddress(_public[sec]);
                for (int i = 0; i < publicSorted.Count; i++)
                {
                    string symbol, address, section, sourceFile;
                    ParseLine(publicSorted[i], out symbol, out address, out section, out sourceFile);

                    int thisAddr = ExtractAddress(publicSorted[i]);
                    int nextAddr = (i + 1 < publicSorted.Count) ? ExtractAddress(publicSorted[i + 1]) : 0;
                    int size = nextAddr > thisAddr ? nextAddr - thisAddr : 0;

                    ListViewItem item = new ListViewItem(symbol);
                    item.SubItems.Add(address);
                    item.SubItems.Add(size > 0 ? size.ToString() : "?");
                    item.SubItems.Add(section);
                    item.SubItems.Add(sourceFile);
                    _publicViews[sec].Items.Add(item);
                }

                // populate local
                var localSorted = SortByAddress(_local[sec]);
                for (int i = 0; i < localSorted.Count; i++)
                {
                    string symbol, address, section, sourceFile;
                    ParseLine(localSorted[i], out symbol, out address, out section, out sourceFile);

                    int thisAddr = ExtractAddress(localSorted[i]);
                    int nextAddr = (i + 1 < localSorted.Count) ? ExtractAddress(localSorted[i + 1]) : 0;
                    int size = nextAddr > thisAddr ? nextAddr - thisAddr : 0;

                    ListViewItem item = new ListViewItem(symbol);
                    item.SubItems.Add(address);
                    item.SubItems.Add(size > 0 ? size.ToString() : "?");
                    item.SubItems.Add(section);
                    item.SubItems.Add(sourceFile);
                    _localViews[sec].Items.Add(item);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            visual_memory vf = new visual_memory(_public, _local);
            vf.Show();
            //visual_memory vf = new visual_memory();
            //vf.Show();
        }

        





    }
}


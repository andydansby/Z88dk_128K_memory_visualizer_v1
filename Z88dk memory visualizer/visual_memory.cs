using System.Collections.Generic;
using System;
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
    public partial class visual_memory : Form
    {
        //private Dictionary<string, Dictionary<string, ModuleInfo>> _modules;
        private Dictionary<string, List<string>> _public;
        private Dictionary<string, List<string>> _local;
        private Dictionary<string, Dictionary<string, ModuleInfo>> _modules;

        private Dictionary<string, Color> _fileColors = new Dictionary<string, Color>();

        private ToolTip _toolTip = new ToolTip();
        private string _lastToolTipText = "";


        private void MakeBuffered(Panel p)
        {
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(p, true, null);
        }

        public visual_memory(Dictionary<string, List<string>> publicData, Dictionary<string, List<string>> localData)
        {
            InitializeComponent();
            _public = publicData;
            _local = localData;

            BuildModules();

            
            panel1.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel1, "CONTENDED", 0x4000, 0x7FFF);
            panel2.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel2, "UNCONTENDED", 0x8000, 0xBFFF);
            panel3.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel3, "RAM0", 0xC000, 0xFFFF);
            panel4.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel4, "RAM1", 0xC000, 0xFFFF);
            panel5.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel5, "RAM3", 0xC000, 0xFFFF);
            panel6.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel6, "RAM4", 0xC000, 0xFFFF);
            panel7.Paint += (s, e) => DrawMemoryPanel(e.Graphics, panel7, "RAM6", 0xC000, 0xFFFF);

            panel1.MouseMove += (s, e) => ShowPanelTooltip(e, panel1, "CONTENDED", 0x4000, 0x7FFF);
            panel2.MouseMove += (s, e) => ShowPanelTooltip(e, panel2, "UNCONTENDED", 0x8000, 0xBFFF);
            panel3.MouseMove += (s, e) => ShowPanelTooltip(e, panel3, "RAM0", 0xC000, 0xFFFF);
            panel4.MouseMove += (s, e) => ShowPanelTooltip(e, panel4, "RAM1", 0xC000, 0xFFFF);
            panel5.MouseMove += (s, e) => ShowPanelTooltip(e, panel5, "RAM3", 0xC000, 0xFFFF);
            panel6.MouseMove += (s, e) => ShowPanelTooltip(e, panel6, "RAM4", 0xC000, 0xFFFF);
            panel7.MouseMove += (s, e) => ShowPanelTooltip(e, panel7, "RAM6", 0xC000, 0xFFFF);

            panel1.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel1, "CONTENDED", 0x4000, 0x7FFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel1, "CONTENDED", 0x4000, 0x7FFF);
            };
            panel2.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel2, "UNCONTENDED", 0x8000, 0xBFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel2, "UNCONTENDED", 0x8000, 0xBFFF);
            };
            panel3.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel3, "RAM0", 0xC000, 0xFFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel3, "RAM0", 0xC000, 0xFFFF);
            };
            panel4.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel4, "RAM1", 0xC000, 0xFFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel4, "RAM1", 0xC000, 0xFFFF);
            };
            panel5.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel5, "RAM3", 0xC000, 0xFFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel5, "RAM3", 0xC000, 0xFFFF);
            };
            panel6.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel6, "RAM4", 0xC000, 0xFFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel6, "RAM4", 0xC000, 0xFFFF);
            };
            panel7.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowSymbolDetail(e, panel7, "RAM6", 0xC000, 0xFFFF);
                if (e.Button == MouseButtons.Right) PanelRightClick(e, panel7, "RAM6", 0xC000, 0xFFFF);
            };

            MakeBuffered(panel1); 
            MakeBuffered(panel2); 
            MakeBuffered(panel3);
            MakeBuffered(panel4);
            MakeBuffered(panel5); 
            MakeBuffered(panel6);
            MakeBuffered(panel7);

            panel1.Tag = "CONTENDED";
            panel2.Tag = "UNCONTENDED";
            panel3.Tag = "RAM0";
            panel4.Tag = "RAM1";
            panel5.Tag = "RAM3";
            panel6.Tag = "RAM4";
            panel7.Tag = "RAM6";


        }

        private void PanelRightClick(MouseEventArgs e, Panel panel, string section, int addrStart, int addrEnd)
        {
            if (e.Button != MouseButtons.Right) return;

            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("Zoom: " + section, (s, ev) =>
            {
                int ramTopAddr = 0;
                int im2Addr = 0;

                // get ramtop for this section
                List<string> lines = new List<string>();
                if (_public.ContainsKey(section)) lines.AddRange(_public[section]);
                if (_local.ContainsKey(section)) lines.AddRange(_local[section]);

                string[] topSymbols = { "_contended_top", "_uncontended_top",
            "_ram0top", "_ram1top", "_ram3top", "_ram4top", "_ram6top" };

                foreach (string line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string sym = line.Substring(0, eq).Trim();
                    foreach (string ts in topSymbols)
                        if (sym == ts) { ramTopAddr = ExtractAddress(line); break; }
                    if (sym == "_IM2_PUSH_POP")
                        im2Addr = ExtractAddress(line);
                }

                zoom_panel zp = new zoom_panel(section, addrStart, addrEnd,
                    _public, _local, _fileColors, ramTopAddr, im2Addr);
                zp.Show();
            });

            cm.Show(panel, e.Location);
        }


        private void ShowSymbolDetail(MouseEventArgs e, Panel panel, string section, int addrStart, int addrEnd)
        {
            int totalRange = addrEnd - addrStart;
            int panelH = panel.Height;

            int addr = addrEnd - (int)((double)e.Y / panelH * totalRange);

            if (!_public.ContainsKey(section)) return;

            var allLines = _public[section].OrderBy(l => ExtractAddress(l)).ToList();

            string foundLine = null;
            string foundSymbol = "";
            string foundAddr = "";
            string foundSource = "";
            int foundSize = 0;

            for (int i = 0; i < allLines.Count; i++)
            {
                int thisAddr = ExtractAddress(allLines[i]);
                int nextAddr = (i + 1 < allLines.Count) ? ExtractAddress(allLines[i + 1]) : addrEnd;

                if (addr >= thisAddr && addr < nextAddr)
                {
                    foundLine = allLines[i];
                    string seg;
                    ParseLine(foundLine, out foundSymbol, out foundAddr, out seg, out foundSource);
                    foundSize = nextAddr - thisAddr;
                    break;
                }
            }

            if (foundLine == null)
            {
                richTextBox1.Clear();
                richTextBox1.AppendText(string.Format("Address: ${0:X4}\r\nNo symbol found", addr));
                return;
            }

            richTextBox1.Clear();
            richTextBox1.AppendText(string.Format("Symbol:  {0}\r\n", foundSymbol));
            richTextBox1.AppendText(string.Format("Address: ${0:X4}\r\n", ExtractAddress(foundLine)));
            richTextBox1.AppendText(string.Format("Size:    {0} bytes\r\n", foundSize));
            richTextBox1.AppendText(string.Format("Source:  {0}\r\n", foundSource));
            richTextBox1.AppendText(string.Format("Section: {0}\r\n", section));
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

        /*private void DrawMemoryPanel(Graphics g, Panel panel, string section, int addrStart, int addrEnd)
        {
            int totalRange = addrEnd - addrStart;
            int panelH = panel.Height;
            int panelW = panel.Width;

            g.FillRectangle(Brushes.Black, 0, 0, panelW, panelH);

            if (!_public.ContainsKey(section) && !_local.ContainsKey(section)) return;

            List<string> allLines = new List<string>();
            if (_public.ContainsKey(section)) allLines.AddRange(_public[section]);
            if (_local.ContainsKey(section)) allLines.AddRange(_local[section]);

            // sort by address
            allLines = allLines.OrderBy(l => ExtractAddress(l)).ToList();

            // remove duplicate addresses - keep only first symbol at each address
            allLines = allLines.GroupBy(l => ExtractAddress(l))
                               .Select(grp => grp.First())
                               .OrderBy(l => ExtractAddress(l))
                               .ToList();

            for (int i = 0; i < allLines.Count; i++)
            {
                int thisAddr = ExtractAddress(allLines[i]);

                if (thisAddr < addrStart || thisAddr > addrEnd) continue;

                int nextAddr = (i + 1 < allLines.Count) ? ExtractAddress(allLines[i + 1]) : addrEnd;

                int clampedEnd = Math.Min(nextAddr, addrEnd);
                int yTop = panelH - (int)((double)(clampedEnd - addrStart) / totalRange * panelH);
                int yBottom = panelH - (int)((double)(thisAddr - addrStart) / totalRange * panelH);
                int height = Math.Max(yBottom - yTop, 1);

                string symbol, address, seg, sourceFile;
                ParseLine(allLines[i], out symbol, out address, out seg, out sourceFile);

                // skip sentinel top markers
                if (symbol == "_uncontended_top" ||
                    symbol == "_ram0_top" ||
                    symbol == "_ram1_top" ||
                    symbol == "_ram3_top" ||
                    symbol == "_ram4_top" ||
                    symbol == "_ram6_top" ||
                    symbol == "_contended_top")
                    continue;


                Color col = Color.Gray;
                if (!string.IsNullOrEmpty(sourceFile) && _fileColors.ContainsKey(sourceFile))
                    col = _fileColors[sourceFile];

                using (var brush = new SolidBrush(col))
                {
                    g.FillRectangle(brush, 0, yTop, panelW, height);
                    if (height > 1)
                        g.DrawLine(Pens.Black, 0, yTop, panelW, yTop);
                }

            }
        }*/
        private void DrawMemoryPanel(Graphics g, Panel panel, string section, int addrStart, int addrEnd)
        {
            int totalRange = addrEnd - addrStart;
            int panelH = panel.Height;
            int panelW = panel.Width;

            g.FillRectangle(Brushes.Black, 0, 0, panelW, panelH);

            if (!_public.ContainsKey(section) && !_local.ContainsKey(section)) return;

            List<string> allLines = new List<string>();
            if (_public.ContainsKey(section)) allLines.AddRange(_public[section]);
            if (_local.ContainsKey(section)) allLines.AddRange(_local[section]);

            // remove duplicate addresses - prefer ramtop markers, otherwise keep first
            allLines = allLines.GroupBy(l => ExtractAddress(l))
                .Select(grp =>
                {
                    string[] topSymbols = { "_contended_top",
                "_uncontended_top", "_ram0top", "_ram1top",
                "_ram3top", "_ram4top", "_ram6top" };
                    string top = null;
                    string sym, addr, seg, src;
                    foreach (string candidate in grp)
                    {
                        ParseLine(candidate, out sym, out addr, out seg, out src);
                        foreach (string ts in topSymbols)
                            if (sym == ts) { top = candidate; break; }
                        if (top != null) break;
                    }
                    return top ?? grp.First();
                })
                .OrderBy(l => ExtractAddress(l))
                .ToList();

            // find ramtop address for this section
            int ramTopAddr = FindRamTop(allLines, section);

            // for UNCONTENDED, find IM2 start address once for reuse
            int im2Addr = 0;
            if (section == "UNCONTENDED")
                im2Addr = FindSymbolAddress(allLines, "_IM2_PUSH_POP");

            for (int i = 0; i < allLines.Count; i++)
            {
                int thisAddr = ExtractAddress(allLines[i]);

                if (thisAddr < addrStart || thisAddr > addrEnd) continue;

                if (section == "UNCONTENDED" && ramTopAddr > 0 && thisAddr >= ramTopAddr)
                {
                    // skip only the gap between ramtop and IM2 start
                    if (im2Addr > 0 && thisAddr < im2Addr) continue;
                }
                else if (section != "UNCONTENDED" && ramTopAddr > 0 && thisAddr >= ramTopAddr)
                {
                    // all other sections stop drawing at ramtop
                    continue;
                }

                int nextAddr = (i + 1 < allLines.Count) ? ExtractAddress(allLines[i + 1]) : addrEnd;

                // clamp next address to ramtop so last block doesn't overshoot
                // (only for non-UNCONTENDED sections)
                if (section != "UNCONTENDED" && ramTopAddr > 0 && nextAddr > ramTopAddr)
                    nextAddr = ramTopAddr;

                int clampedEnd = Math.Min(nextAddr, addrEnd);
                int yTop = panelH - (int)((double)(clampedEnd - addrStart) / totalRange * panelH);
                int yBottom = panelH - (int)((double)(thisAddr - addrStart) / totalRange * panelH);
                int height = Math.Max(yBottom - yTop, 1);

                string symbol, address, seg, sourceFile;
                ParseLine(allLines[i], out symbol, out address, out seg, out sourceFile);

                // skip sentinel ramtop markers — don't draw them as blocks
                if (symbol == "_uncontended_top" ||
                    symbol == "_ram0top" ||
                    symbol == "_ram1top" ||
                    symbol == "_ram3top" ||
                    symbol == "_ram4top" ||
                    symbol == "_ram6top" ||
                    symbol == "_contended_top")
                    continue;

                Color col = Color.Gray;
                if (!string.IsNullOrEmpty(sourceFile) && _fileColors.ContainsKey(sourceFile))
                    col = _fileColors[sourceFile];

                using (var brush = new SolidBrush(col))
                {
                    g.FillRectangle(brush, 0, yTop, panelW, height);
                    if (height > 1)
                        g.DrawLine(Pens.Black, 0, yTop, panelW, yTop);
                }
            }

            // draw free space indicators
            if (ramTopAddr > 0 && ramTopAddr < addrEnd)
            {
                if (section == "UNCONTENDED")
                {
                    // draw black gap between _uncontended_top and _IM2_PUSH_POP
                    if (im2Addr > 0 && im2Addr > ramTopAddr)
                    {
                        int yGapTop = panelH - (int)((double)(im2Addr - addrStart) / totalRange * panelH);
                        int yGapBottom = panelH - (int)((double)(ramTopAddr - addrStart) / totalRange * panelH);
                        g.FillRectangle(Brushes.Black, 0, yGapTop, panelW, yGapBottom - yGapTop);
                        g.DrawLine(Pens.White, 0, yGapBottom, panelW, yGapBottom);
                    }
                }
                else
                {
                    // normal case - black out everything above ramtop to top of panel
                    int yFreeBottom = panelH - (int)((double)(ramTopAddr - addrStart) / totalRange * panelH);
                    g.FillRectangle(Brushes.Black, 0, 0, panelW, yFreeBottom);
                    g.DrawLine(Pens.White, 0, yFreeBottom, panelW, yFreeBottom);
                }
            }
        }



        private int FindSymbolAddress(List<string> allLines, string symbolName)
        {
            foreach (string line in allLines)
            {
                string symbol, address, seg, sourceFile;
                ParseLine(line, out symbol, out address, out seg, out sourceFile);
                if (symbol == symbolName)
                    return ExtractAddress(line);
            }
            return 0;
        }

        private int FindRamTop(List<string> allLines, string section)
        {
            string topSymbol = "";
            switch (section)
            {
                case "CONTENDED": topSymbol = "_contended_top"; break;
                case "UNCONTENDED": topSymbol = "_uncontended_top"; break;
                case "RAM0": topSymbol = "_ram0top"; break;
                case "RAM1": topSymbol = "_ram1top"; break;
                case "RAM3": topSymbol = "_ram3top"; break;
                case "RAM4": topSymbol = "_ram4top"; break;
                case "RAM6": topSymbol = "_ram6top"; break;
            }

            if (string.IsNullOrEmpty(topSymbol)) return 0;

            // search raw lines - bypasses any ParseLine issues
            foreach (string line in allLines)
            {
                if (!line.Contains(topSymbol)) continue;

                int eqPos = line.IndexOf('=');
                if (eqPos < 0) continue;
                string sym = line.Substring(0, eqPos).Trim();
                if (sym == topSymbol)
                    return ExtractAddress(line);
            }

            return 0;
        }


        private void ShowPanelTooltip(MouseEventArgs e, Panel panel, string section, int addrStart, int addrEnd)
        {
            int totalRange = addrEnd - addrStart;
            int panelH = panel.Height;

            // convert mouse Y position back to an address
            // remember Y=0 is top ($FFFF) and Y=panelH is bottom ($addrStart)
            int addr = addrEnd - (int)((double)e.Y / panelH * totalRange);

            addr = Math.Max(addrStart, Math.Min(addrEnd, addr));
            richTextBox2.Text = "$" + addr.ToString("X4");


            // find which module contains this address
            if (!_modules.ContainsKey(section)) return;

            ModuleInfo found = null;
            foreach (var mod in _modules[section].Values)
            {
                if (addr >= mod.StartAddr && addr <= mod.EndAddr)
                {
                    found = mod;
                    break;
                }
            }

            string tipText = found != null
                ? string.Format("{0}\n$({1:X4}) - ${2:X4}\nSize: {3} bytes\nSymbols: {4}",
                    found.Name,
                    found.StartAddr,
                    found.EndAddr,
                    found.Size,
                    found.SymbolCount)
                : string.Format("Address: ${0:X4}", addr);

            // only update tooltip if text has changed to avoid flicker
            if (tipText != _lastToolTipText)
            {
                _lastToolTipText = tipText;
                _toolTip.Show(tipText, panel, e.X + 10, e.Y + 10, 3000);
            }
        }


        private void BuildModules()
        {
            _modules = new Dictionary<string, Dictionary<string, ModuleInfo>>();
            _fileColors = new Dictionary<string, Color>();

            string[] sections = { "CONTENDED", "UNCONTENDED", "RAM0", "RAM1", "RAM3", "RAM4", "RAM6", "LIBRARY" };

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
                Color.FromArgb(255, 255, 100),  // yellow
                Color.FromArgb(255, 100, 255),  // magenta
            };

            

            int globalColorIndex = 0;

            foreach (string sec in sections)
            {
                var moduleDict = new Dictionary<string, ModuleInfo>();

                List<string> allLines = new List<string>();
                allLines.AddRange(_public[sec]);
                allLines.AddRange(_local[sec]);

                foreach (string line in allLines)
                {
                    string symbol, address, section, sourceFile;
                    ParseLine(line, out symbol, out address, out section, out sourceFile);

                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    // assign colour globally per source file so same file
                    // gets same colour across all sections
                    if (!_fileColors.ContainsKey(sourceFile))
                    {
                        _fileColors[sourceFile] = palette[globalColorIndex % palette.Length];
                        globalColorIndex++;
                    }

                    if (!moduleDict.ContainsKey(sourceFile))
                    {
                        moduleDict[sourceFile] = new ModuleInfo
                        {
                            Name = sourceFile,
                            StartAddr = int.MaxValue,
                            EndAddr = int.MinValue,
                            ModuleColor = _fileColors[sourceFile]
                        };
                    }

                    var mod = moduleDict[sourceFile];
                    mod.Lines.Add(line);
                    mod.SymbolCount++;

                    int addr = ExtractAddress(line);
                    if (addr < mod.StartAddr) mod.StartAddr = addr;
                    if (addr > mod.EndAddr) mod.EndAddr = addr;
                }

                //System.Diagnostics.Debug.WriteLine("fileColors count: " + _fileColors.Count);
                /*if (_fileColors.ContainsKey("contended.c"))
                    Debug.WriteLine("contended.c color: " + _fileColors["contended.c"].ToString());
                else
                    Debug.WriteLine("contended.c NOT in fileColors");*/
                


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

            //if (symbol.StartsWith("_")) symbol = symbol.Substring(1);//not sure

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

            // strip full path, keep just filename
            sourceFile = Path.GetFileName(sourceFile);
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

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }

        private void Panel_MouseMove(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            string section = (string)panel.Tag;

            int addrStart, addrEnd;
            GetSectionRange(section, out addrStart, out addrEnd);

            int totalRange = addrEnd - addrStart;
            int panelH = panel.Height;

            // Convert Y pixel back to address (Y=0 is top = high address)
            /*int addr = addrEnd - (int)((double)e.Y / panelH * totalRange);
            addr = Math.Max(addrStart, Math.Min(addrEnd, addr));

            richTextBox2.Text = "$" + addr.ToString("X4");*/
        }

        private void GetSectionRange(string section, out int addrStart, out int addrEnd)
        {
            switch (section)
            {
                case "CONTENDED":   addrStart = 0x5DC0; addrEnd = 0x7FFF; break;
                case "UNCONTENDED": addrStart = 0x8000; addrEnd = 0xBFFF;                   break;
                default:            addrStart = 0xC000; addrEnd = 0xFFFF;                   break;
            }
        }






    }

}





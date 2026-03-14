using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;

namespace Z88dk_memory_visualizer
{
    public partial class zoom_panel : Form
    {
        private string _section;
        private int _addrStart;
        private int _addrEnd;
        private int _zoomStart;
        private int _zoomEnd;
        private List<string> _allLines;
        private Dictionary<string, Color> _fileColors;
        private int _ramTopAddr;
        private int _im2Addr;

        private Panel _canvas;
        private RichTextBox _infoBox;
        private ToolTip _toolTip = new ToolTip();
        private string _lastToolTip = "";

        private const int ADDR_MARGIN = 60;

        private VScrollBar _scrollBar;

        public zoom_panel(
            string section,
            int addrStart,
            int addrEnd,
            Dictionary<string, List<string>> publicData,
            Dictionary<string, List<string>> localData,
            Dictionary<string, Color> fileColors,
            int ramTopAddr,
            int im2Addr)
        {
            InitializeComponent();

            _section = section;
            _addrStart = addrStart;
            _addrEnd = addrEnd;
            _zoomStart = addrStart;
            _zoomEnd = addrEnd;
            _fileColors = fileColors ?? new Dictionary<string, Color>();
            _ramTopAddr = ramTopAddr;
            _im2Addr = im2Addr;

            // build combined sorted deduplicated line list
            var raw = new List<string>();
            if (publicData != null && publicData.ContainsKey(section))
                raw.AddRange(publicData[section]);
            if (localData != null && localData.ContainsKey(section))
                raw.AddRange(localData[section]);

            string[] topSymbols = {
                "_contended_top", "_uncontended_top",
                "_ram0top", "_ram1top", "_ram3top", "_ram4top", "_ram6top"
            };

            _allLines = raw
                .GroupBy(l => ExtractAddress(l))
                .Select(grp =>
                {
                    string top = null;
                    foreach (string candidate in grp)
                    {
                        string sym, a, seg, src;
                        ParseLine(candidate, out sym, out a, out seg, out src);
                        foreach (string ts in topSymbols)
                            if (sym == ts) { top = candidate; break; }
                        if (top != null) break;
                    }
                    return top ?? grp.First();
                })
                .OrderBy(l => ExtractAddress(l))
                .ToList();

            BuildForm();
        }

        private void BuildForm()
        {
            this.Text = string.Format("Zoom: {0}  (${1:X4} - ${2:X4})",
                _section, _addrStart, _addrEnd);
            this.Size = new Size(520, 800);
            this.MinimumSize = new Size(400, 400);

            // info box at bottom
            _infoBox = new RichTextBox();
            _infoBox.Dock = DockStyle.Bottom;
            _infoBox.Height = 80;
            _infoBox.ReadOnly = true;
            _infoBox.BackColor = Color.Black;
            _infoBox.ForeColor = Color.LimeGreen;
            _infoBox.Font = new Font("Courier New", 8f);
            this.Controls.Add(_infoBox);

            _scrollBar = new VScrollBar();
            _scrollBar.Dock = DockStyle.Right;
            _scrollBar.Width = 20;
            _scrollBar.Scroll += ScrollBar_Scroll;
            this.Controls.Add(_scrollBar);

            // canvas fills rest
            _canvas = new Panel();
            _canvas.Dock = DockStyle.Fill;
            _canvas.BackColor = Color.Black;
            MakeBuffered(_canvas);
            this.Controls.Add(_canvas);

            _canvas.Paint += (s, e) => DrawZoom(e.Graphics);
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseWheel += Canvas_MouseWheel;
            _canvas.Resize += (s, e) => _canvas.Invalidate();

            // ensure canvas can receive mouse wheel events
            _canvas.MouseEnter += (s, e) => _canvas.Focus();
        }

        private void ScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            int zoomRange = _zoomEnd - _zoomStart;

            // scrollbar value = distance from addrEnd downward
            int newEnd = _addrEnd - _scrollBar.Value;
            int newStart = newEnd - zoomRange;

            newStart = Math.Max(_addrStart, newStart);
            newEnd = Math.Min(_addrEnd, newStart + zoomRange);

            _zoomStart = newStart;
            _zoomEnd = newEnd;

            this.Text = string.Format("Zoom: {0}  (${1:X4} - ${2:X4}  {3} bytes)",
                _section, _zoomStart, _zoomEnd, _zoomEnd - _zoomStart);

            _canvas.Invalidate();
        }

        private void UpdateScrollBar()
        {
            int fullRange = _addrEnd - _addrStart;
            int zoomRange = _zoomEnd - _zoomStart;

            _scrollBar.Minimum = 0;
            _scrollBar.Maximum = fullRange;
            _scrollBar.LargeChange = zoomRange;
            _scrollBar.SmallChange = Math.Max(1, zoomRange / 16);

            // scrollbar value = distance from bottom
            // value 0 = viewing bottom of section, value max = viewing top
            _scrollBar.Value = Math.Min(
                _scrollBar.Maximum - _scrollBar.LargeChange,
                Math.Max(0, _addrEnd - _zoomEnd));
        }

        private void DrawZoom(Graphics g)
        {
            if (_allLines == null) return;

            int totalRange = _zoomEnd - _zoomStart;
            if (totalRange <= 0) return;

            int panelH = _canvas.Height;
            int panelW = _canvas.Width;
            int blockW = panelW - ADDR_MARGIN;

            g.FillRectangle(Brushes.Black, 0, 0, panelW, panelH);

            // address labels on left margin
            using (var labelFont = new Font("Courier New", 7f))
            using (var labelBrush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            {
                int labelStep = Math.Max(256, (Math.Max(1, totalRange / (panelH / 32)) / 256) * 256);
                int firstLabel = (_zoomStart / labelStep) * labelStep;
                for (int a = firstLabel; a <= _zoomEnd; a += labelStep)
                {
                    if (a < _zoomStart) continue;
                    int y = panelH - (int)((double)(a - _zoomStart) / totalRange * panelH);
                    if (y < 0 || y > panelH) continue;
                    g.DrawLine(Pens.DimGray, ADDR_MARGIN, y, panelW, y);
                    g.DrawString("$" + a.ToString("X4"), labelFont, labelBrush, 0, y - 8);
                }
            }

            // draw blocks
            using (var symFont = new Font("Courier New", 7f))
            {
                for (int i = 0; i < _allLines.Count; i++)
                {
                    int thisAddr = ExtractAddress(_allLines[i]);

                    // skip outside zoom window
                    if (thisAddr < _zoomStart || thisAddr > _zoomEnd) continue;

                    // skip gap region for UNCONTENDED
                    if (_section == "UNCONTENDED" && _ramTopAddr > 0 && thisAddr >= _ramTopAddr)
                        if (_im2Addr > 0 && thisAddr < _im2Addr) continue;

                    // stop at ramtop for other sections
                    if (_section != "UNCONTENDED" && _ramTopAddr > 0 && thisAddr >= _ramTopAddr)
                        continue;

                    int nextAddr = (i + 1 < _allLines.Count)
                        ? ExtractAddress(_allLines[i + 1]) : _addrEnd;

                    if (_section != "UNCONTENDED" && _ramTopAddr > 0 && nextAddr > _ramTopAddr)
                        nextAddr = _ramTopAddr;

                    int clampedEnd = Math.Min(nextAddr, _zoomEnd);
                    int yTop = panelH - (int)((double)(clampedEnd - _zoomStart) / totalRange * panelH);
                    int yBottom = panelH - (int)((double)(thisAddr - _zoomStart) / totalRange * panelH);
                    int height = Math.Max(yBottom - yTop, 1);

                    // clamp to panel
                    yTop = Math.Max(0, Math.Min(panelH, yTop));
                    yBottom = Math.Max(0, Math.Min(panelH, yBottom));
                    height = Math.Max(1, yBottom - yTop);

                    string symbol, address, seg, sourceFile;
                    ParseLine(_allLines[i], out symbol, out address, out seg, out sourceFile);

                    // skip sentinel markers
                    if (symbol == "_contended_top" || symbol == "_uncontended_top" ||
                        symbol == "_ram0top" || symbol == "_ram1top" ||
                        symbol == "_ram3top" || symbol == "_ram4top" ||
                        symbol == "_ram6top")
                        continue;

                    Color col = Color.FromArgb(60, 60, 60);
                    if (!string.IsNullOrEmpty(sourceFile) && _fileColors.ContainsKey(sourceFile))
                        col = _fileColors[sourceFile];

                    using (var brush = new SolidBrush(col))
                        g.FillRectangle(brush, ADDR_MARGIN, yTop, blockW, height);

                    if (height > 1)
                        g.DrawLine(Pens.Black, ADDR_MARGIN, yTop, panelW, yTop);

                    // draw symbol name if block tall enough
                    if (height >= 10)
                    {
                        Color textCol = IsBright(col) ? Color.Black : Color.White;
                        using (var tb = new SolidBrush(textCol))
                        {
                            string label = string.IsNullOrEmpty(symbol) ? "?" :
                                (symbol.Length > 30 ? symbol.Substring(0, 30) + "…" : symbol);
                            g.DrawString(label, symFont, tb,
                                ADDR_MARGIN + 2, yTop + (height - 10) / 2);
                        }
                    }
                }
            }

            // draw free space overlay
            if (_ramTopAddr > 0 && _ramTopAddr < _addrEnd)
            {
                if (_section == "UNCONTENDED" && _im2Addr > 0 && _im2Addr > _ramTopAddr)
                {
                    // only draw gap if it's within the zoom window
                    if (_ramTopAddr <= _zoomEnd && _im2Addr >= _zoomStart)
                    {
                        int gapStart = Math.Max(_ramTopAddr, _zoomStart);
                        int gapEnd = Math.Min(_im2Addr, _zoomEnd);
                        int yGapTop = panelH - (int)((double)(gapEnd - _zoomStart) / totalRange * panelH);
                        int yGapBottom = panelH - (int)((double)(gapStart - _zoomStart) / totalRange * panelH);
                        g.FillRectangle(Brushes.Black, ADDR_MARGIN, yGapTop, blockW, yGapBottom - yGapTop);
                        g.DrawLine(Pens.White, ADDR_MARGIN, yGapBottom, panelW, yGapBottom);
                        int freeBytes = _im2Addr - _ramTopAddr;
                        using (var f = new Font("Courier New", 7f))
                        using (var b = new SolidBrush(Color.FromArgb(150, 150, 150)))
                            g.DrawString(string.Format("{0} bytes free", freeBytes),
                                f, b, ADDR_MARGIN + 2, yGapTop + 2);
                    }
                }
                else if (_section != "UNCONTENDED" && _ramTopAddr >= _zoomStart)
                {
                    int yFreeBottom = panelH - (int)((double)(_ramTopAddr - _zoomStart) / totalRange * panelH);
                    yFreeBottom = Math.Max(0, Math.Min(panelH, yFreeBottom));
                    g.FillRectangle(Brushes.Black, ADDR_MARGIN, 0, blockW, yFreeBottom);
                    g.DrawLine(Pens.White, ADDR_MARGIN, yFreeBottom, panelW, yFreeBottom);
                    int freeBytes = _addrEnd - _ramTopAddr;
                    using (var f = new Font("Courier New", 7f))
                    using (var b = new SolidBrush(Color.FromArgb(150, 150, 150)))
                        g.DrawString(string.Format("{0} bytes free", freeBytes),
                            f, b, ADDR_MARGIN + 2, 2);
                }
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            int totalRange = _zoomEnd - _zoomStart;
            if (totalRange <= 0) return;

            int panelH = _canvas.Height;
            int addr = _zoomEnd - (int)((double)e.Y / panelH * totalRange);
            addr = Math.Max(_addrStart, Math.Min(_addrEnd, addr));

            string foundSymbol = "";
            string foundSource = "";
            int foundSize = 0;

            for (int i = 0; i < _allLines.Count; i++)
            {
                int thisAddr = ExtractAddress(_allLines[i]);
                int nextAddr = (i + 1 < _allLines.Count)
                    ? ExtractAddress(_allLines[i + 1]) : _addrEnd;

                if (addr >= thisAddr && addr < nextAddr)
                {
                    string sym, a, seg, src;
                    ParseLine(_allLines[i], out sym, out a, out seg, out src);
                    foundSymbol = sym;
                    foundSource = src;
                    foundSize = nextAddr - thisAddr;
                    break;
                }
            }

            string info = string.IsNullOrEmpty(foundSymbol)
                ? string.Format("${0:X4}", addr)
                : string.Format(
                    "Addr:   ${0:X4}\r\nSymbol: {1}\r\nSize:   {2} bytes\r\nSource: {3}",
                    addr, foundSymbol, foundSize, foundSource);

            if (_infoBox != null) _infoBox.Text = info;

            if (foundSymbol != _lastToolTip)
            {
                _lastToolTip = foundSymbol;
                if (!string.IsNullOrEmpty(foundSymbol))
                    _toolTip.Show(foundSymbol, _canvas, e.X + 12, e.Y, 2000);
            }
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            int totalRange = _zoomEnd - _zoomStart;
            if (totalRange <= 0) return;

            int panelH = _canvas.Height;
            int mouseAddr = _zoomEnd - (int)((double)e.Y / panelH * totalRange);
            mouseAddr = Math.Max(_addrStart, Math.Min(_addrEnd, mouseAddr));

            // each wheel notch zooms 20%
            double factor = e.Delta > 0 ? 0.8 : 1.25;
            int newRange = (int)(totalRange * factor);

            // clamp range — min 256 bytes, max full section
            newRange = Math.Max(256, Math.Min(_addrEnd - _addrStart, newRange));

            // keep mouse address at same proportional position
            double mouseRatio = (double)(mouseAddr - _zoomStart) / totalRange;
            int newStart = mouseAddr - (int)(mouseRatio * newRange);
            int newEnd = newStart + newRange;

            // clamp to section bounds
            if (newStart < _addrStart) { newStart = _addrStart; newEnd = newStart + newRange; }
            if (newEnd > _addrEnd) { newEnd = _addrEnd; newStart = newEnd - newRange; }
            newStart = Math.Max(_addrStart, newStart);
            newEnd = Math.Min(_addrEnd, newEnd);

            _zoomStart = newStart;
            _zoomEnd = newEnd;

            this.Text = string.Format("Zoom: {0}  (${1:X4} - ${2:X4}  {3} bytes)",
                _section, _zoomStart, _zoomEnd, _zoomEnd - _zoomStart);

            _canvas.Invalidate();
        }

        private bool IsBright(Color c)
        {
            return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 150;
        }

        private void MakeBuffered(Panel p)
        {
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                .SetValue(p, true, null);
        }

        private void ParseLine(string line, out string symbol, out string address,
            out string section, out string sourceFile)
        {
            symbol = ""; address = ""; section = ""; sourceFile = "";
            if (string.IsNullOrEmpty(line)) return;
            int eqPos = line.IndexOf('=');
            if (eqPos < 0) return;
            symbol = line.Substring(0, eqPos).Trim();
            int dollarPos = line.IndexOf('$');
            if (dollarPos >= 0 && dollarPos + 5 <= line.Length)
                address = "$" + line.Substring(dollarPos + 1, 4).Trim();
            string[] parts = line.Split(',');
            if (parts.Length < 5) return;
            section = parts[4].Trim();
            sourceFile = parts.Length > 5 ? parts[5].Trim() : "";
            int colon = sourceFile.LastIndexOf(':');
            if (colon > 0) sourceFile = sourceFile.Substring(0, colon);
            sourceFile = Path.GetFileName(sourceFile);
        }

        private int ExtractAddress(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int dollarPos = line.IndexOf('$');
            if (dollarPos < 0 || dollarPos + 5 > line.Length) return 0;
            string hex = line.Substring(dollarPos + 1, 4);
            int addr;
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out addr))
                return addr;
            return 0;
        }
    }
}
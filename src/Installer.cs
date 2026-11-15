using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetCoreSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1].ToLowerInvariant().StartsWith("/silent"))
            {
                SilentInstall.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    public class SetupForm : Form
    {
        private static readonly Color BgTop = Color.FromArgb(17, 19, 28);
        private static readonly Color BgBottom = Color.FromArgb(10, 11, 17);
        private static readonly Color TextMain = Color.FromArgb(236, 240, 248);
        private static readonly Color TextDim = Color.FromArgb(138, 150, 172);
        private static readonly Color AccentA = Color.FromArgb(59, 130, 246);
        private static readonly Color AccentB = Color.FromArgb(139, 92, 246);
        private static readonly Color Good = Color.FromArgb(52, 211, 153);

        private readonly string _target;
        private Label _statusLine;
        private Label _pathLine;
        private Bar _bar;
        private RoundButton _install;
        private RoundButton _close;
        private RoundCheck _autostart;
        private RoundCheck _launch;
        private bool _busy;

        public SetupForm()
        {
            _target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Prosvet");
            string envT = Environment.GetEnvironmentVariable("NTCORE_TARGET");
            if (!string.IsNullOrEmpty(envT)) _target = envT;
            BuildUi();
            if (Environment.GetEnvironmentVariable("AUTO_INSTALL") == "1") { Timer t0 = new Timer(); t0.Interval = 600; t0.Tick += delegate { t0.Stop(); Install(); }; t0.Start(); }
        }

        private void BuildUi()
        {
            Text = "Установка";
            ClientSize = new Size(540, 372);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            BackColor = BgTop;
            Font = new Font("Segoe UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label title = new Label();
            title.Text = "УСТАНОВКА";
            title.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            title.ForeColor = TextMain;
            title.BackColor = Color.Transparent;
            title.SetBounds(66, 14, 300, 24);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "просвет · локальный обход · без подписок";
            sub.Font = new Font("Segoe UI", 8F);
            sub.ForeColor = TextDim;
            sub.BackColor = Color.Transparent;
            sub.SetBounds(66, 38, 340, 16);
            Controls.Add(sub);

            _close = new RoundButton();
            _close.Caption = "×";
            _close.Radius = 8;
            _close.FillA = Color.FromArgb(34, 38, 52);
            _close.FillB = Color.FromArgb(34, 38, 52);
            _close.TextColor = TextDim;
            _close.SetBounds(492, 16, 30, 28);
            _close.Click += delegate { if (!_busy) Close(); };
            Controls.Add(_close);

            CardPanel card = new CardPanel();
            card.SetBounds(20, 72, 500, 108);
            Controls.Add(card);

            Label what = new Label();
            what.Text = "Будет установлено";
            what.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            what.ForeColor = TextMain;
            what.BackColor = Color.Transparent;
            what.SetBounds(20, 16, 300, 20);
            card.Controls.Add(what);

            Label items = new Label();
            items.Text = "программа, движок обхода, 27 профилей и списки адресов";
            items.Font = new Font("Segoe UI", 8.5F);
            items.ForeColor = TextDim;
            items.BackColor = Color.Transparent;
            items.SetBounds(20, 40, 460, 18);
            card.Controls.Add(items);

            _pathLine = new Label();
            _pathLine.Text = "Куда: " + _target;
            _pathLine.Font = new Font("Segoe UI", 8F);
            _pathLine.ForeColor = Color.FromArgb(110, 122, 145);
            _pathLine.BackColor = Color.Transparent;
            _pathLine.SetBounds(20, 66, 464, 18);
            card.Controls.Add(_pathLine);

            _bar = new Bar();
            _bar.SetBounds(20, 196, 500, 10);
            Controls.Add(_bar);

            _statusLine = new Label();
            _statusLine.Text = "готов к установке";
            _statusLine.Font = new Font("Segoe UI", 8.5F);
            _statusLine.ForeColor = TextDim;
            _statusLine.BackColor = Color.Transparent;
            _statusLine.SetBounds(22, 212, 480, 18);
            Controls.Add(_statusLine);

            _autostart = new RoundCheck();
            _autostart.Caption = "Автозапуск при включении Windows";
            _autostart.Font = new Font("Segoe UI", 8.5F);
            _autostart.SetBounds(20, 242, 420, 24);
            _autostart.Checked = true;
            Controls.Add(_autostart);

            _launch = new RoundCheck();
            _launch.Caption = "Запустить после установки";
            _launch.Font = new Font("Segoe UI", 8.5F);
            _launch.SetBounds(20, 272, 420, 24);
            _launch.Checked = true;
            Controls.Add(_launch);

            _install = new RoundButton();
            _install.Caption = "УСТАНОВИТЬ";
            _install.Radius = 14;
            _install.FillA = AccentA;
            _install.FillB = AccentB;
            _install.TextColor = Color.White;
            _install.Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold);
            _install.SetBounds(20, 308, 500, 48);
            _install.Click += delegate { Install(); };
            Controls.Add(_install);

            MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            sub.MouseDown += DragWindow;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush bg = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), BgTop, BgBottom, 90f))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            try { if (Icon != null) g.DrawIcon(Icon, new Rectangle(22, 14, 32, 32)); } catch { }
            using (Pen p = new Pen(Color.FromArgb(38, 43, 60), 1f))
            using (GraphicsPath path = MainRound(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 16f))
            {
                g.DrawPath(p, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = MainRound(new RectangleF(0, 0, Width, Height), 16f))
            {
                Region = new Region(path);
            }
        }

        public static GraphicsPath MainRound(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ================= установка =================
        private void Install()
        {
            if (_busy) return;
            _busy = true;
            _install.Enabled = false;
            _install.Caption = "УСТАНОВКА...";
            _autostart.Enabled = false;
            _launch.Enabled = false;

            Task.Factory.StartNew(delegate
            {
                try
                {
                    SetStatus("распаковка файлов...", 3);
                    string temp = Path.Combine(Path.GetTempPath(), "netcore_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(temp);

                    string zip = Path.Combine(temp, "payload.zip");
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                    {
                        if (s == null) throw new Exception("В установщике нет данных (payload.zip).");
                        using (FileStream fs = File.Create(zip)) { s.CopyTo(fs); }
                    }

                    string unpack = Path.Combine(temp, "files");
                    ZipFile.ExtractToDirectory(zip, unpack);

                    if (!Directory.Exists(_target)) Directory.CreateDirectory(_target);

                    string[] files = Directory.GetFiles(unpack, "*", SearchOption.AllDirectories);
                    int done = 0;
                    foreach (string src in files)
                    {
                        string rel = src.Substring(unpack.Length).TrimStart('\\');
                        string dst = Path.Combine(_target, rel);
                        string dir = Path.GetDirectoryName(dst);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        File.Copy(src, dst, true);
                        done++;
                        SetStatus("копирование: " + rel, 5 + (int)(80.0 * done / files.Length));
                    }

                    SetStatus("ярлыки...", 88);
                    CreateShortcuts();

                    if (_autostart.Checked && Environment.GetEnvironmentVariable("NTCORE_TEST") != "1")
                    {
                        SetStatus("первый запуск движка...", 93);
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                                string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -Action on",
                                Path.Combine(_target, "engine", "bypass.ps1")));
                            psi.UseShellExecute = false;
                            psi.CreateNoWindow = true;
                            psi.WorkingDirectory = _target;
                            using (Process p = Process.Start(psi)) { p.WaitForExit(150000); }
                        }
                        catch { }
                    }

                    try { Directory.Delete(temp, true); } catch { }

                    SetStatus("готово", 100);
                    Invoke((Action)delegate
                    {
                        _bar.Value = 100;
                        _statusLine.ForeColor = Good;
                        _install.Caption = "ГОТОВО";
                        _busy = false;
                        if (_launch.Checked)
                        {
                            try { Process.Start(Path.Combine(_target, "Просвет.exe")); } catch { }
                        }
                        Timer t = new Timer();
                        t.Interval = 1200;
                        t.Tick += delegate { t.Stop(); Close(); };
                        t.Start();
                    });
                }
                catch (Exception ex)
                {
                    Invoke((Action)delegate
                    {
                        _busy = false;
                        _install.Enabled = true;
                        _install.Caption = "ПОВТОРИТЬ";
                        _statusLine.ForeColor = Color.FromArgb(248, 113, 113);
                        _statusLine.Text = "ошибка: " + ex.Message;
                    });
                }
            });
        }

        private void SetStatus(string text, int percent)
        {
            try
            {
                Invoke((Action)delegate
                {
                    _statusLine.Text = text;
                    _bar.Value = percent;
                });
            }
            catch { }
        }

        private void CreateShortcuts()
        {
            if (Environment.GetEnvironmentVariable("NTCORE_TEST") == "1") return;
            string exe = Path.Combine(_target, "Просвет.exe");
            MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Просвет.lnk"), exe, _target);
            try
            {
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Просвет.lnk");
                MakeShortcut(startMenu, exe, _target);
            }
            catch { }
        }

        internal static void MakeShortcut(string lnkPath, string target, string workDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Просвет" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            catch { }
        }
    }

    internal static class SilentInstall
    {
        public static void Run()
        {
            try
            {
                string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Prosvet");
                string envT = Environment.GetEnvironmentVariable("NTCORE_TARGET");
                if (!string.IsNullOrEmpty(envT)) target = envT;

                bool noKill = Environment.GetEnvironmentVariable("NTCORE_NOKILL") == "1";
                foreach (string name in noKill ? new string[0] : new string[] { "Просвет", "winws", "winws2" })
                {
                    try
                    {
                        foreach (Process p in Process.GetProcessesByName(name))
                        {
                            try { p.Kill(); } catch { }
                        }
                    }
                    catch { }
                }
                System.Threading.Thread.Sleep(1200);

                string temp = Path.Combine(Path.GetTempPath(), "prosvet_upd_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                string zip = Path.Combine(temp, "payload.zip");
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                using (FileStream fs = File.Create(zip)) { s.CopyTo(fs); }

                string unpack = Path.Combine(temp, "files");
                ZipFile.ExtractToDirectory(zip, unpack);

                if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                foreach (string src in Directory.GetFiles(unpack, "*", SearchOption.AllDirectories))
                {
                    string rel = src.Substring(unpack.Length).TrimStart('\\');
                    string dst = Path.Combine(target, rel);
                    string dir = Path.GetDirectoryName(dst);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.Copy(src, dst, true);
                }

                string exe = Path.Combine(target, "Просвет.exe");
                try
                {
                    SetupForm.MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Просвет.lnk"), exe, target);
                    SetupForm.MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Просвет.lnk"), exe, target);
                }
                catch { }

                try { Directory.Delete(temp, true); } catch { }
                if (File.Exists(exe)) Process.Start(exe);
            }
            catch { }
        }
    }

    // ---------- контролы ----------
    public class Bar : Control
    {
        private int _value;
        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0, 0, Width, Height);
            using (GraphicsPath p = SetupForm.MainRound(r, Height / 2f))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 33, 44))) g.FillPath(b, p);
            }
            if (_value > 0)
            {
                float w = Math.Max(Height, Width * (_value / 100f));
                RectangleF r2 = new RectangleF(0, 0, w, Height);
                using (GraphicsPath p = SetupForm.MainRound(r2, Height / 2f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        Rectangle.Round(new RectangleF(0, 0, Math.Max(2, w), Math.Max(2, Height))),
                        Color.FromArgb(59, 130, 246), Color.FromArgb(139, 92, 246), 0f))
                    {
                        g.FillPath(b, p);
                    }
                }
            }
        }
    }

    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath path = SetupForm.MainRound(r, 14f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    Rectangle.Round(new RectangleF(1, 1, Width - 2, Height - 2)),
                    Color.FromArgb(30, 34, 48), Color.FromArgb(22, 25, 36), 90f))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(44, 50, 70), 1f)) g.DrawPath(p, path);
            }
        }
    }

    public class RoundButton : Control
    {
        private bool _hover;
        private bool _down;

        public string Caption = "";
        public int Radius = 12;
        public Color FillA = Color.FromArgb(40, 44, 60);
        public Color FillB = Color.FromArgb(40, 44, 60);
        public Color TextColor = Color.White;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color a = FillA, b = FillB;
            if (!Enabled) { a = Color.FromArgb(30, 33, 44); b = Color.FromArgb(30, 33, 44); }
            else if (_down) { a = Darken(FillA, 0.82f); b = Darken(FillB, 0.82f); }
            else if (_hover) { a = Lighten(FillA, 1.18f); b = Lighten(FillB, 1.18f); }

            using (GraphicsPath path = SetupForm.MainRound(r, Radius))
            {
                using (LinearGradientBrush br = new LinearGradientBrush(
                    Rectangle.Round(new RectangleF(1, 1, Math.Max(2, Width - 2), Math.Max(2, Height - 2))), a, b, 90f))
                {
                    g.FillPath(br, path);
                }
                using (Pen p = new Pen(Color.FromArgb(30, 255, 255, 255), 1f)) g.DrawPath(p, path);
            }

            TextRenderer.DrawText(g, Caption, Font, new Rectangle(0, 0, Width, Height),
                Enabled ? TextColor : Color.FromArgb(110, 118, 138),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A, Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k)));
        }
        private static Color Darken(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }
    }

    public class RoundCheck : Control
    {
        private bool _checked = true;
        private bool _hover;

        public string Caption = "";

        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); }
        }

        public RoundCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { _checked = !_checked; Invalidate(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF box = new RectangleF(0.5f, (Height - 18) / 2f + 0.5f, 18f, 18f);
            using (GraphicsPath p = SetupForm.MainRound(box, 5f))
            {
                if (_checked)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        Rectangle.Round(new RectangleF(1, 1, 16, 16)),
                        Color.FromArgb(59, 130, 246), Color.FromArgb(139, 92, 246), 45f))
                    {
                        g.FillPath(b, p);
                    }
                    using (Pen pen = new Pen(Color.White, 1.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, new PointF[] {
                            new PointF(box.X + 4.2f, box.Y + 9.2f),
                            new PointF(box.X + 7.6f, box.Y + 12.6f),
                            new PointF(box.X + 13.6f, box.Y + 5.6f)
                        });
                    }
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(_hover ? Color.FromArgb(44, 48, 64) : Color.FromArgb(30, 33, 44)))
                    {
                        g.FillPath(b, p);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(70, 76, 96), 1f)) g.DrawPath(pen, p);
                }
            }

            TextRenderer.DrawText(g, Caption, Font, new Rectangle(26, 0, Width - 26, Height),
                _hover ? Color.FromArgb(190, 200, 216) : Color.FromArgb(138, 150, 172),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}

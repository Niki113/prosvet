using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Obhod
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool created;
            _mutex = new Mutex(true, "NetCoreApp_v2", out created);
            if (!created)
            {
                MessageBox.Show("Программа уже запущена — ищите значок в трее.", "Просвет",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        // ---- палитра ----
        private static readonly Color BgTop = Color.FromArgb(17, 19, 28);
        private static readonly Color BgBottom = Color.FromArgb(10, 11, 17);
        private static readonly Color CardBg = Color.FromArgb(25, 28, 40);
        private static readonly Color AccentA = Color.FromArgb(59, 130, 246);
        private static readonly Color AccentB = Color.FromArgb(139, 92, 246);
        private static readonly Color Good = Color.FromArgb(52, 211, 153);
        private static readonly Color Bad = Color.FromArgb(248, 113, 113);
        private static readonly Color Idle = Color.FromArgb(148, 163, 184);
        private static readonly Color TextMain = Color.FromArgb(236, 240, 248);
        private static readonly Color TextDim = Color.FromArgb(138, 150, 172);

        private readonly string _root;
        private readonly string _ps1;
        private readonly string _chosenFile;
        private readonly string _autoFlag;
        private readonly string _wantFlag;

        private const string AppVersion = "1.0.0";
        private readonly string _updateUrl;
        private string _newVersion;
        private string _newUrl;
        private string _newSha;
        private bool _hasUpdate;

        private Label _title;
        private Label _subtitle;
        private Label _status;
        private Label _strategy;
        private Label _hint;
        private RoundButton _toggle;
        private RoundButton _check;
        private RoundButton _tune;
        private RoundButton _auto;
        private RoundButton _min;
        private RoundButton _close;
        private RoundButton _folder;
        private RoundButton _support;
        private RoundCheck _autoChk;
        private RichTextBox _log;
        private NotifyIcon _tray;
        private System.Windows.Forms.Timer _timer;
        private System.Windows.Forms.Timer _health;
        private Process _proc;
        private bool _busy;
        private bool _reallyClose;
        private DateTime _lastHealth = DateTime.MinValue;

        public MainForm()
        {
            _root = Path.GetDirectoryName(Application.ExecutablePath);
            _ps1 = Path.Combine(_root, "engine", "bypass.ps1");
            _chosenFile = Path.Combine(_root, "state", "chosen.txt");
            _autoFlag = Path.Combine(_root, "state", "autostart.on");
            _wantFlag = Path.Combine(_root, "state", "want.on");
            _updateUrl = Environment.GetEnvironmentVariable("PROSVET_UPDATE_URL");
            if (string.IsNullOrEmpty(_updateUrl))
                _updateUrl = "https://raw.githubusercontent.com/Niki113/prosvet/main/version.json";

            BuildUi();
            BuildTray();
            RefreshStatus();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 2000;
            _timer.Tick += delegate { RefreshStatus(); };
            _timer.Start();

            _health = new System.Windows.Forms.Timer();
            _health.Interval = 45000;
            _health.Tick += delegate { HealthTick(); };
            _health.Start();

            Load += delegate { Task.Factory.StartNew(CheckUpdates); };
        }

        // ================= UI =================
        private void BuildUi()
        {
            Text = "Просвет";
            ClientSize = new Size(600, 712);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            BackColor = BgTop;
            Font = new Font("Segoe UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _title = new Label();
            _title.Text = "ПРОСВЕТ";
            _title.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            _title.ForeColor = TextMain;
            _title.BackColor = Color.Transparent;
            _title.SetBounds(66, 12, 200, 20);
            Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.Text = "свет сквозь блокировки";
            _subtitle.Font = new Font("Segoe UI", 8F);
            _subtitle.ForeColor = TextDim;
            _subtitle.BackColor = Color.Transparent;
            _subtitle.SetBounds(66, 32, 260, 16);
            Controls.Add(_subtitle);

            _min = new RoundButton();
            _min.Caption = "—";
            _min.Radius = 8;
            _min.FillA = Color.FromArgb(34, 38, 52);
            _min.FillB = Color.FromArgb(34, 38, 52);
            _min.TextColor = TextDim;
            _min.Font = new Font("Segoe UI", 9F);
            _min.SetBounds(500, 14, 34, 30);
            _min.Click += delegate { WindowState = FormWindowState.Minimized; };
            Controls.Add(_min);

            _close = new RoundButton();
            _close.Caption = "×";
            _close.Radius = 8;
            _close.FillA = Color.FromArgb(34, 38, 52);
            _close.FillB = Color.FromArgb(34, 38, 52);
            _close.TextColor = TextDim;
            _close.Font = new Font("Segoe UI", 11F);
            _close.SetBounds(542, 14, 34, 30);
            _close.Click += delegate
            {
                Hide();
                _tray.ShowBalloonTip(1200, "Обход продолжает работать", "Свёрнуто в трей.", ToolTipIcon.Info);
            };
            Controls.Add(_close);

            CardPanel card = new CardPanel();
            card.SetBounds(20, 64, 560, 118);
            Controls.Add(card);

            _status = new Label();
            _status.Text = "ПРОВЕРКА...";
            _status.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
            _status.ForeColor = Idle;
            _status.BackColor = Color.Transparent;
            _status.SetBounds(28, 20, 480, 34);
            card.Controls.Add(_status);

            _strategy = new Label();
            _strategy.Text = "—";
            _strategy.Font = new Font("Segoe UI", 9F);
            _strategy.ForeColor = TextDim;
            _strategy.BackColor = Color.Transparent;
            _strategy.SetBounds(30, 60, 500, 20);
            card.Controls.Add(_strategy);

            _hint = new Label();
            _hint.Text = "";
            _hint.Font = new Font("Segoe UI", 8F);
            _hint.ForeColor = Color.FromArgb(110, 122, 145);
            _hint.BackColor = Color.Transparent;
            _hint.SetBounds(30, 84, 500, 18);
            card.Controls.Add(_hint);

            _toggle = new RoundButton();
            _toggle.Caption = "ВКЛЮЧИТЬ ОБХОД";
            _toggle.Radius = 16;
            _toggle.FillA = AccentA;
            _toggle.FillB = AccentB;
            _toggle.TextColor = Color.White;
            _toggle.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            _toggle.SetBounds(20, 196, 560, 62);
            _toggle.Click += delegate { ToggleBypass(); };
            Controls.Add(_toggle);

            _check = MakeSecondary("Проверить");
            _check.SetBounds(20, 274, 176, 44);
            _check.Click += delegate { RunAction("check", "Проверка доступа"); };
            Controls.Add(_check);

            _tune = MakeSecondary("Переподбор");
            _tune.SetBounds(212, 274, 176, 44);
            _tune.Click += delegate { RunAction("retune", "Переподбор профиля"); };
            Controls.Add(_tune);

            _auto = MakeSecondary("Автозапуск");
            _auto.SetBounds(404, 274, 176, 44);
            _auto.Click += delegate { ToggleAutostart(); };
            Controls.Add(_auto);

            _autoChk = new RoundCheck();
            _autoChk.Caption = "Авто-режим: следить и переподбирать сам";
            _autoChk.Font = new Font("Segoe UI", 8.5F);
            _autoChk.SetBounds(22, 378, 360, 24);
            _autoChk.Checked = true;
            Controls.Add(_autoChk);

            _folder = new RoundButton();
            _folder.Caption = "Папка";
            _folder.Radius = 8;
            _folder.FillA = CardBg;
            _folder.FillB = CardBg;
            _folder.TextColor = TextDim;
            _folder.Font = new Font("Segoe UI", 8.5F);
            _folder.SetBounds(500, 376, 80, 24);
            _folder.Click += delegate { try { Process.Start(_root); } catch { } };
            Controls.Add(_folder);

            LogPanel logCard = new LogPanel();
            logCard.SetBounds(20, 410, 560, 282);
            Controls.Add(logCard);

            _log = new RichTextBox();
            _log.SetBounds(14, 14, 532, 254);
            _log.BackColor = Color.FromArgb(13, 15, 21);
            _log.ForeColor = Color.FromArgb(176, 186, 204);
            _log.BorderStyle = BorderStyle.None;
            _log.Font = new Font("Consolas", 8.5F);
            _log.ReadOnly = true;
            _log.WordWrap = true;
            _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.DetectUrls = false;
            logCard.Controls.Add(_log);

            _log.Text = "Готово к работе. Нажмите «Проверить», чтобы увидеть доступность сайтов." + Environment.NewLine;
            _support = new RoundButton();
            _support.Caption = "♥   ПОДДЕРЖАТЬ ПРОЕКТ";
            _support.Radius = 12;
            _support.FillA = Color.FromArgb(59, 42, 60);
            _support.FillB = Color.FromArgb(74, 48, 66);
            _support.TextColor = Color.FromArgb(250, 179, 205);
            _support.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            _support.SetBounds(20, 330, 560, 38);
            _support.Click += delegate { ShowSupport(); };
            Controls.Add(_support);

            FormClosing += OnFormClosing;
            MouseDown += DragWindow;
            _title.MouseDown += DragWindow;
            _subtitle.MouseDown += DragWindow;
        }

        private RoundButton MakeSecondary(string text)
        {
            RoundButton b = new RoundButton();
            b.Caption = text;
            b.Radius = 12;
            b.FillA = Color.FromArgb(31, 35, 49);
            b.FillB = Color.FromArgb(31, 35, 49);
            b.TextColor = TextMain;
            b.Font = new Font("Segoe UI", 10F);
            return b;
        }

        private void ShowSupport()
        {
            string cfg = Path.Combine(_root, "support.txt");
            SupportForm sf = new SupportForm(cfg);
            sf.StartPosition = FormStartPosition.Manual;
            sf.Location = new Point(Left + (Width - sf.Width) / 2, Top + (Height - sf.Height) / 2);
            sf.Show(this);
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

            try
            {
                if (Icon != null) g.DrawIcon(Icon, new Rectangle(22, 14, 32, 32));
            }
            catch { }

            using (Pen p = new Pen(Color.FromArgb(38, 43, 60), 1f))
            using (GraphicsPath path = Round(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 16f))
            {
                g.DrawPath(p, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = Round(new RectangleF(0, 0, Width, Height), 16f))
            {
                Region = new Region(path);
            }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
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

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ================= трей =================
        private void BuildTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Icon != null ? Icon : SystemIcons.Shield; } catch { _tray.Icon = SystemIcons.Application; }
            _tray.Text = "Просвет";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Показать", null, delegate { ShowApp(); });
            menu.Items.Add("Включить обход", null, delegate { RunAction("on", "Включение обхода"); });
            menu.Items.Add("Выключить обход", null, delegate { RunAction("off", "Выключение обхода"); });
            menu.Items.Add("Проверить сайты", null, delegate { RunAction("check", "Проверка доступа"); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, delegate
            {
                _reallyClose = true;
                _tray.Visible = false;
                Close();
            });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowApp(); };
        }

        private void ShowApp()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_reallyClose) return;
            e.Cancel = true;
            Hide();
        }

        // ================= логика =================
        // ================= обновления =================
        private static string Grab(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static int CompareVersions(string a, string b)
        {
            string[] x = a.Split('.');
            string[] y = b.Split('.');
            int n = Math.Max(x.Length, y.Length);
            for (int i = 0; i < n; i++)
            {
                int xi = 0, yi = 0;
                if (i < x.Length) int.TryParse(x[i], out xi);
                if (i < y.Length) int.TryParse(y[i], out yi);
                if (xi != yi) return xi > yi ? 1 : -1;
            }
            return 0;
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private void CheckUpdates()
        {
            try
            {
                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Prosvet/" + AppVersion);
                    json = wc.DownloadString(_updateUrl);
                }
                string ver = Grab(json, "version");
                string url = Grab(json, "url");
                string sha = Grab(json, "sha256");
                if (string.IsNullOrEmpty(ver) || string.IsNullOrEmpty(url)) return;
                if (CompareVersions(ver, AppVersion) <= 0) return;

                _newVersion = ver;
                _newUrl = url;
                _newSha = sha;
                BeginInvoke((Action)ShowUpdateHint);
            }
            catch { }
        }

        private void ShowUpdateHint()
        {
            if (IsDisposed) return;
            _hasUpdate = true;
            _hint.Click += delegate { InstallUpdate(); };
            Append("» доступно обновление " + _newVersion + " — нажмите на строку под профилем");
            RefreshStatus();
        }

        private void InstallUpdate()
        {
            if (string.IsNullOrEmpty(_newUrl) || _busy) return;
            if (_busy) return;
            _busy = true;
            SetBusy(true);
            Append("» скачиваю обновление " + _newVersion);

            string url = _newUrl;
            string sha = _newSha;
            string ver = _newVersion;

            Task.Factory.StartNew(delegate
            {
                try
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "prosvet_" + ver + ".exe");
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Prosvet/" + AppVersion);
                        wc.DownloadFile(url, tmp);
                    }

                    if (!string.IsNullOrEmpty(sha))
                    {
                        string got = Sha256(tmp);
                        if (!string.Equals(got, sha, StringComparison.OrdinalIgnoreCase))
                        {
                            BeginInvoke((Action)delegate
                            {
                                Append("» файл обновления повреждён — отменено");
                                _busy = false; SetBusy(false);
                            });
                            return;
                        }
                    }

                    Append("» запускаю установку...");
                    Process.Start(new ProcessStartInfo(tmp, "/silent") { UseShellExecute = true });

                    BeginInvoke((Action)delegate
                    {
                        _reallyClose = true;
                        if (_tray != null) _tray.Visible = false;
                        Close();
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)delegate
                    {
                        Append("» ошибка обновления: " + ex.Message);
                        _busy = false; SetBusy(false);
                    });
                }
            });
        }

        private void ToggleBypass()
        {
            if (IsRunning())
            {
                try { if (File.Exists(_wantFlag)) File.Delete(_wantFlag); } catch { }
                RunAction("off", "Выключение обхода");
            }
            else
            {
                try { File.WriteAllText(_wantFlag, "1"); } catch { }
                RunAction("on", "Включение обхода");
            }
        }

        private void ToggleAutostart()
        {
            if (File.Exists(_autoFlag)) RunAction("autostart-off", "Снятие автозапуска");
            else RunAction("autostart-on", "Установка автозапуска");
        }

        private static bool IsRunning()
        {
            return Process.GetProcessesByName("winws").Length > 0 ||
                   Process.GetProcessesByName("winws2").Length > 0;
        }

        private string StrategyName()
        {
            try
            {
                string labelFile = Path.Combine(_root, "state", "chosen-label.txt");
                if (File.Exists(labelFile))
                {
                    string lab = File.ReadAllText(labelFile).Trim();
                    if (lab.Length > 0) return lab;
                }
                if (!File.Exists(_chosenFile)) return null;
                string s = File.ReadAllText(_chosenFile).Trim();
                if (s.Length == 0) return null;
                if (s.StartsWith("custom|"))
                {
                    string[] p = s.Split('|');
                    return p.Length > 1 ? p[1] : "свой вариант";
                }
                if (s.StartsWith("preset|"))
                {
                    string[] p = s.Split('|');
                    return p.Length > 2 ? p[2] : "профиль";
                }
                if (s.StartsWith("bat|")) return s.Substring(4).Replace(".bat", "");
                return s.Replace(".bat", "");
            }
            catch { return null; }
        }

        private void RefreshStatus()
        {
            bool running = IsRunning();
            if (running)
            {
                _status.Text = "●  ОБХОД ВКЛЮЧЁН";
                _status.ForeColor = Good;
                _toggle.Caption = "ВЫКЛЮЧИТЬ ОБХОД";
                _toggle.FillA = Color.FromArgb(122, 36, 40);
                _toggle.FillB = Color.FromArgb(158, 44, 50);
                _tray.Text = "Просвет · включён";
            }
            else
            {
                _status.Text = "ОБХОД ВЫКЛЮЧЕН";
                _status.ForeColor = Bad;
                _toggle.Caption = "ВКЛЮЧИТЬ ОБХОД";
                _toggle.FillA = AccentA;
                _toggle.FillB = AccentB;
                _tray.Text = "Просвет · выключен";
            }

            string st = StrategyName();
            if (st == null) _strategy.Text = "профиль ещё не выбран";
            else if (st.StartsWith("Профиль")) _strategy.Text = st;
            else _strategy.Text = "Профиль: " + st;
            if (_hasUpdate)
            {
                _hint.Text = "●  доступно обновление " + _newVersion + "  —  нажмите";
                _hint.ForeColor = Color.FromArgb(129, 140, 248);
                _hint.Cursor = Cursors.Hand;
            }
            else
            {
                _hint.Text = running ? "сайты должны открываться" : "нажмите большую кнопку";
                _hint.ForeColor = Color.FromArgb(110, 122, 145);
                _hint.Cursor = Cursors.Default;
            }
            _auto.Caption = File.Exists(_autoFlag) ? "Автозапуск: вкл" : "Автозапуск: выкл";
            _toggle.Invalidate();
        }

        private void SetBusy(bool busy)
        {
            _toggle.Enabled = !busy;
            _check.Enabled = !busy;
            _tune.Enabled = !busy;
            _auto.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void Append(string line)
        {
            if (_log.TextLength > 200000) _log.Clear();
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void AppendSafe(string line)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke((Action)delegate { Append(line); });
            else Append(line);
        }

        private void RunAction(string action, string title)
        {
            if (_busy) return;
            if (!File.Exists(_ps1))
            {
                Append("Не найден движок: " + _ps1);
                return;
            }

            _busy = true;
            SetBusy(true);
            Append("");
            Append("» " + title);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -Action {1}", _ps1, action);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = _root;

            try
            {
                _proc = new Process();
                _proc.StartInfo = psi;
                _proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendSafe(e.Data);
                };
                _proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendSafe(e.Data);
                };
                _proc.EnableRaisingEvents = true;
                _proc.Exited += delegate
                {
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            _busy = false;
                            SetBusy(false);
                            RefreshStatus();
                            Append("» готово");
                        });
                    }
                    catch { }
                };
                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _busy = false;
                SetBusy(false);
                Append("Ошибка запуска: " + ex.Message);
            }
        }

        private static bool QuickOk(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("curl.exe",
                    string.Format("-s -o NUL -w \"%{{http_code}}\" --max-time 8 \"{0}\"", url));
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;

                using (Process p = Process.Start(psi))
                {
                    string s = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(12000);
                    int code;
                    if (int.TryParse(s, out code)) return code >= 200 && code < 500;
                }
            }
            catch { }
            return false;
        }

        private void HealthTick()
        {
            try
            {
                if (_autoChk == null || !_autoChk.Checked) return;
                if (_busy) return;
                if ((DateTime.Now - _lastHealth).TotalSeconds < 150) return;
                _lastHealth = DateTime.Now;

                Task.Factory.StartNew(delegate
                {
                    try
                    {
                        bool running = IsRunning();
                        if (!running)
                        {
                            if (!File.Exists(_wantFlag)) return;
                            BeginInvoke((Action)delegate
                            {
                                Append("» авто: движок не запущен — поднимаю");
                                RunAction("on", "Авто-запуск");
                            });
                            return;
                        }

                        bool yt = QuickOk("https://www.youtube.com/");
                        bool dc = QuickOk("https://discord.com/");
                        if (!yt && !dc)
                        {
                            BeginInvoke((Action)delegate
                            {
                                Append("» авто: сайты не открываются — переподбор");
                                RunAction("retune", "Авто-переподбор");
                            });
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }
    }

    // ================= кастомные контролы =================
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
            using (GraphicsPath path = MainForm.Round(r, 14f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    Rectangle.Round(new RectangleF(1, 1, Width - 2, Height - 2)),
                    Color.FromArgb(30, 34, 48), Color.FromArgb(22, 25, 36), 90f))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(44, 50, 70), 1f))
                {
                    g.DrawPath(p, path);
                }
            }
        }
    }

    public class LogPanel : Panel
    {
        public LogPanel()
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
            using (GraphicsPath path = MainForm.Round(r, 14f))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 18, 26)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(38, 43, 60), 1f))
                {
                    g.DrawPath(p, path);
                }
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

            if (!Enabled)
            {
                a = Color.FromArgb(30, 33, 44);
                b = Color.FromArgb(30, 33, 44);
            }
            else if (_down)
            {
                a = Darken(FillA, 0.82f);
                b = Darken(FillB, 0.82f);
            }
            else if (_hover)
            {
                a = Lighten(FillA, 1.18f);
                b = Lighten(FillB, 1.18f);
            }

            using (GraphicsPath path = MainForm.Round(r, Radius))
            {
                using (LinearGradientBrush br = new LinearGradientBrush(
                    Rectangle.Round(new RectangleF(1, 1, Math.Max(2, Width - 2), Math.Max(2, Height - 2))), a, b, 90f))
                {
                    g.FillPath(br, path);
                }
                using (Pen p = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
                {
                    g.DrawPath(p, path);
                }
            }

            TextRenderer.DrawText(g, Caption, Font,
                new Rectangle(0, 0, Width, Height),
                Enabled ? TextColor : IdleColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static Color IdleColor { get { return Color.FromArgb(110, 118, 138); } }

        private static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A,
                Math.Min(255, (int)(c.R * k)),
                Math.Min(255, (int)(c.G * k)),
                Math.Min(255, (int)(c.B * k)));
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
        public event EventHandler CheckedChanged;

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
        protected override void OnClick(EventArgs e)
        {
            _checked = !_checked;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF box = new RectangleF(0.5f, (Height - 18) / 2f + 0.5f, 18f, 18f);
            using (GraphicsPath p = MainForm.Round(box, 5f))
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

            TextRenderer.DrawText(g, Caption, Font,
                new Rectangle(26, 0, Width - 26, Height),
                _hover ? Color.FromArgb(190, 200, 216) : Color.FromArgb(138, 150, 172),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    public class SupportForm : Form
    {
        private readonly string _file;
        private readonly System.Collections.Generic.List<string[]> _rows = new System.Collections.Generic.List<string[]>();

        public SupportForm(string file)
        {
            _file = file;
            LoadRows();
            Build();
        }

        private void LoadRows()
        {
            try
            {
                if (!File.Exists(_file)) return;
                foreach (string raw in File.ReadAllLines(_file))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("#")) continue;
                    int k = line.IndexOf('|');
                    if (k < 0) continue;
                    _rows.Add(new string[] { line.Substring(0, k).Trim(), line.Substring(k + 1).Trim() });
                }
            }
            catch { }
        }

        private void Build()
        {
            Text = "Поддержать";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(17, 19, 28);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9F);
            int count = Math.Max(1, _rows.Count);
            ClientSize = new Size(470, 118 + count * 48);

            Label t = new Label();
            t.Text = "СПАСИБО";
            t.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            t.ForeColor = Color.FromArgb(236, 240, 248);
            t.BackColor = Color.Transparent;
            t.SetBounds(22, 18, 300, 24);
            Controls.Add(t);

            Label s = new Label();
            s.Text = _rows.Count > 0 ? "выберите удобный способ" : "каналы поддержки ещё не настроены";
            s.Font = new Font("Segoe UI", 8F);
            s.ForeColor = Color.FromArgb(138, 150, 172);
            s.BackColor = Color.Transparent;
            s.SetBounds(24, 44, 400, 16);
            Controls.Add(s);

            int y = 76;
            for (int i = 0; i < _rows.Count; i++)
            {
                string label = _rows[i][0];
                string value = _rows[i][1];

                RoundButton b = new RoundButton();
                b.Caption = label;
                b.Radius = 10;
                b.FillA = Color.FromArgb(31, 35, 49);
                b.FillB = Color.FromArgb(31, 35, 49);
                b.TextColor = Color.FromArgb(236, 240, 248);
                b.Font = new Font("Segoe UI", 9.5F);
                b.SetBounds(22, y, 426, 38);
                b.Click += delegate
                {
                    if (value.StartsWith("http"))
                    {
                        try { Process.Start(value); } catch { }
                    }
                    else
                    {
                        try
                        {
                            Clipboard.SetText(value);
                            b.Caption = label + "  —  скопировано";
                            b.TextColor = Color.FromArgb(52, 211, 153);
                            b.Invalidate();
                        }
                        catch { }
                    }
                };
                Controls.Add(b);
                y += 48;
            }

            RoundButton close = new RoundButton();
            close.Caption = "закрыть";
            close.Radius = 8;
            close.FillA = Color.FromArgb(24, 27, 38);
            close.FillB = Color.FromArgb(24, 27, 38);
            close.TextColor = Color.FromArgb(138, 150, 172);
            close.Font = new Font("Segoe UI", 8.5F);
            close.SetBounds(348, ClientSize.Height - 38, 100, 26);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            RoundButton x = new RoundButton();
            x.Caption = "×";
            x.Radius = 8;
            x.FillA = Color.FromArgb(34, 38, 52);
            x.FillB = Color.FromArgb(34, 38, 52);
            x.TextColor = Color.FromArgb(138, 150, 172);
            x.Font = new Font("Segoe UI", 11F);
            x.SetBounds(422, 14, 30, 28);
            x.Click += delegate { Close(); };
            Controls.Add(x);

            MouseDown += Drag;
            t.MouseDown += Drag;
        }

        private void Drag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), Color.FromArgb(17, 19, 28), Color.FromArgb(10, 11, 17), 90f))
            {
                g.FillRectangle(b, ClientRectangle);
            }
            using (Pen p = new Pen(Color.FromArgb(38, 43, 60), 1f))
            using (GraphicsPath path = MainForm.Round(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 14f))
            {
                g.DrawPath(p, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = MainForm.Round(new RectangleF(0, 0, Width, Height), 14f))
            {
                Region = new Region(path);
            }
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// ALIASY eliminujące konflikty nazw:
using FormsTimer = System.Windows.Forms.Timer;
using IOFile = System.IO.File;

namespace TimerTray
{
    public partial class MainForm : Form
    {
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly FormsTimer _timer;
        private readonly Icon _appIcon;

        private int _totalSeconds = 0;
        private string _message = "Czas minął!";
        private readonly string _startupLinkPath;

        public MainForm()
        {
            InitializeComponent();
            _appIcon = LoadEmbeddedIcon();
            this.Icon = _appIcon;
            this.ShowIcon = true;

            // Menu traya
            _menu = new ContextMenuStrip();

            // Self-test powiadomień
            _menu.Items.Add("🔔 Test powiadomienia", null, (s, e) =>
            {
                PlaySound();
                NotifyDone("Test powiadomienia", "To jest test z TimerTray.");
            });

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Pokaż okno", null, (s, e) => ShowWindow());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Start 5m", null, (s, e) => StartTimer(5 * 60, "Koniec 5 minut"));
            _menu.Items.Add("Start 25m", null, (s, e) => StartTimer(25 * 60, "Koniec 25 minut"));
            _menu.Items.Add("Stop", null, (s, e) => StopTimer());
            _menu.Items.Add(new ToolStripSeparator());
            var autostartItem = new ToolStripMenuItem("Autostart (włączony)")
            {
                Checked = true,
                CheckOnClick = true
            };
            autostartItem.CheckedChanged += (s, e) => ToggleAutostart(autostartItem.Checked);
            _menu.Items.Add(autostartItem);
            _menu.Items.Add("Wyjście", null, (s, e) =>
            {
                _tray.Visible = false;
                Application.Exit();
            });

            // Ikona w zasobniku
            _tray = new NotifyIcon
            {
                Icon = _appIcon,
                Visible = true,
                Text = "TimerTray – kliknij, aby pokazać",
                ContextMenuStrip = _menu,
                BalloonTipIcon = ToolTipIcon.Info
            };
            _tray.BalloonTipClicked += (s, e) => ShowWindow();
            _tray.DoubleClick += (s, e) => ShowWindow();

            // WinFormsowy timer 1s
            _timer = new FormsTimer { Interval = 1000 };
            _timer.Tick += (s, e) => OnTick();

            // Ścieżka skrótu autostartu
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            _startupLinkPath = Path.Combine(startup, "TimerTray.lnk");

            // Pierwszy start: jeśli brak skrótu, utwórz
            bool exists = IOFile.Exists(_startupLinkPath);
            if (!exists)
            {
                TryCreateStartupShortcut();
                exists = IOFile.Exists(_startupLinkPath);
            }

            // Ustaw stan pozycji menu
            foreach (ToolStripItem it in _menu.Items)
            {
                if (it is ToolStripMenuItem mi && mi.Text.StartsWith("Autostart", StringComparison.OrdinalIgnoreCase))
                {
                    mi.Checked = exists;
                    mi.Text = exists ? "Autostart (włączony)" : "Autostart (wyłączony)";
                }
            }

            // Start zminimalizowany do traya
            this.Shown += (s, e) =>
            {
                this.Hide();
                this.ShowInTaskbar = false;
            };
        }

        private static Icon LoadEmbeddedIcon()
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("TimerTray.ico");
            return stream is not null ? new Icon(stream) : SystemIcons.Information;
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void ToggleAutostart(bool on)
        {
            if (on)
                TryCreateStartupShortcut();
            else
                TryRemoveStartupShortcut();

            foreach (ToolStripItem it in _menu.Items)
            {
                if (it is ToolStripMenuItem mi && mi.Text.StartsWith("Autostart", StringComparison.OrdinalIgnoreCase))
                {
                    bool exists = IOFile.Exists(_startupLinkPath);
                    mi.Text = exists ? "Autostart (włączony)" : "Autostart (wyłączony)";
                    mi.Checked = exists;
                }
            }
        }

        // --- AUTOSTART bez COM: late binding WScript.Shell ---
        private void TryCreateStartupShortcut()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string workDir = Path.GetDirectoryName(exePath) ?? string.Empty;

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    throw new InvalidOperationException("WScript.Shell (WSH) niedostępny w systemie.");

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(_startupLinkPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = workDir;
                shortcut.WindowStyle = 7;
                shortcut.Description = "TimerTray";
                shortcut.IconLocation = $"{exePath},0";
                shortcut.Save();

                _tray.Icon = _appIcon;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(2000, "TimerTray", "Dodano do Autostartu.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _tray.Icon = _appIcon;
                _tray.BalloonTipIcon = ToolTipIcon.Warning;
                _tray.ShowBalloonTip(3000, "TimerTray", "Nie udało się dodać do Autostartu: " + ex.Message, ToolTipIcon.Warning);
            }
        }

        private void TryRemoveStartupShortcut()
        {
            try
            {
                if (IOFile.Exists(_startupLinkPath))
                    IOFile.Delete(_startupLinkPath);

                _tray.ShowBalloonTip(2000, "TimerTray", "Usunięto z Autostartu.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _tray.ShowBalloonTip(3000, "TimerTray", "Nie udało się usunąć z Autostartu: " + ex.Message, ToolTipIcon.Warning);
            }
        }
        // --- KONIEC AUTOSTART ---

        // -------- POWIADOMIENIA I DŹWIĘK --------
        private void NotifyDone(string title, string message)
        {
            try
            {
                _tray.BalloonTipTitle = string.IsNullOrWhiteSpace(title) ? "Timer zakończony" : title;
                _tray.BalloonTipText  = string.IsNullOrWhiteSpace(message) ? "Czas minął!" : message;
                _tray.BalloonTipIcon  = ToolTipIcon.Info;
                _tray.ShowBalloonTip(5000);
            }
            catch { }

            try
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(message) ? "Czas minął!" : message,
                    string.IsNullOrWhiteSpace(title) ? "Timer zakończony" : title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch { }
        }

        private void PlaySound()
        {
            // Spróbuj zagrać alarm.wav obok EXE
            try
            {
                string wav = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alarm.wav");
                if (IOFile.Exists(wav))
                {
                    using var sp = new System.Media.SoundPlayer(wav);
                    sp.Play();
                    return;
                }
            }
            catch { }

            // Fallback: sekwencja beep
            try
            {
                Console.Beep(880, 180);
                Console.Beep(988, 180);
                Console.Beep(1046, 300);
                return;
            }
            catch { }

            // Ostatecznie: dźwięk systemowy
            try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
        }

        // -------- LOGIKA TIMERA --------
        private void OnTick()
        {
            if (_totalSeconds > 0)
            {
                _totalSeconds--;
                UpdateDisplay();

                if (_totalSeconds == 0)
                {
                    PlaySound();
                    NotifyDone("Timer zakończony", _message);
                }
            }
        }

        private static string Fmt(int total)
        {
            int m = total / 60; int s = total % 60;
            return $"{m:00}:{s:00}";
        }

        private void UpdateDisplay()
        {
            lblCountdown.Text = Fmt(_totalSeconds);
            _tray.Text = _totalSeconds > 0 ? $"Timer: {Fmt(_totalSeconds)}" : "TimerTray – kliknij, aby pokazać";
        }

        private void StartTimer(int seconds, string message)
        {
            if (seconds <= 0) return;
            _totalSeconds = seconds;
            _message = string.IsNullOrWhiteSpace(message) ? "Czas minął!" : message;
            _timer.Start();
            UpdateDisplay();
        }

        private void StopTimer()
        {
            _timer.Stop();
            _totalSeconds = 0;
            UpdateDisplay();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            int mins = (int)numMin.Value;
            int secs = (int)numSec.Value;
            string msg = txtMsg.Text.Trim();
            StartTimer(mins * 60 + secs, msg);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopTimer();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Zamykanie krzyżykiem minimalizuje do traya
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                _tray.Icon = _appIcon;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(1500, "TimerTray", "Aplikacja działa w zasobniku.", ToolTipIcon.Info);
            }
        }
    }
}

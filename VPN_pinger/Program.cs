using System.Drawing;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    public static readonly string LogDirectory =
        AppDomain.CurrentDomain.BaseDirectory;

    public static readonly string LogPath = Path.Combine(
        LogDirectory,
        "VpnMonitor.log");

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            Directory.CreateDirectory(LogDirectory);

            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff};" +
                $"Application Started;" +
                $"EXE={Application.ExecutablePath}" +
                Environment.NewLine,
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Can't create log file:\n\n" +
                $"{LogPath}\n\n" +
                $"{ex.GetType().Name}: {ex.Message}",
                "VPN Monitor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        Application.Run(new MonitorForm());
    }
}
public sealed class MonitorForm : Form
{
    // Public internet address and internal DNS available via VPN.
    private readonly TargetState[] targets =
    [
        new("Internet", "1.1.1.1"),
        new("VPN", "172.16.33.12")
    ];

    private readonly TimeSpan interval = TimeSpan.FromSeconds(5);
    private readonly int pingTimeoutMs = 2000;

    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer timer = new();

    private bool checkRunning;

    // Logging of interruptions starts only when both connections work simultaneously.
    private bool monitoringStarted;

    public MonitorForm()
    {
        EnableAutoStart();

        Text = "VPN Monitor";

        TopMost = true;
        ShowInTaskbar = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedSingle;

        Opacity = 0.65;

        statusLabel.AutoSize = true;
        statusLabel.Dock = DockStyle.None;
        statusLabel.Padding = new Padding(6, 4, 6, 4);
        statusLabel.Margin = Padding.Empty;
        statusLabel.Font = new Font("Consolas", 9);
        statusLabel.Location = Point.Empty;
        statusLabel.Text = "Monitoring...";
        statusLabel.ForeColor = Color.White;

        Controls.Add(statusLabel);

        timer.Interval = (int)interval.TotalMilliseconds;

        timer.Tick += async (_, _) =>
        {
            await CheckConnectionsAsync();
        };

        Shown += async (_, _) =>
        {
            MoveToTopRight();

            await Task.Delay(TimeSpan.FromMinutes(2));

            TopMost = false;
            TopMost = true;
            BringToFront();
            Activate();

            timer.Start();

            await CheckConnectionsAsync();
        };
    }

    private static void EnableAutoStart()
    {
        const string registryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        const string applicationName = "VpnMonitor";

        string executablePath =
            Environment.ProcessPath
            ?? Application.ExecutablePath;

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            registryPath,
            writable: true);

        key?.SetValue(
            applicationName,
            $"\"{executablePath}\"");
    }

    private void MoveToTopRight()
    {
        Rectangle workingArea =
            Screen.FromControl(this).WorkingArea;

        const int leftMargin = 0;
        const int topMargin = 40;

        Location = new Point(
            workingArea.Right - Width - leftMargin,
            workingArea.Top + topMargin);
    }

    private async Task CheckConnectionsAsync()
    {
        if (checkRunning)
        {
            return;
        }

        checkRunning = true;

        try
        {
            // Both pings are performed in parallel.
            PingResult[] results = await Task.WhenAll(
                targets.Select(target =>
                    PingAddressAsync(target.Address)));

            // We remember previous states before assigning new results.
            bool[] previousStates = targets
                .Select(target => target.IsAvailable)
                .ToArray();

            // Refresh results
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].LastResult = results[i];
            }

            /*
             * After starting the application, we do not log VPN loss.
             * We wait until Internet and VPN work at least once simultaneously.
             */
            if (!monitoringStarted)
            {
                bool allConnected =
                    results.All(result => result.Success);

                if (allConnected)
                {
                    monitoringStarted = true;

                    for (int i = 0; i < targets.Length; i++)
                    {
                        targets[i].IsAvailable = true;
                    }
                }

                UpdateStatus();
                return;
            }

            /*
             * From this point, monitoring is armed.
             * Any state change will be logged.
             */
            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                PingResult result = results[i];

                bool wasAvailable = previousStates[i];
                bool isAvailable = result.Success;

                if (wasAvailable && !isAvailable)
                {
                    Log(
                        target,
                        "No Connection",
                        result.Message,
                        GetConnectionSummary());
                }
                else if (!wasAvailable && isAvailable)
                {
                    Log(
                        target,
                        "Connection restored",
                        $"ping={result.RoundtripTime} ms",
                        GetConnectionSummary());
                }

                // We update the state only after checking if there was a change.
                target.IsAvailable = isAvailable;
            }

            UpdateStatus();
        }
        catch (Exception ex)
        {
            File.AppendAllText(
                Program.LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff};" +
                $"Application Error;{ex.Message}" +
                Environment.NewLine,
                Encoding.UTF8);
        }
        finally
        {
            checkRunning = false;
        }
    }

    private async Task<PingResult> PingAddressAsync(
        string address)
    {
        try
        {
            using var ping = new Ping();

            PingReply reply = await ping.SendPingAsync(
                address,
                pingTimeoutMs);

            if (reply.Status == IPStatus.Success)
            {
                return new PingResult(
                    true,
                    reply.RoundtripTime,
                    "OK");
            }

            return new PingResult(
                false,
                0,
                reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new PingResult(
                false,
                0,
                ex.Message);
        }
    }

    private string GetConnectionSummary()
    {
        return string.Join(
            ";",
            targets.Select(target =>
                $"{target.Name}=" +
                (target.LastResult.Success
                    ? $"OK({target.LastResult.RoundtripTime}ms)"
                    : $"Missing({target.LastResult.Message})")));
    }

    private void UpdateStatus()
    {
        var text = new StringBuilder();

        foreach (TargetState target in targets)
        {
            string status = target.LastResult.Success
                ? $"OK  {target.LastResult.RoundtripTime,4} ms"
                : $"Missing  {target.LastResult.Message}";

            text.AppendLine(
                $"{target.Name,-10} " +
                $"{target.Address,-15} " +
                $"{status}");
        }

        // TrimEnd removes the line break after the last address.
        // This prevents an empty area at the bottom.
        statusLabel.Text = text.ToString().TrimEnd();

        ClientSize = new Size(
            Math.Max(330, statusLabel.PreferredWidth),
            statusLabel.PreferredHeight);

        bool internetAvailable = targets
            .First(target => target.Name == "Internet")
            .LastResult.Success;

        bool vpnAvailable = targets
            .First(target => target.Name == "VPN")
            .LastResult.Success;

        /*
         * Blue:
         * The application is still waiting for the first simultaneous
         * successful Internet + VPN connection.
         */
        if (!monitoringStarted)
        {
            BackColor = Color.SteelBlue;
        }
        /*
         * Green:
         * Monitoring is armed and both connections are working.
         */
        else if (internetAvailable && vpnAvailable)
        {
            BackColor = Color.MediumSeaGreen;
        }
        /*
         * Orange:
         * Internet works, but VPN is lost.
         */
        else if (internetAvailable && !vpnAvailable)
        {
            BackColor = Color.DarkOrange;
        }
        /*
         * Red:
         * Internet is unavailable. VPN will usually also be unreachable.
         */
        else
        {
            BackColor = Color.Crimson;
        }

        statusLabel.ForeColor = Color.White;

        // The window width may change with the message,
        // so we move it to the right edge again.
        MoveToTopRight();
    }

    private void Log(
        TargetState target,
        string eventName,
        string details,
        string connectionSummary)
    {
        string line =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff};" +
            $"{target.Name};" +
            $"{target.Address};" +
            $"{eventName};" +
            $"{details};" +
            $"{connectionSummary}";

        File.AppendAllText(
            Program.LogPath,
            line + Environment.NewLine,
            Encoding.UTF8);
    }
}

public sealed class TargetState
{
    public string Name { get; }
    public string Address { get; }

    public bool IsAvailable { get; set; } = true;

    public PingResult LastResult { get; set; } =
        new(false, 0, "Waiting");

    public TargetState(
        string name,
        string address)
    {
        Name = name;
        Address = address;
    }
}

public sealed record PingResult(
    bool Success,
    long RoundtripTime,
    string Message);

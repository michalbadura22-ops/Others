using System.Net.NetworkInformation;
using System.Text;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MonitorForm());
    }
}

public sealed class MonitorForm : Form
{
    // Ping official DNS and FrotiClien KTW gate
    private readonly TargetState[] targets =
    [
        new("Internet", "1.1.1.1"),
        new("VPN", "10.61.32.144")
    ];

    private readonly TimeSpan interval = TimeSpan.FromSeconds(5);
    private readonly int pingTimeoutMs = 2000;

    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer timer = new();

    private readonly string logPath = Path.Combine(
        AppContext.BaseDirectory,
        "VpnMonitor.log");

    private bool checkRunning;

    public MonitorForm()
    {
        Text = "VPN Monitor";
        ClientSize = new Size(350, 58);

        TopMost = true;
        ShowInTaskbar = true;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;

        statusLabel.AutoSize = true;
        statusLabel.Dock = DockStyle.None;
        statusLabel.Padding = new Padding(6, 4, 6, 4);
        statusLabel.Margin = Padding.Empty;
        statusLabel.Font = new Font("Consolas", 9);
        statusLabel.Location = Point.Empty;
        statusLabel.Text = "Monitoring...";

        Controls.Add(statusLabel);

        timer.Interval = (int)interval.TotalMilliseconds;
        timer.Tick += async (_, _) => await CheckConnectionsAsync();

        Shown += async (_, _) =>
        {
            timer.Start();
            await CheckConnectionsAsync();
        };
    }

    private async Task CheckConnectionsAsync()
    {
        if (checkRunning)
            return;

        checkRunning = true;

        try
        {
            // Check connection.
            PingResult[] results = await Task.WhenAll(
                targets.Select(target => PingAddressAsync(target.Address)));

            // Save results.
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].LastResult = results[i];
            }

            // Analyze if something is missing, log when connection is missing.
            for (int i = 0; i < targets.Length; i++)
            {
                TargetState target = targets[i];
                PingResult result = results[i];

                if (!result.Success && target.IsAvailable)
                {
                    target.IsAvailable = false;

                    Log(
                        target,
                        "No connection",
                        result.Message,
                        GetConnectionSummary());
                }
                else if (result.Success && !target.IsAvailable)
                {
                    target.IsAvailable = true;

                    Log(
                        target,
                        "Restored",
                        $"ping={result.RoundtripTime} ms",
                        GetConnectionSummary());
                }
            }

            UpdateStatus();
        }
        finally
        {
            checkRunning = false;
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

    private async Task<PingResult> PingAddressAsync(string address)
    {
        try
        {
            using var ping = new Ping();

            PingReply reply = await ping.SendPingAsync(
                address,
                pingTimeoutMs);

            return reply.Status == IPStatus.Success
                ? new PingResult(
                    true,
                    reply.RoundtripTime,
                    "OK")
                : new PingResult(
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

    private void UpdateStatus()
    {
        var text = new StringBuilder();

        foreach (TargetState target in targets)
        {
            string status = target.LastResult.Success
                ? $"OK  {target.LastResult.RoundtripTime,4} ms"
                : $"Missing  {target.LastResult.Message}";

            text.AppendLine(
                $"{target.Name,-10} {target.Address,-15} {status}");
        }

        //text.AppendLine();
        //text.Append($"Next test {interval.TotalSeconds:0} s");

        statusLabel.Text = text.ToString();

        ClientSize = new Size(
            Math.Max(350, statusLabel.PreferredWidth),
            statusLabel.PreferredHeight);

        BackColor = targets.All(t => t.LastResult.Success)
            ? Color.Honeydew
            : Color.MistyRose;
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

        try
        {
            File.AppendAllText(
                logPath,
                line + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Brak zapisu nie zatrzymuje monitorowania.
        }
    }
}

public sealed class TargetState
{
    public string Name { get; }
    public string Address { get; }

    public bool IsAvailable { get; set; } = true;

    public PingResult LastResult { get; set; } =
        new(false, 0, "Waiting");

    public TargetState(string name, string address)
    {
        Name = name;
        Address = address;
    }
}

public sealed record PingResult(
    bool Success,
    long RoundtripTime,
    string Message);
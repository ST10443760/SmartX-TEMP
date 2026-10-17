namespace SmartX.Simulator;

/// <summary>
/// Command-line options. Every flag can also come from an environment
/// variable (SIM_API, SIM_DEVICES, ...) so docker-compose can configure it.
/// </summary>
public sealed class SimulatorOptions
{
    public string ApiBaseUrl { get; private set; } = "http://localhost:5074/";

    /// <summary>How many extra simulated devices to register through the Part 1 registration endpoint.</summary>
    public int DeviceCount { get; private set; } = 6;

    public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(2);

    /// <summary>Also drive the devices the API seeded at startup (Moisture Sensor A, Pump A, ...).</summary>
    public bool IncludeSeeded { get; private set; } = true;

    /// <summary>Send one critical spike on the first tick.</summary>
    public bool SpikeOnStart { get; private set; }

    /// <summary>Send a critical spike every N seconds (0 = never). Handy when there is no keyboard, e.g. in Docker.</summary>
    public int SpikeEverySeconds { get; private set; }

    /// <summary>Device (MAC or name) to silence from the start, to demo the disconnect detector.</summary>
    public string? DisconnectTarget { get; private set; }

    public static SimulatorOptions Parse(string[] args)
    {
        var options = new SimulatorOptions();

        string? Env(string name) => Environment.GetEnvironmentVariable(name);
        options.ApiBaseUrl = Env("SIM_API") ?? options.ApiBaseUrl;
        if (int.TryParse(Env("SIM_DEVICES"), out var devices)) options.DeviceCount = devices;
        if (double.TryParse(Env("SIM_INTERVAL"), System.Globalization.CultureInfo.InvariantCulture, out var interval)) options.Interval = TimeSpan.FromSeconds(interval);
        if (int.TryParse(Env("SIM_SPIKE_EVERY"), out var spikeEvery)) options.SpikeEverySeconds = spikeEvery;
        if (bool.TryParse(Env("SIM_INCLUDE_SEEDED"), out var includeSeeded)) options.IncludeSeeded = includeSeeded;
        options.DisconnectTarget = Env("SIM_DISCONNECT") ?? options.DisconnectTarget;

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");

            switch (args[i].ToLowerInvariant())
            {
                case "--api": options.ApiBaseUrl = Next(); break;
                case "--devices": options.DeviceCount = int.Parse(Next()); break;
                case "--interval": options.Interval = TimeSpan.FromSeconds(double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture)); break;
                case "--no-seeded": options.IncludeSeeded = false; break;
                case "--spike": options.SpikeOnStart = true; break;
                case "--spike-every": options.SpikeEverySeconds = int.Parse(Next()); break;
                case "--disconnect": options.DisconnectTarget = Next(); break;
                case "--help" or "-h": PrintHelp(); Environment.Exit(0); break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'. Use --help.");
            }
        }

        if (!options.ApiBaseUrl.EndsWith('/'))
        {
            options.ApiBaseUrl += "/";
        }

        if (options.DeviceCount < 0 || options.Interval <= TimeSpan.Zero)
        {
            throw new ArgumentException("--devices must be >= 0 and --interval must be > 0.");
        }

        return options;
    }

    public static void PrintHelp() => Console.WriteLine("""
        SmartX.Simulator - fake ESP32 fleet that talks to the gateway over HTTP

          --api <url>           Gateway base URL (default http://localhost:5074/)   [SIM_API]
          --devices <n>         Extra devices to register (default 6)               [SIM_DEVICES]
          --interval <seconds>  Seconds between telemetry rounds (default 2)        [SIM_INTERVAL]
          --no-seeded           Only drive the devices this simulator registered    [SIM_INCLUDE_SEEDED=false]
          --spike               Send one critical spike on the first round
          --spike-every <s>     Send a critical spike every s seconds               [SIM_SPIKE_EVERY]
          --disconnect <mac|name>  Start with this device silent                    [SIM_DISCONNECT]

        Keys while running:  c = critical spike   d = disconnect a device
                             r = reconnect all    q = quit
        """);
}

using SmartX.Simulator;

SimulatorOptions options;
try
{
    options = SimulatorOptions.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    SimulatorOptions.PrintHelp();
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var http = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(10) };
var simulator = new FleetSimulator(http, options);

Console.WriteLine($"SmartX.Simulator -> {options.ApiBaseUrl}  ({options.DeviceCount} new devices, every {options.Interval.TotalSeconds:0.#}s)");

// The API may still be starting (always true under docker-compose), so wait for it.
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var ping = await http.GetAsync("", cts.Token);
        if (ping.IsSuccessStatusCode)
        {
            break;
        }
    }
    catch (HttpRequestException) when (attempt < 60)
    {
    }
    catch (TaskCanceledException) when (!cts.IsCancellationRequested && attempt < 60)
    {
    }

    Console.WriteLine($"  waiting for the gateway ({attempt})...");
    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
}

Console.WriteLine("Registering the fleet through POST /api/sensors:");
await simulator.RegisterFleetAsync(cts.Token);
Console.WriteLine($"Streaming telemetry for {simulator.Fleet.Count} devices.");

var interactive = !Console.IsInputRedirected;
Console.WriteLine(interactive
    ? "Keys: [c] critical spike  [d] disconnect a device  [r] reconnect all  [q] quit"
    : "No keyboard attached - use --spike-every / --disconnect flags to inject events.");

if (options.SpikeOnStart)
{
    simulator.RequestSpike();
}

if (interactive)
{
    _ = Task.Run(() =>
    {
        while (!cts.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true).KeyChar;
            switch (char.ToLowerInvariant(key))
            {
                case 'c':
                    simulator.RequestSpike();
                    Console.WriteLine("  [c] critical spike queued for the next round");
                    break;
                case 'd':
                    var device = simulator.DisconnectRandom();
                    Console.WriteLine(device is null ? "  [d] nothing left to disconnect" : $"  [d] {device} stopped sending - the gateway flags it after its timeout, once");
                    break;
                case 'r':
                    Console.WriteLine($"  [r] reconnected {simulator.ReconnectAll()} device(s) - expect 'recovered' events");
                    break;
                case 'q':
                    cts.Cancel();
                    break;
            }
        }
    });
}

var round = 0;
var lastSpike = DateTime.UtcNow;
using var timer = new PeriodicTimer(options.Interval);
try
{
    do
    {
        if (options.SpikeEverySeconds > 0 && DateTime.UtcNow - lastSpike >= TimeSpan.FromSeconds(options.SpikeEverySeconds))
        {
            simulator.RequestSpike();
            lastSpike = DateTime.UtcNow;
        }

        var (sent, priority, failed) = await simulator.SendRoundAsync(cts.Token);
        round++;

        var silent = simulator.Fleet.Count(d => d.IsDisconnected);
        Console.WriteLine($"round {round,4}: {sent} packets accepted (202){(priority > 0 ? $", {priority} via priority lane" : "")}{(failed > 0 ? $", {failed} failed" : "")}{(silent > 0 ? $", {silent} silent" : "")}");
    }
    while (await timer.WaitForNextTickAsync(cts.Token));
}
catch (OperationCanceledException)
{
    // Ctrl+C or [q].
}

Console.WriteLine("Simulator stopped.");
return 0;

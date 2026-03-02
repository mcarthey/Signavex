using Signavex.Domain.Interfaces;

namespace Signavex.Worker;

public class EconomicDataSyncService : BackgroundService
{
    private readonly IFredApiClient _fredClient;
    private readonly IEconomicDataStore _dataStore;
    private readonly IScanCommandStore _commandStore;
    private readonly ILogger<EconomicDataSyncService> _logger;

    public const string CommandType = "EconomicSync";
    private static readonly TimeSpan RunTime = new(16, 30, 0); // 4:30 PM ET
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private bool _isSyncing;

    public EconomicDataSyncService(
        IFredApiClient fredClient,
        IEconomicDataStore dataStore,
        IScanCommandStore commandStore,
        ILogger<EconomicDataSyncService> logger)
    {
        _fredClient = fredClient;
        _dataStore = dataStore;
        _commandStore = commandStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EconomicDataSyncService started");

        // First-run: if no series have ever been synced, run immediately
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // let app fully start
        if (await IsFirstRunAsync(stoppingToken))
        {
            _logger.LogInformation("First run detected — syncing economic data immediately");
            await RunSyncAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll for manual sync commands every 5s
            try
            {
                if (!_isSyncing)
                {
                    var command = await DequeueEconomicCommandAsync(stoppingToken);
                    if (command is not null)
                    {
                        _logger.LogInformation("Manual economic sync requested (command {Id})", command.Id);
                        await RunSyncAsync(stoppingToken);
                        await _commandStore.CompleteCommandAsync(command.Id, stoppingToken);
                        continue; // check for more commands immediately
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling economic sync commands");
            }

            // Check if scheduled time has arrived
            var delay = GetDelayUntilNextRun();
            if (delay <= TimeSpan.Zero)
            {
                await RunSyncAsync(stoppingToken);
                // After running, wait at least a minute to avoid re-triggering
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            // Wait a polling interval before checking again
            var waitTime = delay < PollInterval ? delay : PollInterval;
            await Task.Delay(waitTime, stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _isSyncing = true;
        try
        {
            _logger.LogInformation("Starting economic data sync");
            await SyncAllSeriesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Economic data sync failed");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task<bool> IsFirstRunAsync(CancellationToken ct)
    {
        var series = await _dataStore.GetAllSeriesAsync(enabledOnly: true, ct);
        foreach (var s in series)
        {
            var sync = await _dataStore.GetSyncStatusAsync(s.SeriesId, ct);
            if (sync is not null) return false;
        }
        return series.Count > 0; // true if series exist but none synced
    }

    private async Task<Domain.Models.ScanCommand?> DequeueEconomicCommandAsync(CancellationToken ct)
    {
        return await _commandStore.DequeueCommandAsync(CommandType, ct);
    }

    internal async Task SyncAllSeriesAsync(CancellationToken ct)
    {
        var series = await _dataStore.GetAllSeriesAsync(enabledOnly: true, ct);
        _logger.LogInformation("Syncing {Count} enabled economic series", series.Count);

        var synced = 0;
        var errors = 0;

        foreach (var s in series)
        {
            try
            {
                var syncStatus = await _dataStore.GetSyncStatusAsync(s.SeriesId, ct);

                // Skip if already synced today
                if (syncStatus is not null &&
                    (DateTime.UtcNow - syncStatus.LastSyncUtc).TotalHours < 20)
                {
                    _logger.LogDebug("Skipping {SeriesId} — synced recently", s.SeriesId);
                    continue;
                }

                // Fetch observations since last sync (or all if first sync)
                DateOnly? startDate = syncStatus is not null
                    ? DateOnly.FromDateTime(syncStatus.LastSyncUtc.AddDays(-7))
                    : null;

                var observations = await _fredClient.GetObservationsAsync(s.SeriesId, startDate, ct);

                if (observations.Count > 0)
                {
                    await _dataStore.UpsertObservationsAsync(s.SeriesId, observations, ct);
                }

                // Get total count for tracking
                var allObs = await _dataStore.GetObservationsAsync(s.SeriesId, ct: ct);
                await _dataStore.UpdateSyncTimestampAsync(s.SeriesId, allObs.Count, ct);

                synced++;
                _logger.LogDebug("Synced {SeriesId}: {NewCount} observations fetched, {TotalCount} total",
                    s.SeriesId, observations.Count, allObs.Count);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "Failed to sync {SeriesId}", s.SeriesId);
            }
        }

        _logger.LogInformation("Economic data sync complete: {Synced} synced, {Errors} errors", synced, errors);
    }

    internal static TimeSpan GetDelayUntilNextRun()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        var nextRun = now.Date.Add(RunTime);

        if (now.TimeOfDay >= RunTime)
            nextRun = nextRun.AddDays(1);

        // Skip weekends
        while (nextRun.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}

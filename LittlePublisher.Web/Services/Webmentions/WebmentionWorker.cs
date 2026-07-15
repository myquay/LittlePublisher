using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Webmentions;

public sealed class WebmentionWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AppConfiguration _config;
    private readonly ILogger<WebmentionWorker> _logger;

    public WebmentionWorker(IServiceProvider services, AppConfiguration config, ILogger<WebmentionWorker> logger)
    { _services = services; _config = config; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Webmention.Enabled) return;
        var nextReverificationSweep = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                using var scope = _services.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IWebmentionQueue>();
                if (DateTimeOffset.UtcNow >= nextReverificationSweep)
                {
                    var storage = scope.ServiceProvider.GetRequiredService<IWebmentionStorage>();
                    var threshold = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(_config.Webmention.ReverifyApprovedAfterDays, 1, 90));
                    var approved = await storage.ListIncomingAsync(WebmentionStates.Approved, 200, stoppingToken);
                    foreach (var item in approved.Where(x => x.VerifiedUtc < threshold)) await queue.EnqueueVerificationAsync(item.Id, stoppingToken);
                    nextReverificationSweep = DateTimeOffset.UtcNow.AddDays(1);
                }
                var incoming = await queue.ReceiveVerificationAsync(stoppingToken);
                if (incoming is not null)
                {
                    worked = true;
                    await ProcessAsync(incoming, () => scope.ServiceProvider.GetRequiredService<IIncomingWebmentionService>().VerifyAsync(incoming.Body.RecordId, stoppingToken), queue, stoppingToken);
                }
                var outgoing = await queue.ReceiveSendAsync(stoppingToken);
                if (outgoing is not null)
                {
                    worked = true;
                    await ProcessAsync(outgoing, () => scope.ServiceProvider.GetRequiredService<IOutgoingWebmentionService>().SendAsync(outgoing.Body.RecordId, stoppingToken), queue, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Webmention worker polling failed."); }
            if (!worked) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessAsync(QueuedWebmentionMessage message, Func<Task> action, IWebmentionQueue queue, CancellationToken cancellationToken)
    {
        try
        {
            await action();
            await queue.CompleteAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webmention operation {Operation} for {RecordId} failed.", message.Body.Operation, message.Body.RecordId);
            if (message.Message.DequeueCount >= 5) await queue.CompleteAsync(message, cancellationToken);
            else await queue.AbandonAsync(message, TimeSpan.FromMinutes(Math.Pow(2, message.Message.DequeueCount)), cancellationToken);
        }
    }
}

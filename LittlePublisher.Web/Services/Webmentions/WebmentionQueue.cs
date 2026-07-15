using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Webmentions;

public interface IWebmentionQueue
{
    Task EnqueueVerificationAsync(string recordId, CancellationToken cancellationToken);
    Task EnqueueSendAsync(string recordId, CancellationToken cancellationToken);
    Task<QueuedWebmentionMessage?> ReceiveVerificationAsync(CancellationToken cancellationToken);
    Task<QueuedWebmentionMessage?> ReceiveSendAsync(CancellationToken cancellationToken);
    Task CompleteAsync(QueuedWebmentionMessage message, CancellationToken cancellationToken);
    Task AbandonAsync(QueuedWebmentionMessage message, TimeSpan delay, CancellationToken cancellationToken);
    Task CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed record QueuedWebmentionMessage(QueueClient Queue, QueueMessage Message, WebmentionQueueMessage Body);

public sealed class AzureWebmentionQueue : IWebmentionQueue
{
    private readonly QueueClient? _verification;
    private readonly QueueClient? _send;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public AzureWebmentionQueue(AppConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Storage.ConnectionString)) return;
        var prefix = Normalize(config.Webmention.QueuePrefix);
        _verification = new QueueClient(config.Storage.ConnectionString, $"{prefix}-webmention-verify");
        _send = new QueueClient(config.Storage.ConnectionString, $"{prefix}-webmention-send");
    }

    public Task EnqueueVerificationAsync(string recordId, CancellationToken cancellationToken) => EnqueueAsync(Verification, new(1, "verify", recordId), cancellationToken);
    public Task EnqueueSendAsync(string recordId, CancellationToken cancellationToken) => EnqueueAsync(Send, new(1, "send", recordId), cancellationToken);
    public Task<QueuedWebmentionMessage?> ReceiveVerificationAsync(CancellationToken cancellationToken) => ReceiveAsync(Verification, cancellationToken);
    public Task<QueuedWebmentionMessage?> ReceiveSendAsync(CancellationToken cancellationToken) => ReceiveAsync(Send, cancellationToken);

    public async Task CompleteAsync(QueuedWebmentionMessage message, CancellationToken cancellationToken) =>
        await message.Queue.DeleteMessageAsync(message.Message.MessageId, message.Message.PopReceipt, cancellationToken);

    public async Task AbandonAsync(QueuedWebmentionMessage message, TimeSpan delay, CancellationToken cancellationToken) =>
        await message.Queue.UpdateMessageAsync(message.Message.MessageId, message.Message.PopReceipt, message.Message.Body, delay, cancellationToken);

    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        await Verification.GetPropertiesAsync(cancellationToken);
        await Send.GetPropertiesAsync(cancellationToken);
    }

    private async Task EnqueueAsync(QueueClient queue, WebmentionQueueMessage body, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        await queue.SendMessageAsync(JsonSerializer.Serialize(body), cancellationToken);
    }

    private async Task<QueuedWebmentionMessage?> ReceiveAsync(QueueClient queue, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        var response = await queue.ReceiveMessagesAsync(1, TimeSpan.FromMinutes(2), cancellationToken);
        var message = response.Value.FirstOrDefault();
        if (message is null) return null;
        try
        {
            var body = JsonSerializer.Deserialize<WebmentionQueueMessage>(message.Body.ToString());
            return body is null ? null : new(queue, message, body);
        }
        catch (JsonException)
        {
            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
            return null;
        }
    }

    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await Verification.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await Send.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally { _initializeLock.Release(); }
    }

    private QueueClient Verification => _verification ?? Missing();
    private QueueClient Send => _send ?? Missing();
    private static QueueClient Missing() => throw new InvalidOperationException("App:Storage:ConnectionString is not configured.");
    private static string Normalize(string value)
    {
        var normalized = new string((value ?? "littlepublisher").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "littlepublisher" : normalized[..Math.Min(normalized.Length, 35)];
    }
}

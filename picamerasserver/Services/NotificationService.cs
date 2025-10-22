using MudBlazor;

namespace picamerasserver.Services;

public class NotificationService
{
    public event Action<Notification>? OnNotification;

    public async Task AddAsync(string message, Severity severity)
    {
        await Task.Run(() => { OnNotification?.Invoke(new Notification { Message = message, Severity = severity }); });
    }

    public void Add(string message, Severity severity)
    {
        OnNotification?.Invoke(new Notification { Message = message, Severity = severity });
    }
}

public class Notification
{
    public required Severity Severity { get; init; }
    public required string Message { get; init; }
}
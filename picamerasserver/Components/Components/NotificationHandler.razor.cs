using Microsoft.AspNetCore.Components;
using picamerasserver.Services;

namespace picamerasserver.Components.Components;

public partial class NotificationHandler : ComponentBase, IDisposable
{
    [Inject] protected NotificationService NotificationService { get; init; } = null!;
    
    private void OnNotification(Notification notification)
    {
        Snackbar.Add(notification.Message, notification.Severity);
    }
    
    protected override void OnInitialized()
    {
        NotificationService.OnNotification += OnNotification;
    }

    public void Dispose()
    {
        NotificationService.OnNotification -= OnNotification;
        GC.SuppressFinalize(this);
    }
}
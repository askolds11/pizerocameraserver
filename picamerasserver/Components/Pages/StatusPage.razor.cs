using System.Drawing;
using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Pages;

public partial class StatusPage : ComponentBase, IDisposable
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;
    private bool PingActive => PiZeroCameraManager.PingActive;
    private bool CanStopPing => PiZeroCameraManager.PingActive && _pingCancellationTokenSource != null;
    private CancellationTokenSource? _pingCancellationTokenSource;


    private async Task Ping()
    {
        // Cancel any existing ping operation
        if (_pingCancellationTokenSource != null)
        {
            await _pingCancellationTokenSource.CancelAsync();
        }

        _pingCancellationTokenSource?.Dispose();

        _pingCancellationTokenSource = new CancellationTokenSource();

        try
        {
            await PiZeroCameraManager.Ping(null, _pingCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _pingCancellationTokenSource?.Dispose();
            _pingCancellationTokenSource = null;
        }
    }

    private async Task StopPing()
    {
        if (_pingCancellationTokenSource != null)
        {
            await _pingCancellationTokenSource.CancelAsync();
        }
    }
    
    private async Task GetStatus()
    {
        await PiZeroCameraManager.GetStatus();
    }

    private void OnGlobalChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnChangePing += OnGlobalChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChangePing -= OnGlobalChanged;
        _pingCancellationTokenSource?.Cancel();
        _pingCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    private Color ColorTransform(string id)
    {
        return PiZeroCameraManager.PiZeroCameras[id].Pingable switch
        {
            true => Color.FromArgb(0x00, 0xFF, 0x00),
            false => Color.FromArgb(0xFF, 0x00, 0x00),
            null => Color.FromArgb(0x00, 0x00, 0x00)
        };
    }
    
    private Color ColorTransformStatus(string id)
    {
        return PiZeroCameraManager.PiZeroCameras[id].Status switch
        {
            null => Color.FromArgb(0xFF, 0x00, 0x00),
            _ => Color.FromArgb(0x00, 0xFF, 0x00),
        };
    }
}
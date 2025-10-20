using Microsoft.AspNetCore.Components;
using MudBlazor;
using picamerasserver.Settings;

namespace picamerasserver.Components.Pages;

public partial class SettingsPage : ComponentBase
{
    [Inject] protected SettingsService SettingsService { get; init; } = null!;

    private int _maxConcurrentNtp;
    private int _maxConcurrentSend;
    private int _requestPictureDelay;

    private async Task SaveMaxConcurrentNtp()
    {
        if (_maxConcurrentNtp <= 0)
        {
            Snackbar.Add("Value must be greater than 0!", Severity.Error);
            return;
        }
        await SettingsService.SetAsync(new Setting.MaxConcurrentNtp(_maxConcurrentNtp));
    }

    private async Task SaveMaxConcurrentSend()
    {
        if (_maxConcurrentSend <= 0)
        {
            Snackbar.Add("Value must be greater than 0!", Severity.Error);
            return;
        }
        await SettingsService.SetAsync(new Setting.MaxConcurrentSend(_maxConcurrentSend));
    }

    private async Task SaveRequestPictureDelay()
    {
        if (_requestPictureDelay <= 0)
        {
            Snackbar.Add("Value must be greater than 0!", Severity.Error);
            return;
        }
        await SettingsService.SetAsync(new Setting.RequestPictureDelay(_requestPictureDelay));
    }

    protected override async Task OnInitializedAsync()
    {
        var maxConcurrentNtpResult = await SettingsService.GetAsync<Setting.MaxConcurrentNtp>();
        if (maxConcurrentNtpResult.IsSuccess)
        {
            _maxConcurrentNtp = maxConcurrentNtpResult.Value.Value;
        }
        else
        {
            _maxConcurrentNtp = -1;
            var exception = maxConcurrentNtpResult.Error;
            Snackbar.Add($"Error loading MaxConcurrentNtp: ${exception.Message}", Severity.Error);
        }

        var maxConcurrentSendResult = await SettingsService.GetAsync<Setting.MaxConcurrentSend>();
        if (maxConcurrentSendResult.IsSuccess)
        {
            _maxConcurrentSend = maxConcurrentSendResult.Value.Value;
        }
        else
        {
            _maxConcurrentSend = -1;
            var exception = maxConcurrentSendResult.Error;
            Snackbar.Add($"Error loading MaxConcurrentSend: ${exception.Message}", Severity.Error);
        }

        var requestPictureDelayResult = await SettingsService.GetAsync<Setting.RequestPictureDelay>();
        if (requestPictureDelayResult.IsSuccess)
        {
            _requestPictureDelay = requestPictureDelayResult.Value.Value;
        }
        else
        {
            _requestPictureDelay = -1;
            var exception = maxConcurrentNtpResult.Error;
            Snackbar.Add($"Error loading RequestPictureDelay: ${exception.Message}", Severity.Error);
        }
    }
}
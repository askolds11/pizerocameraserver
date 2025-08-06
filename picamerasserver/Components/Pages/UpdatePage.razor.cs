using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using MudBlazor;
using picamerasserver.Options;

namespace picamerasserver.Components.Pages;

public partial class UpdatePage(
    // MqttStuff mqttStuff
    IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor
) : ComponentBase
{
    [GeneratedRegex(@"\[MYAPPVERSION:([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex Version();

    // private void OnGlobalChanged()
    // {
    //     // Marshal safely to the Blazor rendering thread
    //     InvokeAsync(StateHasChanged);
    // }
    //
    // protected override void OnInitialized()
    // {
    //     mqttStuff.OnChange += OnGlobalChanged; // Subscribe
    // }
    //
    // public void Dispose()
    // {
    //     mqttStuff.OnChange -= OnGlobalChanged; // Unsubscribe
    // }
    private const string DefaultDragClass =
        "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";

    private string _dragClass = DefaultDragClass;
    private MudFileUpload<IBrowserFile>? _fileUpload;
    private IBrowserFile? _file;
    private string? _version;

    private async Task ClearAsync()
    {
        await (_fileUpload?.ClearAsync() ?? Task.CompletedTask);
        _file = null;
        _version = null;
        ClearDragClass();
    }

    private Task OpenFilePickerAsync()
        => _fileUpload?.OpenFilePickerAsync() ?? Task.CompletedTask;

    private async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        ClearDragClass();
        _file = e.File;

        await using var stream = _file.OpenReadStream(maxAllowedSize: 100 * 1024000);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var data = ms.ToArray();

        var ascii = Encoding.ASCII.GetString(data);
        var match = Version().Match(ascii);

        if (match.Success)
        {
            _version = match.Groups[1].Value;
            // CanSave = true;
        }
        else
        {
            _version = null;
        }
    }

    private async Task Upload()
    {
        if (_file == null) return;
        try
        {
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UpdateDirectory, $"pizerocamera-{_version}");

            await using FileStream fs = new(path, FileMode.Create);
            await _file.OpenReadStream(100 * 1024000).CopyToAsync(fs);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Snackbar.Add("Error uploading file", Severity.Error);
        }

        // Upload the files here
        // Snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopCenter;
        // Snackbar.Add("TODO: Upload your files!");
    }

    private void SetDragClass()
        => _dragClass = $"{DefaultDragClass} mud-border-primary";

    private void ClearDragClass()
        => _dragClass = DefaultDragClass;
}
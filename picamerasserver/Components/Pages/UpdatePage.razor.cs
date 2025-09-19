using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Update;
using Color = System.Drawing.Color;

namespace picamerasserver.Components.Pages;

public partial class UpdatePage(
    // MqttStuff mqttStuff
    IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor
) : ComponentBase, IDisposable
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected IUpdateManager UpdateManager { get; init; } = null!;

    [GeneratedRegex(@"\[MYAPPVERSION:([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex Version();
    private string? _version;

    private MudDataGrid<UpdateElement> _gridData = null!;
    
    private bool UpdateActive => UpdateManager.UpdateActive;

    private async Task SelectUpdate(string selectedVersion)
    {
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        await piDbContext.Updates
            .Where(x => x.IsCurrent)
            .ExecuteUpdateAsync(x => x.SetProperty(
                b => b.IsCurrent,
                false)
            );
        await piDbContext.Updates
            .Where(x => x.Version == selectedVersion)
            .ExecuteUpdateAsync(x => x.SetProperty(
                b => b.IsCurrent,
                true)
            );
        await _gridData.ReloadServerData();
    }

    private async Task<GridData<UpdateElement>> ServerReload(GridState<UpdateElement> state)
    {
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();

        var data = piDbContext.Updates
            .OrderByDescending(x => x.Version)
            .AsNoTracking()
            .Select(x => new UpdateElement(x));

        var totalItems = await data.CountAsync();
        var pagedData = await data.Skip(state.Page * state.PageSize).Take(state.PageSize).ToArrayAsync();

        return new GridData<UpdateElement>
        {
            TotalItems = totalItems,
            Items = pagedData
        };
    }

    private async Task SendUpdate()
    {
        await UpdateManager.RequestUpdateChannels(3);
    }
    
    private async Task CancelUpdate()
    {
        await UpdateManager.CancelUpdate();
    }
    
    // File upload control
    private const string DefaultDragClass =
        "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";

    private string _dragClass = DefaultDragClass;
    private MudFileUpload<IBrowserFile>? _fileUpload;
    private IBrowserFile? _file;

    /// <summary>
    /// Clears the file upload control
    /// </summary>
    private async Task ClearAsync()
    {
        await (_fileUpload?.ClearAsync() ?? Task.CompletedTask);
        _file = null;
        _version = null;
        ClearDragClass();
    }

    /// <summary>
    /// Opens file picker
    /// </summary>
    private Task OpenFilePickerAsync()
        => _fileUpload?.OpenFilePickerAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Called when a file is selected (not saved)
    /// </summary>
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

    /// <summary>
    /// Save file
    /// </summary>
    private async Task Upload()
    {
        if (_file == null)
        {
            Snackbar.Add("No file!", Severity.Error);
            return;
        }

        if (_version == null)
        {
            Snackbar.Add("No version!", Severity.Error);
            return;
        }

        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        if (piDbContext.Updates.Any(x => x.Version == _version))
        {
            Snackbar.Add($"Version {_version} already exists!", Severity.Error);
            return;
        }

        try
        {
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UpdateDirectory, $"pizerocamera-{_version}");

            await using FileStream fs = new(path, FileMode.Create);
            await _file.OpenReadStream(100 * 1024000).CopyToAsync(fs);

            var updateModel = new UpdateModel
            {
                Version = _version,
                UploadedTime = DateTimeOffset.UtcNow
            };

            piDbContext.Updates.Add(updateModel);
            await piDbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Snackbar.Add("Error uploading file", Severity.Error);
        }

        Snackbar.Add("File uploaded!", Severity.Success);
        await ClearAsync();
        await _gridData.ReloadServerData();
    }

    /// <summary>
    /// File is dragged over control
    /// </summary>
    private void SetDragClass()
        => _dragClass = $"{DefaultDragClass} mud-border-primary";

    /// <summary>
    /// File is not dragged over control
    /// </summary>
    private void ClearDragClass()
        => _dragClass = DefaultDragClass;

    private Color ColorTransform(string cameraId)
    {
        PiZeroCameraManager.PiZeroCameras.TryGetValue(cameraId, out var cameraPicture);
        if (cameraPicture == null)
        {
            return Color.FromArgb(0x00, 0x00, 0x00);
        }

        return cameraPicture.UpdateRequest switch
        {
            PiZeroCameraUpdateRequest.Requested => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroCameraUpdateRequest.Downloading => Color.FromArgb(0x0, 0x55, 0xA0),
            PiZeroCameraUpdateRequest.Downloaded => Color.FromArgb(0x00, 0x55, 0x00),
            PiZeroCameraUpdateRequest.Success => Color.FromArgb(0x00, 0xFF, 0x00),
            PiZeroCameraUpdateRequest.UnknownSuccess => Color.FromArgb(0xA0, 0xA0, 0x00),
            PiZeroCameraUpdateRequest.Cancelled => Color.FromArgb(0xFF, 0x55, 0x00),
            PiZeroCameraUpdateRequest.Failure.FailedToRequest => Color.FromArgb(0xFF, 0x00, 0x00),
            PiZeroCameraUpdateRequest.Failure.UnknownFailure _ => Color.FromArgb(0xFF, 0x00, 0xAA),
            PiZeroCameraUpdateRequest.Failure.VersionMismatch _ => Color.FromArgb(0xFF, 0xAA, 0x00),
            PiZeroCameraUpdateRequest.Failure.Failed => Color.FromArgb(0x55, 0x00, 0x00),
            null => Color.FromArgb(0x00, 0x00, 0x00),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private async Task OnGlobalChanged()
    {
        await InvokeAsync(async () =>
        {
            StateHasChanged();
            await _gridData.ReloadServerData();
        });
    }

    protected override void OnInitialized()
    {
        ChangeListener.OnUpdateChange += OnGlobalChanged;
    }

    public void Dispose()
    {
        ChangeListener.OnUpdateChange -= OnGlobalChanged;
        GC.SuppressFinalize(this);
    }
}

public class UpdateElement(UpdateModel updateModel)
{
    public readonly string Version = updateModel.Version;

    public readonly DateTime UploadedTime = TimeZoneInfo.ConvertTime(updateModel.UploadedTime.LocalDateTime,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Riga"));

    public readonly DateTime? UpdatedTime = updateModel.UpdatedTime == null
        ? null
        : TimeZoneInfo.ConvertTime(updateModel.UpdatedTime.Value.LocalDateTime,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Riga"));

    public bool IsCurrent = updateModel.IsCurrent;
}
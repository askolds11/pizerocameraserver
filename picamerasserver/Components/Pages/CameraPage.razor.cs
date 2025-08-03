using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.manager;
using Color = System.Drawing.Color;

namespace picamerasserver.Components.Pages;

public partial class CameraPage : ComponentBase, IDisposable
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;

    private MudDataGrid<PictureElement> _gridData = null!;
    private PictureElement? _selectedPicture;
    private string? _previewStreamUrl;

    private void OnGlobalChanged()
    {
        InvokeAsync(async () =>
        {
            await _gridData.ReloadServerData();
            StateHasChanged();
        });
    }

    /// <summary>
    /// Refreshes an individual picture
    /// </summary>
    /// <param name="uuid"></param>
    private void OnPictureChanged(Guid uuid)
    {
        InvokeAsync(async () =>
        {
            if (_selectedPicture != null)
            {
                await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
                var updatedItem = await piDbContext.PictureRequests
                    .Include(x => x.CameraPictures)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Uuid == uuid);

                if (updatedItem != null && _selectedPicture != null)
                {
                    _selectedPicture = new PictureElement(updatedItem);
                    UpdateTooltipTransform();
                }

                await _gridData.ReloadServerData();
                
                // State will only change if a picture is selected
                StateHasChanged();
            }
        });
    }

    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnChange += OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange += OnPictureChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChange -= OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange -= OnPictureChanged;
        GC.SuppressFinalize(this);
    }

    private async Task TakePicture()
    {
        var pictureRequestModel = await PiZeroCameraManager.RequestTakePicture();
        _selectedPicture = new PictureElement(pictureRequestModel);
        await _gridData.ReloadServerData();
    }

    private async Task SendPicture()
    {
        if (_selectedPicture == null)
        {
            throw new ArgumentNullException(nameof(_selectedPicture));
        }

        await PiZeroCameraManager.RequestSendPicture(_selectedPicture.Uuid);
    }

    private async Task StartPreview()
    {
        await PiZeroCameraManager.StartPreview();
        // wait for the preview to start
        // TODO: Maybe some kind of confirmation?
        await Task.Delay(2000);
        _previewStreamUrl = "http://pizeroA1.local:8000/stream.mjpg";
    }

    private async Task StopPreview()
    {
        await PiZeroCameraManager.StopPreview();
        _previewStreamUrl = null;
    }

    private async Task OnSubmitConfig()
    {
        // var controls = new CameraRequest.SetControls();
        // controls.NoiseReductionMode = NoiseReductionModeEnum.Off;
        // await MqttStuff.SetConfig(controls);
    }

    private async Task OnGetConfig()
    {
        // await MqttStuff.SetConfig(new CameraRequest.GetControlLimits());
    }

    private async Task SelectPicture(Guid uuid)
    {
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        var updatedItem = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        _selectedPicture = updatedItem == null ? null : new PictureElement(updatedItem);
        UpdateTooltipTransform();
    }

    private Color ColorTransform(string cameraId)
    {
        var cameraPicture = _selectedPicture?.CameraPictures.FirstOrDefault(x => x.CameraId == cameraId);
        if (cameraPicture == null)
        {
            return Color.FromArgb(0x00, 0x00, 0x00);
        }

        return cameraPicture.CameraPictureStatus switch
        {
            CameraPictureStatus.Requested => Color.FromArgb(0x55, 0x55, 0x00),
            CameraPictureStatus.FailedToRequest => Color.FromArgb(0xFF, 0x00, 0x00),
            CameraPictureStatus.Taken => Color.FromArgb(0x00, 0x55, 0x55),
            CameraPictureStatus.SavedOnDevice => Color.FromArgb(0x00, 0x55, 0x00),
            CameraPictureStatus.Failed => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.PictureFailedToSave => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.PictureFailedToSchedule => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.PictureFailedToTake => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.RequestedSend => Color.FromArgb(0x55, 0x55, 0x00),
            CameraPictureStatus.FailedToRequestSend => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.FailureSend => Color.FromArgb(0x99, 0x00, 0x00),
            CameraPictureStatus.Success => Color.FromArgb(0x00, 0xFF, 0x00),
            CameraPictureStatus.Unknown => Color.FromArgb(0x00, 0x00, 0xFF),
            CameraPictureStatus.PictureFailedToRead => Color.FromArgb(0x55, 0x00, 0x00),
            CameraPictureStatus.PictureFailedToSend => Color.FromArgb(0x55, 0x00, 0x00),
            null => Color.FromArgb(0x00, 0x00, 0x00),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private async Task<GridData<PictureElement>> ServerReload(GridState<PictureElement> state)
    {
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();

        var data = piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .OrderByDescending(x => x.Uuid)
            .AsNoTracking()
            .Select(x => new PictureElement(x));
        var totalItems = await data.CountAsync();
        var pagedData = await data.Skip(state.Page * state.PageSize).Take(state.PageSize).ToArrayAsync();

        return new GridData<PictureElement>
        {
            TotalItems = totalItems,
            Items = pagedData
        };
    }
}

public class PictureElement(PictureRequestModel pictureRequestModel)
{
    public readonly Guid Uuid = pictureRequestModel.Uuid;

    public readonly DateTime RequestTime = TimeZoneInfo.ConvertTime(pictureRequestModel.RequestTime.LocalDateTime,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Riga"));

    public readonly int TakenCount = pictureRequestModel.CameraPictures.Count(x => x.ReceivedTaken != null);
    public readonly int SentCount = pictureRequestModel.CameraPictures.Count(x => x.ReceivedSent != null);
    public readonly int TotalCount = pictureRequestModel.CameraPictures.Count(x => x.CameraPictureStatus != null);

    public readonly bool CanSend = pictureRequestModel.CameraPictures.Where(x => x.CameraPictureStatus != null)
        .All(x => x.CameraPictureStatus == CameraPictureStatus.Taken);

    public readonly List<CameraPictureModel> CameraPictures = pictureRequestModel.CameraPictures;
}
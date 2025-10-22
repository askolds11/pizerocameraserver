using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.TakePicture;

namespace picamerasserver.Components.Components.NewPicture;

public partial class PictureTakeSet : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required PictureRequestType PictureRequestType { get; set; }
    [Parameter, EditorRequired] public required Guid? PictureSetUId { get; set; }
    [Parameter, EditorRequired] public required PictureRequestModel? PictureRequestModel { get; set; }
    [Parameter, EditorRequired] public required bool Disabled { get; set; }
    [Parameter, EditorRequired] public required bool Finished { get; set; }

    [Inject] protected PiZeroManager PiZeroManager { get; init; } = null!;
    [Inject] protected ITakePictureManager TakePictureManager { get; init; } = null!;
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;

    private bool TakePicActive => TakePictureManager.TakePictureActive;

    private PictureRequestModel? _pictureRequestModel = null;


    private async Task TakePicture()
    {
        if (PictureSetUId == null)
        {
            throw new ArgumentNullException(nameof(PictureSetUId));
        }

        // Make the old one inactive
        if (_pictureRequestModel != null)
        {
            await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
            await piDbContext.PictureRequests.Where(x => x.Uuid == _pictureRequestModel.Uuid)
                .ExecuteUpdateAsync(x => x.SetProperty(
                    b => b.IsActive,
                    false)
                );
        }
        
        var pictureRequestResult = await TakePictureManager.RequestTakePictureAll(PictureRequestType, PictureSetUId);
        if (pictureRequestResult.IsFailure)
        {
            Snackbar.Add($"Failed to take picture: {pictureRequestResult.Error}", Severity.Error);
        }
        else
        {
            _pictureRequestModel = pictureRequestResult.Value;
            _canTryAgain = false;
        }
    }

    private async Task CancelTakePicture()
    {
        await TakePictureManager.CancelTakePicture();
    }

    private void TryAgain()
    {
        _canTryAgain = true;
    }

    private bool _canTryAgain = false;

    private int TakenCount =>
        _pictureRequestModel?.CameraPictures.Count(x => x.ReceivedTaken != null) ?? 0;

    private int SavedCount =>
        _pictureRequestModel?.CameraPictures.Count(x => x.ReceivedSaved != null) ?? 0;

    private int SentCount =>
        _pictureRequestModel?.CameraPictures.Count(x => x.ReceivedSent != null) ?? 0;

    private int FailedCount =>
        _pictureRequestModel?.CameraPictures.Count(x =>
            (x.ReceivedTaken == null || x.ReceivedSaved == null) &&
            x.CameraPictureStatus
                is not CameraPictureStatus.Requested
                and not CameraPictureStatus.Taken
                and not CameraPictureStatus.SavedOnDevice
        ) ?? 0;

    private int FailedSendCount =>
        _pictureRequestModel?.CameraPictures.Count(x =>
            x.ReceivedSent == null &&
            x.CameraPictureStatus
                is not CameraPictureStatus.Success
                and not CameraPictureStatus.RequestedSend
                and not CameraPictureStatus.SavedOnDevice
        ) ?? 0;

    private int AliveCount =>
        PiZeroManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });

    private int RequestCount => _pictureRequestModel?.CameraPictures.Count ?? AliveCount;

    private int ProgressTaken => RequestCount == 0 ? 0 : TakenCount * 100 / RequestCount;
    private int ProgressSaved => RequestCount == 0 ? 0 : SavedCount * 100 / RequestCount;
    private int ProgressSent => RequestCount == 0 ? 0 : SentCount * 100 / RequestCount;

    private async Task OnPingGlobalChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private static readonly Func<PiDbContext, Guid, Task<PictureRequestModel?>> GetPictureRequestModelByUuid =
        EF.CompileAsyncQuery((PiDbContext context, Guid uuid) =>
            context.PictureRequests
                .Include(x => x.CameraPictures)
                .AsNoTracking()
                .FirstOrDefault(x => x.Uuid == uuid && x.IsActive == true)
        );

    private async Task OnPictureChanged(Guid uuid)
    {
        await InvokeAsync(async () =>
        {
            if (uuid == _pictureRequestModel?.Uuid)
            {
                await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
                // _pictureRequestModel = await piDbContext.PictureRequests
                //     .Include(x => x.CameraPictures)
                //     .AsNoTracking()
                //     .FirstOrDefaultAsync(x => x.Uuid == uuid);
                _pictureRequestModel = await GetPictureRequestModelByUuid(piDbContext, uuid);

                UpdateTooltipTransform();
            }

            StateHasChanged();
        });
    }

    protected override void OnInitialized()
    {
        ChangeListener.OnPingChange += OnPingGlobalChanged;
        ChangeListener.OnNtpChange += OnPingGlobalChanged;
        ChangeListener.OnPictureChange += OnPictureChanged;
    }

    public void Dispose()
    {
        ChangeListener.OnPingChange -= OnPingGlobalChanged;
        ChangeListener.OnNtpChange -= OnPingGlobalChanged;
        ChangeListener.OnPictureChange -= OnPictureChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnParametersSet()
    {
        // Only set model if not already set to prevent desync
        if (_pictureRequestModel == null)
        {
            _pictureRequestModel = PictureRequestModel;

            UpdateTooltipTransform();
        }
    }
}
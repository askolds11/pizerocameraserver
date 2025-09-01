using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Components;

public partial class PictureTakeSet : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required PictureRequestType PictureRequestType { get; set; }
    [Parameter, EditorRequired] public required Guid? PictureSetUId { get; set; }
    [Parameter, EditorRequired] public required PictureRequestModel? PictureRequestModel { get; set; }
    [Parameter, EditorRequired] public required bool Disabled { get; set; }

    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
    [Inject] protected ITakePictureManager TakePictureManager { get; init; } = null!;
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;

    private bool TakePicActive => PiZeroCameraManager.TakePictureActive;
    
    private async Task TakePicture()
    {
        if (PictureSetUId == null)
        {
            throw new ArgumentNullException(nameof(PictureSetUId));
        }

        // Make the old one inactive
        if (PictureRequestModel != null)
        {
            await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
            await piDbContext.PictureRequests.Where(x => x.Uuid == PictureRequestModel.Uuid)
                .ExecuteUpdateAsync(x => x.SetProperty(
                    b => b.IsActive,
                    false)
                );
        }

        // TODO: Error handling
        PictureRequestModel = (await TakePictureManager.RequestTakePictureAll(PictureRequestType, PictureSetUId)).Value;
        // _selectedPicture = new PictureElement(pictureRequestModel);
        // await _gridData.ReloadServerData();
        _canTryAgain = false;
    }

    private void TryAgain()
    {
        _canTryAgain = true;
    }

    private bool _canTryAgain = false;

    private int TakenCount =>
        PictureRequestModel?.CameraPictures.Count(x => x.ReceivedTaken != null) ?? 0;

    private int SavedCount =>
        PictureRequestModel?.CameraPictures.Count(x => x.ReceivedSaved != null) ?? 0;

    private int FailedCount =>
        PictureRequestModel?.CameraPictures.Count(x =>
            (x.ReceivedTaken == null || x.ReceivedSaved == null) &&
            x.CameraPictureStatus
                is not CameraPictureStatus.Requested
                and not CameraPictureStatus.Taken
                and not CameraPictureStatus.SavedOnDevice
        ) ?? 0;

    private int AliveCount =>
        PiZeroCameraManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });

    private int RequestCount => PictureRequestModel?.CameraPictures.Count ?? AliveCount;

    private int ProgressTaken => RequestCount == 0 ? 0 : TakenCount * 100 / RequestCount;
    private int ProgressSent => RequestCount == 0 ? 0 : SavedCount * 100 / RequestCount;

    private void OnGlobalChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnPictureChanged(Guid uuid)
    {
        InvokeAsync(async () =>
        {
            if (uuid == PictureRequestModel?.Uuid)
            {
                await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
                PictureRequestModel = await piDbContext.PictureRequests
                    .Include(x => x.CameraPictures)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Uuid == uuid);

                UpdateTooltipTransform();
            }
            
            StateHasChanged();
        });
    }

    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnChangePing += OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange += OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange += OnPictureChanged;
        
        UpdateTooltipTransform();
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChangePing -= OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange -= OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange -= OnPictureChanged;
        GC.SuppressFinalize(this);
    }
}
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

        // TODO: Error handling
        _pictureRequestModel = (await TakePictureManager.RequestTakePictureAll(PictureRequestType, PictureSetUId)).Value;
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
        _pictureRequestModel?.CameraPictures.Count(x => x.ReceivedTaken != null) ?? 0;

    private int SavedCount =>
        _pictureRequestModel?.CameraPictures.Count(x => x.ReceivedSaved != null) ?? 0;

    private int FailedCount =>
        _pictureRequestModel?.CameraPictures.Count(x =>
            (x.ReceivedTaken == null || x.ReceivedSaved == null) &&
            x.CameraPictureStatus
                is not CameraPictureStatus.Requested
                and not CameraPictureStatus.Taken
                and not CameraPictureStatus.SavedOnDevice
        ) ?? 0;

    private int AliveCount =>
        PiZeroCameraManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });

    private int RequestCount => _pictureRequestModel?.CameraPictures.Count ?? AliveCount;

    private int ProgressTaken => RequestCount == 0 ? 0 : TakenCount * 100 / RequestCount;
    private int ProgressSent => RequestCount == 0 ? 0 : SavedCount * 100 / RequestCount;

    private void OnGlobalChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private static readonly Func<PiDbContext, Guid, Task<PictureRequestModel?>> GetPictureRequestModelByUuid =
        EF.CompileAsyncQuery((PiDbContext context, Guid uuid) =>
            context.PictureRequests
                .Include(x => x.CameraPictures)
                .AsNoTracking()
                .FirstOrDefault(x => x.Uuid == uuid)
        );

    private async Task OnPictureChanged(Guid uuid)
    {
        InvokeAsync(async () =>
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
        PiZeroCameraManager.OnChangePing += OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange += OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange += OnPictureChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChangePing -= OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange -= OnGlobalChanged;
        PiZeroCameraManager.OnPictureChange -= OnPictureChanged;
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
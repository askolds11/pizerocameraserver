using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using picamerasserver.Components.Components.NewPicture;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.PiZero;

namespace picamerasserver.Components.Pages;

public partial class NewPicturePage : ComponentBase, IDisposable
{
    [Parameter] public Guid? Uuid { get; set; }
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected IServiceScopeFactory ScopeFactory { get; init; } = null!;

    private IServiceScope _scope = null!;
    private SharedState _sharedState = null!;


    private PictureSetModel? _pictureSet;
    
    private bool AnyActive => _sharedState.AnyActive;


    private PictureRequestModel? PictureRequestStandingSpread => _pictureSet?.PictureRequests.FirstOrDefault(x =>
        x is { PictureRequestType: PictureRequestType.StandingSpread, IsActive: true });

    private PictureRequestModel? PictureRequestStandingTogether => _pictureSet?.PictureRequests.FirstOrDefault(x =>
        x is { PictureRequestType: PictureRequestType.StandingTogether, IsActive: true });

    private PictureRequestModel? PictureRequestSitting =>
        _pictureSet?.PictureRequests.FirstOrDefault(x => x is
            { PictureRequestType: PictureRequestType.Sitting, IsActive: true });

    [MaxLength(200)] private string Name { get; set; } = "";

    private async Task CreatePictureSet()
    {
        _pictureSet = new PictureSetModel
        {
            Uuid = Guid.CreateVersion7(),
            Name = Name,
            Created = DateTimeOffset.UtcNow
        };
        _sharedState.PictureSet = _pictureSet;
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        piDbContext.PictureSets.Add(_pictureSet);
        await piDbContext.SaveChangesAsync();
        NavigationManager.NavigateTo($"/NewPicturePage/{_pictureSet.Uuid}", replace: true);
    }

    private async Task UpdatePictureSet()
    {
        if (_pictureSet == null)
        {
            throw new ArgumentNullException(nameof(_pictureSet));
        }

        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        await piDbContext.PictureSets.Where(x => x.Uuid == _pictureSet.Uuid)
            .ExecuteUpdateAsync(x => x.SetProperty(
                b => b.Name, Name)
            );
        await RefreshPictureSet();
    }

    private bool Alived => _sharedState.Alived;
    private bool NtpSynced => _sharedState.NtpSynced;
    private bool SyncedFrames => _sharedState.SyncedFrames;

    private int AliveCount => _sharedState.AliveCount;

    /// <summary>
    /// Refreshes list
    /// </summary>
    /// <param name="uuid"></param>
    private async Task OnPictureSetChanged(Guid uuid)
    {
        await InvokeAsync(async () =>
        {
            if (_pictureSet != null && _pictureSet.Uuid == uuid)
            {
                await RefreshPictureSet();
                StateHasChanged();
            }
        });
    }


    private async Task RefreshPictureSet()
    {
        if (Uuid == null)
        {
            throw new ArgumentNullException(nameof(Uuid));
        }

        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        _pictureSet = await piDbContext.PictureSets
            .Include(x => x.PictureRequests.Where(y => y.IsActive == true))
            .ThenInclude(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == Uuid);
        _sharedState.PictureSet = _pictureSet;
    }

    protected override async Task OnInitializedAsync()
    {
        if (Uuid != null)
        {
            await RefreshPictureSet();
        }

        Name = _pictureSet?.Name ?? "";
    }

    private async Task OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        _scope = ScopeFactory.CreateScope();
        _sharedState = _scope.ServiceProvider.GetRequiredService<SharedState>();

        ChangeListener.OnPictureSetChange += OnPictureSetChanged;
        _sharedState.OnChange += OnChange;
    }

    public void Dispose()
    {
        ChangeListener.OnPictureSetChange -= OnPictureSetChanged;
        _sharedState.OnChange -= OnChange;

        _scope.Dispose();

        GC.SuppressFinalize(this);
    }
}
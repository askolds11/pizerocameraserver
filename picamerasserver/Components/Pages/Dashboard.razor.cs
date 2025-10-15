using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.SendPicture;

namespace picamerasserver.Components.Pages;

public partial class Dashboard : ComponentBase, IDisposable
{
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; init; } = null!;
    [Inject] protected ISendPictureSetManager SendPictureSetManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;

    private MudDataGrid<PictureSetElement> _gridData = null!;
    private bool SendSetActive => SendPictureSetManager.SendSetActive;

    /// <summary>
    /// Refreshes list
    /// </summary>
    /// <param name="uuid"></param>
    private async Task OnPictureSetChanged(Guid uuid)
    {
        await InvokeAsync(async () =>
        {
            StateHasChanged();
            await _gridData.ReloadServerData();
        });
    }

    private async Task SendPicture(Guid uuid)
    {
        await SendPictureSetManager.RequestSendPictureSet(uuid);
    }

    private void NavigateToNew()
    {
        NavigationManager.NavigateTo("/NewPicturePage", replace: false);
    }

    private async Task<GridData<PictureSetElement>> ServerReload(GridState<PictureSetElement> state)
    {
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();

        var data = piDbContext.PictureSets
            .Include(x => x.PictureRequests.Where(y => y.IsActive == true))
            .ThenInclude(x => x.CameraPictures)
            .OrderByDescending(x => x.Uuid)
            .AsNoTracking()
            .Select(x => new PictureSetElement(x));
        var totalItems = await piDbContext.PictureSets.CountAsync();
        var pagedData = await data.Skip(state.Page * state.PageSize).Take(state.PageSize).ToArrayAsync();

        return new GridData<PictureSetElement>
        {
            TotalItems = totalItems,
            Items = pagedData
        };
    }

    protected override void OnInitialized()
    {
        ChangeListener.OnPictureSetChange += OnPictureSetChanged;
    }

    public void Dispose()
    {
        ChangeListener.OnPictureSetChange -= OnPictureSetChanged;
        GC.SuppressFinalize(this);
    }
}

public class PictureSetElement(PictureSetModel pictureSetModel)
{
    public Guid Uuid => pictureSetModel.Uuid;
    public string Name => pictureSetModel.Name;
    public bool IsDone => pictureSetModel.IsDone;
    public int PictureSetCount => pictureSetModel.PictureRequests.Count;

    // public DateTime RequestTime = TimeZoneInfo.ConvertTime(pictureRequestModel.RequestTime.LocalDateTime,
    //     TimeZoneInfo.FindSystemTimeZoneById("Europe/Riga"));

    public int TakenCount =>
        pictureSetModel.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.ReceivedSaved != null));

    public int SentCount =>
        pictureSetModel.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.ReceivedSent != null));

    public int TotalCount =>
        pictureSetModel.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.CameraPictureStatus != null));
}
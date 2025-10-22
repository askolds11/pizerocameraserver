using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.SendPicture;

namespace picamerasserver.Components.Components.NewPicture;

public partial class ActionsTab : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected ISendPictureSetManager SendPictureSetManager { get; init; } = null!;
    [Inject] protected IUploadManager UploadToServer { get; init; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; init; } = null!;

    private PictureSetModel? PictureSet => SharedState.PictureSet;

    private bool SendSetActive => SharedState.SendSetActive;
    private bool UploadActive => SharedState.UploadActive;
    private bool AnyActive => SharedState.AnyActive;

    private int AllSentCount => PictureSet?.PictureRequests
        .Sum(x => x.CameraPictures.Count(y => y.ReceivedSent != null)) ?? 0;

    private int AllSentFailedCount => PictureSet?.PictureRequests
        .Sum(x => x.CameraPictures.Count(y =>
            y is
            {
                ReceivedTaken: not null, CameraPictureStatus: CameraPictureStatus.Failed
                or CameraPictureStatus.PictureFailedToRead or CameraPictureStatus.PictureFailedToSend
                or CameraPictureStatus.Unknown or CameraPictureStatus.CancelledSend
            })) ?? 0;

    private int AllTotalCount =>
        PictureSet?.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.ReceivedTaken != null)) ?? 0;

    private int AllUploadedCount =>
        PictureSet?.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.Synced)) ?? 0;


    private void NavigateToNew()
    {
        NavigationManager.NavigateTo("/", replace: false);
    }
    
    private async Task FinishPictureSet()
    {
        if (PictureSet == null)
        {
            throw new ArgumentNullException(nameof(PictureSet));
        }

        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        await piDbContext.PictureSets.Where(x => x.Uuid == PictureSet.Uuid)
            .ExecuteUpdateAsync(x => x.SetProperty(
                b => b.IsDone, true)
            );
        ChangeListener.UpdatePictureSet(PictureSet.Uuid);
    }

    private async Task SendPictureSet()
    {
        if (PictureSet == null)
        {
            throw new ArgumentNullException(nameof(PictureSet));
        }

        await SendPictureSetManager.RequestSendPictureSet(PictureSet.Uuid);
    }

    private async Task UploadSmb()
    {
        if (PictureSet == null)
        {
            throw new ArgumentNullException(nameof(PictureSet));
        }

        var result = await UploadToServer.Upload(PictureSet.Uuid);

        if (result.IsFailure)
        {
            Snackbar.Add($"Upload failed: {result.Error}!", Severity.Error);
        }
        else
        {
            Snackbar.Add("Upload completed!", Severity.Success);
        }
    }

    private async Task CancelSendSet()
    {
        await SendPictureSetManager.CancelSendSet();
    }

    private async Task CancelUpload()
    {
        await UploadToServer.CancelUpload();
    }

    private async Task OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }
    
    protected override void OnInitialized()
    {
        SharedState.OnChange += OnChange;
    }

    public void Dispose()
    {
        SharedState.OnChange -= OnChange;
        GC.SuppressFinalize(this);
    }
}
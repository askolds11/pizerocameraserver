using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.Components.Pages;

public partial class NewPicturePage : ComponentBase, IDisposable
{
    [Parameter] public Guid? Uuid { get; set; }
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
    [Inject] protected IDbContextFactory<PiDbContext> DbContextFactory { get; init; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; init; } = null!;
    [Inject] protected ISendPictureSetManager SendPictureSetManager { get; init; } = null!;

    private PictureSetModel? _pictureSet;
    private bool SendSetActive => SendPictureSetManager.SendSetActive;
    private bool PingActive => PiZeroCameraManager.PingActive;
    private bool NtpActive => PiZeroCameraManager.NtpActive;

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
            Name = Name
        };
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

        await RefreshPictureSet();
        _pictureSet.Name = Name;
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        piDbContext.PictureSets.Update(_pictureSet);
        await piDbContext.SaveChangesAsync();
    }

    private async Task FinishPictureSet()
    {
        if (_pictureSet == null)
        {
            throw new ArgumentNullException(nameof(_pictureSet));
        }

        await RefreshPictureSet();
        _pictureSet.IsDone = true;
        await using var piDbContext = await DbContextFactory.CreateDbContextAsync();
        piDbContext.PictureSets.Update(_pictureSet);
        await piDbContext.SaveChangesAsync();
    }

    private async Task SendPictureSet()
    {
        if (_pictureSet == null)
        {
            throw new ArgumentNullException(nameof(_pictureSet));
        }

        await SendPictureSetManager.RequestSendPictureSet(_pictureSet.Uuid);
    }

    private bool Alived { get; set; } = false;
    private bool Synced { get; set; } = false;

    private int SyncedCount =>
        PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.NtpRequest is PiZeroCameraNtpRequest.Success);

    private int PingedCount => PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.Pingable == true);
    private int StatusCount => PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.Status != null);

    private int AliveCount =>
        PiZeroCameraManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });

    private int TotalCount => PiZeroCameraManager.PiZeroCameras.Count;

    private int AllSentCount => _pictureSet?.PictureRequests
        .Sum(x => x.CameraPictures.Count(y => y.ReceivedSent != null)) ?? 0;

    private int AllTotalCount =>
        _pictureSet?.PictureRequests
            .Sum(x => x.CameraPictures.Count(y => y.CameraPictureStatus != null)) ?? 0;

    private async Task GetAlive()
    {
        Alived = false;
        if (_pictureSet == null)
        {
            await CreatePictureSet();
        }

        try
        {
            var task1 = PiZeroCameraManager.Ping();
            var task2 = PiZeroCameraManager.GetStatus();
            await Task.WhenAll(task1, task2);
            Alived = true;
        }
        catch (OperationCanceledException)
        {
            Alived = false;
        }
    }

    private async Task RequestNtpSyncStep()
    {
        Synced = true;
        if (_pictureSet == null)
        {
            await CreatePictureSet();
        }

        try
        {
            await PiZeroCameraManager.RequestNtpSync(new NtpRequest.Step());
            Synced = true;
        }
        catch (OperationCanceledException)
        {
            Synced = false;
        }
    }

    private void OverrideGetAlive()
    {
        Alived = true;
    }

    private void OverrideNtp()
    {
        Synced = true;
    }
    
    private List<float>? GetOffsets()
    {
        var offsets = PiZeroCameraManager.PiZeroCameras.Values
            .Where(x => x.LastNtpOffsetMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpOffsetMillis!))
            .ToList();

        return offsets.Count == 0 ? null : offsets;
    }

    private List<float>? GetErrors()
    {
        var errors = PiZeroCameraManager.PiZeroCameras.Values
            .Where(x => x.LastNtpErrorMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpErrorMillis!))
            .ToList();

        return errors.Count == 0 ? null : errors;
    }

    private float? MinOffset => GetOffsets()?.Min();
    private float? MaxOffset => GetOffsets()?.Max();
    private float? MinError => GetErrors()?.Min();
    private float? MaxError => GetErrors()?.Max();

    private void OnGlobalChanged()
    {
        InvokeAsync(StateHasChanged);
    }

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

    private void OnNtpChanged()
    {
        InvokeAsync(() =>
        {
            UpdateTooltipTransformNtp();
            StateHasChanged();
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
    }

    private async Task CancelTakePic()
    {
        await PiZeroCameraManager.CancelTakePicture();
    }

    private async Task CancelSend()
    {
        await SendPictureSetManager.CancelSendSet();
    }

    private async Task CancelNtpSync()
    {
        await PiZeroCameraManager.CancelNtpSync();
    }

    private async Task CancelPing()
    {
        await PiZeroCameraManager.CancelPing();
    }

    protected override async Task OnInitializedAsync()
    {
        if (Uuid != null)
        {
            await RefreshPictureSet();
        }

        Name = _pictureSet?.Name ?? "";
    }

    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnChangePing += OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange += OnNtpChanged;
        PiZeroCameraManager.OnPictureSetChange += OnPictureSetChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChangePing -= OnGlobalChanged;
        PiZeroCameraManager.OnNtpChange -= OnNtpChanged;
        PiZeroCameraManager.OnPictureSetChange -= OnPictureSetChanged;
        GC.SuppressFinalize(this);
    }
}
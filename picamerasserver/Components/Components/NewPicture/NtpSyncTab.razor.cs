using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.Ntp;
using picamerasserver.PiZero.Requests;

namespace picamerasserver.Components.Components.NewPicture;

public partial class NtpSyncTab : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected PiZeroManager PiZeroManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected INtpManager NtpManager { get; init; } = null!;

    private PictureSetModel? PictureSet => SharedState.PictureSet;

    private bool NtpActive => SharedState.NtpActive;
    private bool AnyActive => SharedState.AnyActive;

    private bool Alived => SharedState.Alived;

    private bool NtpSynced
    {
        get => SharedState.NtpSynced;
        set => SharedState.NtpSynced = value;
    }

    private bool IndicatorNtped => SharedState.IndicatorNtped;


    private int NtpSyncedCount =>
        PiZeroManager.PiZeroCameras.Values.Count(x => x.NtpRequest is PiZeroNtpRequest.Success);

    private int AliveCount => SharedState.AliveCount;

    private List<float>? GetOffsets()
    {
        var offsets = PiZeroManager.PiZeroCameras.Values
            .Where(x => x.LastNtpOffsetMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpOffsetMillis!))
            .ToList();
        if (PiZeroManager.PiZeroIndicator.LastNtpOffsetMillis != null)
        {
            offsets.Add((float)PiZeroManager.PiZeroIndicator.LastNtpOffsetMillis);
        }

        return offsets.Count == 0 ? null : offsets;
    }

    private List<float>? GetErrors()
    {
        var errors = PiZeroManager.PiZeroCameras.Values
            .Where(x => x.LastNtpErrorMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpErrorMillis!))
            .ToList();
        if (PiZeroManager.PiZeroIndicator.LastNtpErrorMillis != null)
        {
            errors.Add((float)PiZeroManager.PiZeroIndicator.LastNtpErrorMillis);
        }

        return errors.Count == 0 ? null : errors;
    }

    private float? MinOffset => GetOffsets()?.Min();
    private float? MaxOffset => GetOffsets()?.Max();
    private float? MinError => GetErrors()?.Min();
    private float? MaxError => GetErrors()?.Max();

    private async Task RequestNtpSyncStep()
    {
        NtpSynced = false;

        try
        {
            await NtpManager.RequestNtpSync(new NtpRequest.Step());
            NtpSynced = true;
        }
        catch (OperationCanceledException)
        {
            NtpSynced = false;
        }
    }

    private async Task CancelNtp()
    {
        await NtpManager.CancelNtpSync();
    }

    private void OverrideNtp()
    {
        NtpSynced = true;
    }

    private async Task OnNtpChanged()
    {
        await InvokeAsync(() =>
        {
            UpdateTooltipTransformNtp();
            UpdateIndicatorTooltip();
            StateHasChanged();
        });
    }

    private async Task OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        SharedState.OnChange += OnChange;
        ChangeListener.OnNtpChange += OnNtpChanged;

        UpdateTooltipTransformNtp();
        UpdateIndicatorTooltip();
    }

    public void Dispose()
    {
        SharedState.OnChange -= OnChange;
        ChangeListener.OnNtpChange -= OnNtpChanged;
        GC.SuppressFinalize(this);
    }
}
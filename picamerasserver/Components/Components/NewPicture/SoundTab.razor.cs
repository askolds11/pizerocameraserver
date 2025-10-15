using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera;

namespace picamerasserver.Components.Components.NewPicture;

public partial class SoundTab : ComponentBase
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected Sound Sound { get; init; } = null!;

    private PictureSetModel? PictureSet => SharedState.PictureSet;
    
    private bool AnyActive => SharedState.AnyActive;

    private bool Alived => SharedState.Alived;
    private bool NtpSynced => SharedState.NtpSynced;
    
    private bool IndicatorAlive => SharedState.IndicatorAlive;

    private int AliveCount => SharedState.AliveCount;

    private async Task TestSignal()
    {
        await Sound.SendSignal();
    }
}
using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Components;

public partial class StatusTable : ComponentBase
{
    [Parameter, EditorRequired]
    public required Func<PiZeroCamera, string> ColorTransform { get; set; }
    
    [Inject]
    protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;
    
    // Letters as columns (A-P)
    private List<string> Columns { get; set; } = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();

    // Numbers as rows (1-6)
    private List<int> RowNumbers { get; set; } = Enumerable.Range(1, 6).ToList();

    private string GetCellColor(string letter, int number)
    {
        return ColorTransform(PiZeroCameraManager.PiZeroCameras[letter + number]);
    }
}
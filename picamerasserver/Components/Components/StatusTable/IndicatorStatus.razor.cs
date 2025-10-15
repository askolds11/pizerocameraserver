using System.Drawing;
using Microsoft.AspNetCore.Components;

namespace picamerasserver.Components.Components.StatusTable;

public partial class IndicatorStatus : ComponentBase
{
    [Parameter, EditorRequired]
    public required Func<Color> ColorTransform { get; set; }
    [Parameter, EditorRequired]
    public required RenderFragment? TooltipTransform { get; set; }
    
    private string GetCellColor()
    {
        var color = ColorTransform();
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
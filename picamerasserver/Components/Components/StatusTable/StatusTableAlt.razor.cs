using System.Drawing;
using Microsoft.AspNetCore.Components;

namespace picamerasserver.Components.Components.StatusTable;

public partial class StatusTableAlt : ComponentBase
{
    [Parameter, EditorRequired]
    public required Func<string, Color> ColorTransform { get; set; }
    [Parameter, EditorRequired]
    public required RenderFragment<string> TooltipTransform { get; set; }
    
    // Numbers as columns (1-6)
    private readonly List<int> _columns = Enumerable.Range(1, 6).ToList();
    
    // Letters as rows (A-P)
    private readonly List<string> _rows = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();

    private string GetCellColor(string letter, int number)
    {
        var color = ColorTransform(letter + number);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
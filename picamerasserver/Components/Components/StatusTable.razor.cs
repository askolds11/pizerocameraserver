using System.Drawing;
using Microsoft.AspNetCore.Components;

namespace picamerasserver.Components.Components;

public partial class StatusTable : ComponentBase
{
    [Parameter, EditorRequired]
    public required Func<string, Color> ColorTransform { get; set; }
    
    // Letters as columns (A-P)
    private readonly List<string> _columns = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();

    // Numbers as rows (1-6)
    private readonly List<int> _rows = Enumerable.Range(1, 6).ToList();

    private string GetCellColor(string letter, int number)
    {
        var color = ColorTransform(letter + number);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
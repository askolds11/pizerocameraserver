using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using picamerasserver.Options;
using picamerasserver.PiZero.Manager;

namespace picamerasserver.Components.Pages;

public partial class Utils : ComponentBase
{
    [Inject]
    protected PiZeroManager PiZeroManager { get; set; } = null!;
    [Inject]
    protected IOptionsMonitor<ServerOptions> ServerOptionsMonitor { get; set; } = null!;
    
    private async Task ShutdownIndicator()
    {
        var result = await PiZeroManager.ShutdownIndicator();
        if (result)
        {
            Snackbar.Add("Shutdown message sent!", Severity.Success);
        }
        else
        {
            Snackbar.Add("Shutdown message failed to send!", Severity.Error);
        }
    }
    
    private async Task ShutdownPis()
    {
        var result = await PiZeroManager.ShutdownPis();
        if (result)
        {
            Snackbar.Add("Shutdown message sent!", Severity.Success);
        }
        else
        {
            Snackbar.Add("Shutdown message failed to send!", Severity.Error);
        }
    }
    
    private async Task ShutdownCamerasQuadrant(char start)
    {
        var result = await PiZeroManager.ShutdownCamerasQuadrant(start);
        if (result)
        {
            Snackbar.Add("Shutdown message sent!", Severity.Success);
        }
        else
        {
            Snackbar.Add("Shutdown message failed to send!", Severity.Error);
        }
    }

    private async Task ShutdownCamerasQuadrantA()
    {
        await ShutdownCamerasQuadrant('A');
    }
    
    private async Task ShutdownCamerasQuadrantE()
    {
        await ShutdownCamerasQuadrant('E');
    }
    
    private async Task ShutdownCamerasQuadrantI()
    {
        await ShutdownCamerasQuadrant('I');
    }
    
    private async Task ShutdownCamerasQuadrantM()
    {
        await ShutdownCamerasQuadrant('M');
    }

    private async Task Shutdown()
    {
        var options = ServerOptionsMonitor.CurrentValue;
        
        var command = $"-p {options.Password} " +
                      $"ssh -o StrictHostKeyChecking=no {options.Username}@host.docker.internal " +
                      $"echo {options.Password} | sudo -S shutdown -h now";
        // var result = "";
        using var proc = new Process();
        proc.StartInfo.FileName = "/usr/bin/sshpass";
        proc.StartInfo.Arguments = command;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        // result += await proc.StandardOutput.ReadToEndAsync();
        // result += await proc.StandardError.ReadToEndAsync();
        
        // var result = await proc.StandardOutput.ReadToEndAsync();
        // Console.WriteLine(result);

        await proc.WaitForExitAsync();
        // return result;
    }
}
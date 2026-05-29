using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BoostModeCommon;

namespace BoostModeService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "BoostModeSvc";
        });

        builder.Services.AddSingleton<PowerModeSwitcher>();
        builder.Services.AddSingleton<CpuMonitor>();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}

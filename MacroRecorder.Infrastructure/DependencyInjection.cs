using System.Net.Http.Headers;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Persistence;
using MacroRecorder.Infrastructure.Playback;
using MacroRecorder.Infrastructure.Recording;
using MacroRecorder.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMacroRecorderInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMacroRepository, JsonMacroRepository>();
        services.AddSingleton<IPlaybackHotkeyStore, JsonPlaybackHotkeyStore>();
        services.AddSingleton<MacroQueueFileStore>();
        services.AddSingleton<IRecordingEngine, LowLevelRecordingEngine>();
        services.AddSingleton<IPlaybackService>(sp =>
            new SendInputPlaybackService(() => sp.GetService<IPlaybackUiFeedback>()));
        services.AddSingleton<IExternalUriOpener, ProcessExternalUriOpener>();
        services.AddSingleton(_ =>
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MacroRecorder", "1.0"));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return httpClient;
        });
        services.AddSingleton<IUpdateCheckService, GitHubReleaseUpdateCheckService>();
        return services;
    }
}

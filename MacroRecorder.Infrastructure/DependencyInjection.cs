using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Persistence;
using MacroRecorder.Infrastructure.Playback;
using MacroRecorder.Infrastructure.Recording;
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
        return services;
    }
}

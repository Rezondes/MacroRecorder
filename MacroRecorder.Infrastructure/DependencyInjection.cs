using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Input;
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
        services.AddSingleton<IRecordingEngine, LowLevelRecordingEngine>();
        services.AddSingleton<IPlaybackService, SendInputPlaybackService>();
        services.AddSingleton<ICursorPositionProvider, CursorPositionProvider>();
        return services;
    }
}

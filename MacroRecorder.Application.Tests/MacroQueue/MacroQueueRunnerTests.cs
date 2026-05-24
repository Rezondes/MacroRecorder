using MacroRecorder.Application;
using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Application.Tests.MacroQueue;

public sealed class MacroQueueRunnerTests
{
    [Fact]
    public async Task RunAsync_with_no_steps_completes_without_playback()
    {
        var playback = new FakePlaybackService();
        var runner = new MacroQueueRunner(
            new MacroWorkspaceService(new InMemoryMacroRepository(), new NullPlaybackHotkeyStore()),
            playback);
        var document = MacroQueueDocument.Create("empty", []);

        await runner.RunAsync(document, 0, false, false);

        Assert.Equal(0, playback.PlayCallCount);
    }

    [Fact]
    public async Task RunAsync_loops_whole_queue_when_enabled()
    {
        var macroId = MacroId.New();
        var macro = CreateMacroWithKeyDown(macroId);
        var repository = new InMemoryMacroRepository();
        await repository.SaveAsync(macro);
        var playback = new FakePlaybackService();
        var runner = new MacroQueueRunner(new MacroWorkspaceService(repository, new NullPlaybackHotkeyStore()), playback);
        var document = MacroQueueDocument.Create("q", [new QueueStep(macroId)], loopWholeQueue: true);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(document, 0, false, false, cancellationSource.Token));

        Assert.True(playback.PlayCallCount >= 2);
    }

    [Fact]
    public async Task RunAsync_plays_each_step_once_by_default()
    {
        var macroId = MacroId.New();
        var macro = CreateMacroWithKeyDown(macroId);
        var repository = new InMemoryMacroRepository();
        await repository.SaveAsync(macro);
        var playback = new FakePlaybackService();
        var runner = new MacroQueueRunner(new MacroWorkspaceService(repository, new NullPlaybackHotkeyStore()), playback);
        var document = MacroQueueDocument.Create("q", [new QueueStep(macroId)]);

        await runner.RunAsync(document, 0, false, false);

        Assert.Equal([macroId], playback.PlayedMacroIds);
    }

    [Fact]
    public async Task RunAsync_repeats_step_when_repeat_count_is_greater_than_one()
    {
        var macroId = MacroId.New();
        var macro = CreateMacroWithKeyDown(macroId);
        var repository = new InMemoryMacroRepository();
        await repository.SaveAsync(macro);
        var playback = new FakePlaybackService();
        var runner = new MacroQueueRunner(new MacroWorkspaceService(repository, new NullPlaybackHotkeyStore()), playback);
        var document = MacroQueueDocument.Create("q", [new QueueStep(macroId, RepeatCount: 3)]);

        await runner.RunAsync(document, 0, false, false);

        Assert.Equal(3, playback.PlayCallCount);
    }

    [Fact]
    public async Task RunAsync_throws_when_macro_is_missing()
    {
        var missingId = MacroId.New();
        var runner = new MacroQueueRunner(
            new MacroWorkspaceService(new InMemoryMacroRepository(), new NullPlaybackHotkeyStore()),
            new FakePlaybackService());
        var document = MacroQueueDocument.Create("q", [new QueueStep(missingId)]);

        await Assert.ThrowsAsync<MacroQueueMissingMacroException>(() =>
            runner.RunAsync(document, 0, false, false));
    }

    [Fact]
    public async Task RunAsync_throws_when_macro_has_no_events()
    {
        var macroId = MacroId.New();
        var emptyMacro = Macro.CreateEmpty("empty");
        var repository = new InMemoryMacroRepository();
        await repository.SaveAsync(emptyMacro);
        var runner = new MacroQueueRunner(
            new MacroWorkspaceService(repository, new NullPlaybackHotkeyStore()),
            new FakePlaybackService());
        var document = MacroQueueDocument.Create("q", [new QueueStep(emptyMacro.Id)]);

        await Assert.ThrowsAsync<MacroQueueEmptyMacroException>(() =>
            runner.RunAsync(document, 0, false, false));
    }

    [Fact]
    public async Task RunAsync_honors_cancellation_before_playback()
    {
        var macroId = MacroId.New();
        var macro = CreateMacroWithKeyDown(macroId);
        var repository = new InMemoryMacroRepository();
        await repository.SaveAsync(macro);
        var playback = new FakePlaybackService();
        var runner = new MacroQueueRunner(new MacroWorkspaceService(repository, new NullPlaybackHotkeyStore()), playback);
        var document = MacroQueueDocument.Create("q", [new QueueStep(macroId)]);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(document, 0, false, false, cancellationSource.Token));
    }

    [Fact]
    public void EstimateTotalDuration_includes_repeats_and_delays()
    {
        var macroId = MacroId.New();
        var document = MacroQueueDocument.Create(
            "q",
            [new QueueStep(
                macroId,
                RepeatCount: 2,
                InitialDelay: TimeSpan.FromSeconds(1),
                DelayBetweenRuns: TimeSpan.FromSeconds(2),
                PostStepDelay: TimeSpan.FromSeconds(3))]);
        var durations = new Dictionary<MacroId, TimeSpan> { [macroId] = TimeSpan.FromSeconds(5) };

        var total = MacroQueueRunner.EstimateTotalDuration(document, durations);

        Assert.Equal(TimeSpan.FromSeconds(16), total);
    }

    private static Macro CreateMacroWithKeyDown(MacroId macroId) =>
        new(macroId, "sample", RecordingMetadata.Empty(), [TestEvents.KeyDown()]);
}

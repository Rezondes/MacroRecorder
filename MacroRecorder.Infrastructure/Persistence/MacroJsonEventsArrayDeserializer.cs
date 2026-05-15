using System.Globalization;
using System.Text.Json;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Infrastructure.Persistence;

internal static class MacroJsonEventsArrayDeserializer
{
    public static List<RecordedInputEvent> Deserialize(JsonElement eventsArray, JsonSerializerOptions options)
    {
        if (eventsArray.ValueKind != JsonValueKind.Array)
            return [];

        var length = eventsArray.GetArrayLength();
        if (length == 0)
            return [];

        var first = eventsArray[0];
        var isLegacyTiming = first.TryGetProperty("elapsedSinceSessionStart", out _);

        if (!isLegacyTiming)
        {
            var list = new List<RecordedInputEvent>(length);
            foreach (var element in eventsArray.EnumerateArray())
                list.Add(JsonSerializer.Deserialize<RecordedInputEvent>(element.GetRawText(), options)
                         ?? throw new JsonException("Event deserialized to null."));
            return list;
        }

        var legacyEvents = new List<RecordedInputEvent>(length);
        var legacyWaitUntil = new List<TimeSpan>(length);
        foreach (var element in eventsArray.EnumerateArray())
        {
            var (ev, waitUntil) = DeserializeLegacyEvent(element);
            legacyEvents.Add(ev);
            legacyWaitUntil.Add(waitUntil);
        }

        LegacyElapsedTimingMigration.ApplyDelaysFromLegacyWaitUntilTimes(legacyEvents, legacyWaitUntil);
        return legacyEvents;
    }

    private static (RecordedInputEvent Event, TimeSpan LegacyWaitUntil) DeserializeLegacyEvent(JsonElement el)
    {
        var waitUntil = ReadTimeSpan(el, "elapsedSinceSessionStart");
        var sequence = ReadUInt64(el, "sequence");
        var discriminator = el.GetProperty("discriminator").GetString() ?? "";

        return discriminator switch
        {
            "keyDown" => (new KeyDownRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                Vk = ReadUInt16(el, "vk"),
                ScanCode = ReadUInt16(el, "scanCode"),
                IsExtendedKey = ReadBoolean(el, "isExtendedKey"),
                IsAltDown = ReadBoolean(el, "isAltDown"),
                IsInjected = ReadBoolean(el, "isInjected"),
                RepeatCount = el.TryGetProperty("repeatCount", out var rc) ? rc.GetInt32() : 1
            }, waitUntil),
            "keyUp" => (new KeyUpRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                Vk = ReadUInt16(el, "vk"),
                ScanCode = ReadUInt16(el, "scanCode"),
                IsExtendedKey = ReadBoolean(el, "isExtendedKey"),
                IsAltDown = ReadBoolean(el, "isAltDown"),
                IsInjected = ReadBoolean(el, "isInjected")
            }, waitUntil),
            "mouseMove" => (new MouseMoveRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                ScreenX = ReadInt32(el, "screenX"),
                ScreenY = ReadInt32(el, "screenY")
            }, waitUntil),
            "mouseButtonDown" => (new MouseButtonDownRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                Button = (MouseButtonKind)ReadInt32(el, "button"),
                ScreenX = ReadInt32(el, "screenX"),
                ScreenY = ReadInt32(el, "screenY")
            }, waitUntil),
            "mouseButtonUp" => (new MouseButtonUpRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                Button = (MouseButtonKind)ReadInt32(el, "button"),
                ScreenX = ReadInt32(el, "screenX"),
                ScreenY = ReadInt32(el, "screenY")
            }, waitUntil),
            "mouseWheel" => (new MouseWheelRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                ScreenX = ReadInt32(el, "screenX"),
                ScreenY = ReadInt32(el, "screenY"),
                WheelDelta = ReadInt32(el, "wheelDelta"),
                IsHorizontal = ReadBoolean(el, "isHorizontal")
            }, waitUntil),
            "focusChanged" => (new FocusChangedRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                Hwnd = el.TryGetProperty("hwnd", out var hwndEl) && hwndEl.ValueKind == JsonValueKind.Number
                    ? hwndEl.GetUInt64()
                    : null,
                WindowTitle = ReadString(el, "windowTitle"),
                ProcessName = ReadString(el, "processName"),
                ProcessId = el.TryGetProperty("processId", out var pid) && pid.ValueKind == JsonValueKind.Number
                    ? pid.GetUInt32()
                    : null,
                ReferenceClientWidth = ReadNullableInt32(el, "referenceClientWidth"),
                ReferenceClientHeight = ReadNullableInt32(el, "referenceClientHeight"),
                ReferenceClientWidthTolerance = ReadInt32OrDefault(el, "referenceClientWidthTolerance"),
                ReferenceClientHeightTolerance = ReadInt32OrDefault(el, "referenceClientHeightTolerance")
            }, waitUntil),
            "syntheticWait" => (new SyntheticWaitRecordedEvent
            {
                DelayBefore = default,
                Sequence = sequence,
                AdditionalDelay = ReadTimeSpan(el, "additionalDelay")
            }, waitUntil),
            _ => throw new JsonException($"Unknown event discriminator: {discriminator}")
        };
    }

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";

    private static bool ReadBoolean(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => p.GetBoolean()
        };
    }

    private static int ReadInt32(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetInt32() : 0;

    private static int ReadInt32OrDefault(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

    private static int? ReadNullableInt32(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return null;
        return p.GetInt32();
    }

    private static ushort ReadUInt16(JsonElement el, string name) =>
        (ushort)(el.TryGetProperty(name, out var p) ? p.GetInt32() : 0);

    private static ulong ReadUInt64(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetUInt64() : 0UL;

    private static TimeSpan ReadTimeSpan(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return TimeSpan.Zero;
        if (p.ValueKind == JsonValueKind.String)
            return TimeSpan.Parse(p.GetString()!, CultureInfo.InvariantCulture);
        return p.Deserialize<TimeSpan>();
    }
}

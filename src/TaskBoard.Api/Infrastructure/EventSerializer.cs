using System.Text.Json;
using TaskBoard.Domain.Events;

namespace TaskBoard.Api.Infrastructure;

/// <summary>
/// Serializes events as JSON plus a type name. Types are resolved from an explicit allow-list
/// (the domain events), never from a type name found in stored data.
/// </summary>
public static class EventSerializer
{
    private static readonly Dictionary<string, Type> Types = typeof(TaskCreated).Assembly
        .GetTypes()
        .Where(t => t.Namespace == typeof(TaskCreated).Namespace && t.IsClass)
        .ToDictionary(t => t.Name);

    public static (string Type, string Json) Serialize(object @event) =>
        (@event.GetType().Name, JsonSerializer.Serialize(@event, @event.GetType()));

    public static object Deserialize(string type, string json) =>
        Types.TryGetValue(type, out var clrType)
            ? JsonSerializer.Deserialize(json, clrType)!
            : throw new InvalidOperationException($"Unknown event type '{type}'.");
}

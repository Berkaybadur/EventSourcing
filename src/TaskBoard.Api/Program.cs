using System.Text.Json.Serialization;
using MongoDB.Driver;
using Scalar.AspNetCore;
using TaskBoard.Api.Infrastructure;
using TaskBoard.Api.ReadModel;
using TaskBoard.Domain;
using TaskBoard.Domain.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

if (builder.Configuration["EventStore:Provider"] == "InMemory")
{
    builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();
}
else
{
    builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(builder.Configuration.GetConnectionString("MongoDb")));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>()
        .GetDatabase(builder.Configuration["EventStore:Database"] ?? "taskboard"));
    builder.Services.AddSingleton<IEventStore, MongoEventStore>();
}

builder.Services.AddSingleton<TaskReadModel>();
builder.Services.AddSingleton<IProjection>(sp => sp.GetRequiredService<TaskReadModel>());
builder.Services.AddSingleton<AggregateRepository>();
builder.Services.AddHostedService<ProjectionRebuilder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // API reference UI at /scalar
}

// Map domain / concurrency failures to HTTP problem responses.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception e) when (e is DomainException or ConcurrencyException or ArgumentException)
    {
        var (status, title) = e switch
        {
            TaskNotFoundException => (StatusCodes.Status404NotFound, "Task not found"),
            ConcurrencyException or TaskAlreadyCreatedException or TaskCompletedException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Invalid request")
        };
        await Results.Problem(title: title, detail: e.Message, statusCode: status).ExecuteAsync(context);
    }
});

// ---- Commands (write side): load aggregate -> execute -> append events ----
var tasks = app.MapGroup("/tasks");

tasks.MapPost("/", async (CreateTaskRequest request, AggregateRepository repository) =>
{
    var id = Guid.NewGuid();
    var task = new TaskItem();
    task.Create(id, request.Title, request.CreatedBy);
    await repository.SaveAsync(task);
    return Results.Created($"/tasks/{id}", new { id });
});

tasks.MapPost("/{id:guid}/assign", (Guid id, AssignTaskRequest request, AggregateRepository repository) =>
    Execute(id, repository, task => task.Assign(request.AssignedTo, request.AssignedBy)));

tasks.MapPost("/{id:guid}/move", (Guid id, MoveTaskRequest request, AggregateRepository repository) =>
    Execute(id, repository, task => task.Move(request.Section, request.MovedBy)));

tasks.MapPost("/{id:guid}/complete", (Guid id, CompleteTaskRequest request, AggregateRepository repository) =>
    Execute(id, repository, task => task.Complete(request.CompletedBy)));

// ---- Queries (read side): served from the projection ----
tasks.MapGet("/", (TaskReadModel readModel) => readModel.List());

tasks.MapGet("/{id:guid}", (Guid id, TaskReadModel readModel) =>
    readModel.Get(id) is { } task ? Results.Ok(task) : Results.NotFound());

// The full audit trail straight from the event stream.
tasks.MapGet("/{id:guid}/history", async (Guid id, IEventStore store) =>
{
    var history = await store.ReadStreamAsync(AggregateRepository.StreamName(typeof(TaskItem), id));
    return history.Count == 0
        ? Results.NotFound()
        : Results.Ok(history.Select(e => new { e.Version, Type = e.Data.GetType().Name, e.Data, e.Timestamp }));
});

app.Run();

static async Task<IResult> Execute(Guid id, AggregateRepository repository, Action<TaskItem> command)
{
    var task = await repository.LoadAsync<TaskItem>(id) ?? throw new TaskNotFoundException();
    command(task);
    await repository.SaveAsync(task);
    return Results.NoContent();
}

public record CreateTaskRequest(string Title, string CreatedBy);

public record AssignTaskRequest(string AssignedTo, string AssignedBy);

public record MoveTaskRequest(BoardSection Section, string MovedBy);

public record CompleteTaskRequest(string CompletedBy);

public partial class Program;

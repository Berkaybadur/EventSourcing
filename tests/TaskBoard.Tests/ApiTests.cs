using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskBoard.Tests;

[TestClass]
public class ApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("EventStore:Provider", "InMemory"));
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> CreateTaskAsync(string title = "Write docs")
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { title, createdBy = "ann" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [TestMethod]
    public async Task Task_lifecycle_is_reflected_in_read_model_and_history()
    {
        var id = await CreateTaskAsync();

        Assert.AreEqual(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/tasks/{id}/assign", new { assignedTo = "bob", assignedBy = "ann" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/tasks/{id}/move", new { section = "InProgress", movedBy = "bob" })).StatusCode);

        var view = await _client.GetFromJsonAsync<JsonElement>($"/tasks/{id}");
        Assert.AreEqual("bob", view.GetProperty("assignedTo").GetString());
        Assert.AreEqual("InProgress", view.GetProperty("section").GetString());

        Assert.AreEqual(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/tasks/{id}/complete", new { completedBy = "bob" })).StatusCode);

        var history = await _client.GetFromJsonAsync<JsonElement>($"/tasks/{id}/history");
        CollectionAssert.AreEqual(
            new[] { "TaskCreated", "TaskAssigned", "TaskMoved", "TaskCompleted" },
            history.EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToArray());
    }

    [TestMethod]
    public async Task Changing_a_completed_task_returns_conflict()
    {
        var id = await CreateTaskAsync();
        await _client.PostAsJsonAsync($"/tasks/{id}/complete", new { completedBy = "ann" });

        var response = await _client.PostAsJsonAsync($"/tasks/{id}/assign", new { assignedTo = "bob", assignedBy = "ann" });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [TestMethod]
    public async Task Unknown_task_returns_not_found()
    {
        var id = Guid.NewGuid();

        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.GetAsync($"/tasks/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/tasks/{id}/complete", new { completedBy = "ann" })).StatusCode);
    }

    [TestMethod]
    public async Task Blank_title_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { title = " ", createdBy = "ann" });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task List_returns_created_tasks()
    {
        await CreateTaskAsync("Alpha");
        await CreateTaskAsync("Beta");

        var list = await _client.GetFromJsonAsync<JsonElement>("/tasks");

        CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, list.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToArray());
    }
}

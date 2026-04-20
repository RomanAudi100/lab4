using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var notes = new ConcurrentDictionary<int, Note>();
var nextNoteId = 0;

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    time = DateTimeOffset.UtcNow
}))
.WithName("Health");

app.MapGet("/version", (IConfiguration configuration) => Results.Ok(new
{
    name = configuration["App:Name"],
    version = configuration["App:Version"]
}))
.WithName("Version");

app.MapGet("/db/ping", async (IConfiguration configuration) =>
{
    var connectionString = configuration.GetConnectionString("Mssql");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Json(new
        {
            status = "error",
            message = "Connection string 'Mssql' is not configured."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        return Results.Ok(new { status = "ok" });
    }
    catch (Exception exception)
    {
        return Results.Json(new
        {
            status = "error",
            message = exception.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("DatabasePing");

app.MapPost("/api/notes", (CreateNoteRequest request) =>
{
    var errors = ValidateCreateNoteRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var note = new Note(
        Interlocked.Increment(ref nextNoteId),
        request.Title!.Trim(),
        request.Text!.Trim(),
        DateTimeOffset.UtcNow);

    notes[note.Id] = note;

    return Results.Created($"/api/notes/{note.Id}", note);
})
.WithName("CreateNote");

app.MapGet("/api/notes", () => Results.Ok(notes.Values.OrderBy(note => note.Id)))
.WithName("GetNotes");

app.MapGet("/api/notes/{id:int}", (int id) =>
{
    return notes.TryGetValue(id, out var note)
        ? Results.Ok(note)
        : Results.NotFound();
})
.WithName("GetNoteById");

app.MapDelete("/api/notes/{id:int}", (int id) =>
{
    return notes.TryRemove(id, out _)
        ? Results.NoContent()
        : Results.NotFound();
})
.WithName("DeleteNote");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

static Dictionary<string, string[]> ValidateCreateNoteRequest(CreateNoteRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Title))
    {
        errors[nameof(request.Title)] = ["Title is required."];
    }
    else if (request.Title.Length > 100)
    {
        errors[nameof(request.Title)] = ["Title must be 100 characters or fewer."];
    }

    if (string.IsNullOrWhiteSpace(request.Text))
    {
        errors[nameof(request.Text)] = ["Text is required."];
    }

    return errors;
}

record CreateNoteRequest(string? Title, string? Text);

record Note(int Id, string Title, string Text, DateTimeOffset CreatedAt);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

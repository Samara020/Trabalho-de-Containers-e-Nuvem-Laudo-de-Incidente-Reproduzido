using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dados = new ConcurrentDictionary<string, byte[]>();
long gravacoes = 0;
long leituras = 0;
long transicoes = 0;
var lockStats = new object();

app.MapPost("/item/{id}", async (string id, HttpRequest req) =>
{
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    await Task.Delay(2);
    dados[id] = ms.ToArray();
    lock (lockStats)
    {
        gravacoes++;
        transicoes += 2;
    }
    return Results.Text("ok");
});

app.MapGet("/item/{id}", async (string id) =>
{
    await Task.Delay(2);
    dados.TryRemove(id, out var valor);
    lock (lockStats)
    {
        leituras++;
        transicoes++;
    }
    return Results.Bytes(valor ?? Array.Empty<byte>());
});

app.MapPost("/limpar", () =>
{
    dados.Clear();
    lock (lockStats)
    {
        gravacoes = 0;
        leituras = 0;
        transicoes = 0;
    }
    return Results.Ok();
});

app.MapGet("/estatisticas", () =>
{
    lock (lockStats)
    {
        return Results.Json(new { gravacoes, leituras, transicoes });
    }
});

app.Run();
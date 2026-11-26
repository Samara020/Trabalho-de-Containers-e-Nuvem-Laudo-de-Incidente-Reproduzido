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

app.MapGet("/health", () =>
{
    return Results.Ok("ok");
});

app.MapPost("/lote", async (HttpRequest req) =>
{
    var json = await new StreamReader(req.Body).ReadToEndAsync();
    var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    lock (lockStats)
    {
        gravacoes += 1;
        transicoes += 2;
    }
    foreach (var el in root.EnumerateArray())
    {
        if (el.TryGetProperty("id", out var idProp) && el.TryGetProperty("dados", out var dadosProp))
        {
            var id = idProp.GetString();
            var base64 = dadosProp.GetString();
            if (id != null && base64 != null)
            {
                dados[id] = Convert.FromBase64String(base64);
            }
        }
    }
    await Task.Delay(2);
    return Results.Ok(new { processados = root.GetArrayLength() });
});

app.MapGet("/lote/{id}", async (string id) =>
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

app.MapPost("/lote_ler", async (List<string> ids) =>
{
    var result = new List<string>();
    lock (lockStats)
    {
        foreach (var id in ids)
        {
            if (dados.TryGetValue(id, out var valor))
            {
                result.Add(Convert.ToBase64String(valor));
                dados.TryRemove(id, out _);
            }
        }
        leituras += 1;
        transicoes += 1;
    }
    await Task.Delay(2);
    return Results.Json(new { itens = result });
});

app.Run();
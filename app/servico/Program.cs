using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var modo = Environment.GetEnvironmentVariable("MODO") ?? "dividido";
var mitigacao = Environment.GetEnvironmentVariable("MITIGACAO") ?? "nao";
var maxSimultaneos = int.TryParse(Environment.GetEnvironmentVariable("MAX_SIMULTANEOS"), out var msMax) ? msMax : 4;
var semaforo = new SemaphoreSlim(maxSimultaneos, maxSimultaneos);
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

string AnalisarItem(byte[] conteudo)
{
    var resumo = conteudo;
    for (int i = 0; i < 150; i++)
    {
        resumo = SHA256.HashData(resumo);
    }
    return Convert.ToHexString(resumo)[..12].ToLowerInvariant();
}

async Task<string> ProcessarUmAsync(string id, byte[] conteudo)
{
    if (string.Equals(modo, "dividido", StringComparison.OrdinalIgnoreCase))
    {
        using var content = new ByteArrayContent(conteudo);
        var respPost = await http.PostAsync($"http://armazenamento:8001/item/{id}", content);
        respPost.EnsureSuccessStatusCode();
        var lido = await http.GetByteArrayAsync($"http://armazenamento:8001/item/{id}");
        return AnalisarItem(lido);
    }
    return AnalisarItem(conteudo);
}

app.MapPost("/analisar", async (HttpRequest req) =>
{
    var id = req.Headers["X-Item-Id"].FirstOrDefault() ?? "0";
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    var bytes = ms.ToArray();

    string resultado;
    if (string.Equals(mitigacao, "sim", StringComparison.OrdinalIgnoreCase))
    {
        await semaforo.WaitAsync();
        try { resultado = await ProcessarUmAsync(id, bytes); }
        finally { semaforo.Release(); }
    }
    else
    {
        resultado = await ProcessarUmAsync(id, bytes);
    }

    return Results.Text(resultado);
});

app.MapPost("/analisar_lote", async (HttpRequest req) =>
{
    var itens = await JsonSerializer.DeserializeAsync<List<ItemLote>>(req.Body) ?? new List<ItemLote>();
    var processados = 0;

    if (string.Equals(mitigacao, "sim", StringComparison.OrdinalIgnoreCase))
    {
        await semaforo.WaitAsync();
        try
        {
            foreach (var item in itens)
            {
                await ProcessarUmAsync(item.id, Convert.FromBase64String(item.dados));
                processados++;
            }
        }
        finally { semaforo.Release(); }
    }
    else
    {
        foreach (var item in itens)
        {
            await ProcessarUmAsync(item.id, Convert.FromBase64String(item.dados));
            processados++;
        }
    }

    return Results.Json(new { processados });
});

app.Run();

record ItemLote(string id, string dados);
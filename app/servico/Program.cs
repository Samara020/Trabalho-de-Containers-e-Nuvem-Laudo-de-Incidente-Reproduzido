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

app.MapGet("/health", () =>
{
    return Results.Ok("ok");
});

app.MapGet("/configuracao", () => Results.Json(new { modo, mitigacao, maxSimultaneos }));

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
    var itensJson = await new StreamReader(req.Body).ReadToEndAsync();
    var doc = JsonDocument.Parse(itensJson);
    var root = doc.RootElement;
    var processados = 0;

    if (string.Equals(mitigacao, "sim", StringComparison.OrdinalIgnoreCase) && string.Equals(modo, "dividido", StringComparison.OrdinalIgnoreCase))
    {
        await semaforo.WaitAsync();
        try
        {
            // Enviar lote inteiro em uma única chamada POST /lote
            var loteContent = new StringContent(itensJson, System.Text.Encoding.UTF8, "application/json");
            var respLote = await http.PostAsync($"http://armazenamento:8001/lote", loteContent);
            respLote.EnsureSuccessStatusCode();

            // Buscar todos os resultados em uma única chamada POST /lote_ler
            var idsArray = new List<string>();
            foreach (var el in root.EnumerateArray())
            {
                if (el.TryGetProperty("id", out var idProp))
                {
                    idsArray.Add(idProp.GetString()!);
                }
            }

            var lerContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(idsArray), System.Text.Encoding.UTF8, "application/json");
            var respLer = await http.PostAsync($"http://armazenamento:8001/lote_ler", lerContent);
            respLer.EnsureSuccessStatusCode();
            var lerJson = await respLer.Content.ReadAsStringAsync();
            using var lerDoc = JsonDocument.Parse(lerJson);
            if (lerDoc.RootElement.TryGetProperty("itens", out var itensElem))
            {
                foreach (var item in itensElem.EnumerateArray())
                {
                    var b64 = item.GetString();
                    if (b64 != null)
                    {
                        _ = AnalisarItem(Convert.FromBase64String(b64));
                        processados++;
                    }
                }
            }
        }
        finally { semaforo.Release(); }
    }
    else
    {
        foreach (var el in root.EnumerateArray())
        {
            if (el.TryGetProperty("id", out var idProp) && el.TryGetProperty("dados", out var dadosProp))
            {
                var id = idProp.GetString();
                var base64 = dadosProp.GetString();
                if (id != null && base64 != null)
                {
                    await ProcessarUmAsync(id, Convert.FromBase64String(base64));
                    processados++;
                }
            }
        }
    }

    return Results.Json(new { processados });
});

app.Run();

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

int totalItens = int.TryParse(Environment.GetEnvironmentVariable("TOTAL_ITENS"), out var t) ? t : 1000;
int concorrencia = int.TryParse(Environment.GetEnvironmentVariable("CONCORRENCIA"), out var c) ? c : 16;
string mitigacao = Environment.GetEnvironmentVariable("MITIGACAO") ?? "nao";
int tamanhoLote = int.TryParse(Environment.GetEnvironmentVariable("TAMANHO_LOTE"), out var l) ? l : 16;
string modo = Environment.GetEnvironmentVariable("MODO") ?? "dividido";

if (totalItens <= 0 || concorrencia <= 0 || tamanhoLote <= 0)
    throw new ArgumentException("TOTAL_ITENS, CONCORRENCIA e TAMANHO_LOTE devem ser positivos.");
if (mitigacao != "sim" && mitigacao != "nao")
    throw new ArgumentException("MITIGACAO deve ser sim ou nao.");
if (modo != "dividido" && modo != "unico")
    throw new ArgumentException("MODO deve ser dividido ou unico.");

var dadosBrutos = Encoding.ASCII.GetBytes(new string('A', 4096));
var dadosBase64 = Convert.ToBase64String(dadosBrutos);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
using var configuracaoResp = await http.GetAsync("http://servico:8000/configuracao");
configuracaoResp.EnsureSuccessStatusCode();
using var configuracaoDoc = JsonDocument.Parse(await configuracaoResp.Content.ReadAsStringAsync());
var configuracao = configuracaoDoc.RootElement;
string? modoServidor = configuracao.GetProperty("modo").GetString();
string? mitigacaoServidor = configuracao.GetProperty("mitigacao").GetString();
int maxSimultaneosServidor = configuracao.GetProperty("maxSimultaneos").GetInt32();
Console.WriteLine($"CONFIGURACAO_SERVIDOR: MODO={modoServidor} | MITIGACAO={mitigacaoServidor} | MAX_SIMULTANEOS={maxSimultaneosServidor}");
if (!string.Equals(modo, modoServidor, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(mitigacao, mitigacaoServidor, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Cenário divergente: gerador MODO={modo}, MITIGACAO={mitigacao}; servidor MODO={modoServidor}, MITIGACAO={mitigacaoServidor}. Recrie servico com o ambiente correto antes de medir.");

using var limparResp = await http.PostAsync("http://armazenamento:8001/limpar", null);
limparResp.EnsureSuccessStatusCode();

var latencias = new List<double>();
var latenciasLote = new List<double>();
var lockLat = new object();
int sucessos = 0;
int falhas = 0;
var erros = new List<string>();

var swGeral = Stopwatch.StartNew();

async Task EnviarUmAsync(int indice)
{
    var sw = Stopwatch.StartNew();
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "http://servico:8000/analisar");
        req.Headers.Add("X-Item-Id", indice.ToString());
        req.Content = new ByteArrayContent(dadosBrutos);
        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        sw.Stop();
        lock (lockLat) { latencias.Add(sw.Elapsed.TotalMilliseconds); sucessos++; }
    }
    catch (Exception ex)
    {
        sw.Stop();
        lock (lockLat)
        {
            latencias.Add(sw.Elapsed.TotalMilliseconds);
            falhas++;
            if (erros.Count < 10) erros.Add($"Item {indice}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

async Task EnviarLoteAsync(List<ItemLote> lote)
{
    var sw = Stopwatch.StartNew();
    try
    {
        using var resp = await http.PostAsJsonAsync("http://servico:8000/analisar_lote", lote);
        resp.EnsureSuccessStatusCode();
        using var respostaLote = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        int processados = respostaLote.RootElement.GetProperty("processados").GetInt32();
        if (processados != lote.Count)
            throw new InvalidOperationException($"Lote incompleto: esperados {lote.Count}, processados {processados}.");
        sw.Stop();
        double msLote = sw.Elapsed.TotalMilliseconds;
        double msPorItem = lote.Count > 0 ? msLote / lote.Count : msLote;
        // Validate that the server processed all items
        var respBody = await resp.Content.ReadAsStringAsync();
        using var respDoc = JsonDocument.Parse(respBody);
        var respProcessados = respDoc.RootElement.GetProperty("processados").GetInt32();
        lock (lockLat)
        {
            latenciasLote.Add(msLote);
            for (int k = 0; k < lote.Count; k++) latencias.Add(msPorItem);
            // Only count as sucesso the items actually processed by the server
            sucessos += Math.Min(respProcessados, lote.Count);
            // Log discrepancy if different
            if (respProcessados != lote.Count)
            {
                // Could add warning here, but for simplicity just count the minimum
            }
            falhas += Math.Max(0, lote.Count - respProcessados);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        double msLote = sw.Elapsed.TotalMilliseconds;
        double msPorItem = lote.Count > 0 ? msLote / lote.Count : msLote;
        lock (lockLat)
        {
            latenciasLote.Add(msLote);
            for (int k = 0; k < lote.Count; k++) latencias.Add(msPorItem);
            falhas += lote.Count;
            if (erros.Count < 10) erros.Add($"Lote de {lote.Count} itens: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

using var sem = new SemaphoreSlim(concorrencia, concorrencia);
var tarefas = new List<Task>();

if (string.Equals(mitigacao, "sim", StringComparison.OrdinalIgnoreCase))
{
    for (int i = 0; i < totalItens; i += tamanhoLote)
    {
        var lote = new List<ItemLote>();
        for (int j = i; j < Math.Min(i + tamanhoLote, totalItens); j++)
        {
            lote.Add(new ItemLote(j.ToString(), dadosBase64));
        }
        await sem.WaitAsync();
        tarefas.Add(Task.Run(async () =>
        {
            try { await EnviarLoteAsync(lote); }
            finally { sem.Release(); }
        }));
    }
}
else
{
    for (int i = 0; i < totalItens; i++)
    {
        int idx = i;
        await sem.WaitAsync();
        tarefas.Add(Task.Run(async () =>
        {
            try { await EnviarUmAsync(idx); }
            finally { sem.Release(); }
        }));
    }
}

await Task.WhenAll(tarefas);
swGeral.Stop();

var estJson = await http.GetStringAsync("http://armazenamento:8001/estatisticas");
using var doc = JsonDocument.Parse(estJson);
var root = doc.RootElement;
long gravacoes = root.GetProperty("gravacoes").GetInt64();
long leituras = root.GetProperty("leituras").GetInt64();
long transicoes = root.GetProperty("transicoes").GetInt64();

latencias.Sort();
double p95 = latencias.Count > 0 ? latencias[(int)(0.95 * (latencias.Count - 1))] : 0.0;
latenciasLote.Sort();
double p95Lote = latenciasLote.Count > 0 ? latenciasLote[(int)(0.95 * (latenciasLote.Count - 1))] : 0.0;

double tempoTotalS = swGeral.Elapsed.TotalSeconds;
double vazao = tempoTotalS > 0 ? sucessos / tempoTotalS : 0.0;
double taxaErro = totalItens > 0 ? (falhas * 100.0) / totalItens : 0.0;

double fator = 10000.0 / Math.Max(sucessos, 1);
double custo10k =
    (transicoes * fator / 1000.0) * 0.025 +
    ((gravacoes + leituras) * fator / 1000.0) * 0.005 +
    (tempoTotalS * fator) * 0.002;

Console.WriteLine("=== RESULTADO DO TESTE ===");
Console.WriteLine($"TOTAL_ITENS={totalItens} | CONCORRENCIA={concorrencia} | MITIGACAO={mitigacao}");
Console.WriteLine($"MODO={modoServidor} | TAMANHO_LOTE={tamanhoLote} | MAX_SIMULTANEOS={maxSimultaneosServidor}");
Console.WriteLine($"TEMPO_TOTAL_S={tempoTotalS:F4}");
Console.WriteLine($"SUCESSOS={sucessos} | FALHAS={falhas} | TAXA_ERRO_PCT={taxaErro:F2}%");
Console.WriteLine($"VAZAO_ITENS_S={vazao:F2}");
Console.WriteLine($"LATENCIA_P95_MS={p95:F2}");
if (latenciasLote.Count > 0)
{
    Console.WriteLine($"LATENCIA_LOTE_P95_MS={p95Lote:F2}");
}
Console.WriteLine($"GRAVACOES={gravacoes} | LEITURAS={leituras} | TRANSICOES={transicoes}");
Console.WriteLine($"CUSTO_ESTIMADO_10K={custo10k:F4}");
foreach (var erro in erros) Console.WriteLine($"ERRO: {erro}");
if (falhas > 0) Environment.ExitCode = 1;

record ItemLote(string id, string dados);

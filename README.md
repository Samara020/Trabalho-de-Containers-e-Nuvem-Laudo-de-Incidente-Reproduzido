# Experimento SRE — Caso Prime Vídeo (2023)

Miniatura em C#/.NET 8 com os containers `armazenamento`, `servico` e `gerador` na rede Docker `rede_teste`. Compara processamento dividido com processamento em memória e avalia uma mitigação alternativa por lotes.

Repositório: https://github.com/Samara020/Trabalho-de-Containers-e-Nuvem-Laudo-de-Incidente-Reproduzido

Artigo: Marcin Kolny, *Scaling up the Prime Video audio/video monitoring service and reducing costs by 90%*, Prime Video Tech, 22/03/2023. [Publicação original](https://www.primevideotech.com/video-streaming/scaling-up-the-prime-video-audio-video-monitoring-service-and-reducing-costs-by-90) e [cópia preservada por terceiro](https://www.wudsn.com/productions/www/site/news/2023/2023-05-08-microservices-01.pdf).

## Arquivos da entrega

- `docker-compose.yml`: serviços, rede, ambiente e espera por saúde.
- `app/armazenamento/`: armazenamento em RAM e contagem de operações.
- `app/servico/`: análise sintética, modos dividido/único e lotes.
- `carga/`: gerador, validação do cenário efetivo e métricas.
- `logs/`: saídas brutas dos experimentos.
- [docs/laudo.pdf](docs/laudo.pdf): laudo reorganizado em seis páginas.
- `IA.md`: prompts, decisões, erros e verificações.

## Resultados de três rodadas por cenário

Foram executadas 12 medições locais em Linux/Docker: três de cada configuração, em ciclos dividido → único → antes → depois. Os dois serviços foram recriados antes de cada execução, sem aquecimento separado. Todos os itens foram processados, com zero falhas nas 12 medições.

| Cenário | Itens por rodada | Concorrência | Vazão média (itens/s) | Índice médio de custo / 10 mil | Logs das três rodadas |
|---|---:|---:|---:|---:|---|
| Dividido | 1000 | 16 | 1.342,44 | 0,8649 | [08-dividido-r1.log](logs/08-dividido-r1.log), [12-dividido-r2.log](logs/12-dividido-r2.log), [16-dividido-r3.log](logs/16-dividido-r3.log) |
| Em memória | 1000 | 16 | 5.435,85 | 0,0040 | [09-unico-r1.log](logs/09-unico-r1.log), [13-unico-r2.log](logs/13-unico-r2.log), [17-unico-r3.log](logs/17-unico-r3.log) |
| Antes da mitigação | 1500 | 64 | 3.057,98 | 0,8566 | [10-antes-r1.log](logs/10-antes-r1.log), [14-antes-r2.log](logs/14-antes-r2.log), [18-antes-r3.log](logs/18-antes-r3.log) |
| Depois da mitigação | 1500 | 64 | 3.141,49 | 0,0600 | [11-depois-r1.log](logs/11-depois-r1.log), [15-depois-r2.log](logs/15-depois-r2.log), [19-depois-r3.log](logs/19-depois-r3.log) |

- Em memória versus dividido: vazão média 304,92% maior e índice médio 99,53% menor.
- Depois versus antes da mitigação: índice médio 93,00% menor e vazão média 2,73% maior. A vazão piorou na segunda e na terceira rodadas; não há ganho consistente de desempenho.
- Médias aritméticas dos valores logados. Os percentuais foram calculados antes do arredondamento das médias exibidas. Três rodadas não permitem afirmar significância estatística ou prever desempenho em produção.

## Método e limitações

- `CUSTO_ESTIMADO_10K` é um índice simulado, sem unidade monetária; não é cobrança real da AWS. Com `f = 10000 / max(sucessos, 1)`, a fórmula é `(transicoes * f / 1000) * 0.025 + ((gravacoes + leituras) * f / 1000) * 0.005 + (tempoTotalS * f) * 0.002`. Os coeficientes não foram alterados.
- Cada chamada de gravação conta uma gravação e duas transições; cada chamada de leitura conta uma leitura e uma transição. O mesmo modelo se aplica a chamadas individuais e de lote, com atraso sintético de 2 ms por chamada.
- Para 1.500 itens em lotes de 16: 94 gravações, 94 leituras e 282 transições. O modelo não cobra proporcionalmente aos bytes; essa economia não comprova desconto equivalente em S3 ou Step Functions reais.
- No modo por lote, `LATENCIA_P95_MS` é amortizada (`tempo do lote / quantidade de itens`). Ela não equivale ao p95 individual e não pode ser comparada diretamente com ele. `LATENCIA_LOTE_P95_MS` mede a requisição completa do lote.
- Os dois Dockerfiles e o Compose agora usam `GET /health`. Cada medição conferiu que chamadas repetidas às sondas mantinham gravações, leituras e transições em zero antes da carga.
- O gerador consulta `GET /configuracao`, rejeita cenário divergente, exige limpeza HTTP bem-sucedida e confere a quantidade processada por lote.
- **Validado em Linux/Docker. Sem execução nativa no PowerShell 5.1 e sem teste de carga no Projeto Origem.** O Origem foi inspecionado somente por leitura.

## Como executar no PowerShell 5.1

Pré-requisitos: Docker Desktop com containers Linux e Compose v2 com suporte a `--wait`. Execute na raiz do repositório. O SDK .NET é usado dentro das imagens, sem instalação obrigatória no host. Os comandos abaixo foram revisados para PowerShell 5.1, mas não foram executados nativamente em Windows nesta rodada.

O procedimento executa três rodadas de cada um dos quatro cenários. Cada reexecução cria um arquivo com timestamp, sem sobrescrever os logs entregues.

```powershell
docker compose build armazenamento servico gerador
if ($LASTEXITCODE -ne 0) { throw "Falha no build" }

$cenarios = @(
    @{ Nome="dividido"; Modo="dividido"; Mitigacao="nao"; Itens=1000; Concorrencia=16 }
    @{ Nome="unico"; Modo="unico"; Mitigacao="nao"; Itens=1000; Concorrencia=16 }
    @{ Nome="antes"; Modo="dividido"; Mitigacao="nao"; Itens=1500; Concorrencia=64 }
    @{ Nome="depois"; Modo="dividido"; Mitigacao="sim"; Itens=1500; Concorrencia=64 }
)
$env:TAMANHO_LOTE = "16"
$env:MAX_SIMULTANEOS = "16"

foreach ($rodada in 1..3) {
    foreach ($cenario in $cenarios) {
        $env:MODO = $cenario.Modo
        $env:MITIGACAO = $cenario.Mitigacao
        $env:TOTAL_ITENS = [string]$cenario.Itens
        $env:CONCORRENCIA = [string]$cenario.Concorrencia

        docker compose up -d --force-recreate --wait armazenamento servico
        if ($LASTEXITCODE -ne 0) { throw "Servicos indisponiveis" }

        $nomeLog = "reexecucao-{0}-r{1}-{2}.log" -f $cenario.Nome, $rodada, (Get-Date -Format yyyyMMdd-HHmmss-fff)
        $arquivoLog = Join-Path "logs" $nomeLog
        if (Test-Path $arquivoLog) { throw "Arquivo de log ja existe" }
        docker compose run --rm --no-deps -e MODO=$env:MODO -e MITIGACAO=$env:MITIGACAO -e TOTAL_ITENS=$env:TOTAL_ITENS -e CONCORRENCIA=$env:CONCORRENCIA -e TAMANHO_LOTE=$env:TAMANHO_LOTE gerador 2>&1 | Tee-Object -FilePath $arquivoLog
        if ($LASTEXITCODE -ne 0) { throw "Carga falhou; consulte o log" }
    }
}
docker compose down
```

As variáveis do servidor precisam ser definidas antes de recriar `servico`. Alterar apenas o ambiente do gerador não muda o servidor. `--no-deps` evita recriar dependências com defaults de outro ambiente durante a carga.

## Execução equivalente em Bash

```bash
set -euo pipefail
docker compose build armazenamento servico gerador
export TAMANHO_LOTE=16 MAX_SIMULTANEOS=16
for rodada in 1 2 3; do
  for nome in dividido unico antes depois; do
    case "$nome" in
      dividido) export MODO=dividido MITIGACAO=nao TOTAL_ITENS=1000 CONCORRENCIA=16 ;;
      unico) export MODO=unico MITIGACAO=nao TOTAL_ITENS=1000 CONCORRENCIA=16 ;;
      antes) export MODO=dividido MITIGACAO=nao TOTAL_ITENS=1500 CONCORRENCIA=64 ;;
      depois) export MODO=dividido MITIGACAO=sim TOTAL_ITENS=1500 CONCORRENCIA=64 ;;
    esac
    docker compose up -d --force-recreate --wait armazenamento servico
    arquivoLog="logs/reexecucao-${nome}-r${rodada}-$(date +%Y%m%d-%H%M%S-%N).log"
    test ! -e "$arquivoLog"
    docker compose run --rm --no-deps -e MODO -e MITIGACAO -e TOTAL_ITENS -e CONCORRENCIA -e TAMANHO_LOTE gerador 2>&1 | tee "$arquivoLog"
  done
done
docker compose down
```

## Procedência dos logs

- **01–04:** execuções históricas locais da equipe em UTF-16, preservadas byte a byte. `03` e `04` documentam a primeira mitigação, que piorou vazão e custo. Esses dados nunca vieram do artigo original.
- **05:** teste negativo de configuração divergente, aprovado. A exceção/código diferente de zero são esperados: o gerador rejeitou o cenário incorreto antes de medir.
- **06–07:** primeira comparação validada, com uma execução por cenário; mantida como histórico.
- **08–19:** comparação ampliada com três rodadas de cada cenário, usada no laudo revisado. Cada log inclui configuração, sistema, versões, imagens, hashes, comandos, validação das sondas e resultados brutos.

## Equipe

- Allana Silva
- Carlos Henrique
- Leandro Manuel
- Nicolas de Castro
- Samara Mendonça

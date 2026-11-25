# Experimento SRE — Caso Prime Vídeo (2023)

Miniatura em Docker Compose com 3 containers em C# (.NET 8) na rede isolada `rede_teste` (`armazenamento`, `servico` e `gerador`), reproduzindo o mecanismo do artigo *"Scaling up the Prime Video audio/video monitoring service and reducing costs by 90%"* (Marcin Kolny, Prime Video Tech, mar/2023).

## Estrutura do Repositório

- `docker-compose.yml` — Orquestração dos containers `armazenamento`, `servico` e `gerador` na rede `rede_teste`.
- `app/armazenamento/` — Serviço HTTP em C# (.NET 8) que simula armazenamento intermediário e contagem de transições.
- `app/servico/` — Serviço HTTP em C# (.NET 8) que executa o processamento em modo `dividido` ou `unico` (em memória), com opção de mitigação (`MITIGACAO=sim`).
- `carga/` — Gerador de carga em C# (.NET 8) que mede vazão (itens/s), latência p95 (ms), taxa de erro (%) e índice de custo simulado por 10.000 unidades.
- `logs/` — Saídas brutas das execuções citadas no laudo (`01-dividido.log`, `02-unico.log`, `03-antes.log`, `04-depois.log`).
- `IA.md` — Registro de uso de IA e erros corrigidos durante o desenvolvimento.

## Metodologia e limitações das métricas

- **Índice de custo simulado (`CUSTO_ESTIMADO_10K`):** não representa cobrança financeira real de nuvem; é um índice comparativo simulado projetado para 10.000 itens a partir de coeficientes fixos no código (`0.025` a cada 1.000 transições, `0.005` a cada 1.000 operações de gravação/leitura e `0.002` por segundo de duração total).
- **Latência amortizada por item em lotes (`LATENCIA_P95_MS` vs. `LATENCIA_LOTE_P95_MS`):** no modo em lotes (`MITIGACAO=sim`), `LATENCIA_P95_MS` é uma estimativa amortizada por item (`msLote / lote.Count`) calculada a partir do tempo total da requisição do lote, acompanhada de `LATENCIA_LOTE_P95_MS` para a chamada de lote. Isso não equivale à medição individual cronometrada de cada item isolado.
- **Prontidão dos serviços antes da carga:** `Start-Sleep` oferece apenas uma espera simples; para maior confiabilidade antes de rodar o `gerador`, é recomendável aguardar os serviços responderem na rede/containers antes de disparar a carga.

## Como rodar no Windows PowerShell 5.1

### 1. Cenário dividido (`logs\01-dividido.log`)

$env:MODO = "dividido"; $env:MITIGACAO = "nao"; $env:CONCORRENCIA = "16"; $env:TOTAL_ITENS = "1000"
docker compose up -d --build armazenamento servico; docker compose build gerador; Start-Sleep -Seconds 3
docker compose run --rm gerador | Tee-Object -FilePath .\logs\01-dividido.log
docker compose down -v

### 2. Cenário processo único em memória (`logs\02-unico.log`)

$env:MODO = "unico"; $env:MITIGACAO = "nao"; $env:CONCORRENCIA = "16"; $env:TOTAL_ITENS = "1000"
docker compose up -d --build armazenamento servico; docker compose build gerador; Start-Sleep -Seconds 3
docker compose run --rm gerador | Tee-Object -FilePath .\logs\02-unico.log
docker compose down -v

### 3. Antes da mitigação (`logs\03-antes.log`)

$env:MODO = "dividido"; $env:MITIGACAO = "nao"; $env:CONCORRENCIA = "64"; $env:TOTAL_ITENS = "1500"
docker compose up -d --build armazenamento servico; docker compose build gerador; Start-Sleep -Seconds 3
docker compose run --rm gerador | Tee-Object -FilePath .\logs\03-antes.log
docker compose down -v

### 4. Depois da mitigação (`logs\04-depois.log`)

$env:MODO = "dividido"; $env:MITIGACAO = "sim"; $env:CONCORRENCIA = "64"; $env:TOTAL_ITENS = "1500"; $env:TAMANHO_LOTE = "16"; $env:MAX_SIMULTANEOS = "4"
docker compose up -d --build armazenamento servico; docker compose build gerador; Start-Sleep -Seconds 3
docker compose run --rm gerador | Tee-Object -FilePath .\logs\04-depois.log
docker compose down -v

### Conferir os logs

Get-Content .\logs\*.log

## Equipe

- Allana
- Carlos
- Leandro
- Nicolas
- Samara
# Registro de Uso de IA — IA.md

## Prompts principais e decisões da equipe

1. **Análise do artigo do Prime Vídeo (2023):**
   - **Prompt enviado (resumo fiel):** *"Explique o gargalo e a mudança de arquitetura do artigo do Prime Vídeo de 2023 em linguagem simples, com a linha do tempo e o conceito de acoplamento/comunicação."*
   - **Aceito:** gargalo em torno de ~5% da carga esperada por transições no Step Functions e operações no S3, e redução de >90% nos custos ao consolidar em processo único em memória.
   - **Alterado:** adaptação da linguagem para português simples e padronização da grafia **Prime Vídeo**.

2. **Miniatura em C# (.NET 8) e comandos de execução no PowerShell:**
   - **Prompt enviado (resumo fiel):** *"Ajuste os comandos e a estrutura da miniatura em C# (.NET 8) no Docker Compose (`armazenamento`, `servico`, `gerador` na `rede_teste`) e o passo a passo no PowerShell para gerar os logs."*
   - **Aceito:** três serviços na rede `rede_teste`, variáveis de ambiente (`MODO`, `MITIGACAO`, `CONCORRENCIA`, `TOTAL_ITENS`, `TAMANHO_LOTE`, `MAX_SIMULTANEOS`) e saída salva com `Tee-Object`.
   - **Alterado:** separação de `docker compose up -d --build armazenamento servico` e `docker compose build gerador` antes de rodar `docker compose run --rm gerador`.
   - **Descartado:** uso de números fictícios antes de rodar as execuções reais.

3. **Medição de latência em lotes e análise da mitigação:**
   - **Prompt enviado (resumo fiel):** *"Corrija a medição para registrar também a latência por item em `EnviarLoteAsync` e explique o trade-off da mitigação com lotes e limite de concorrência."*
   - **Aceito:** cálculo da latência amortizada por item (`msLote / lote.Count`) em `LATENCIA_P95_MS` junto com `LATENCIA_LOTE_P95_MS`, mantendo a análise honesta do trade-off nos resultados atuais:
     - `01-dividido.log`: `928.30 itens/s`, `LATENCIA_P95_MS=16.77 ms`, custo `$0.8715`
     - `02-unico.log`: `3282.21 itens/s`, `LATENCIA_P95_MS=3.24 ms`, custo `$0.0061`
     - `03-antes.log`: `2172.99 itens/s`, `LATENCIA_P95_MS=27.97 ms`, custo `$0.8592`
     - `04-depois.log`: `429.13 itens/s`, `LATENCIA_P95_MS=147.65 ms` (amortizada por item), `LATENCIA_LOTE_P95_MS=2249.49 ms` (por lote), custo `$0.8966`

## Erros reais de IA encontrados na execução e correções

1. **Sugestão de encadeamento de comandos com `&&` e blocos Markdown para o PowerShell 5.1:**
   - **O que a IA sugeriu:** comandos encadeados com `&&` e blocos com formatação Markdown para rodar/aplicar no terminal.
   - **O que aconteceu ao executar:** no Windows PowerShell 5.1, `&&` falhou por incompatibilidade de sintaxe do shell e linhas com `-`, `##` ou cercas de código coladas direto no prompt geraram erro de parser/cmdlet não reconhecido.
   - **Por que estava errado:** a sugestão assumiu sintaxe de shell Unix (`&&`) e colagem direta no prompt em vez de comandos nativos do PowerShell 5.1 e gravação segura de arquivo.
   - **Correção realizada:** substituição de `&&` por `;` nos comandos do PowerShell 5.1 e gravação de arquivos usando here-string (`@' ... '@`) com `[System.IO.File]::WriteAllText(..., UTF8Encoding($false))`.

2. **Subida inicial com `docker compose up -d --build` incluindo o `gerador` e reconstrução do `gerador`:**
   - **O que a IA sugeriu:** iniciar o ambiente com `docker compose up -d --build` sem restringir explicitamente `armazenamento` e `servico` antes de executar `docker compose run --rm gerador`.
   - **O que aconteceu ao executar:** o serviço `gerador` também era iniciado durante o `up`, gerando execução concorrente/prévia antes da rodada isolada de medição salva em `logs\`, e a imagem do `gerador` precisava de build explícito após alteração em `carga\Program.cs`.
   - **Por que estava errado:** o `gerador` no `docker-compose.yml` não devia subir no `up -d` da infraestrutura base antes da medição controlada.
   - **Correção realizada:** separação de `docker compose up -d --build armazenamento servico` e `docker compose build gerador` antes de `docker compose run --rm gerador`.
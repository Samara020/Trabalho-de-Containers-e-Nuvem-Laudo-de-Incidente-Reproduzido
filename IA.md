# Registro de Uso de IA (`IA.md`)

A equipe utilizou ferramentas de IA como apoio pontual na leitura e síntese do artigo da Prime Video (2023), na organização e revisão de erros do código em C#/.NET 8 e Docker Compose, e na estruturação final do laudo em PDF. Todas as execuções experimentais, a conferência dos logs e as decisões técnicas foram conduzidas e validadas pela equipe.

---

## 1. Prompts Principais da Equipe e Decisões Tomadas

### Prompt 1 — Leitura e análise do artigo do Prime Video (2023)
- **Prompt enviado (resumo fiel):** *"Explique o gargalo e a mudança de arquitetura do artigo do Prime Vídeo de 2023 em linguagem simples, com a linha do tempo e o conceito de acoplamento/comunicação."*
- **O que a equipe aceitou:** síntese do gargalo em torno de ~5% da carga esperada por excesso de transições no AWS Step Functions e operações no Amazon S3, consolidação dos componentes em processo único em memória (ECS/EC2) e relato de redução superior a 90% nos custos, vinculados ao conceito de **acoplamento**.
- **O que a equipe mudou:** conferência direta do texto original de Marcin Kolny (22/03/2023, via cópia preservada), localização exata de cada um dos 6 eventos nas seções reais do artigo (*Introdução*, *Distributed systems overhead*, *From distributed microservices to a monolith application* e *Results and takeaways*) e ajuste para linguagem direta em português.
- **O que a equipe descartou:** afirmações genéricas de que toda a plataforma Prime Video virou monólito, comentários de terceiros fora do artigo e qualquer invenção de horários/datas inexistentes no texto original.

### Prompt 2 — Estruturação da miniatura em C# (.NET 8) e execução via Docker Compose
- **Prompt enviado (resumo fiel):** *"Ajuste os comandos e a estrutura da miniatura em C# (.NET 8) no Docker Compose (`armazenamento`, `servico`, `gerador` na `rede_teste`) e o passo a passo no PowerShell para gerar os logs."*
- **O que a equipe aceitou:** topologia de três containers na rede bridge `rede_teste`, configuração por variáveis de ambiente (`MODO`, `MITIGACAO`, `CONCORRENCIA`, `TOTAL_ITENS`, `TAMANHO_LOTE`, `MAX_SIMULTANEOS`) e captura das saídas brutas em `logs/`.
- **O que a equipe mudou:** separação entre a subida da infraestrutura (`armazenamento` e `servico`), o build do `gerador` e a execução isolada via `docker compose run --rm`, além da adoção de `depends_on` com `condition: service_healthy`.
- **O que a equipe descartou:** qualquer preenchimento prévio de números ou estimativas antes de executar os containers e coletar os logs reais.

### Prompt 3 — Medição de latência em lotes e revisão da mitigação
- **Prompt enviado (resumo fiel):** *"Corrija a medição para registrar também a latência por item em `EnviarLoteAsync` e explique o trade-off da mitigação com lotes e limite de concorrência."*
- **O que a equipe aceitou:** separação explícita entre a latência da requisição completa do lote (`LATENCIA_LOTE_P95_MS`) e a estimativa amortizada por item (`LATENCIA_P95_MS = msLote / lote.Count`), além do registro transparente da primeira tentativa de mitigação em `logs/03-antes.log` e `logs/04-depois.log` (que piorou vazão e custo).
- **O que a equipe mudou:** na revisão posterior da mitigação, consolidou a gravação e a leitura no `armazenamento` para realmente operar por lote (`POST /lote` e `POST /lote_ler`), elevou `MAX_SIMULTANEOS` para `16` nos testes comparativos e adicionou validação de configuração do servidor (`GET /configuracao`) para impedir medições com cenário divergente.
- **O que a equipe descartou:** tratar `CUSTO_ESTIMADO_10K` como valor monetário (`$` ou `R$`) da AWS ou comparar diretamente `LATENCIA_P95_MS` amortizado com p95 individual.

### Prompt 4 — Revisão de código, healthchecks e depuração dos endpoints de lote
- **Prompt enviado (resumo fiel):** *"Revise os serviços `armazenamento`, `servico` e `gerador` para corrigir os erros nos endpoints de lote, restaurar `/limpar` e `/estatisticas`, usar um `/health` sem impacto nos contadores e garantir que os coeficientes da fórmula de custo não sejam alterados."*
- **O que a equipe aceitou:** criação do endpoint sem estado `GET /health` nos dois serviços, preservação da fórmula de `CUSTO_ESTIMADO_10K` em `carga/Program.cs` (linhas 172–176), leitura dos itens retornados por `/lote_ler` com execução de `AnalisarItem` para cada item e validação de integridade da resposta (`processados`).
- **O que a equipe mudou:** ajuste dos comandos do `README.md` para exportar as variáveis antes de recriar `armazenamento` e `servico` (`--force-recreate --wait`) e usar `--no-deps` ao disparar o `gerador`, mantendo intactos os logs históricos `01`–`04`.
- **O que a equipe descartou:** hipóteses incorretas que atribuíam erros HTTP 404 do `gerador` a falha de rede do Docker e qualquer alteração nos pesos da fórmula de custo para inflar o ganho da mitigação.

### Prompt 5 — Organização final do laudo em seis páginas e ampliação para três rodadas
- **Prompt enviado (resumo fiel):** *"Reorganize o laudo para ficar mais legível usando até seis páginas, corrija os Dockerfiles para usar `GET /health`, execute três rodadas por cenário e mantenha explícitas as limitações da miniatura e da validação."*
- **O que a equipe aceitou:** divisão clara do PDF (`docs/laudo.pdf`) em 6 páginas (caso/mecanismo, miniatura, método/limitações, arquitetura dividido vs. em memória, mitigação por lotes + surpresa e parecer sobre o `Projeto Integrador Origem`), correção dos dois Dockerfiles para `GET /health` e execução de 3 rodadas por cenário (`logs/08` a `logs/19`).
- **O que a equipe mudou:** inclusão das médias aritméticas e faixas no `README.md` e no laudo (com percentuais calculados antes do arredondamento), consolidação da tabela de limitações em 4 pontos específicos e inclusão das ressalvas explícitas (execução validada em Linux/Docker, sem execução nativa no PowerShell 5.1 na rodada final e inspeção apenas por leitura no `Projeto Integrador Origem`).
- **O que a equipe descartou:** afirmar ganho consistente de vazão com lotes (já que a vazão piorou nas rodadas 2 e 3) e manter scripts auxiliares temporários de montagem do PDF no repositório.

---

## 2. Erros Reais da IA Encontrados na Execução e Correções Feitas

1. **Encadeamento com `&&` e blocos Markdown no Windows PowerShell 5.1:**
   - **Erro encontrado ao rodar:** a IA sugeriu comandos encadeados com `&&` (sintaxe Unix/PowerShell 7) e trechos com formatação Markdown. No Windows PowerShell 5.1, `&&` gerou erro de sintaxe e linhas de Markdown coladas no terminal falharam como comandos inválidos.
   - **Correção feita:** substituição por comandos compatíveis com PowerShell 5.1 (`;` ou instruções separadas) e gravação segura de arquivos via `[System.IO.File]::WriteAllText` em UTF-8 sem BOM.

2. **Subida com `docker compose up -d --build` disparando o `gerador` antes da medição:**
   - **Erro encontrado ao rodar:** seguindo a sugestão inicial da IA de subir todos os serviços com `docker compose up -d --build`, o container `gerador` executava imediatamente junto com a infraestrutura, antes da rodada controlada salva em `logs/`, e não era reconstruído de forma isolada após ajustes em `carga/Program.cs`.
   - **Correção feita:** separação do procedimento em build das imagens, subida controlada apenas de `armazenamento` e `servico`, e execução isolada do teste com `docker compose run --rm --no-deps ... gerador`.

3. **Sondas de `HEALTHCHECK` inválidas e poluindo métricas:**
   - **Erro encontrado ao rodar:** nas sugestões iniciais de healthcheck para `mcr.microsoft.com/dotnet/aspnet:8.0`, a IA tentou `wget` (ausente na imagem, deixando containers `unhealthy`), depois `/dev/tcp/localhost/8001/estatisticas` (inválido no bash por incluir caminho HTTP) e `POST /analisar` (que executava gravação e leitura reais no `armazenamento` a cada 5 segundos, contaminando `gravacoes`, `leituras` e `transicoes`).
   - **Correção feita:** instalação enxuta de `curl` nos Dockerfiles e implementação de endpoint dedicado `GET /health` em `app/armazenamento/Program.cs` e `app/servico/Program.cs`, validado em `logs/08`–`19` com contadores zerados antes de cada carga.

4. **Troca de `MODO` e `MITIGACAO` apenas no `gerador` sem recriar `servico`:**
   - **Erro encontrado ao rodar:** ao sugerir `docker compose run --rm -e MITIGACAO=sim gerador` sem `--no-deps` e sem recriar `servico` com as novas variáveis, o servidor continuava rodando com `MITIGACAO=nao` (e `MAX_SIMULTANEOS=4`), fazendo o teste continuar registrando `4500` transições item a item.
   - **Correção feita:** criação da rota `GET /configuracao` no `servico`, verificação obrigatória de paridade no início do `carga/Program.cs` (testada negativamente em `logs/05-validacao-cenario-divergente.log`), uso de `docker compose up -d --force-recreate --wait armazenamento servico` antes de cada cenário e `--no-deps` no `gerador`.

5. **Remoção acidental de rotas no `armazenamento` e erro de compilação C# (`CS0019`) no modo em lotes:**
   - **Erro encontrado ao rodar:** durante uma edição sugerida para adicionar `/health` e `/lote`, a IA removeu `/limpar` e `/estatisticas` de `app/armazenamento/Program.cs` (causando HTTP 404 no `gerador`), escreveu `if (lerResult != null)` sobre o struct não anulável `JsonElement` em `app/servico/Program.cs` (erro de compilação `CS0019`) e contou operações por item em vez de por lote.
   - **Correção feita:** restauração de `/limpar` e `/estatisticas`, leitura correta de `/lote_ler` via `JsonDocument.Parse` com decodificação Base64 e chamada real de `AnalisarItem` para cada item, e contabilização consolidada por lote (`1` gravação + `2` transições em `/lote` e `1` leitura + `1` transição em `/lote_ler`).

---

## 3. Conferência e Validação pela Equipe

- **Rastreabilidade dos números:** todos os valores citados em `docs/laudo.pdf` e `README.md` foram extraídos das saídas brutas em `logs/` (`03`–`04` para o registro histórico da primeira tentativa de mitigação e `08`–`19` para as 12 medições finais em 3 rodadas por cenário).
- **Verificação algébrica:** a fórmula de `CUSTO_ESTIMADO_10K` (`carga/Program.cs`, linhas 172–176), as médias das três rodadas e as variações percentuais foram recalculadas e conferidas contra os logs.
- **Limites mantidos explícitos:** os testes finais (`logs/08`–`19`) foram executados em Linux/Docker sem reexecução nativa no PowerShell 5.1; o `Projeto Integrador Origem` citado na Parte D foi analisado somente por leitura de código, sem teste de carga.

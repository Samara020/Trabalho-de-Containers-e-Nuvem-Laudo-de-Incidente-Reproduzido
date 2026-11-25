## Registro de Uso de IA

### 1. Análise do artigo da Prime Video
A IA foi consultada para esclarecer o gargalo da arquitetura original, a mudança para processamento em um único fluxo e a redução de custos apresentada no artigo. A equipe revisou as informações e selecionou os pontos utilizados na reprodução.

### 2. Dúvidas sobre implementação e execução
A IA foi utilizada para esclarecer dúvidas sobre a configuração dos serviços em Docker Compose, as variáveis de ambiente e os comandos de execução no PowerShell. As sugestões foram adaptadas à estrutura do projeto.

### 3. Revisão das métricas
A IA auxiliou na interpretação da latência por lote e da latência amortizada por item. Também foi discutida a inclusão da métrica `LATENCIA_LOTE_P95_MS` para diferenciar as duas medidas. Os valores finais foram obtidos dos logs das execuções.

### 4. Erros identificados nas sugestões da IA

- **PowerShell:** foi sugerido o uso de `&&`, incompatível com o PowerShell 5.1. A equipe substituiu o comando por instruções compatíveis e ajustou a escrita dos arquivos com `WriteAllText`.
- **Docker Compose:** a inicialização de todos os serviços também iniciava o gerador antes da medição. O procedimento foi separado em inicialização da infraestrutura, construção do gerador e execução controlada.

### 5. Validação
A equipe executou os cenários, conferiu os resultados nos arquivos de log e verificou a correspondência entre as métricas registradas e a implementação. As sugestões da IA foram utilizadas como apoio, não como substituto da validação experimental.
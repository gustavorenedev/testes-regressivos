# Memory — cob.regressivo (Pipeline Orchestrator)

## Visão Geral do Sistema

Três projetos em uma solution .NET 8:

| Projeto | Tipo | Responsabilidade |
|---|---|---|
| `cob.regressivo.Core` | Class Library | Engine compartilhado: HTTP, templates, assertions, PDF |
| `cob.regressivo.CLI` | Console App | Interface interativa Spectre.Console; lê pipelines de `pipelines/*.json` |
| `cob.regressivo.Web` | Blazor Server | Interface web; mesma engine, pipelines via `appsettings.json` |

---

## Como a CLI Funciona (Fluxo Completo)

```
1. Descobre *.json em pipelines/
2. Deserializa PipelineFileConfig (globals + variables + pipelines[])
3. Exibe tabela + SelectionPrompt (Spectre)
4. Usuário seleciona pipeline
5. PipelineOrchestrator.ExecuteAsync(fileConfig, pipeline, onProgress)
   a. Gera CorrelationId (12 chars GUID sem hífens, maiúsculo)
   b. Cria PipelineExecutionContext (variáveis, globals)
   c. Ordena steps: TopologicalSorter (Kahn's algorithm)
   d. Para cada step:
      - Pula se dependência falhou (failedSteps HashSet)
      - RequestBuilder.Build → aplica TemplateEngine (Scriban)
      - StepExecutor.ExecuteAsync → retry loop + Flurl HTTP
      - AssertionEvaluator.Evaluate → statusCode + JSONPath
      - JsonPathExtractor.Extract → adiciona ao contexto
      - Cria ExecutionRecord, chama onProgress callbacks
   e. Retorna (records, correlationId)
6. Resolve outputPath (suporta {{correlationId}}, {{$timestamp}})
7. PdfReportGenerator.GenerateToFile → QuestPDF → disco
```

---

## Estrutura do Core (`cob.regressivo.Core`)

```
Configuration/
  PipelineFileConfig    → raiz do JSON (version, globals, variables, pipelines[])
  GlobalsConfig         → baseUrl, timeoutSeconds, defaultHeaders, retryPolicy
  PipelineConfig        → id, name, description, tags, onError, steps[], report
  StepConfig            → id, name, dependsOn[], endpoint, extract, assertions, retryPolicy
  EndpointConfig        → url, method, headers, queryParams, body
  BodyConfig            → type (json|form|xml|raw), content (JToken)
  AssertionConfig       → path (JSONPath), operator, value, statusCode
  RetryPolicyConfig     → maxAttempts=3, backoffSeconds=[1,2,5], retryOnStatusCodes=[429,500,502,503]
  ReportConfig          → title, outputPath, includeRequestBody, includeResponseBody, sensitiveFields

Engine/
  PipelineExecutionContext  → estado de execução: correlationId, variables, extractions, globals
  ExecutionRecord           → log imutável de 1 step (request, response, assertions, duration)
  AssertionResult           → (bool Passed, string Message)
  StepResult                → resultado bruto do HTTP (statusCode, body, headers, duration, success, errorMessage)
  TopologicalSorter         → ordena steps respeitando dependsOn (Kahn's + detecção de ciclo)
  StepExecutor              → loop de retry + Flurl HTTP (WithHeader individual, AllowAnyHttpStatus)
  PipelineOrchestrator      → orquestra tudo; aceita Action<string>? onProgress para log em tempo real

Http/
  RequestBuilder            → monta BuiltRequest: renderiza templates, headers globais+locais, body (json/form/xml)
  BuiltRequest              → record imutável (method, url, headers, body, contentType)

Assertions/
  AssertionEvaluator        → valida statusCode e JSONPath (notEmpty, equals, contains, notNull)

Extraction/
  JsonPathExtractor         → extrai aliases via JSONPath usando Newtonsoft.Json

Templating/
  TemplateEngine            → Scriban: {{globals.baseUrl}}, {{variables.key}}, {{steps.id.alias}}, {{correlationId}}
                              Normaliza hífens em step IDs → underscores no modelo de template
                              Aceita {{$key}} como alias

Reporting/
  PdfReportGenerator        → QuestPDF: header (título+logos), tabela resumo, detalhe por step
                              Generate() → byte[]  /  GenerateToFile() → escreve no disco
                              Logos: logoBtg.png + logoPan.png no assetsDir
  SensitiveFieldMasker      → mascara headers e campos JSON com "***"
```

---

## Decisões de Design Críticas

### 1. `WithHeader` individual (não `WithHeaders(dict)`)
Flurl 4.x trata `WithHeaders(Dictionary)` via reflection de propriedades (lê `Count`, `Keys`, `Values`). Usar `foreach + WithHeader(k, v)` é obrigatório.

### 2. Headers duplicados (ex: `Vary`)
APIs como PokeAPI retornam o mesmo header múltiplas vezes. `ToDictionary()` lança `ArgumentException`. Solução: `GroupBy(h.Name) + string.Join(", ", values)`.

### 3. IDs de step normalizados para Scriban
Scriban não aceita hífens em identificadores. `PipelineExecutionContext.NormalizeId()` substitui `-` por `_` **somente** na chave do modelo de template. O `step.Id` original é preservado para `dependsOn` e `failedSteps`.

### 4. `failedSteps` HashSet para dependências
Steps que falham são adicionados ao HashSet. Steps filhos verificam o HashSet antes de executar — se um pai falhou, o filho é ignorado automaticamente com `ErrorMessage` explicando.

### 5. `configByPipelineId` no CLI
Cada arquivo JSON pode conter múltiplos pipelines. O `globals` e `variables` são **por arquivo**, não globais. O dicionário mapeia cada `pipeline.Id` ao seu `PipelineFileConfig` de origem.

### 6. QuestPDF — API correta
- `Image(byte[]).FitArea()` (API 2024.x) — **não** `Image(byte[], ImageScaling)` (obsoleta)
- Formatos suportados: PNG, JPG, JPEG, WEBP, BMP — **não** SVG

### 7. Scriban `StrictVariables = false`
Variáveis undefined viram string vazia (não erro). Necessário para steps que referenciam extrações de steps anteriores que podem ter sido pulados.

---

## Como o WebApp Reutiliza a Engine

```
appsettings.json
  PipelineRunner:
    PipelinesDirectory  → pasta com *.json (pode apontar para a pasta do CLI)
    AssetsDirectory     → pasta com logos (pode apontar para assets do CLI)
    PdfOutputDirectory  → onde salvar o PDF no disco

Features/PipelineRunner/
  CarregarPipelinesQuery    → lê *.json da PipelinesDirectory via MediatR
  ExecutarPipelineCommand   → chama PipelineOrchestrator do Core via MediatR
  PipelineRunnerPage.razor  → tabela de seleção → log em tempo real → resultado
  PipelineTable.razor       → tabela de pipelines disponíveis
  ExecutionLog.razor        → console colorido durante execução
  PipelineResult.razor      → accordion com steps, assertions, botão PDF

Comportamento em tempo real (Blazor Server):
  - ExecutarPipelineCommand aceita Action<string> OnProgress
  - Cada linha de log chama InvokeAsync(StateHasChanged)
  - O accordion e a tabela de resumo aparecem após execução completa
  - PDF: PdfReportGenerator.Generate() → byte[] → downloadFile(JS) + File.WriteAllBytes(disco)

Features/CobrancaFlow/
  CobrancaFlowPipelineBuilder  → converte dados do wizard mock em ExecutionRecords + PipelineConfig
                                 para que Resultado.razor possa chamar PdfReportGenerator do Core
                                 (mesmo formato de PDF para os dois fluxos)
```

---

## Formato do JSON de Pipeline

```json
{
  "version": "1.1",
  "globals": {
    "baseUrl": "https://api.example.com",
    "timeoutSeconds": 30,
    "defaultHeaders": { "X-Correlation-ID": "{{correlationId}}" },
    "retryPolicy": { "maxAttempts": 3, "backoffSeconds": [1,2,5], "retryOnStatusCodes": [429,500,502,503] }
  },
  "variables": { "userId": "1" },
  "pipelines": [{
    "id": "meu_pipeline",
    "name": "Meu Pipeline",
    "onError": "continue",
    "steps": [{
      "id": "buscar_dados",
      "name": "Buscar Dados",
      "dependsOn": [],
      "endpoint": {
        "url": "{{globals.baseUrl}}/dados/{{variables.userId}}",
        "method": "GET",
        "headers": { "Accept": "application/json" }
      },
      "extract": { "dataId": "$.id", "dataUrl": "$.url" },
      "assertions": [
        { "statusCode": 200 },
        { "path": "$.id", "operator": "notEmpty" }
      ]
    }],
    "report": {
      "title": "Relatório de Dados",
      "outputPath": "C:\\Users\\reneg\\pdf\\report-{{correlationId}}.pdf",
      "includeRequestBody": true,
      "includeResponseBody": true,
      "sensitiveFields": ["Authorization", "password"]
    }
  }]
}
```

---

## Pipelines de Demonstração (CLI)

| Arquivo | Pipelines | API |
|---|---|---|
| `demo-jsonplaceholder.json` | buscar_usuario_e_posts | jsonplaceholder.typicode.com |
| `dog-api.json` | listar_racas, buscar_imagens | dog.ceo/api |
| `pokeapi.json` | buscar_pokemon, buscar_especie | pokeapi.co |

---

## Pacotes Principais por Projeto

| Pacote | Versão | Uso |
|---|---|---|
| `Flurl.Http` | 4.0.2 | HTTP client (Core) |
| `Newtonsoft.Json` | 13.0.3 | Deserialização + JSONPath (Core) |
| `QuestPDF` | 2024.10.0 | Geração de PDF (Core) |
| `Scriban` | 7.1.0 | Template engine (Core) |
| `Spectre.Console` | 0.49.1 | UI interativa (somente CLI) |
| `MediatR` | 12.4.1 | CQRS handlers (somente Web) |

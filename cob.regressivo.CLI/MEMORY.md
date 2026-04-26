# MEMORY.md — cob.regressivo.CLI

Documento de memória técnica do projeto. Atualizar sempre que uma decisão arquitetural relevante for tomada.

---

## 1. Visão Geral

Ferramenta de linha de comando (CLI) para execução de **testes regressivos HTTP** orientados a pipelines declarativos. O operador define um ou mais fluxos em JSON, a ferramenta executa os steps em ordem topológica, extrai valores intermediários via JSONPath, aplica assertions e gera um relatório PDF ao final.

**Stack:**
- Runtime: .NET 8 / C# 12
- HTTP: Flurl.Http 4.x
- Templating: Scriban
- PDF: QuestPDF (Community)
- JSON: Newtonsoft.Json
- CLI/UI: Spectre.Console

---

## 2. Arquitetura

```
Program.cs
│
├── UI/PipelineSelector          → Apresentação da lista de pipelines no terminal
│
├── Core/PipelineOrchestrator    → Coordena a execução de um pipeline
│   ├── Core/TopologicalSorter   → Ordena steps respeitando dependências (Kahn)
│   ├── Http/RequestBuilder      → Monta a BuiltRequest (URL, headers, body)
│   ├── Core/StepExecutor        → Executa HTTP com retry + Stopwatch
│   ├── Assertions/AssertionEvaluator → Avalia assertions JSONPath
│   └── Extraction/JsonPathExtractor  → Extrai valores para o contexto
│
├── Core/PipelineExecutionContext → Repositório de variáveis e extrações de steps
├── Templating/TemplateEngine     → Renderiza {{variables.x}} / {{steps.y.z}} via Scriban
│
├── Core/PipelineRunSummary       → DTO agregado de resultado (sem dependência de PipelineConfig)
└── Reporting/PdfReportGenerator  → Gera PDF a partir do PipelineRunSummary
    └── Reporting/SensitiveFieldMasker → Mascara campos sensíveis em headers e body JSON
```

### Organização de namespaces / camadas

| Namespace | Responsabilidade |
|---|---|
| `Configuration` | POCOs de desserialização do JSON de pipeline |
| `Core` | Orquestração, contexto de execução, resultados |
| `Http` | Construção e execução de requests HTTP |
| `Templating` | Resolução de templates `{{...}}` |
| `Extraction` | JSONPath sobre responses |
| `Assertions` | Avaliação de assertions declarativas |
| `Reporting` | Geração de artefato PDF |
| `UI` | Interação com o terminal (Spectre.Console) |

---

## 3. Fluxo Principal de Execução

```
1. Program.cs carrega todos os *.json de /pipelines
2. PipelineSelector exibe lista e captura seleção do usuário
3. PipelineOrchestrator.ExecuteAsync(pipeline):
   a. Cria PipelineExecutionContext com variáveis globais
   b. TopologicalSorter ordena steps por dependências (DAG)
   c. Para cada step:
      i.  Verifica se alguma dependência falhou → pula com erro
      ii. RequestBuilder monta URL + headers + body (com TemplateEngine)
      iii.StepExecutor executa HTTP (com retry e Stopwatch)
      iv. AssertionEvaluator avalia assertions
      v.  JsonPathExtractor extrai variáveis para o contexto
      vi. Registra ExecutionRecord
   d. Retorna PipelineRunSummary (tempo total, status, records)
4. Program.cs exibe resultado no terminal
5. PdfReportGenerator.Generate(summary, path) gera PDF
```

---

## 4. Formato do Pipeline JSON

Dois formatos de `body` são suportados (via `BodyConfigConverter`):

```json
// Shorthand — objeto plano é tratado diretamente como body JSON
"body": { "campo": "{{variables.valor}}" }

// Explícito — útil para form/xml
"body": {
  "type": "form",
  "content": { "campo": "{{variables.valor}}" }
}
```

**Variáveis de template disponíveis:**

| Expressão | Resolve para |
|---|---|
| `{{globals.baseUrl}}` | URL base do arquivo de pipeline |
| `{{variables.x}}` | Variável do bloco `variables` |
| `{{steps.step_id.alias}}` | Valor extraído de um step anterior |
| `{{correlationId}}` | ID único da execução (gerado em runtime) |
| `{{timestamp}}` | Timestamp de início no formato `yyyyMMddHHmmss` |

> **Atenção:** IDs de steps com hífens são normalizados para underscores no contexto de template. Ex: `meu-step` → `{{steps.meu_step.campo}}`.

---

## 5. Decisões Técnicas e Trade-offs

### 5.1 Scriban como motor de templates
**Decisão:** Scriban com `StrictVariables = false`.  
**Motivo:** Sintaxe `{{...}}` familiar, suporte a acesso aninhado (`steps.x.y`), sem throws em variáveis ausentes.  
**Trade-off:** Scriban normaliza nomes de chave para lowercase em `ScriptObject`; camelCase nas extrações funciona porque o lookup também é case-insensitive.

### 5.2 Newtonsoft.Json (não System.Text.Json)
**Decisão:** Newtonsoft por suporte a `JToken` dinâmico e `JsonConverter` customizado.  
**Trade-off:** Dependência extra; System.Text.Json seria mais performático, mas `JToken`/JSONPath são essenciais para extração e templating.

### 5.3 BodyConfigConverter
**Decisão:** `JsonConverter<BodyConfig>` que detecta automaticamente formato shorthand vs explícito.  
**Motivo:** Usuários escrevem `"body": { ... }` naturalmente; exigir o wrapper `"content"` seria verboso.  
**Trade-off:** Um campo chamado exatamente `"content"` no body real seria interpretado como wrapper explícito — documentar como limitação.

### 5.4 PipelineRunSummary como DTO de saída
**Decisão:** Orchestrator retorna `PipelineRunSummary` em vez de `(records, correlationId)`.  
**Motivo:** Desacopla `PdfReportGenerator` de `PipelineConfig`; o gerador de PDF só conhece dados de resultado.  
**Trade-off:** Pequeno overhead de um record extra; compensa em testabilidade.

### 5.5 Stopwatch no StepExecutor e no Orchestrator
**Decisão:** `StepExecutor` mede duração por requisição HTTP; `PipelineOrchestrator` mede duração total do pipeline (inclui template rendering, retry backoff, etc.).  
**Motivo:** Separação clara entre "tempo de rede" (step) e "tempo de execução total" (pipeline).

### 5.6 QuestPDF Community License
**Decisão:** `LicenseType.Community` configurado em cada chamada a `Generate`.  
**Atenção:** Verificar termos da licença antes de uso comercial. Para projetos não-comerciais está liberado.

---

## 6. Thresholds de Performance no PDF

Definidos como constantes em `PdfReportGenerator`:

| Threshold | Valor | Cor |
|---|---|---|
| Normal | < 1 000ms | Verde |
| Alerta `!` | 1 000ms – 3 000ms | Laranja |
| Crítico `⚠` | > 3 000ms | Vermelho |

Esses valores são **hardcoded**. Possível evolução: torná-los configuráveis em `ReportConfig`.

---

## 7. Pontos de Atenção

- **Dependências circulares:** `TopologicalSorter` lança exception em ciclos; garantir que steps do pipeline formem um DAG.
- **Retry e tempo total:** O backoff de retry é contabilizado no tempo total do pipeline, mas não no `Duration` do `ExecutionRecord` (que mede apenas a última tentativa HTTP).
- **Variáveis numéricas no JSON:** `Dictionary<string, string>` aceita números via Newtonsoft (conversão automática); porém, o valor sempre chega como string no template.
- **Mascaramento de dados sensíveis:** Funciona por regex no JSON do body. Campos aninhados ou em arrays não são mascarados.
- **Steps ignorados:** Quando um step é pulado por dependência falha, `Duration` é `TimeSpan.Zero` e `ResponseStatusCode` é 0 — o PDF trata isso como "–".

---

## 8. Possíveis Melhorias Futuras

| Item | Descrição |
|---|---|
| Thresholds configuráveis | Mover `WarningThresholdMs` / `CriticalThresholdMs` para `ReportConfig` |
| Retry count no record | Registrar quantas tentativas foram feitas por step |
| Exportação JSON | Além do PDF, gerar `summary.json` para ingestão em sistemas de observabilidade |
| Execução paralela | Steps sem dependência entre si poderiam rodar em paralelo (`Task.WhenAll`) |
| Variáveis de ambiente no pipeline | Já suportado via `${ENV:NOME}`, mas sem documentação de onboarding |
| Mascaramento de arrays | `SensitiveFieldMasker` não cobre arrays JSON; possível melhoria com JsonPath |
| Assertions avançadas | Operadores: `greaterThan`, `lessThan`, `matches` (regex), `count` |
| Cache de templates Scriban | Re-parsear o template a cada step é desnecessário; `Template.Parse` pode ser cacheado |

---

## 9. Convenções de Código

- **Namespaces:** `Cob.Regressivo.CLI.<Camada>`
- **Records** para dados imutáveis de resultado (`StepResult`, `ExecutionRecord`, `PipelineRunSummary`)
- **Classes estáticas** para serviços sem estado (`RequestBuilder`, `TemplateEngine`, `AssertionEvaluator`)
- **Classes de instância** apenas quando há estado (`PipelineOrchestrator`, `PipelineExecutionContext`)
- Sem comentários de código exceto onde o "porquê" não é óbvio

---

*Última atualização: 2026-04-26*

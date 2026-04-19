using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Core;
using Cob.Regressivo.CLI.Reporting;
using Cob.Regressivo.CLI.UI;
using Newtonsoft.Json;
using Spectre.Console;

var pipelinesDir = Path.Combine(AppContext.BaseDirectory, "pipelines");

if (!Directory.Exists(pipelinesDir))
{
    AnsiConsole.MarkupLine("[red]Pasta 'pipelines/' não encontrada.[/]");
    return 1;
}

var allPipelines = new List<PipelineConfig>();

// Cada pipeline ID aponta para o PipelineFileConfig do seu arquivo de origem.
// Isso garante que globals e variables corretos sejam usados independente da ordem de carga.
var configByPipelineId = new Dictionary<string, PipelineFileConfig>();

foreach (var file in Directory.GetFiles(pipelinesDir, "*.json", SearchOption.TopDirectoryOnly))
{
    try
    {
        var json = await File.ReadAllTextAsync(file);
        var fileConfig = JsonConvert.DeserializeObject<PipelineFileConfig>(json);
        if (fileConfig?.Pipelines == null || fileConfig.Pipelines.Count == 0) continue;

        foreach (var pipeline in fileConfig.Pipelines)
        {
            allPipelines.Add(pipeline);
            configByPipelineId[pipeline.Id] = fileConfig;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[yellow]Aviso:[/] Falha ao carregar {Path.GetFileName(file)}: {Markup.Escape(ex.Message)}");
    }
}

if (allPipelines.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Nenhum pipeline encontrado em 'pipelines/'.[/]");
    return 1;
}

var selected = PipelineSelector.Show(allPipelines);

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"[bold]Executando:[/] [cyan]{Markup.Escape(selected.Name)}[/]");
AnsiConsole.WriteLine();

// Usa o PipelineFileConfig do arquivo de origem do pipeline selecionado
var selectedFileConfig = configByPipelineId[selected.Id];
var orchestrator = new PipelineOrchestrator(selectedFileConfig);
var (records, correlationId) = await orchestrator.ExecuteAsync(selected);

var passed = records.Count(r => r.Success);
var color  = passed == records.Count ? "green" : passed == 0 ? "red" : "yellow";

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"[bold]Resultado:[/] [{color}]{passed}/{records.Count}[/] steps com sucesso");

var rawPath = (selected.Report?.OutputPath ?? @"C:\Users\reneg\pdf\report-{{correlationId}}.pdf")
    .Replace("{{$correlationId}}", correlationId)
    .Replace("{{correlationId}}", correlationId)
    .Replace("{{$timestamp}}", DateTime.Now.ToString("yyyyMMddHHmmss"));

// Resolve sempre relativo ao diretório do executável
var outputPath = Path.IsPathRooted(rawPath)
    ? rawPath
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawPath));

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"Gerando PDF em:");
AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(outputPath)}[/]");

try
{
    PdfReportGenerator.Generate(records, selected, outputPath);
    AnsiConsole.MarkupLine($"[green]✓ Relatório gerado com sucesso![/]");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Erro ao gerar PDF:[/] {Markup.Escape(ex.Message)}");
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[grey]Pressione qualquer tecla para sair...[/]");
Console.ReadKey(intercept: true);
return 0;

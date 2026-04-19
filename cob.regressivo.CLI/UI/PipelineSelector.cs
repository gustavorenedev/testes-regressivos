using Cob.Regressivo.CLI.Configuration;
using Spectre.Console;

namespace Cob.Regressivo.CLI.UI;

public static class PipelineSelector
{
    public static PipelineConfig Show(List<PipelineConfig> pipelines)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Pipeline Orchestrator").Centered().Color(Color.Cyan1));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Nome[/]")
            .AddColumn("[bold]Tags[/]")
            .AddColumn("[bold]Steps[/]")
            .AddColumn("[bold]Descrição[/]");

        foreach (var p in pipelines)
            table.AddRow(
                $"[cyan]{Markup.Escape(p.Id)}[/]",
                Markup.Escape(p.Name),
                Markup.Escape(string.Join(", ", p.Tags)),
                p.Steps.Count.ToString(),
                Markup.Escape(p.Description));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        return AnsiConsole.Prompt(
            new SelectionPrompt<PipelineConfig>()
                .Title("[bold]Selecione o pipeline para executar:[/]")
                .PageSize(10)
                .UseConverter(p => $"[cyan]{p.Id}[/] — {p.Name}")
                .AddChoices(pipelines));
    }
}

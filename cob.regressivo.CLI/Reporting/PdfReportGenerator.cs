using Cob.Regressivo.CLI.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cob.Regressivo.CLI.Reporting;

public static class PdfReportGenerator
{
    // Thresholds de performance para destaque visual
    private const double WarningThresholdMs  = 1_000;
    private const double CriticalThresholdMs = 3_000;

    private static string AssetsDir =>
        Path.Combine(AppContext.BaseDirectory, "assets");

    private static readonly HashSet<string> SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public static void Generate(PipelineRunSummary summary, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        var report    = summary.Report;
        var sensitive = report.SensitiveFields;
        var btgBytes  = TryLoadImage("logoBtg.png");
        var panBytes  = TryLoadImage("logoPan.png");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ── HEADER ────────────────────────────────────────────────────────────
                page.Header().Column(header =>
                {
                    header.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(report.Title)
                                .Bold().FontSize(16).FontColor(Colors.Blue.Darken2);

                            col.Item().Text($"Pipeline: {summary.PipelineName}")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);

                            col.Item().Text($"ID: {summary.PipelineId}  |  CorrelationId: {summary.CorrelationId}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);

                            col.Item().Text($"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        row.AutoItem().AlignMiddle().Row(r =>
                        {
                            if (panBytes != null)
                                r.ConstantItem(40).Height(20).Image(panBytes).FitArea();
                            else
                                r.ConstantItem(40).AlignMiddle().AlignCenter()
                                    .Text("PAN").Bold().FontSize(10);

                            r.ConstantItem(1).Height(20).AlignMiddle().LineVertical(1).LineColor(Colors.Grey.Medium);
                            r.ConstantItem(6);

                            if (btgBytes != null)
                                r.ConstantItem(70).Height(25).Image(btgBytes).FitArea();
                            else
                                r.ConstantItem(70).AlignMiddle().AlignCenter()
                                    .Text("BTG").Bold().FontSize(10);
                        });
                    });
                });

                // ── CONTENT ───────────────────────────────────────────────────────────
                page.Content().PaddingTop(10).Column(col =>
                {
                    RenderStatusBox(col, summary);
                    RenderPerformanceTimeline(col, summary);
                    RenderSummaryTable(col, summary);
                    RenderStepDetails(col, summary, sensitive, report);
                });

                // ── FOOTER ────────────────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" de ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf(outputPath);
    }

    // ── SEÇÃO 1: caixa de status geral ────────────────────────────────────────
    private static void RenderStatusBox(ColumnDescriptor col, PipelineRunSummary s)
    {
        var (bgColor, textColor) = s.OverallStatus switch
        {
            "SUCESSO"       => (Colors.Green.Lighten4,  Colors.Green.Darken3),
            "FALHA PARCIAL" => (Colors.Orange.Lighten4, Colors.Orange.Darken4),
            _               => (Colors.Red.Lighten4,    Colors.Red.Darken3)
        };

        col.Item().PaddingBottom(12).Border(1).BorderColor(textColor)
            .Background(bgColor).Padding(10).Column(box =>
        {
            // Linha 1: status + duração
            box.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(s.OverallStatus)
                        .Bold().FontSize(14).FontColor(textColor);

                    left.Item().PaddingTop(2).Text(
                        $"Iniciado: {s.StartedAt:HH:mm:ss}  |  " +
                        $"Duração total: {FormatDuration(s.TotalDuration)}  |  " +
                        $"Steps: {s.PassedSteps}/{s.TotalSteps} OK")
                        .FontSize(9).FontColor(textColor);
                });

                // Taxa de sucesso — destaque visual
                row.ConstantItem(70).AlignMiddle().AlignCenter().Column(right =>
                {
                    var pct = s.TotalSteps == 0 ? 0 : s.PassedSteps * 100 / s.TotalSteps;
                    right.Item().AlignCenter().Text($"{pct}%")
                        .Bold().FontSize(20).FontColor(textColor);
                    right.Item().AlignCenter().Text("sucesso")
                        .FontSize(8).FontColor(textColor);
                });
            });

            // Linha 2: descrição + tags
            if (!string.IsNullOrWhiteSpace(s.PipelineDescription))
                box.Item().PaddingTop(4).Text(s.PipelineDescription)
                    .FontSize(8).FontColor(Colors.Grey.Darken2).Italic();

            if (s.Tags.Count > 0)
                box.Item().PaddingTop(4).Text($"Tags: {string.Join("  ·  ", s.Tags)}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    // ── SEÇÃO 2: timeline visual de performance ────────────────────────────────
    private static void RenderPerformanceTimeline(ColumnDescriptor col, PipelineRunSummary s)
    {
        if (s.Records.Count == 0) return;

        var maxMs = s.Records.Max(r => r.Duration.TotalMilliseconds);
        if (maxMs < 1) maxMs = 1;

        col.Item().PaddingBottom(4).Text("Timeline de Performance").Bold().FontSize(12);
        col.Item().PaddingBottom(12).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(timeline =>
        {
            foreach (var r in s.Records)
            {
                var pct      = Math.Max(0.002f, (float)(r.Duration.TotalMilliseconds / maxMs));
                var empty    = Math.Max(0.002f, 1f - pct);
                var barColor = DurationColor(r.Duration);
                var label    = r.StepName.Length > 24 ? r.StepName[..23] + "…" : r.StepName;

                timeline.Item().PaddingBottom(5).Row(row =>
                {
                    // Nome do step
                    row.ConstantItem(150).AlignMiddle()
                        .Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);

                    // Barra proporcional
                    row.RelativeItem().Height(12).AlignMiddle().Row(bar =>
                    {
                        bar.RelativeItem(pct).Background(barColor).Height(12);
                        bar.RelativeItem(empty).Background(Colors.Grey.Lighten3).Height(12);
                    });

                    // Duração + alerta
                    var durationText = $"  {r.Duration.TotalMilliseconds:F0}ms";
                    var alert = r.Duration.TotalMilliseconds >= CriticalThresholdMs ? " ⚠"
                        : r.Duration.TotalMilliseconds >= WarningThresholdMs ? " !"
                        : string.Empty;

                    row.ConstantItem(70).AlignMiddle().AlignRight()
                        .Text($"{durationText}{alert}").FontSize(8).FontColor(barColor);
                });
            }

            // Linha de total
            timeline.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            timeline.Item().PaddingTop(4).Row(row =>
            {
                row.ConstantItem(150).Text("Total pipeline").FontSize(8).Bold();
                row.RelativeItem();
                row.ConstantItem(70).AlignRight()
                    .Text($"  {FormatDuration(s.TotalDuration)}").FontSize(8).Bold();
            });

            // Legenda de thresholds
            timeline.Item().PaddingTop(8).Row(leg =>
            {
                leg.AutoItem().PaddingRight(12).Row(r =>
                {
                    r.ConstantItem(10).Height(10).Background(Colors.Green.Darken2);
                    r.AutoItem().PaddingLeft(4).Text("< 1 000ms").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                leg.AutoItem().PaddingRight(12).Row(r =>
                {
                    r.ConstantItem(10).Height(10).Background(Colors.Orange.Darken2);
                    r.AutoItem().PaddingLeft(4).Text("1 000 – 3 000ms  (!)").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                leg.AutoItem().Row(r =>
                {
                    r.ConstantItem(10).Height(10).Background(Colors.Red.Darken2);
                    r.AutoItem().PaddingLeft(4).Text("> 3 000ms  (⚠)").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    // ── SEÇÃO 3: tabela resumo ─────────────────────────────────────────────────
    private static void RenderSummaryTable(ColumnDescriptor col, PipelineRunSummary s)
    {
        col.Item().PaddingBottom(4).Text("Resumo da Execução").Bold().FontSize(12);
        col.Item().PaddingBottom(16).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.5f); // step
                c.RelativeColumn(4f);   // url
                c.RelativeColumn(1f);   // status
                c.RelativeColumn(1.5f); // duração
                c.RelativeColumn(1f);   // resultado
            });

            table.Header(h =>
            {
                foreach (var hdr in new[] { "Step", "URL", "Status", "Duração", "Resultado" })
                    h.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                        .Text(hdr).Bold().FontSize(9);
            });

            foreach (var r in s.Records)
            {
                var resultColor  = r.Success ? Colors.Green.Darken2 : Colors.Red.Darken2;
                var durationColor = DurationColor(r.Duration);

                table.Cell().Padding(4).Text(r.StepName).FontSize(9);
                table.Cell().Padding(4).Text(r.RequestUrl).FontSize(8);
                table.Cell().Padding(4).Text(r.ResponseStatusCode > 0 ? r.ResponseStatusCode.ToString() : "–")
                    .FontSize(9).FontColor(resultColor);
                table.Cell().Padding(4)
                    .Text(r.Duration.TotalMilliseconds > 0 ? $"{r.Duration.TotalMilliseconds:F0}ms" : "–")
                    .FontSize(9).FontColor(durationColor).Bold();
                table.Cell().Padding(4).Text(r.Success ? "✓ OK" : "✗ FALHA")
                    .FontSize(9).FontColor(resultColor).Bold();
            }
        });
    }

    // ── SEÇÃO 4: detalhe por step ──────────────────────────────────────────────
    private static void RenderStepDetails(
        ColumnDescriptor col,
        PipelineRunSummary s,
        IEnumerable<string> sensitive,
        Configuration.ReportConfig report)
    {
        col.Item().PaddingBottom(4).Text("Detalhe dos Steps").Bold().FontSize(12);

        foreach (var record in s.Records)
        {
            var resultColor = record.Success ? Colors.Green.Darken2 : Colors.Red.Darken2;
            var headerBg    = record.Success ? Colors.Green.Lighten5 : Colors.Red.Lighten5;

            col.Item().PaddingBottom(16).Border(1).BorderColor(Colors.Grey.Lighten2).Column(sc =>
            {
                // Cabeçalho do step
                sc.Item().Background(headerBg).Padding(8).Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Text(record.StepName).Bold().FontSize(12);
                        row.AutoItem().Text(record.Success ? "✓ SUCESSO" : "✗ FALHA")
                            .Bold().FontSize(10).FontColor(resultColor);
                    });

                    h.Item().PaddingTop(2).Row(meta =>
                    {
                        meta.AutoItem().Text($"ID: {record.StepId}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        meta.ConstantItem(16);
                        meta.AutoItem().Text($"Horário: {record.Timestamp:HH:mm:ss.fff}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        meta.ConstantItem(16);
                        meta.AutoItem()
                            .Text($"Duração: {record.Duration.TotalMilliseconds:F0}ms")
                            .FontSize(8).Bold().FontColor(DurationColor(record.Duration));
                    });
                });

                // Bloco de métricas de performance inline
                if (record.Duration.TotalMilliseconds >= WarningThresholdMs)
                {
                    var alertColor = record.Duration.TotalMilliseconds >= CriticalThresholdMs
                        ? Colors.Red.Lighten3 : Colors.Orange.Lighten3;
                    var alertText  = record.Duration.TotalMilliseconds >= CriticalThresholdMs
                        ? $"⚠  Requisição crítica: {record.Duration.TotalMilliseconds:F0}ms (acima de {CriticalThresholdMs:F0}ms)"
                        : $"!  Requisição lenta: {record.Duration.TotalMilliseconds:F0}ms (acima de {WarningThresholdMs:F0}ms)";

                    sc.Item().Background(alertColor).PaddingVertical(4).PaddingHorizontal(8)
                        .Text(alertText).FontSize(8).Bold();
                }

                sc.Item().Padding(8).Column(body =>
                {
                    // REQUEST
                    body.Item().PaddingBottom(4).Text("REQUEST")
                        .Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                    body.Item().Background(Colors.Grey.Lighten5).Border(1)
                        .BorderColor(Colors.Grey.Lighten2).Padding(6).Column(req =>
                    {
                        req.Item().Text($"{record.RequestMethod}  {record.RequestUrl}")
                            .Bold().FontSize(9);

                        if (record.RequestHeaders.Count > 0)
                        {
                            req.Item().PaddingTop(4).Text("Headers:").Bold().FontSize(8);
                            req.Item().Text(
                                SensitiveFieldMasker.MaskHeaders(record.RequestHeaders, sensitive))
                                .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                        }

                        if (report.IncludeRequestBody && record.RequestBody != null)
                        {
                            req.Item().PaddingTop(4).Text("Body:").Bold().FontSize(8);
                            req.Item().Text(
                                FormatJson(SensitiveFieldMasker.MaskBody(record.RequestBody, sensitive)))
                                .FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                        }
                    });

                    // RESPONSE
                    body.Item().PaddingTop(8).PaddingBottom(4).Text("RESPONSE")
                        .Bold().FontSize(10).FontColor(resultColor);
                    body.Item().Background(Colors.Grey.Lighten5).Border(1)
                        .BorderColor(Colors.Grey.Lighten2).Padding(6).Column(res =>
                    {
                        res.Item().Row(row =>
                        {
                            row.AutoItem().Text($"Status: {record.ResponseStatusCode}")
                                .Bold().FontSize(10).FontColor(resultColor);
                            row.ConstantItem(16);
                            row.AutoItem().Text($"Duração: {record.Duration.TotalMilliseconds:F0}ms")
                                .FontSize(9).FontColor(DurationColor(record.Duration));
                        });

                        if (report.IncludeResponseBody && record.ResponseBody != null)
                        {
                            res.Item().PaddingTop(4).Text("Body:").Bold().FontSize(8);
                            res.Item().Text(
                                FormatJson(SensitiveFieldMasker.MaskBody(record.ResponseBody, sensitive)))
                                .FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                        }
                    });

                    // ASSERTIONS
                    if (record.Assertions.Count > 0)
                    {
                        body.Item().PaddingTop(8).PaddingBottom(4).Text("ASSERTIONS")
                            .Bold().FontSize(10);
                        foreach (var a in record.Assertions)
                            body.Item().PaddingBottom(2).Row(row =>
                            {
                                row.ConstantItem(16).Text(a.Passed ? "✓" : "✗")
                                    .FontColor(a.Passed ? Colors.Green.Darken2 : Colors.Red.Darken2)
                                    .Bold().FontSize(9);
                                row.RelativeItem().Text(a.Message).FontSize(9);
                            });
                    }

                    // ERRO
                    if (!record.Success && record.ErrorMessage != null)
                        body.Item().PaddingTop(8).Background(Colors.Red.Lighten4)
                            .Border(1).BorderColor(Colors.Red.Lighten2).Padding(6)
                            .Column(err =>
                            {
                                err.Item().Text("ERRO CAPTURADO").Bold().FontSize(9)
                                    .FontColor(Colors.Red.Darken3);
                                err.Item().PaddingTop(2).Text(record.ErrorMessage)
                                    .FontSize(8.5f).FontColor(Colors.Red.Darken3);
                            });
                });
            });
        }
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private static string DurationColor(TimeSpan d) =>
        d.TotalMilliseconds >= CriticalThresholdMs ? Colors.Red.Darken2
        : d.TotalMilliseconds >= WarningThresholdMs ? Colors.Orange.Darken2
        : Colors.Green.Darken2;

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalMilliseconds < 1_000)
            return $"{d.TotalMilliseconds:F0}ms";
        if (d.TotalSeconds < 60)
            return $"{d.TotalSeconds:F2}s";
        return $"{(int)d.TotalMinutes}m {d.Seconds}s";
    }

    private static string FormatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            return JToken.Parse(json).ToString(Formatting.Indented);
        }
        catch
        {
            return json;
        }
    }

    private static byte[]? TryLoadImage(string fileName)
    {
        var path = Path.Combine(AssetsDir, fileName);
        if (!File.Exists(path)) return null;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext)) return null;

        return File.ReadAllBytes(path);
    }
}

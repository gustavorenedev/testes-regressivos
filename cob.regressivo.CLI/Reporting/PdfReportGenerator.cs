using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Core;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cob.Regressivo.CLI.Reporting;

public static class PdfReportGenerator
{
    private static string AssetsDir =>
        Path.Combine(AppContext.BaseDirectory, "assets");

    private static readonly HashSet<string> SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    private static byte[]? TryLoadImage(string fileName)
    {
        var path = Path.Combine(AssetsDir, fileName);

        if (!File.Exists(path))
        {
            Console.WriteLine($"[assets] Arquivo não encontrado: {path}");
            return null;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
        {
            Console.WriteLine($"[assets] Formato '{ext}' não suportado pelo QuestPDF. Use PNG, JPG ou WEBP. Arquivo: {fileName}");
            return null;
        }

        return File.ReadAllBytes(path);
    }

    public static void Generate(List<ExecutionRecord> records, PipelineConfig pipeline, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        var report = pipeline.Report;
        var sensitive = report.SensitiveFields;

        var btgBytes = TryLoadImage("logoBtg.png");
        var panBytes = TryLoadImage("logoPan.png");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().PaddingTop(8).Row(row =>
                    {
                        // 📄 LADO ESQUERDO — infos
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(report.Title)
                                .Bold()
                                .FontSize(16)
                                .FontColor(Colors.Blue.Darken2);

                            col.Item().Text($"Pipeline: {pipeline.Name}")
                                .FontSize(11)
                                .FontColor(Colors.Grey.Darken1);

                            col.Item().Text($"CorrelationId: {records.FirstOrDefault()?.CorrelationId}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Medium);

                            col.Item().Text($"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Medium);
                        });

                        // 🖼️ LADO DIREITO — logos menores
                        row.AutoItem().AlignMiddle().Row(r =>
                        {
                            // PAN
                            if (panBytes != null)
                                r.ConstantItem(40).Height(20).Image(panBytes).FitArea();
                            else
                                r.ConstantItem(40).AlignMiddle().AlignCenter()
                                    .Text("PAN").FontColor(Colors.Black).Bold().FontSize(10);

                            // Divisória
                            r.ConstantItem(1).Height(20).AlignMiddle().LineVertical(1).LineColor(Colors.Grey.Medium);

                            r.ConstantItem(6);

                            // BTG
                            if (btgBytes != null)
                                r.ConstantItem(70).Height(25).Image(btgBytes).FitArea();
                            else
                                r.ConstantItem(70).AlignMiddle().AlignCenter()
                                    .Text("BTG").FontColor(Colors.Black).Bold().FontSize(10);
                        });
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Tabela resumo
                    col.Item().Text("Resumo da Execução").Bold().FontSize(13);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            foreach (var hdr in new[] { "Step", "URL", "Status", "Duração", "Resultado" })
                                h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text(hdr).Bold().FontSize(9);
                        });

                        foreach (var r in records)
                        {
                            var statusColor = r.Success ? Colors.Green.Darken2 : Colors.Red.Darken2;
                            table.Cell().Padding(4).Text(r.StepName).FontSize(9);
                            table.Cell().Padding(4).Text(r.RequestUrl).FontSize(8);
                            table.Cell().Padding(4).Text(r.ResponseStatusCode.ToString()).FontSize(9).FontColor(statusColor);
                            table.Cell().Padding(4).Text($"{r.Duration.TotalMilliseconds:F0}ms").FontSize(9);
                            table.Cell().Padding(4).Text(r.Success ? "OK" : "FALHA").FontSize(9).FontColor(statusColor);
                        }
                    });

                    // Detalhe por step
                    foreach (var record in records)
                    {
                        col.Item().PaddingTop(20).Column(sc =>
                        {
                            // Cabeçalho do step
                            sc.Item().Background(Colors.Blue.Lighten4).Padding(8).Column(h =>
                            {
                                h.Item().Text(record.StepName).Bold().FontSize(13);
                                h.Item().Text($"ID: {record.StepId}  |  Tempo Requisição: {record.Timestamp:HH:mm:ss.fff}  |  Correlation: {record.CorrelationId}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });

                            // REQUEST
                            sc.Item().PaddingTop(6).Text("REQUEST").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                            sc.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(req =>
                            {
                                req.Item().Text($"{record.RequestMethod}  {record.RequestUrl}").Bold().FontSize(9);

                                if (record.RequestHeaders.Count > 0)
                                {
                                    req.Item().PaddingTop(4).Text("Headers:").Bold().FontSize(8);
                                    req.Item().Text(SensitiveFieldMasker.MaskHeaders(record.RequestHeaders, sensitive))
                                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                                }

                                if (report.IncludeRequestBody && record.RequestBody != null)
                                {
                                    req.Item().PaddingTop(4).Text("Body:").Bold().FontSize(8);
                                    req.Item().Text(SensitiveFieldMasker.MaskBody(record.RequestBody, sensitive) ?? "")
                                        .FontSize(8);
                                }
                            });

                            // RESPONSE
                            var respColor = record.Success ? Colors.Green.Darken2 : Colors.Red.Darken2;
                            sc.Item().PaddingTop(6).Text("RESPONSE").Bold().FontSize(11).FontColor(respColor);
                            sc.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(res =>
                            {
                                res.Item().Text($"Status: {record.ResponseStatusCode}").Bold().FontSize(10).FontColor(respColor);

                                if (report.IncludeResponseBody && record.ResponseBody != null)
                                {
                                    res.Item().PaddingTop(4).Text("Body:").Bold().FontSize(8);
                                    res.Item().Text(SensitiveFieldMasker.MaskBody(record.ResponseBody, sensitive) ?? "")
                                        .FontSize(8);
                                }
                            });

                            // ASSERTIONS
                            if (record.Assertions.Count > 0)
                            {
                                sc.Item().PaddingTop(6).Text("ASSERTIONS").Bold().FontSize(11);
                                foreach (var a in record.Assertions)
                                    sc.Item().Row(row =>
                                    {
                                        row.ConstantItem(16).Text(a.Passed ? "✓" : "✗")
                                            .FontColor(a.Passed ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                                        row.RelativeItem().Text(a.Message).FontSize(9);
                                    });
                            }

                            if (!record.Success && record.ErrorMessage != null)
                                sc.Item().PaddingTop(6).Background(Colors.Red.Lighten4).Padding(6)
                                    .Text($"Erro: {record.ErrorMessage}").FontSize(9).FontColor(Colors.Red.Darken3);
                        });
                    }
                });

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
}

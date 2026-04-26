using Cob.Regressivo.Web.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<PipelineService>();
builder.Services.AddSingleton<ExecutionHistoryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/download/{correlationId}", (string correlationId, ExecutionHistoryService history) =>
{
    var bytes = history.Get(correlationId);
    if (bytes is null)
        return Results.NotFound();
    return Results.File(bytes, "application/pdf", $"report-{correlationId}.pdf");
});

app.MapRazorComponents<Cob.Regressivo.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

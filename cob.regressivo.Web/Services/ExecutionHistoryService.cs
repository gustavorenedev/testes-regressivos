using System.Collections.Concurrent;

namespace Cob.Regressivo.Web.Services;

public class ExecutionHistoryService
{
    private readonly ConcurrentDictionary<string, byte[]> _pdfs = new();

    public void Store(string correlationId, byte[] pdfBytes)
        => _pdfs[correlationId] = pdfBytes;

    public byte[]? Get(string correlationId)
        => _pdfs.TryGetValue(correlationId, out var bytes) ? bytes : null;
}

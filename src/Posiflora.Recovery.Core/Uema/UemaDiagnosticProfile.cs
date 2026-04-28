using Posiflora.Recovery.Core.Diagnostics;

namespace Posiflora.Recovery.Core.Uema;

public static class UemaDiagnosticProfile
{
    public static DiagnosticProfile Metadata { get; } = new(
        "uema.error11",
        "UEMA / ошибка 11",
        "Диагностика служб uem-agent и uem-updater, файлов службы и cloud connection.");

    public static CheckResult Build(IReadOnlyList<UemaSnapshot> snapshots)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var findings = snapshots.SelectMany(UemaFindingMapper.Map).ToArray();

        return new CheckResult(
            Metadata.Id,
            startedAt,
            DateTimeOffset.UtcNow,
            findings);
    }
}

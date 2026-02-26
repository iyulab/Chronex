using BenchmarkDotNet.Attributes;

namespace Chronex.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class NextOccurrenceBenchmarks
{
    private ChronexExpression _simpleCron = null!;
    private ChronexExpression _complexCron = null!;
    private ChronexExpression _withTz = null!;
    private ChronexExpression _specialL = null!;
    private ChronexExpression _specialW = null!;
    private ChronexExpression _specialHash = null!;
    private ChronexExpression _interval = null!;
    private ChronexExpression _onceFuture = null!;
    private ChronexExpression _alias = null!;

    private DateTimeOffset _from;

    [GlobalSetup]
    public void Setup()
    {
        _simpleCron = ChronexExpression.Parse("*/5 * * * *");
        _complexCron = ChronexExpression.Parse("0 9 * * MON-FRI");
        _withTz = ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI");
        _specialL = ChronexExpression.Parse("0 0 L * *");
        _specialW = ChronexExpression.Parse("0 0 15W * *");
        _specialHash = ChronexExpression.Parse("0 0 * * MON#2");
        _interval = ChronexExpression.Parse("@every 30m");
        _onceFuture = ChronexExpression.Parse("@once 2026-06-01T09:00:00Z");
        _alias = ChronexExpression.Parse("@daily");

        _from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    [Benchmark(Description = "Next: simple cron")]
    public DateTimeOffset? SimpleCron() => _simpleCron.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: complex cron")]
    public DateTimeOffset? ComplexCron() => _complexCron.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: with timezone")]
    public DateTimeOffset? WithTimezone() => _withTz.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: special L")]
    public DateTimeOffset? SpecialL() => _specialL.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: special W")]
    public DateTimeOffset? SpecialW() => _specialW.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: special #")]
    public DateTimeOffset? SpecialHash() => _specialHash.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: @every interval")]
    public DateTimeOffset? Interval() => _interval.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: @once future")]
    public DateTimeOffset? OnceFuture() => _onceFuture.GetNextOccurrence(_from);

    [Benchmark(Description = "Next: @daily alias")]
    public DateTimeOffset? Alias() => _alias.GetNextOccurrence(_from);
}

using BenchmarkDotNet.Attributes;

namespace Chronex.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class EnumerateBenchmarks
{
    private ChronexExpression _simpleCron = null!;
    private ChronexExpression _complexCron = null!;
    private ChronexExpression _interval = null!;
    private DateTimeOffset _from;

    [GlobalSetup]
    public void Setup()
    {
        _simpleCron = ChronexExpression.Parse("*/5 * * * *");
        _complexCron = ChronexExpression.Parse("0 9 * * MON-FRI");
        _interval = ChronexExpression.Parse("@every 30m");
        _from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    [Benchmark(Description = "Enumerate 10: simple cron")]
    public int SimpleCron10() => _simpleCron.Enumerate(_from, 10).Count();

    [Benchmark(Description = "Enumerate 100: simple cron")]
    public int SimpleCron100() => _simpleCron.Enumerate(_from, 100).Count();

    [Benchmark(Description = "Enumerate 10: weekday cron")]
    public int ComplexCron10() => _complexCron.Enumerate(_from, 10).Count();

    [Benchmark(Description = "Enumerate 100: weekday cron")]
    public int ComplexCron100() => _complexCron.Enumerate(_from, 100).Count();

    [Benchmark(Description = "Enumerate 100: @every interval")]
    public int Interval100() => _interval.Enumerate(_from, 100).Count();
}

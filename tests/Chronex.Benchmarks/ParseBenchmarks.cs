using BenchmarkDotNet.Attributes;

namespace Chronex.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ParseBenchmarks
{
    [Benchmark(Description = "Parse: simple cron")]
    public ChronexExpression SimpleCron() => ChronexExpression.Parse("*/5 * * * *");

    [Benchmark(Description = "Parse: complex cron")]
    public ChronexExpression ComplexCron() => ChronexExpression.Parse("0 9 * * MON-FRI");

    [Benchmark(Description = "Parse: 6-field cron")]
    public ChronexExpression SixFieldCron() => ChronexExpression.Parse("30 */5 * * * *");

    [Benchmark(Description = "Parse: with timezone")]
    public ChronexExpression WithTimezone() => ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI");

    [Benchmark(Description = "Parse: with options")]
    public ChronexExpression WithOptions() => ChronexExpression.Parse("0 9 * * * {jitter:30s, max:100}");

    [Benchmark(Description = "Parse: full expression")]
    public ChronexExpression FullExpression() => ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI {jitter:30s, max:100, tag:report}");

    [Benchmark(Description = "Parse: alias @daily")]
    public ChronexExpression Alias() => ChronexExpression.Parse("@daily");

    [Benchmark(Description = "Parse: @every interval")]
    public ChronexExpression Interval() => ChronexExpression.Parse("@every 30m");

    [Benchmark(Description = "Parse: @every range")]
    public ChronexExpression IntervalRange() => ChronexExpression.Parse("@every 1h-2h");

    [Benchmark(Description = "Parse: @once absolute")]
    public ChronexExpression OnceAbsolute() => ChronexExpression.Parse("@once 2026-06-01T09:00:00Z");

    [Benchmark(Description = "Parse: special L")]
    public ChronexExpression SpecialL() => ChronexExpression.Parse("0 0 L * *");

    [Benchmark(Description = "Parse: special W")]
    public ChronexExpression SpecialW() => ChronexExpression.Parse("0 0 15W * *");

    [Benchmark(Description = "Parse: special #")]
    public ChronexExpression SpecialHash() => ChronexExpression.Parse("0 0 * * MON#2");
}

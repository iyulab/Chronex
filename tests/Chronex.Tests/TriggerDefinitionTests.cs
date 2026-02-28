using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class TriggerDefinitionTests
{
    [Fact]
    public void Deserialize_Full()
    {
        var json = """
        {
            "id": "health-check",
            "expression": "TZ=UTC @every 15m {stagger:3m}",
            "enabled": true,
            "metadata": {
                "endpoint": "https://api.example.com/health",
                "env": "prod"
            }
        }
        """;

        var def = JsonSerializer.Deserialize<TriggerDefinition>(json);
        def.Should().NotBeNull();
        def!.Id.Should().Be("health-check");
        def.Expression.Should().Be("TZ=UTC @every 15m {stagger:3m}");
        def.Enabled.Should().BeTrue();
        def.Metadata.Should().NotBeNull();
        def.Metadata!["endpoint"].Should().Be("https://api.example.com/health");
        def.Metadata["env"].Should().Be("prod");
    }

    [Fact]
    public void Deserialize_Minimal()
    {
        var json = """
        {
            "id": "test",
            "expression": "@daily"
        }
        """;

        var def = JsonSerializer.Deserialize<TriggerDefinition>(json);
        def.Should().NotBeNull();
        def!.Id.Should().Be("test");
        def.Enabled.Should().BeTrue(); // default
        def.Metadata.Should().BeNull();
    }

    [Fact]
    public void Deserialize_Disabled()
    {
        var json = """
        {
            "id": "test",
            "expression": "@daily",
            "enabled": false
        }
        """;

        var def = JsonSerializer.Deserialize<TriggerDefinition>(json);
        def!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        var def = new TriggerDefinition
        {
            Id = "my-trigger",
            Expression = "0 9 * * MON-FRI",
            Metadata = new Dictionary<string, string>
            {
                ["scope"] = "production"
            }
        };

        var json = JsonSerializer.Serialize(def);
        var deserialized = JsonSerializer.Deserialize<TriggerDefinition>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("my-trigger");
        deserialized.Expression.Should().Be("0 9 * * MON-FRI");
        deserialized.Metadata!["scope"].Should().Be("production");
    }

    // Integration: Register via TriggerDefinition

    [Fact]
    public async Task Register_WithDefinition()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var def = new TriggerDefinition
        {
            Id = "test",
            Expression = "* * * * *",
            Metadata = new Dictionary<string, string>
            {
                ["key"] = "value"
            }
        };

        Dictionary<string, string>? capturedMetadata = null;
        scheduler.Register(def, (ctx, ct) =>
        {
            capturedMetadata = new Dictionary<string, string>(ctx.Metadata);
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        capturedMetadata.Should().NotBeNull();
        capturedMetadata!["key"].Should().Be("value");
    }

    [Fact]
    public async Task Register_WithDefinition_DisabledByDefault()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var def = new TriggerDefinition
        {
            Id = "test",
            Expression = "* * * * *",
            Enabled = false
        };

        var fired = false;
        scheduler.Register(def, (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeFalse();
    }

    [Fact]
    public void Register_WithDefinition_MetadataAvailableOnTrigger()
    {
        var scheduler = new ChronexScheduler();

        var def = new TriggerDefinition
        {
            Id = "test",
            Expression = "* * * * *",
            Metadata = new Dictionary<string, string> { ["env"] = "test" }
        };

        var reg = scheduler.Register(def, (ctx, ct) => Task.CompletedTask);
        reg.Metadata.Should().NotBeNull();
        reg.Metadata!["env"].Should().Be("test");
    }
}

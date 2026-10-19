using FluentAssertions;
using PointLurker.Core.Application;

namespace Core.Application.UnitTests;

public sealed class ScopedInstanceTests
{
    private sealed class ScopeSpy : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Disposal_closes_the_scope_the_instance_came_from()
    {
        var scope = new ScopeSpy();
        var instance = new ScopedInstance<string>("unit", scope);

        instance.Value.Should().Be("unit");
        await instance.DisposeAsync();

        scope.DisposeCount.Should().Be(1);
    }

    /// <summary>
    /// Tests build a unit over their own fakes and have no scope to hand over; disposing such an
    /// instance must be harmless rather than a null reference.
    /// </summary>
    [Fact]
    public async Task Disposal_is_a_no_op_without_a_scope()
    {
        var instance = new ScopedInstance<string>("unit");

        var act = async () => await instance.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}

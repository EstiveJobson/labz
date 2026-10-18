using FluentAssertions;
using Labz.Core.Units;

namespace Labz.Core.Units.Tests;

public class PipelineSmokeTests
{
    [Fact]
    public void Units_assembly_is_referenced()
    {
        typeof(UnitsAssembly).Assembly.GetName().Name.Should().Be("Labz.Core.Units");
    }
}

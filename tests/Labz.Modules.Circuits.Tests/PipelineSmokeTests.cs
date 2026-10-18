using FluentAssertions;
using Labz.Modules.Circuits;

namespace Labz.Modules.Circuits.Tests;

public class PipelineSmokeTests
{
    [Fact]
    public void Circuits_assembly_is_referenced()
    {
        typeof(CircuitsAssembly).Assembly.GetName().Name.Should().Be("Labz.Modules.Circuits");
    }
}

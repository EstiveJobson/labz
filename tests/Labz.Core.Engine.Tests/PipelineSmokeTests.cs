using FluentAssertions;
using Labz.Core.Engine;

namespace Labz.Core.Engine.Tests;

public class PipelineSmokeTests
{
    [Fact]
    public void Engine_assembly_is_referenced()
    {
        typeof(EngineAssembly).Assembly.GetName().Name.Should().Be("Labz.Core.Engine");
    }
}

using FluentAssertions;
using Labz.Core.Drawing;
using Labz.Core.Project;
using Labz.Core.Units;
using Labz.Modules.Circuits;

namespace Labz.Modules.Circuits.Tests;

public class SchematicEditorTests
{
    [Fact]
    public void Library_places_all_ten_types_and_they_round_trip()
    {
        var session = Blank();
        var x = 0d;
        foreach (var definition in ComponentLibrary.All)
        {
            var id = session.Place(definition.Type, x, 40);
            ComponentIds.Pattern.IsMatch(id).Should().BeTrue();
            id.Should().StartWith(definition.IdPrefix);
            x += 120;
        }

        var folder = TempFolder();
        ProjectStore.Save(folder, session.ToDocument());
        var opened = ProjectStore.Open(folder);
        opened.Status.Should().Be(ProjectLoadStatus.Success, opened.Error);
        var types = opened.Document!.Modules.Circuits.Components.Select(component => component.Type).ToArray();
        types.Should().BeEquivalentTo(ComponentLibrary.All.Select(definition => definition.Type));

        var restored = SchematicSession.FromDocument(opened.Document);
        var circuit = CircuitMapper.ToCircuit(restored.State);
        circuit.Components.Should().HaveCount(10);
        circuit.Components.OfType<ResistorComponent>().Single().PowerRatingWatts.Should().Be(0.25);
        circuit.Components.OfType<SwitchComponent>().Single().RonOhms.Should().Be(SwitchComponent.DefaultRonOhms);
        circuit.Components.OfType<SwitchComponent>().Single().RoffOhms.Should().Be(SwitchComponent.DefaultRoffOhms);
        circuit.Components.OfType<DiodeComponent>().Single().Model.Should().BeEquivalentTo(DiodePresets.In4148);
        circuit.Nets.Should().ContainSingle(net => net.Id == "0");
    }

    [Fact]
    public void Params_accept_portuguese_quantities_and_reject_the_wrong_dimension()
    {
        var session = Blank();
        var resistor = session.Place("resistor", 0, 0);
        session.TrySetParam(resistor, "resistance", "4,7 kΩ", out var error).Should().BeTrue();
        error.Should().BeNull();
        session.TrySetParam(resistor, "resistance", "4.7k", out _).Should().BeTrue();
        var capacitor = session.Place("capacitor", 120, 0);
        session.TrySetParam(capacitor, "capacitance", "100 nF", out _).Should().BeTrue();

        var before = session.State.Find(resistor)!.Params["resistance"];
        session.TrySetParam(resistor, "resistance", "100 nF", out error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        session.State.Find(resistor)!.Params["resistance"].Should().Be(before);

        var circuit = CircuitMapper.ToCircuit(session.State);
        circuit.Components.OfType<ResistorComponent>().Single().ResistanceOhms.Should().Be(4700);
        circuit.Components.OfType<CapacitorComponent>().Single().CapacitanceFarads.Should().BeApproximately(1e-7, 1e-18);
    }

    [Fact]
    public void Moving_a_component_keeps_its_nets_and_the_wire_follows_the_pin()
    {
        var session = Blank();
        var resistor = session.Place("resistor", 0, 0);
        var ground = session.Place("ground", 200, 0);
        session.ConnectPins(resistor, "2", ground, "g");
        var before = NetPins(session, "0");

        session.Move(resistor, 80, 40);
        NetPins(session, "0").Should().Equal(before);
        session.State.Find(resistor)!.X.Should().Be(80);
        session.State.Find(resistor)!.Y.Should().Be(40);

        var scene = SceneBuilder.Build(session.State, null, null);
        var wire = scene.Wires.Should().ContainSingle().Subject;
        OrthogonalRouter.IsOrthogonal(wire.Points).Should().BeTrue();
        var pin = scene.Symbols.Single(symbol => symbol.Id == resistor).Pins.Single(item => item.Name == "2").Position;
        wire.Points.Should().Contain(point => point.DistanceTo(pin) < 1e-6);
    }

    [Fact]
    public void Undo_and_redo_cover_each_action_and_fifty_steps()
    {
        var session = Blank();
        var resistor = session.Place("resistor", 0, 0);
        session.Undo();
        session.State.Components.Should().BeEmpty();
        session.Redo();
        session.State.Find(resistor).Should().NotBeNull();

        session.Move(resistor, 40, 20);
        session.Undo();
        session.State.Find(resistor)!.X.Should().Be(0);

        session.Redo();
        var ground = session.Place("ground", 160, 0);
        session.ConnectPins(resistor, "1", ground, "g");
        session.State.Nets.Should().Contain(net => net.Id == "0" && net.Connections.Any(pin => pin.ComponentId == resistor));
        session.Undo();
        session.State.Nets.SelectMany(net => net.Connections).Should().NotContain(pin => pin.ComponentId == resistor);
        session.Redo();

        session.Delete(ground);
        session.State.Find(ground).Should().BeNull();
        session.Undo();
        session.State.Find(ground).Should().NotBeNull();

        session.TrySetParam(resistor, "resistance", "4,7 kΩ", out _).Should().BeTrue();
        session.Undo();
        session.State.Find(resistor)!.Params["resistance"].Should().Be("1 kΩ");
        session.Redo();
        session.State.Find(resistor)!.Params["resistance"].Should().Be("4,7 kΩ");

        var depthBefore = session.UndoDepth;
        for (var step = 0; step < 50; step++)
        {
            session.Place("resistor", step * 20, 200);
        }

        session.UndoDepth.Should().BeGreaterThanOrEqualTo(depthBefore + 50);
        for (var step = 0; step < 50; step++)
        {
            session.Undo().Should().BeTrue();
        }

        session.State.Components.Should().NotContain(component => component.Y == 200);
        for (var step = 0; step < 50; step++)
        {
            session.Redo().Should().BeTrue();
        }

        session.State.Components.Count(component => component.Y == 200).Should().Be(50);
    }

    [Fact]
    public void A_junction_on_an_existing_wire_joins_that_net()
    {
        var session = Blank();
        var first = session.Place("resistor", 0, 0);
        var second = session.Place("resistor", 160, 0);
        var third = session.Place("resistor", 80, 80);
        session.ConnectPins(first, "2", second, "1");
        var netId = session.State.Nets.Single(net => net.Connections.Any(pin => pin.ComponentId == first)).Id;
        session.ConnectPinToNet(third, "1", netId);
        session.State.Nets.Single(net => net.Id == netId).Connections
            .Select(pin => pin.ComponentId)
            .Should().BeEquivalentTo(new[] { first, second, third });
        session.Undo();
        session.State.Nets.Single(net => net.Id == netId).Connections.Should().HaveCount(2);
    }

    [Fact]
    public void Schottky_preset_replaces_the_diode_model()
    {
        var session = Blank();
        var diode = session.Place("diode", 0, 0);
        session.ApplyDiodePreset(diode, schottky: true);
        var model = CircuitMapper.ToCircuit(session.State).Components.OfType<DiodeComponent>().Single().Model;
        model.SaturationCurrent.Should().Be(DiodePresets.Schottky.SaturationCurrent);
        model.BreakdownVoltage.Should().Be(DiodePresets.Schottky.BreakdownVoltage);
    }

    [Fact]
    public void Geometry_snaps_hits_and_rotates_pins()
    {
        GridSnap.Snap(14).Should().Be(20);
        GridSnap.Snap(10).Should().Be(20);
        var route = OrthogonalRouter.Route(new CanvasPoint(0, 0), new CanvasPoint(40, 80));
        OrthogonalRouter.IsOrthogonal(route).Should().BeTrue();
        route.First().Should().Be(new CanvasPoint(0, 0));
        route.Last().Should().Be(new CanvasPoint(40, 80));

        var session = Blank();
        var resistor = session.Place("resistor", 0, 0);
        var scene = SceneBuilder.Build(session.State, resistor, null);
        var pin = scene.Symbols[0].Pins.Single(item => item.Name == "2");
        HitTester.Hit(scene, pin.Position.X, pin.Position.Y).Kind.Should().Be(HitKind.Pin);
        HitTester.Hit(scene, 0, 0).Kind.Should().Be(HitKind.Body);

        session.Rotate(resistor);
        var rotated = SceneBuilder.Build(session.State, null, null);
        rotated.Symbols[0].Pins.Single(item => item.Name == "2").Position.Y.Should().BeGreaterThan(0);
        session.Undo();
        session.State.Find(resistor)!.Rotation.Should().Be(0);
    }

    private static SchematicSession Blank() =>
        SchematicSession.CreateBlank("Teste", new DateTimeOffset(2026, 10, 18, 16, 0, 0, TimeSpan.FromHours(-3)));

    private static IEnumerable<string> NetPins(SchematicSession session, string netId) =>
        session.State.Nets.Single(net => net.Id == netId).Connections
            .Select(pin => pin.ComponentId + "." + pin.Pin)
            .Order(StringComparer.Ordinal);

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

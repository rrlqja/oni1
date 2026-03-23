using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DisplacementTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte SandId = 2;
        private const byte OxygenId = 3;
        private const byte WaterId = 4;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(9, 9);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        [Test]
        public void Liquid_Sinks_Through_Gas_Below()
        {
            SetCell(4, 0, BedrockId, 0);
            SetCell(4, 3, WaterId, 1_000_000);
            SetCell(4, 2, OxygenId, 1_000);
            SetCell(4, 1, OxygenId, 1_000);
            // 낙하 경로 전체에 벽
            for (int y = 1; y <= 3; y++) { SetCell(3, y, BedrockId, 0); SetCell(5, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 2).ElementId, Is.EqualTo(WaterId),
                "물이 가스를 통과해서 아래로 내려와야 합니다");
            Assert.That(_grid.GetCell(4, 3).ElementId, Is.EqualTo(OxygenId),
                "기체가 위로 올라와야 합니다");
        }

        [Test]
        public void Liquid_Falls_Through_Multiple_Gas_Over_Ticks()
        {
            SetCell(4, 0, BedrockId, 0);
            SetCell(4, 5, WaterId, 1_000_000);
            SetCell(4, 4, OxygenId, 1_000);
            SetCell(4, 3, OxygenId, 1_000);
            SetCell(4, 2, OxygenId, 1_000);
            SetCell(4, 1, OxygenId, 1_000);
            for (int y = 1; y <= 5; y++) { SetCell(3, y, BedrockId, 0); SetCell(5, y, BedrockId, 0); }

            for (int t = 1; t <= 10; t++) _runner.Step(t);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(WaterId),
                "물이 여러 틱에 걸쳐 바닥까지 가라앉아야 합니다");
            Assert.That(SumMassOfElement(WaterId), Is.EqualTo(1_000_000));
        }

        [Test]
        public void Liquid_Does_Not_Displace_Gas_When_Below_Is_Open()
        {
            SetCell(4, 3, WaterId, 1_000_000);
            SetCell(5, 3, OxygenId, 1_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 3).ElementId, Is.Not.EqualTo(WaterId),
                "아래가 비어있으면 수직 이동이 우선되어야 합니다");
            Assert.That(SumMassOfElement(OxygenId), Is.EqualTo(1_000));
        }

        [Test]
        public void PlaceWithDisplacement_Pushes_Gas_To_Vacuum()
        {
            SetCell(4, 4, OxygenId, 1_000);
            int index = _grid.ToIndex(4, 4);
            DisplacementResolver.TryPlaceWithDisplacement(_grid, _registry, index, new SimCell(BedrockId, 0, 0, SimCellFlags.None));
            Assert.That(_grid.GetCell(4, 4).ElementId, Is.EqualTo(BedrockId));
            Assert.That(SumMassOfElement(OxygenId), Is.EqualTo(1_000));
        }

        [Test]
        public void PlaceWithDisplacement_Merges_Gas_Into_Same_Element()
        {
            SetCell(4, 4, OxygenId, 500);
            SetCell(5, 4, OxygenId, 300);
            SetCell(3, 4, BedrockId, 0);
            SetCell(4, 5, BedrockId, 0);
            SetCell(4, 3, BedrockId, 0);
            int index = _grid.ToIndex(4, 4);
            DisplacementResolver.TryPlaceWithDisplacement(_grid, _registry, index, new SimCell(BedrockId, 0, 0, SimCellFlags.None));
            Assert.That(_grid.GetCell(4, 4).ElementId, Is.EqualTo(BedrockId));
            Assert.That(_grid.GetCell(5, 4).Mass, Is.EqualTo(800));
        }

        [Test]
        public void DisplaceResolver_Returns_False_When_No_Space()
        {
            SetCell(4, 4, OxygenId, 1_000);
            SetCell(3, 4, BedrockId, 0);
            SetCell(5, 4, BedrockId, 0);
            SetCell(4, 5, BedrockId, 0);
            SetCell(4, 3, BedrockId, 0);
            Assert.That(DisplacementResolver.TryDisplace(_grid, _registry, _grid.ToIndex(4, 4)), Is.False);
            Assert.That(_grid.GetCell(4, 4).Mass, Is.EqualTo(1_000));
        }

        // ================================================================
        private void FillAllVacuum() { for (int y = 0; y < _grid.Height; y++) for (int x = 0; x < _grid.Width; x++) SetCell(x, y, VacuumId, 0); }
        private void SetCell(int x, int y, byte elementId, int mass) { ref SimCell c = ref _grid.GetCellRef(_grid.ToIndex(x, y)); c = new SimCell(elementId, mass, 0, SimCellFlags.None); }
        private int SumMassOfElement(byte elementId) { int t = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) t += _grid.GetCellByIndex(i).Mass; return t; }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[] {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
                CreateElement(SandId, "Sand", ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid, 2f, 500_000, 1_000_000, 1, 0, true, new Color32(200,180,100,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.1f, 1_000, 1_000, 1, 0, false, new Color32(180,220,255,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GravityProcessorIntegrationTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte SandId = 2;
        private const byte WaterId = 3;
        private const byte OxygenId = 4;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(7, 7);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ================================================================
        //  FallingSolid 중력
        // ================================================================

        [Test]
        public void FallingSolid_Falls_Into_Vacuum()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);
            for (int t = 1; t <= 10; t++) _runner.Step(t);
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(500_000));
        }

        [Test]
        public void FallingSolid_Swaps_With_Gas()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, OxygenId, 1_000);
            SetCell(3, 0, BedrockId, 0);
            for (int t = 1; t <= 10; t++) _runner.Step(t);
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
            Assert.That(SumMassOfElement(OxygenId), Is.EqualTo(1_000));
        }

        [Test]
        public void FallingSolid_Swaps_With_Liquid()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, BedrockId, 0);
            SetCell(2, 2, BedrockId, 0);
            SetCell(4, 2, BedrockId, 0);
            for (int t = 1; t <= 10; t++) _runner.Step(t);
            Assert.That(SumMassOfElement(SandId) + SumEntityMass(SandId), Is.EqualTo(500_000));
            Assert.That(SumMassOfElement(WaterId) + SumEntityMass(WaterId), Is.EqualTo(1_000_000));
        }

        [Test]
        public void FallingSolid_Merges_Same_Element()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 400_000);
            SetCell(3, 0, BedrockId, 0);
            _runner.Step(1);
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(900_000));
        }

        [Test]
        public void FallingSolid_Merge_Overflow()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 800_000);
            SetCell(3, 0, BedrockId, 0);
            _runner.Step(1);
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(1_000_000));
            Assert.That(_grid.GetCell(3, 2).Mass, Is.EqualTo(300_000));
        }

        [Test]
        public void FallingSolid_Blocked_By_Bedrock()
        {
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);
            _runner.Step(1);
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
        }

        // ================================================================
        //  Liquid 중력 — 낙하 경로 전체에 벽 필수
        // ================================================================

        [Test]
        public void Liquid_Falls_Into_Vacuum()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            for (int y = 1; y <= 3; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void Liquid_Sinks_Through_Gas()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            SetCell(3, 2, OxygenId, 1_000);
            for (int y = 1; y <= 3; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(OxygenId));
        }

        [Test]
        public void Liquid_Merges_Same_Liquid_Below()
        {
            SetCell(3, 2, WaterId, 600_000);
            SetCell(3, 1, WaterId, 300_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(900_000));
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void Liquid_Blocked_By_Full_Same_Liquid()
        {
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).Mass, Is.EqualTo(1_000_000));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Sand_Column_Only_Bottom_Falls_Per_Tick()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);
            _runner.Step(1);
            Assert.That(CountElementCells(SandId), Is.GreaterThanOrEqualTo(1));
            Assert.That(SumMassOfElement(SandId) + SumEntityMass(SandId), Is.EqualTo(1_500_000));
        }

        [Test]
        public void Liquid_Sinks_Through_Multiple_Gas_Over_Ticks()
        {
            SetCell(3, 5, WaterId, 1_000_000);
            SetCell(3, 4, OxygenId, 1_000);
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 2, BedrockId, 1_000);
            SetCell(3, 1, BedrockId, 1_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 3; y <= 5; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            for (int t = 1; t <= 5; t++) _runner.Step(t);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(WaterId));
        }

        [Test]
        public void No_Vacuum_Artifact_After_Swap_With_Vacuum()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            for (int y = 1; y <= 3; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(3, 3).Mass, Is.EqualTo(0));
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0); }
        private void SetCell(int x, int y, byte elementId, int mass) { _grid.SetCell(x, y, new SimCell(elementId, mass, 0f, SimCellFlags.None)); }
        private int SumMassOfElement(byte elementId) { int t = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) t += _grid.GetCellByIndex(i).Mass; return t; }
        private int SumEntityMass(byte elementId) { int t = 0; var e = _runner.FallingEntities.ActiveEntities; for (int i = 0; i < e.Count; i++) if (e[i].ElementId == elementId) t += e[i].Mass; return t; }
        private int CountElementCells(byte elementId) { int c = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) c++; return c; }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[] {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
                CreateElement(SandId, "Sand", ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid, 2f, 500_000, 1_000_000, 1, 0, true, new Color32(200,180,100,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.1f, 1_000, 1_000, 1, 0, false, new Color32(180,220,255,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SimulationRunnerSwapTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte SandId = 2;
        private const byte OxygenId = 3;
        private const byte WaterId = 4;
        private const byte BedrockId = 1;

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

        [Test]
        public void FallingSolid_Swaps_With_Gas_Below()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, OxygenId, 1_000);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 3);
            SimCell bottom = _grid.GetCell(3, 2);

            Assert.That(bottom.ElementId, Is.EqualTo(SandId),
                "모래가 아래로 이동해야 합니다");
            Assert.That(top.ElementId, Is.EqualTo(OxygenId),
                "산소가 위로 밀려나야 합니다");
        }

        [Test]
        public void FallingSolid_Swaps_With_Liquid_Below()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, BedrockId, 0);

            // 좌우 차단해서 물이 옆으로 퍼지는 것을 방지
            SetCell(2, 2, BedrockId, 0);
            SetCell(4, 2, BedrockId, 0);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 3);
            SimCell bottom = _grid.GetCell(3, 2);

            Assert.That(bottom.ElementId, Is.EqualTo(SandId),
                "모래가 물 아래로 가라앉아야 합니다");
            Assert.That(top.ElementId, Is.EqualTo(WaterId),
                "물이 위로 밀려나야 합니다");
        }

        [Test]
        public void FallingSolid_Does_Not_Swap_With_StaticSolid()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, BedrockId, 0);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 3);
            SimCell bottom = _grid.GetCell(3, 2);

            Assert.That(top.ElementId, Is.EqualTo(SandId),
                "모래가 Bedrock 위에서 멈춰야 합니다");
            Assert.That(bottom.ElementId, Is.EqualTo(BedrockId),
                "Bedrock은 변하지 않아야 합니다");
        }

        [Test]
        public void FallingSolid_Moves_Into_Vacuum_Below()
        {
            SetCell(3, 3, SandId, 500_000);

            _runner.Step(1);

            SimCell original = _grid.GetCell(3, 3);
            SimCell below = _grid.GetCell(3, 2);

            Assert.That(original.ElementId, Is.EqualTo(VacuumId),
                "원래 위치는 진공이어야 합니다");
            Assert.That(below.ElementId, Is.EqualTo(SandId),
                "모래가 아래로 이동해야 합니다");
        }

        // ── 유틸리티 ──

        private void FillAllVacuum()
        {
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    SetCell(x, y, VacuumId, 0);
        }

        private void SetCell(int x, int y, byte elementId, int mass)
        {
            ref SimCell cell = ref _grid.GetCellRef(_grid.ToIndex(x, y));
            cell = new SimCell(elementId, mass, 0, SimCellFlags.None);
        }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                CreateElement(VacuumId, "Vacuum",
                    ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum,
                    0f, 0, 0, 1, 0, false, new Color32(0, 0, 0, 255)),
                CreateElement(BedrockId, "Bedrock",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    9999f, 0, 0, 1, 0, true, new Color32(100, 100, 100, 255)),
                CreateElement(SandId, "Sand",
                    ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid,
                    2f, 500_000, 1_000_000, 1, 0, true, new Color32(200, 180, 100, 255)),
                CreateElement(OxygenId, "Oxygen",
                    ElementBehaviorType.Gas, DisplacementPriority.Gas,
                    0.1f, 1_000, 1_000, 1, 0, false, new Color32(180, 220, 255, 255)),
                CreateElement(WaterId, "Water",
                    ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                    1f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80, 120, 255, 255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(
            byte id, string name,
            ElementBehaviorType behaviorType, DisplacementPriority displacementPriority,
            float density, int defaultMass, int maxMass,
            int viscosity, int minSpreadMass, bool isSolid, Color32 color)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(id, name, behaviorType, displacementPriority,
                density, defaultMass, maxMass, viscosity, minSpreadMass, isSolid, color);
            return def;
        }
    }
}
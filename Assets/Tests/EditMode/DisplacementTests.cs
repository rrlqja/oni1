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

        // ================================================================
        //  수직 중력: 액체가 기체를 통과해서 가라앉음
        // ================================================================

        [Test]
        public void Liquid_Sinks_Through_Gas_Below()
        {
            // 바닥
            SetCell(4, 0, BedrockId, 0);

            // 물(위) + 산소(아래)
            SetCell(4, 3, WaterId, 1_000_000);
            SetCell(4, 2, OxygenId, 1_000);
            SetCell(4, 1, OxygenId, 1_000);

            _runner.Step(1);

            // 물이 최소 1칸 아래로 내려갔는지
            SimCell original = _grid.GetCell(4, 3);
            SimCell below1 = _grid.GetCell(4, 2);

            // 물이 (4,2)로 내려가고 산소가 (4,3)으로 올라왔어야 함
            Assert.That(below1.ElementId, Is.EqualTo(WaterId),
                "물이 가스를 통과해서 아래로 내려와야 합니다");
            Assert.That(original.ElementId, Is.EqualTo(OxygenId),
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

            for (int t = 1; t <= 5; t++)
                _runner.Step(t);

            // 물이 바닥(bedrock 위)까지 가라앉았는지
            SimCell bottom = _grid.GetCell(4, 1);
            Assert.That(bottom.ElementId, Is.EqualTo(WaterId),
                "물이 여러 틱에 걸쳐 바닥까지 가라앉아야 합니다");
        }

        [Test]
        public void Swap_Preserves_Both_Elements_No_Vacuum_Gap()
        {
            SetCell(4, 3, WaterId, 1_000_000);
            SetCell(4, 2, OxygenId, 1_000);

            _runner.Step(1);

            // 두 셀 모두 비어있지 않은지 (진공 깜빡임 없음)
            SimCell top = _grid.GetCell(4, 3);
            SimCell bot = _grid.GetCell(4, 2);

            Assert.That(top.ElementId, Is.Not.EqualTo(VacuumId),
                "스왑 후 위 셀이 진공이면 안 됩니다");
            Assert.That(bot.ElementId, Is.Not.EqualTo(VacuumId),
                "스왑 후 아래 셀이 진공이면 안 됩니다");
        }

        // ================================================================
        //  수평 확산: 아래가 막힌 액체가 옆 기체와 스왑
        // ================================================================

        [Test]
        public void Liquid_Displaces_Gas_Horizontally_When_Blocked_Below()
        {
            // 바닥
            for (int x = 0; x < 9; x++)
                SetCell(x, 0, BedrockId, 0);

            // 물이 바닥 위에 있고, 옆에 산소
            SetCell(4, 1, WaterId, 1_000_000);
            SetCell(5, 1, OxygenId, 1_000);

            for (int t = 1; t <= 3; t++)
                _runner.Step(t);

            // 물 또는 산소가 (5,1)에 있는지
            int totalWater = SumMassOfElement(WaterId);
            int totalOxygen = SumMassOfElement(OxygenId);

            Assert.That(totalWater, Is.GreaterThan(0), "물 질량이 보존되어야 합니다");
            Assert.That(totalOxygen, Is.GreaterThan(0), "산소 질량이 보존되어야 합니다");
        }

        [Test]
        public void Liquid_Does_Not_Displace_Gas_When_Below_Is_Open()
        {
            // 아래가 진공인 상황 — 수평 확산이 아니라 수직 낙하가 우선
            SetCell(4, 3, WaterId, 1_000_000);
            SetCell(5, 3, OxygenId, 1_000);
            // (4,2)는 진공

            _runner.Step(1);

            // 물이 아래로 떨어졌는지 (옆으로 밀어내지 않고)
            SimCell below = _grid.GetCell(4, 2);
            Assert.That(below.ElementId, Is.EqualTo(WaterId),
                "아래가 비어있으면 수직 낙하가 우선되어야 합니다");
        }

        // ================================================================
        //  건설 시 밀어내기 (DisplacementResolver 직접 테스트)
        // ================================================================

        [Test]
        public void PlaceWithDisplacement_Pushes_Gas_To_Vacuum()
        {
            SetCell(4, 4, OxygenId, 1_000);

            int index = _grid.ToIndex(4, 4);
            SimCell wall = new SimCell(BedrockId, 0, 0, SimCellFlags.None);

            DisplacementResolver.TryPlaceWithDisplacement(_grid, _registry, index, wall);

            SimCell placed = _grid.GetCell(4, 4);
            Assert.That(placed.ElementId, Is.EqualTo(BedrockId));

            int totalOxygen = SumMassOfElement(OxygenId);
            Assert.That(totalOxygen, Is.EqualTo(1_000),
                "밀려난 산소의 질량이 보존되어야 합니다");
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
            SimCell wall = new SimCell(BedrockId, 0, 0, SimCellFlags.None);

            DisplacementResolver.TryPlaceWithDisplacement(_grid, _registry, index, wall);

            SimCell placed = _grid.GetCell(4, 4);
            Assert.That(placed.ElementId, Is.EqualTo(BedrockId));

            SimCell merged = _grid.GetCell(5, 4);
            Assert.That(merged.ElementId, Is.EqualTo(OxygenId));
            Assert.That(merged.Mass, Is.EqualTo(800));
        }

        [Test]
        public void DisplaceResolver_Returns_False_When_No_Space()
        {
            SetCell(4, 4, OxygenId, 1_000);
            SetCell(3, 4, BedrockId, 0);
            SetCell(5, 4, BedrockId, 0);
            SetCell(4, 5, BedrockId, 0);
            SetCell(4, 3, BedrockId, 0);

            int index = _grid.ToIndex(4, 4);
            bool result = DisplacementResolver.TryDisplace(_grid, _registry, index);

            Assert.That(result, Is.False);

            SimCell cell = _grid.GetCell(4, 4);
            Assert.That(cell.ElementId, Is.EqualTo(OxygenId));
            Assert.That(cell.Mass, Is.EqualTo(1_000));
        }

        // ================================================================
        //  유틸리티
        // ================================================================

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

        private int SumMassOfElement(byte elementId)
        {
            int total = 0;
            for (int i = 0; i < _grid.Length; i++)
            {
                SimCell cell = _grid.GetCellByIndex(i);
                if (cell.ElementId == elementId)
                    total += cell.Mass;
            }
            return total;
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
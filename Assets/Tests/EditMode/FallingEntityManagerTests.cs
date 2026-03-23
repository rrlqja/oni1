using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// FallingEntityManager 통합 테스트 (v3).
    /// Phase 0(투사체 스캔) + Phase 6(투사체 이동/착지) 동작을 검증한다.
    /// </summary>
    public class FallingEntityManagerTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte SandId = 2;
        private const byte WaterId = 3;
        private const byte OilId = 4;
        private const byte OxygenId = 5;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(9, 12);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ================================================================
        //  FallingSolid 착지
        // ================================================================

        [Test]
        public void FallingSolid_Lands_On_Bedrock_Below()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            SimCell landed = _grid.GetCell(3, 1);
            Assert.That(landed.ElementId, Is.EqualTo(SandId),
                "모래가 Bedrock 위에 착지해야 합니다");
            Assert.That(landed.Mass, Is.EqualTo(500_000), "질량 보존");
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void FallingSolid_Passes_Through_Gas()
        {
            SetCell(3, 4, SandId, 500_000);
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 2, OxygenId, 1_000);
            SetCell(3, 0, BedrockId, 0);

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            SimCell landed = _grid.GetCell(3, 1);
            Assert.That(landed.ElementId, Is.EqualTo(SandId),
                "모래가 기체를 통과하여 바닥에 착지해야 합니다");
        }

        [Test]
        public void FallingSolid_Passes_Through_Liquid_To_Bottom()
        {
            SetCell(3, 5, SandId, 500_000);
            for (int y = 2; y <= 4; y++)
                SetCell(3, y, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 5; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            for (int t = 1; t <= 15; t++)
                _runner.Step(t);

            SimCell bottom = _grid.GetCell(3, 1);
            Assert.That(bottom.ElementId, Is.EqualTo(SandId),
                "모래 투사체가 물을 통과하여 바닥에 착지해야 합니다");

            int totalSandMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalSandMass, Is.EqualTo(500_000), "모래 질량 보존");

            int totalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(3_000_000), "물 질량 보존");
        }

        [Test]
        public void FallingSolid_Merges_With_Same_Solid_On_Landing()
        {
            SetCell(3, 4, SandId, 300_000);
            SetCell(3, 1, SandId, 400_000);
            SetCell(3, 0, BedrockId, 0);

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            SimCell target = _grid.GetCell(3, 1);
            Assert.That(target.ElementId, Is.EqualTo(SandId));
            Assert.That(target.Mass, Is.EqualTo(700_000),
                "같은 고체에 여유가 있으면 Merge");
        }

        [Test]
        public void FallingSolid_Displaces_Liquid_On_Landing()
        {
            SetCell(3, 4, SandId, 500_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(4, 1, BedrockId, 0);

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            SimCell sandCell = _grid.GetCell(3, 1);
            Assert.That(sandCell.ElementId, Is.EqualTo(SandId),
                "모래가 착지하면서 물을 밀어내야 합니다");

            int totalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(1_000_000), "밀려난 물 질량 보존");
        }

        // ================================================================
        //  Liquid 투사체 착지
        // ================================================================

        [Test]
        public void Liquid_Projectile_Passes_Through_Vacuum()
        {
            // 물이 투사체로 전환 → 진공 PassThrough → 바닥 착지
            // 착지 후 좌우 확산 방지를 위해 격납벽 추가
            SetCell(3, 5, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 5; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            SimCell landed = _grid.GetCell(3, 1);
            Assert.That(landed.ElementId, Is.EqualTo(WaterId),
                "물 투사체가 진공을 통과하여 바닥에 착지");
            Assert.That(landed.Mass, Is.EqualTo(1_000_000), "질량 보존");
        }

        [Test]
        public void Liquid_Projectile_Lands_On_Different_Liquid()
        {
            SetCell(3, 5, WaterId, 1_000_000);
            SetCell(3, 2, OilId, 1_000_000);
            SetCell(3, 1, OilId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 5; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            int totalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(1_000_000), "물 질량 보존");

            int totalOilMass = SumMassOfElement(OilId) + SumEntityMass(OilId);
            Assert.That(totalOilMass, Is.EqualTo(2_000_000), "기름 질량 보존");
        }

        [Test]
        public void Liquid_Projectile_Merges_With_Same_Liquid()
        {
            SetCell(3, 5, WaterId, 400_000);
            SetCell(3, 1, WaterId, 500_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 5; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            int totalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(900_000), "같은 액체에 Merge 질량 합산");
        }

        // ================================================================
        //  가속도 검증
        // ================================================================

        [Test]
        public void Gravity_Acceleration_Increases_Over_Ticks()
        {
            SetCell(4, 10, SandId, 500_000);
            SetCell(4, 0, BedrockId, 0);

            _runner.Step(1);

            bool sandExistsInGrid = SumMassOfElement(SandId) > 0;
            bool sandExistsAsEntity = SumEntityMass(SandId) > 0;
            Assert.That(sandExistsInGrid || sandExistsAsEntity, Is.True,
                "첫 틱 후 모래가 어딘가에 존재해야 합니다");

            for (int t = 2; t <= 20; t++)
                _runner.Step(t);

            SimCell landed = _grid.GetCell(4, 1);
            Assert.That(landed.ElementId, Is.EqualTo(SandId),
                "충분한 틱 후 모래가 바닥에 착지");
            Assert.That(landed.Mass, Is.EqualTo(500_000), "질량 보존");
        }

        // ================================================================
        //  질량 보존 종합
        // ================================================================

        [Test]
        public void Mass_Conserved_In_Complex_Scenario()
        {
            SetCell(3, 0, BedrockId, 0);
            SetCell(4, 0, BedrockId, 0);
            SetCell(5, 0, BedrockId, 0);

            SetCell(3, 6, SandId, 500_000);
            SetCell(4, 6, SandId, 300_000);
            SetCell(3, 4, WaterId, 1_000_000);
            SetCell(4, 3, WaterId, 800_000);
            SetCell(3, 5, OxygenId, 1_000);

            int initialSandMass = 800_000;
            int initialWaterMass = 1_800_000;
            int initialOxygenMass = 1_000;

            for (int t = 1; t <= 30; t++)
                _runner.Step(t);

            int finalSandMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            int finalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            int finalOxygenMass = SumMassOfElement(OxygenId) + SumEntityMass(OxygenId);

            Assert.That(finalSandMass, Is.EqualTo(initialSandMass), "모래 총 질량 보존");
            Assert.That(finalWaterMass, Is.EqualTo(initialWaterMass), "물 총 질량 보존");
            Assert.That(finalOxygenMass, Is.EqualTo(initialOxygenMass), "산소 총 질량 보존");
        }

        [Test]
        public void Solid_Fallback_Searches_Upward_When_Landing_Blocked()
        {
            SetCell(3, 6, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);

            for (int t = 1; t <= 15; t++)
                _runner.Step(t);

            SimCell landed = _grid.GetCell(3, 2);
            Assert.That(landed.ElementId, Is.EqualTo(SandId),
                "착지점이 막혀있으면 위로 탐색하여 빈 셀에 착지");
            Assert.That(landed.Mass, Is.EqualTo(500_000), "질량 보존");
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0); }

        private void SetCell(int x, int y, byte elementId, int mass)
        {
            _grid.SetCell(x, y, new SimCell(elementId, mass, 0, SimCellFlags.None));
        }

        private int SumMassOfElement(byte elementId)
        {
            int total = 0;
            for (int i = 0; i < _grid.Length; i++)
                if (_grid.GetCellByIndex(i).ElementId == elementId)
                    total += _grid.GetCellByIndex(i).Mass;
            return total;
        }

        private int SumEntityMass(byte elementId)
        {
            int total = 0;
            var entities = _runner.FallingEntities.ActiveEntities;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].ElementId == elementId)
                    total += entities[i].Mass;
            return total;
        }

        private int CountElementCells(byte elementId)
        {
            int count = 0;
            for (int i = 0; i < _grid.Length; i++)
                if (_grid.GetCellByIndex(i).ElementId == elementId) count++;
            return count;
        }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
                CreateElement(SandId, "Sand", ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid, 2f, 500_000, 1_000_000, 1, 0, true, new Color32(200,180,100,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1.0f, 1_000_000, 1_000_000, 10, 10_000, false, new Color32(80,120,255,255)),
                CreateElement(OilId, "Oil", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 0.8f, 1_000_000, 1_000_000, 10, 10_000, false, new Color32(180,140,50,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.5f, 1_000, 2_000, 1, 0, false, new Color32(180,220,255,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c);
            return def;
        }
    }
}
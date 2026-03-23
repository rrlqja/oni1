using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// ProjectileScanProcessor 통합 테스트 (v3).
    /// Phase 0(투사체 스캔) + Phase 6(투사체 이동) 동작을 검증한다.
    /// </summary>
    public class ProjectileScanProcessorTests
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
            _grid = new WorldGrid(9, 9);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ================================================================
        //  FallingSolid → 투사체 전환
        // ================================================================

        [Test]
        public void FallingSolid_Over_Vacuum_Becomes_Projectile()
        {
            SetCell(3, 5, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            // Phase 0에서 투사체 전환 → 원래 위치 진공
            Assert.That(_grid.GetCell(3, 5).ElementId, Is.EqualTo(VacuumId),
                "Phase 0에서 원래 위치가 진공이 되어야 합니다");

            int totalSandMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalSandMass, Is.EqualTo(500_000), "모래 질량 보존");
        }

        [Test]
        public void FallingSolid_Over_Gas_Becomes_Projectile()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, OxygenId, 1_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            // 원래 위치 진공 확인 (기체가 Phase 4/5에서 이 자리로 확산할 수 있음)
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.Not.EqualTo(SandId),
                "모래가 투사체로 전환되어 원래 위치에서 사라져야 합니다");

            int totalSandMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalSandMass, Is.EqualTo(500_000), "모래 질량 보존");
        }

        [Test]
        public void FallingSolid_Over_Liquid_Becomes_Projectile()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.Not.EqualTo(SandId),
                "모래가 투사체로 전환되어야 합니다");

            int totalSandMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalSandMass, Is.EqualTo(500_000), "모래 질량 보존");
        }

        [Test]
        public void FallingSolid_Over_Same_Solid_With_Capacity_Does_Not_Become_Projectile()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 400_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            SimCell below = _grid.GetCell(3, 1);
            Assert.That(below.ElementId, Is.EqualTo(SandId));
            Assert.That(below.Mass, Is.EqualTo(900_000),
                "같은 고체에 여유가 있으면 Merge(셀 낙하)로 처리");
        }

        [Test]
        public void FallingSolid_Over_Bedrock_Stays()
        {
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId),
                "아래가 고체(다른 종류)이면 정지");
        }

        // ================================================================
        //  컬럼당 1개 제한
        // ================================================================

        [Test]
        public void FallingSolid_Column_Limit_Only_One_Per_Column()
        {
            // 같은 x=3 컬럼에 모래 3개, 바닥 없음 (투사체 전환 대상)
            SetCell(3, 4, SandId, 500_000);
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, SandId, 500_000);

            _runner.Step(1);

            // 컬럼당 1개 제한: Phase 0에서 최대 1개만 투사체 전환
            // 나머지는 Phase 0에서 "아래가 같은 고체" → 투사체 아님
            // Phase 1에서 추가 낙하 가능하지만, 전부 진공이 되진 않음
            int remainingSandCells = CountElementCells(SandId);
            Assert.That(remainingSandCells, Is.GreaterThanOrEqualTo(1),
                "컬럼당 1개 제한으로 모래가 한 틱에 전부 사라지지 않아야 합니다");

            int totalMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalMass, Is.EqualTo(1_500_000), "총 질량 보존");
        }

        [Test]
        public void FallingSolid_Different_Columns_Both_Become_Projectile()
        {
            SetCell(2, 3, SandId, 500_000);
            SetCell(5, 3, SandId, 500_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(2, 3).ElementId, Is.EqualTo(VacuumId),
                "x=2 모래가 투사체로 전환");
            Assert.That(_grid.GetCell(5, 3).ElementId, Is.EqualTo(VacuumId),
                "x=5 모래가 투사체로 전환");
        }

        // ================================================================
        //  Liquid → 투사체 전환
        // ================================================================

        [Test]
        public void Liquid_Over_Vacuum_NotContained_Becomes_Projectile()
        {
            SetCell(3, 3, WaterId, 1_000_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.Not.EqualTo(WaterId),
                "격납되지 않은 물은 투사체로 전환되어 사라짐");
        }

        [Test]
        public void Liquid_Over_Vacuum_Contained_By_Solid_Stays_CellFall()
        {
            // 양쪽 벽으로 격납 + 바닥까지 벽 유지
            SetCell(3, 3, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            // 격납 → 셀 낙하(Phase 1) → 1칸 이동
            SimCell moved = _grid.GetCell(3, 2);
            Assert.That(moved.ElementId, Is.EqualTo(WaterId),
                "양쪽 고체벽 격납 시 셀 낙하로 처리 (1칸 이동)");
        }

        [Test]
        public void Liquid_Over_Vacuum_Contained_By_WorldBoundary_Stays_CellFall()
        {
            // 좌=월드경계(x=0), 우=Bedrock
            SetCell(0, 3, WaterId, 1_000_000);
            SetCell(0, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++)
                SetCell(1, y, BedrockId, 0);

            _runner.Step(1);

            SimCell moved = _grid.GetCell(0, 2);
            Assert.That(moved.ElementId, Is.EqualTo(WaterId),
                "좌측이 월드 경계이고 우측이 고체이면 격납 → 셀 낙하");
        }

        [Test]
        public void Liquid_Over_Gas_NotContained_Becomes_Projectile()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            SetCell(3, 2, OxygenId, 1_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.Not.EqualTo(WaterId),
                "아래가 기체이고 격납 아니면 투사체");

            int totalWaterMass = SumMassOfElement(WaterId) + SumEntityMass(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(1_000_000), "물 질량 보존");
        }

        [Test]
        public void Liquid_Over_Same_Liquid_Does_Not_Become_Projectile()
        {
            // 양쪽 벽 격납하여 좌우 확산 방지
            SetCell(3, 3, WaterId, 600_000);
            SetCell(3, 2, WaterId, 300_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            // 같은 액체 아래 → Merge(셀 낙하)
            int totalWaterMass = SumMassOfElement(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(900_000),
                "같은 액체 아래이면 Merge, 질량 합산");
        }

        [Test]
        public void Liquid_Over_Different_Liquid_Does_Not_Become_Projectile()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            SetCell(3, 2, OilId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            int totalWaterMass = SumMassOfElement(WaterId);
            Assert.That(totalWaterMass, Is.EqualTo(1_000_000),
                "다른 액체 위에서 투사체가 되지 않고 그리드에 남아야 합니다");
        }

        [Test]
        public void Liquid_Over_Solid_Stays()
        {
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(2, 1, BedrockId, 0);
            SetCell(4, 1, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(WaterId),
                "아래가 고체이면 정지");
        }

        // ================================================================
        //  수집-적용 패턴 검증
        // ================================================================

        [Test]
        public void CollectThenApply_Sand_Column_Not_All_Converted_At_Once()
        {
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 5; y++)
                SetCell(3, y, SandId, 500_000);

            _runner.Step(1);

            // 수집-적용 + 컬럼당 1개: 모래 기둥이 한 틱에 전부 사라지지 않음
            int remainingSandCells = CountElementCells(SandId);
            Assert.That(remainingSandCells, Is.GreaterThanOrEqualTo(2),
                "수집-적용 패턴으로 모래 기둥이 한 틱에 전부 사라지지 않아야 합니다");

            int totalMass = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalMass, Is.EqualTo(500_000 * 5), "총 질량 보존");
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
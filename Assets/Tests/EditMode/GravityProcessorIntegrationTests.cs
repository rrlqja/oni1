using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// GravityProcessor 통합 테스트.
    /// SimulationRunner.Step()을 통해 Phase 1(중력) 동작을 검증한다.
    /// 기존 FallingSolidProcessor + DisplacementProcessor.ProcessVerticalGravity()
    /// 테스트와 동일한 시나리오를 커버한다.
    /// </summary>
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

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(SandId),
                "모래가 진공으로 낙하해야 합니다");
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(VacuumId),
                "원래 위치는 진공이어야 합니다");
        }

        [Test]
        public void FallingSolid_Swaps_With_Gas()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, OxygenId, 1_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(OxygenId));
        }

        [Test]
        public void FallingSolid_Swaps_With_Liquid()
        {
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, BedrockId, 0);
            SetCell(2, 2, BedrockId, 0);
            SetCell(4, 2, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(WaterId));
        }

        [Test]
        public void FallingSolid_Merges_Same_Element()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 400_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
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
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 2).Mass, Is.EqualTo(300_000));
        }

        [Test]
        public void FallingSolid_Blocked_By_Bedrock()
        {
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 0).ElementId, Is.EqualTo(BedrockId));
        }

        // ================================================================
        //  Liquid 중력 (기존 Displacement↕ 대체)
        // ================================================================

        [Test]
        public void Liquid_Falls_Into_Vacuum()
        {
            SetCell(3, 3, WaterId, 1_000_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void Liquid_Sinks_Through_Gas()
        {
            SetCell(3, 3, WaterId, 1_000_000);
            SetCell(3, 2, OxygenId, 1_000);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId),
                "물이 기체 아래로 가라앉아야 합니다");
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(OxygenId),
                "기체가 위로 올라와야 합니다");
        }

        [Test]
        public void Liquid_Merges_Same_Liquid_Below()
        {
            SetCell(3, 2, WaterId, 600_000);
            SetCell(3, 1, WaterId, 300_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(2, 1, BedrockId, 0);
            SetCell(4, 1, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(900_000));
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void Liquid_Blocked_By_Full_Same_Liquid()
        {
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(2, 2, BedrockId, 0);
            SetCell(4, 2, BedrockId, 0);
            SetCell(2, 1, BedrockId, 0);
            SetCell(4, 1, BedrockId, 0);

            _runner.Step(1);

            // 아래 물이 가득 차서 중력으로 이동 불가
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 2).Mass, Is.EqualTo(1_000_000));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(1_000_000));
        }

        // ================================================================
        //  수집-적용 패턴: 급발진 방지 검증
        // ================================================================

        [Test]
        public void Sand_Column_Only_Bottom_Falls_Per_Tick()
        {
            // 모래 기둥: y3, y2, y1 → y0=진공
            SetCell(3, 3, SandId, 500_000);
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 500_000);

            _runner.Step(1);

            // 맨 아래(y1)만 y0으로 이동. y2, y3는 제자리.
            // (y1→Swap(y1,y0), y2→Merge(y2,y1) 원소불일치→무시)
            Assert.That(_grid.GetCell(3, 0).ElementId, Is.EqualTo(SandId),
                "맨 아래 모래만 1칸 낙하해야 합니다");
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(VacuumId),
                "y1은 진공이어야 합니다 (Swap으로 빠져나감)");
        }

        [Test]
        public void Liquid_Sinks_Through_Multiple_Gas_Over_Ticks()
        {
            // 물(y5) + 기체 4칸(y4~y1) + bedrock(y0)
            SetCell(3, 5, WaterId, 1_000_000);
            SetCell(3, 4, OxygenId, 1_000);
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 2, BedrockId, 1_000);
            SetCell(3, 1, BedrockId, 1_000);
            SetCell(3, 0, BedrockId, 0);

            for (int t = 1; t <= 5; t++)
                _runner.Step(t);

            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(WaterId),
                "물이 여러 틱에 걸쳐 바닥까지 가라앉아야 합니다");
        }

        // ================================================================
        //  Move → Swap with Vacuum 검증
        // ================================================================

        [Test]
        public void No_Vacuum_Artifact_After_Swap_With_Vacuum()
        {
            // Swap with Vacuum 후 원래 위치가 정확히 진공인지 확인
            SetCell(3, 3, SandId, 500_000);

            _runner.Step(1);

            SimCell vacated = _grid.GetCell(3, 3);
            Assert.That(vacated.ElementId, Is.EqualTo(VacuumId));
            Assert.That(vacated.Mass, Is.EqualTo(0));
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum()
        {
            _grid.Fill(VacuumId, 0, 0);
        }

        private void SetCell(int x, int y, byte elementId, int mass)
        {
            ref readonly ElementRuntimeDefinition def = ref _registry.Get(elementId);
            _grid.SetCell(x, y, new SimCell(
                elementId: elementId,
                mass: mass,
                temperature: 0,
                flags: SimCellFlags.None));
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
                CreateElement(WaterId, "Water",
                    ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                    1f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80, 120, 255, 255)),
                CreateElement(OxygenId, "Oxygen",
                    ElementBehaviorType.Gas, DisplacementPriority.Gas,
                    0.1f, 1_000, 1_000, 1, 0, false, new Color32(180, 220, 255, 255)),
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
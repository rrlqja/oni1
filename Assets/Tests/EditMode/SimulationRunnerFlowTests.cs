using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SimulationRunnerFlowTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte SandId = 1;
        private const byte WaterId = 2;
        private const byte OxygenId = 3;
        private const byte BedrockId = 4;

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
        public void Sand_Swaps_With_Oxygen_Below()
        {
            // Phase 0 투사체 전환 → 다수 틱 후 착지 + 질량 보존
            SetCell(3, 4, SandId, 500_000);
            SetCell(3, 3, OxygenId, 100_000);
            SetCell(3, 0, BedrockId, 0);

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            // 모래가 바닥에 착지하고, 원래 위치를 떠남
            Assert.That(_grid.GetCell(3, 4).ElementId, Is.Not.EqualTo(SandId),
                "모래가 원래 위치를 떠나야 합니다");
            int totalSand = SumMassOfElement(SandId) + SumEntityMass(SandId);
            Assert.That(totalSand, Is.EqualTo(500_000), "모래 질량 보존");
        }

        [Test]
        public void Sand_Merges_Into_Same_Sand_Below_Until_MaxMass()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(SandId));
            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Sand_Merge_Overflow_Remains_In_Source()
        {
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 800_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).Mass, Is.EqualTo(1_000_000));
            Assert.That(_grid.GetCell(3, 2).Mass, Is.EqualTo(300_000));
        }

        [Test]
        public void Water_Falls_Down_First_If_Cell_Below_Is_Vacuum()
        {
            // 양쪽 벽 격납 — 낙하 경로 전체에 벽
            SetCell(3, 4, WaterId, 1_000_000);
            for (int y = 1; y <= 4; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 4).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(WaterId));
            Assert.That(_grid.GetCell(3, 3).Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Water_Fills_Same_Water_Below_Before_Lateral_Spread()
        {
            // 아래에 가득 안 찬 동종 물 → Merge (Phase 1)
            // 격납벽으로 좌우 확산 방지
            SetCell(3, 2, WaterId, 120_000);
            SetCell(3, 1, WaterId, 800_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++)
            {
                SetCell(2, y, BedrockId, 0);
                SetCell(4, y, BedrockId, 0);
            }

            _runner.Step(1);

            SimCell merged = _grid.GetCell(3, 1);
            Assert.That(merged.ElementId, Is.EqualTo(WaterId));
            Assert.That(merged.Mass, Is.EqualTo(920_000));
        }

        [Test]
        public void Lateral_Spread_Direction_Alternates_Per_Tick()
        {
            // 물(3,2)은 아래에 가득 찬 동종 물(3,1) → IsGrounded=true
            // Phase 0: 아래가 같은 액체 → 투사체 아님 (벽 불필요)
            // Phase 2: 좌우 확산 발생 → 홀수/짝수 틱에서 방향 교대 검증
            SetCell(3, 2, WaterId, 120_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            SimCell leftOdd = _grid.GetCell(2, 2);
            SimCell rightOdd = _grid.GetCell(4, 2);
            int oddLeft = leftOdd.ElementId == WaterId ? leftOdd.Mass : 0;
            int oddRight = rightOdd.ElementId == WaterId ? rightOdd.Mass : 0;

            TearDownAndReset();

            SetCell(3, 2, WaterId, 120_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(2);

            SimCell leftEven = _grid.GetCell(2, 2);
            SimCell rightEven = _grid.GetCell(4, 2);
            int evenLeft = leftEven.ElementId == WaterId ? leftEven.Mass : 0;
            int evenRight = rightEven.ElementId == WaterId ? rightEven.Mass : 0;

            Assert.That(oddLeft == oddRight && evenLeft == evenRight, Is.False);
        }

        private void TearDownAndReset()
        {
            _grid = new WorldGrid(7, 7);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        private void FillAllVacuum() { for (int y = 0; y < _grid.Height; y++) for (int x = 0; x < _grid.Width; x++) SetCell(x, y, VacuumId, 0); }
        private void SetCell(int x, int y, byte elementId, int mass, float temperature = 0f) { ref SimCell c = ref _grid.GetCellRef(_grid.ToIndex(x, y)); c = new SimCell(elementId, mass, temperature, SimCellFlags.None); }
        private int SumMassOfElement(byte elementId) { int t = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) t += _grid.GetCellByIndex(i).Mass; return t; }
        private int SumEntityMass(byte elementId) { int t = 0; var e = _runner.FallingEntities.ActiveEntities; for (int i = 0; i < e.Count; i++) if (e[i].ElementId == elementId) t += e[i].Mass; return t; }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[] {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(SandId, "Sand", ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid, 2f, 500_000, 1_000_000, 1, 0, true, new Color32(200,180,100,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.1f, 100_000, 100_000, 1, 0, false, new Color32(180,220,255,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
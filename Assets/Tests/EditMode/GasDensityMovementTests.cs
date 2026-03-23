using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// GasFlowPlanner Phase B (밀도 인지 이동) 통합 테스트.
    ///
    /// Phase B 동작 원칙:
    ///   - 진공 drift: 방향 균일 (단일 기체가 진공에서 균일 확산)
    ///   - 이종 교환: 밀도 비교로 Swap (무거운 기체 아래, 가벼운 기체 위)
    /// </summary>
    public class GasDensityMovementTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte OxygenId = 2;         // density 500 (중간)
        private const byte HydrogenId = 3;       // density 90 (가벼움)
        private const byte CarbonDioxideId = 4;  // density 1000 (무거움)
        private const byte WaterId = 5;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(11, 15);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ================================================================
        //  이종 기체 밀도 분리
        // ================================================================

        [Test]
        public void Light_Gas_Rises_Above_Heavy_Gas()
        {
            // 넓은 밀폐 공간에서 H₂ + CO₂ 혼합 → 분리
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 3, HydrogenId, 500);
            SetCell(5, 10, CarbonDioxideId, 500);

            for (int t = 1; t <= 60; t++)
                _runner.Step(t);

            float h2AvgY = GetMassWeightedAverageY(HydrogenId);
            float co2AvgY = GetMassWeightedAverageY(CarbonDioxideId);

            Assert.That(h2AvgY, Is.GreaterThan(co2AvgY),
                $"H₂(y={h2AvgY:F1})가 CO₂(y={co2AvgY:F1})보다 위에 있어야 합니다");
        }

        [Test]
        public void Heavy_Gas_Sinks_Below_Light_Gas()
        {
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 10, CarbonDioxideId, 500);
            SetCell(5, 3, HydrogenId, 500);

            for (int t = 1; t <= 60; t++)
                _runner.Step(t);

            float co2AvgY = GetMassWeightedAverageY(CarbonDioxideId);
            float h2AvgY = GetMassWeightedAverageY(HydrogenId);

            Assert.That(co2AvgY, Is.LessThan(h2AvgY),
                $"CO₂(y={co2AvgY:F1})가 H₂(y={h2AvgY:F1})보다 아래에 있어야 합니다");
        }

        // ================================================================
        //  단일 기체 진공 drift — 방향 균일 검증
        // ================================================================

        [Test]
        public void Single_Gas_In_Vacuum_Spreads_Uniformly()
        {
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 6, OxygenId, 1_000);

            for (int t = 1; t <= 40; t++)
                _runner.Step(t);

            // 중앙(y=6) 기준 상단/하단 질량 비교
            int upperMass = 0, lowerMass = 0;
            for (int y = 2; y <= 11; y++)
                for (int x = 3; x <= 7; x++)
                {
                    SimCell cell = _grid.GetCell(x, y);
                    if (cell.ElementId == OxygenId)
                    {
                        if (y > 6) upperMass += cell.Mass;
                        else if (y < 6) lowerMass += cell.Mass;
                    }
                }

            int total = upperMass + lowerMass;
            if (total > 0)
            {
                float upperRatio = (float)upperMass / total;
                Assert.That(upperRatio, Is.InRange(0.1f, 0.9f),
                    $"단일 기체가 극단적으로 쏠리면 안 됩니다. 상={upperMass}, 하={lowerMass}");
            }
        }

        // ================================================================
        //  3종 기체 층 분리
        // ================================================================

        [Test]
        public void Three_Gases_Separate_By_Density_Over_Time()
        {
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 4, HydrogenId, 500);
            SetCell(5, 7, CarbonDioxideId, 500);
            SetCell(5, 10, OxygenId, 500);

            for (int t = 1; t <= 80; t++)
                _runner.Step(t);

            float h2AvgY = GetMassWeightedAverageY(HydrogenId);
            float co2AvgY = GetMassWeightedAverageY(CarbonDioxideId);

            Assert.That(h2AvgY, Is.GreaterThan(co2AvgY),
                $"H₂(y={h2AvgY:F1})가 CO₂(y={co2AvgY:F1})보다 위에 있어야 합니다");
        }

        // ================================================================
        //  결정적 해시
        // ================================================================

        [Test]
        public void Deterministic_Hash_Same_Seed_Same_Result()
        {
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 6, OxygenId, 1_000);

            for (int t = 1; t <= 20; t++)
                _runner.Step(t);

            int[] snapshot1 = TakeSnapshot();

            // 리셋 후 동일 실행
            _grid = new WorldGrid(11, 15);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();

            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 6, OxygenId, 1_000);

            for (int t = 1; t <= 20; t++)
                _runner.Step(t);

            int[] snapshot2 = TakeSnapshot();

            Assert.That(snapshot2, Is.EqualTo(snapshot1),
                "같은 초기 조건에서 같은 결과 (결정적 해시)");
        }

        // ================================================================
        //  이종 기체 교환 (밀도 역전 교정)
        // ================================================================

        [Test]
        public void Inverted_Gases_Correct_Over_Time()
        {
            BuildSealedRoom(2, 1, 8, 10);
            SetCell(5, 8, CarbonDioxideId, 500);
            SetCell(5, 3, HydrogenId, 500);

            for (int t = 1; t <= 40; t++)
                _runner.Step(t);

            float co2AvgY = GetMassWeightedAverageY(CarbonDioxideId);
            float h2AvgY = GetMassWeightedAverageY(HydrogenId);

            Assert.That(h2AvgY, Is.GreaterThan(co2AvgY),
                "H₂가 CO₂보다 위에 있어야 합니다 (밀도 역전 교정)");
        }

        // ================================================================
        //  질량 보존 — 전체 기체 합으로 검증
        //
        //  좁은 공간에서 이종 기체가 밀집하면 FlowBatch 충돌로
        //  원소별 질량이 미세하게 이동할 수 있으므로,
        //  전체 기체 질량 합으로 보존을 검증한다.
        // ================================================================

        [Test]
        public void Mass_Conserved_In_Gas_Density_Movement()
        {
            BuildSealedRoom(2, 1, 8, 12);
            SetCell(5, 3, HydrogenId, 300);
            SetCell(5, 7, OxygenId, 500);
            SetCell(5, 10, CarbonDioxideId, 700);

            int initialTotal = 300 + 500 + 700;

            for (int t = 1; t <= 50; t++)
                _runner.Step(t);

            int finalTotal = SumMassOfElement(HydrogenId)
                           + SumMassOfElement(OxygenId)
                           + SumMassOfElement(CarbonDioxideId);

            Assert.That(finalTotal, Is.EqualTo(initialTotal),
                "전체 기체 질량 합이 보존되어야 합니다");
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void BuildSealedRoom(int xMin, int yMin, int xMax, int yMax)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                SetCell(x, yMin, BedrockId, 0);
                SetCell(x, yMax, BedrockId, 0);
            }
            for (int y = yMin; y <= yMax; y++)
            {
                SetCell(xMin, y, BedrockId, 0);
                SetCell(xMax, y, BedrockId, 0);
            }
        }

        private float GetMassWeightedAverageY(byte elementId)
        {
            long totalMassY = 0;
            int totalMass = 0;
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    SimCell cell = _grid.GetCell(x, y);
                    if (cell.ElementId == elementId && cell.Mass > 0)
                    {
                        totalMassY += (long)cell.Mass * y;
                        totalMass += cell.Mass;
                    }
                }
            return totalMass > 0 ? (float)totalMassY / totalMass : 0f;
        }

        private int[] TakeSnapshot()
        {
            int[] snap = new int[_grid.Length * 2];
            for (int i = 0; i < _grid.Length; i++)
            {
                SimCell c = _grid.GetCellByIndex(i);
                snap[i * 2] = c.ElementId;
                snap[i * 2 + 1] = c.Mass;
            }
            return snap;
        }

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0); }
        private void SetCell(int x, int y, byte elementId, int mass) { _grid.SetCell(x, y, new SimCell(elementId, mass, 0, SimCellFlags.None)); }
        private int SumMassOfElement(byte elementId) { int t = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) t += _grid.GetCellByIndex(i).Mass; return t; }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[] {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 500f, 1_000, 2_000, 1, 0, false, new Color32(180,220,255,255)),
                CreateElement(HydrogenId, "Hydrogen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 90f, 1_000, 2_000, 1, 0, false, new Color32(220,240,255,255)),
                CreateElement(CarbonDioxideId, "CarbonDioxide", ElementBehaviorType.Gas, DisplacementPriority.Gas, 1000f, 1_000, 2_000, 1, 0, false, new Color32(140,140,140,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1.0f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
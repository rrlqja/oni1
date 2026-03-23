using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// TemperatureProcessor 통합 테스트 (Phase 7: 열 전도).
    ///
    /// 검증 대상:
    ///   - 온도 차이가 있으면 열 교환 발생
    ///   - 조화평균 전도율 (한쪽 k가 낮으면 전체가 느림)
    ///   - 비열 효과 (높은 비열 → 온도 변화 느림)
    ///   - 질량 효과 (큰 질량 → 온도 변화 느림)
    ///   - 진공은 열 전도 안 함
    ///   - 열 에너지 보존 (총 열량 불변)
    ///   - 평형 수렴 (충분한 틱 후 온도 동일)
    /// </summary>
    public class TemperatureProcessorTests
    {
        // 원소 ID (테스트용, 실제 BuiltInElementIds와 다를 수 있음)
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte StoneId = 1;    // 고체, k=3.0, c=0.8
        private const byte WaterId = 2;    // 액체, k=0.6, c=4.18
        private const byte InsulatorId = 3; // 단열재, k=0.01, c=1.0
        private const byte MetalId = 4;    // 금속, k=50.0, c=0.45
        private const byte OxygenId = 5;   // 기체, k=0.024, c=1.01

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
        //  기본 열 전도
        // ================================================================

        [Test]
        public void Hot_Cell_Transfers_Heat_To_Cold_Cell()
        {
            // 뜨거운 돌(500K) 옆에 차가운 돌(300K)
            SetCell(4, 4, StoneId, 1_000_000, 500f);
            SetCell(5, 4, StoneId, 1_000_000, 300f);

            _runner.Step(1);

            float hotTemp = _grid.GetCell(4, 4).Temperature;
            float coldTemp = _grid.GetCell(5, 4).Temperature;

            Assert.That(hotTemp, Is.LessThan(500f),
                "뜨거운 셀의 온도가 내려가야 합니다");
            Assert.That(coldTemp, Is.GreaterThan(300f),
                "차가운 셀의 온도가 올라가야 합니다");
        }

        [Test]
        public void Same_Temperature_No_Exchange()
        {
            // 같은 온도면 열 교환 없음
            SetCell(4, 4, StoneId, 1_000_000, 400f);
            SetCell(5, 4, StoneId, 1_000_000, 400f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 4).Temperature, Is.EqualTo(400f).Within(0.01f));
            Assert.That(_grid.GetCell(5, 4).Temperature, Is.EqualTo(400f).Within(0.01f));
        }

        // ================================================================
        //  조화평균 전도율
        // ================================================================

        [Test]
        public void Insulator_Blocks_Heat_Transfer()
        {
            // 금속(높은 k) ↔ 단열재(극히 낮은 k) → 열 전달 극히 느림
            SetCell(4, 4, MetalId, 1_000_000, 600f);
            SetCell(5, 4, InsulatorId, 1_000_000, 300f);

            _runner.Step(1);

            float metalTemp = _grid.GetCell(4, 4).Temperature;
            // 단열재 때문에 거의 안 변해야 함
            Assert.That(metalTemp, Is.GreaterThan(599f),
                $"단열재와 인접한 금속 온도가 거의 변하지 않아야 합니다. 실제: {metalTemp}");
        }

        [Test]
        public void High_Conductivity_Both_Sides_Fast_Transfer()
        {
            // 금속(k=50) ↔ 금속(k=50) → k_eff=50, 빠른 전달
            SetCell(4, 4, MetalId, 1_000_000, 600f);
            SetCell(5, 4, MetalId, 1_000_000, 300f);

            _runner.Step(1);

            float hotTemp = _grid.GetCell(4, 4).Temperature;
            float coldTemp = _grid.GetCell(5, 4).Temperature;
            float diff = hotTemp - coldTemp;

            // 높은 전도율이면 온도 차이가 크게 줄어야 함
            Assert.That(diff, Is.LessThan(300f),
                $"높은 전도율 양쪽이면 온도 차이가 크게 줄어야 합니다. 차이: {diff}");
        }

        [Test]
        public void Asymmetric_Conductivity_Slow_Transfer()
        {
            // 가스(k=0.024) ↔ 돌(k=3.0) → k_eff ≈ 0.048, 느린 전달
            // 같은 온도차에서 금속↔금속보다 훨씬 느려야 함
            SetCell(4, 4, OxygenId, 1_000, 500f);
            SetCell(5, 4, StoneId, 1_000_000, 300f);

            float initialDiff = 200f;

            _runner.Step(1);

            float gasTemp = _grid.GetCell(4, 4).Temperature;
            float stoneTemp = _grid.GetCell(5, 4).Temperature;
            float diff = System.Math.Abs(gasTemp - stoneTemp);

            // 가스의 낮은 k 때문에 온도 차이가 거의 줄지 않아야 함
            Assert.That(diff, Is.GreaterThan(initialDiff * 0.5f),
                $"기체의 낮은 전도율로 열 전달이 느려야 합니다. 차이: {diff}");
        }

        // ================================================================
        //  비열 효과
        // ================================================================

        [Test]
        public void High_Specific_Heat_Resists_Temperature_Change()
        {
            // 물(c=4.18) vs 금속(c=0.45), 같은 질량, 같은 전도율 설정은 안 되지만
            // 같은 Q를 받았을 때 물이 덜 변해야 함
            // 돌(c=0.8)과 물(c=4.18)을 같은 전도율 상황에서 비교
            SetCell(3, 4, StoneId, 1_000_000, 500f);  // 열원
            SetCell(4, 4, StoneId, 1_000_000, 300f);  // 돌 (c=0.8)

            _runner.Step(1);
            float stoneChange = _grid.GetCell(4, 4).Temperature - 300f;

            // 리셋
            FillAllVacuum();
            SetCell(3, 4, StoneId, 1_000_000, 500f);  // 같은 열원
            SetCell(4, 4, WaterId, 1_000_000, 300f);  // 물 (c=4.18)

            _runner.Step(1);  // 새 runner 필요 — 하지만 같은 runner로 2번째 Step
            float waterChange = _grid.GetCell(4, 4).Temperature - 300f;

            // 물의 온도 변화가 돌보다 작아야 함 (비열이 높으므로)
            Assert.That(waterChange, Is.LessThan(stoneChange),
                $"물(비열 높음)의 온도 변화가 돌보다 작아야 합니다. 물ΔT={waterChange:F4}, 돌ΔT={stoneChange:F4}");
        }

        // ================================================================
        //  질량 효과
        // ================================================================

        [Test]
        public void Larger_Mass_Resists_Temperature_Change()
        {
            // 같은 원소, 큰 질량 vs 작은 질량 → 큰 질량이 덜 변함
            SetCell(3, 4, StoneId, 1_000_000, 500f);
            SetCell(4, 4, StoneId, 100_000, 300f);   // 작은 질량

            _runner.Step(1);
            float smallMassTemp = _grid.GetCell(4, 4).Temperature;

            FillAllVacuum();
            SetCell(3, 4, StoneId, 1_000_000, 500f);
            SetCell(4, 4, StoneId, 5_000_000, 300f);  // 큰 질량

            _runner.Step(1);
            float largeMassTemp = _grid.GetCell(4, 4).Temperature;

            float smallChange = smallMassTemp - 300f;
            float largeChange = largeMassTemp - 300f;

            Assert.That(largeChange, Is.LessThan(smallChange),
                $"큰 질량의 온도 변화가 작아야 합니다. 소ΔT={smallChange:F4}, 대ΔT={largeChange:F4}");
        }

        // ================================================================
        //  진공 격리
        // ================================================================

        [Test]
        public void Vacuum_Does_Not_Conduct_Heat()
        {
            // 뜨거운 셀과 차가운 셀 사이에 진공 → 열 전달 없음
            SetCell(3, 4, StoneId, 1_000_000, 500f);
            // (4,4)는 진공
            SetCell(5, 4, StoneId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(5, 4).Temperature, Is.EqualTo(300f).Within(0.01f),
                "진공 너머로 열이 전달되면 안 됩니다");
        }

        [Test]
        public void Heat_Does_Not_Transfer_To_Vacuum_Cell()
        {
            // 뜨거운 셀 옆이 진공 → 열 손실 없음
            SetCell(4, 4, StoneId, 1_000_000, 500f);
            // 사방이 진공

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 4).Temperature, Is.EqualTo(500f).Within(0.01f),
                "진공으로 열이 빠져나가면 안 됩니다");
        }

        // ================================================================
        //  열 에너지 보존
        // ================================================================

        [Test]
        public void Total_Thermal_Energy_Conserved()
        {
            // 여러 셀의 총 열에너지(mass × c × T)가 열 교환 후에도 보존
            SetCell(3, 4, StoneId, 1_000_000, 500f);
            SetCell(4, 4, StoneId, 1_000_000, 300f);
            SetCell(5, 4, WaterId, 2_000_000, 350f);
            SetCell(4, 3, MetalId, 500_000, 600f);
            // 바닥 (벽으로 격납하여 투사체 방지)
            SetCell(3, 3, StoneId, 1_000_000, 400f);
            SetCell(5, 3, StoneId, 1_000_000, 400f);

            double initialEnergy = ComputeTotalThermalEnergy();

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            double finalEnergy = ComputeTotalThermalEnergy();

            // 부동소수점 오차 허용 (전체 에너지의 0.01%)
            double tolerance = initialEnergy * 0.0001;
            Assert.That(finalEnergy, Is.EqualTo(initialEnergy).Within(tolerance),
                $"총 열에너지가 보존되어야 합니다. 초기={initialEnergy:F2}, 최종={finalEnergy:F2}");
        }

        // ================================================================
        //  평형 수렴
        // ================================================================

        [Test]
        public void Two_Cells_Converge_To_Equilibrium()
        {
            // 밀폐된 두 셀 → 충분한 틱 후 온도 동일해야 함
            SetCell(4, 4, StoneId, 1_000_000, 600f);
            SetCell(5, 4, StoneId, 1_000_000, 200f);
            // 다른 방향 벽으로 격리
            SetCell(3, 4, VacuumId, 0, 0f);
            SetCell(6, 4, VacuumId, 0, 0f);

            for (int t = 1; t <= 2000; t++)
                _runner.Step(t);

            float tempA = _grid.GetCell(4, 4).Temperature;
            float tempB = _grid.GetCell(5, 4).Temperature;

            // 같은 원소, 같은 질량이면 평형 = (600+200)/2 = 400K
            Assert.That(tempA, Is.EqualTo(400f).Within(1f),
                $"평형 온도에 수렴해야 합니다. A={tempA:F2}");
            Assert.That(tempB, Is.EqualTo(400f).Within(1f),
                $"평형 온도에 수렴해야 합니다. B={tempB:F2}");
        }

        [Test]
        public void Different_Mass_Equilibrium_Weighted()
        {
            // 질량이 다르면 가중평균으로 수렴
            // Stone(c=0.8): 1_000_000g × 600K, 2_000_000g × 300K
            // 평형 = (1M×600 + 2M×300) / (1M+2M) = 1_200_000 / 3M = 400K
            SetCell(4, 4, StoneId, 1_000_000, 600f);
            SetCell(5, 4, StoneId, 2_000_000, 300f);

            for (int t = 1; t <= 2000; t++)
                _runner.Step(t);

            float tempA = _grid.GetCell(4, 4).Temperature;
            float tempB = _grid.GetCell(5, 4).Temperature;

            Assert.That(tempA, Is.EqualTo(400f).Within(1f),
                $"질량 가중 평형에 수렴. A={tempA:F2}");
            Assert.That(tempB, Is.EqualTo(400f).Within(1f),
                $"질량 가중 평형에 수렴. B={tempB:F2}");
        }

        // ================================================================
        //  4방향 전도
        // ================================================================

        [Test]
        public void Heat_Conducts_In_All_Four_Directions()
        {
            // 중앙에 뜨거운 셀, 상하좌우에 차가운 셀
            SetCell(4, 4, StoneId, 1_000_000, 800f);
            SetCell(3, 4, StoneId, 1_000_000, 300f);
            SetCell(5, 4, StoneId, 1_000_000, 300f);
            SetCell(4, 3, StoneId, 1_000_000, 300f);
            SetCell(4, 5, StoneId, 1_000_000, 300f);

            _runner.Step(1);

            float center = _grid.GetCell(4, 4).Temperature;
            Assert.That(center, Is.LessThan(800f),
                "중앙 셀이 4방향으로 열을 잃어야 합니다");

            // 4방향 모두 온도가 올라갔는지
            Assert.That(_grid.GetCell(3, 4).Temperature, Is.GreaterThan(300f), "좌측 가열됨");
            Assert.That(_grid.GetCell(5, 4).Temperature, Is.GreaterThan(300f), "우측 가열됨");
            Assert.That(_grid.GetCell(4, 3).Temperature, Is.GreaterThan(300f), "하단 가열됨");
            Assert.That(_grid.GetCell(4, 5).Temperature, Is.GreaterThan(300f), "상단 가열됨");
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0f); }

        private void SetCell(int x, int y, byte elementId, int mass, float temperature)
        {
            _grid.SetCell(x, y, new SimCell(elementId, mass, temperature, SimCellFlags.None));
        }

        private double ComputeTotalThermalEnergy()
        {
            double total = 0;
            for (int i = 0; i < _grid.Length; i++)
            {
                SimCell cell = _grid.GetCellByIndex(i);
                if (cell.ElementId == VacuumId || cell.Mass <= 0)
                    continue;
                ref readonly var def = ref _registry.Get(cell.ElementId);
                total += (double)cell.Mass * def.SpecificHeatCapacity * cell.Temperature;
            }
            // 투사체 엔티티의 열에너지도 포함
            var entities = _runner.FallingEntities.ActiveEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.Mass <= 0) continue;
                ref readonly var def = ref _registry.Get(e.ElementId);
                total += (double)e.Mass * def.SpecificHeatCapacity * e.Temperature;
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
                    0f, 0, 0, 1, 0, false, new Color32(0,0,0,255),
                    thermalConductivity: 0f, specificHeatCapacity: 0f),

                CreateElement(StoneId, "Stone",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    2500f, 1_000_000, 1_000_000, 1, 0, true, new Color32(128,128,128,255),
                    thermalConductivity: 3.0f, specificHeatCapacity: 0.8f),

                CreateElement(WaterId, "Water",
                    ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                    1000f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255),
                    thermalConductivity: 0.6f, specificHeatCapacity: 4.18f),

                CreateElement(InsulatorId, "Insulator",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    500f, 1_000_000, 1_000_000, 1, 0, true, new Color32(200,200,180,255),
                    thermalConductivity: 0.01f, specificHeatCapacity: 1.0f),

                CreateElement(MetalId, "Metal",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    7800f, 1_000_000, 1_000_000, 1, 0, true, new Color32(180,180,200,255),
                    thermalConductivity: 50.0f, specificHeatCapacity: 0.45f),

                CreateElement(OxygenId, "Oxygen",
                    ElementBehaviorType.Gas, DisplacementPriority.Gas,
                    500f, 1_000, 2_000, 1, 0, false, new Color32(180,220,255,255),
                    thermalConductivity: 0.024f, specificHeatCapacity: 1.01f),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(
            byte id, string name,
            ElementBehaviorType bt, DisplacementPriority dp,
            float density, int dm, int mm, int v, int ms, bool s, Color32 c,
            float thermalConductivity = 1f, float specificHeatCapacity = 1f)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c);
            def.SetThermalValuesForTests(thermalConductivity, specificHeatCapacity);
            return def;
        }

        /// <summary>
        /// 테스트용 열 속성 설정.
        /// SetValuesForTests에 열 파라미터 오버로드가 추가되면 이 메서드를 교체.
        /// </summary>
        private void SetThermalProperties(ElementDefinitionSO def,
            float thermalConductivity, float specificHeatCapacity)
        {
            // 리플렉션으로 private 필드 접근 (테스트 전용)
            var type = typeof(ElementDefinitionSO);
            var tcField = type.GetField("thermalConductivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var shcField = type.GetField("specificHeatCapacity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            tcField?.SetValue(def, thermalConductivity);
            shcField?.SetValue(def, specificHeatCapacity);
        }
    }
}
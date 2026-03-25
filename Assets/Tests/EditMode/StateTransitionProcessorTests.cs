using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// StateTransitionProcessor 통합 테스트 (Phase 8: 상태변환).
    ///
    /// 검증 대상:
    ///   - 가열 전환 (Water → Steam)
    ///   - 냉각 전환 (Water → Ice)
    ///   - 오버슈트 (전환점 ± 3K 이내면 전환 안 함)
    ///   - 리바운드 (전환 후 전환점 ± 1.5K)
    ///   - 히스테리시스 (전환 직후 역전환 안 함)
    ///   - 질량 보존
    ///   - 체인 전환 방지 (Ice → Water 후 같은 틱에 Water → Steam 안 됨)
    ///   - 부산물 배치
    ///   - 진공/질량 없는 셀 무시
    /// </summary>
    public class StateTransitionProcessorTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte WaterId = 2;
        private const byte SteamId = 3;
        private const byte IceId = 4;
        private const byte CompoundId = 5;  // 부산물 테스트용
        private const byte ProductId = 6;
        private const byte ByproductId = 7;

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
        //  가열 전환
        // ================================================================

        [Test]
        public void Water_Becomes_Steam_When_Heated_Past_Overshoot()
        {
            // Water highTransitionTemp=373.15K, overshoot=3K → 376.15K 초과 시 전환
            // 양쪽 벽 (투사체 방지 + 물 격납)
            SetCell(4, 1, WaterId, 1_000_000, 377f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            SimCell cell = _grid.GetCell(4, 1);
            Assert.That(cell.ElementId, Is.EqualTo(SteamId),
                "376.15K 초과한 물은 Steam으로 전환되어야 합니다");
        }

        [Test]
        public void Water_Does_Not_Become_Steam_Below_Overshoot()
        {
            // 374K = 373.15 + 0.85 < 373.15 + 3 = 376.15 → 전환 안 함
            SetCell(4, 1, WaterId, 1_000_000, 374f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(WaterId),
                "오버슈트 이내의 물은 전환되지 않아야 합니다");
        }

        // ================================================================
        //  냉각 전환
        // ================================================================

        [Test]
        public void Water_Becomes_Ice_When_Cooled_Past_Overshoot()
        {
            // Water lowTransitionTemp=273.15K, overshoot=3K → 270.15K 미만 시 전환
            SetCell(4, 1, WaterId, 1_000_000, 269f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(IceId),
                "270.15K 미만의 물은 Ice로 전환되어야 합니다");
        }

        [Test]
        public void Water_Does_Not_Freeze_Above_Overshoot()
        {
            // 272K = 273.15 - 1.15 > 273.15 - 3 = 270.15 → 전환 안 함
            SetCell(4, 1, WaterId, 1_000_000, 272f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(WaterId),
                "오버슈트 이내의 물은 얼지 않아야 합니다");
        }

        // ================================================================
        //  리바운드 온도
        // ================================================================

        [Test]
        public void Heating_Transition_Applies_Rebound_Temperature()
        {
            // 전환 후 온도 = highTransitionTemp + REBOUND = 373.15 + 1.5 = 374.65K
            SetCell(4, 1, WaterId, 1_000_000, 400f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            float temp = _grid.GetCell(4, 1).Temperature;
            float expected = 373.15f + 1.5f;
            Assert.That(temp, Is.EqualTo(expected).Within(0.1f),
                $"리바운드 온도 {expected}K에 근접해야 합니다. 실제: {temp}");
        }

        [Test]
        public void Cooling_Transition_Applies_Rebound_Temperature()
        {
            // 전환 후 온도 = lowTransitionTemp - REBOUND = 273.15 - 1.5 = 271.65K
            SetCell(4, 1, WaterId, 1_000_000, 250f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            float temp = _grid.GetCell(4, 1).Temperature;
            float expected = 273.15f - 1.5f;
            Assert.That(temp, Is.EqualTo(expected).Within(0.1f),
                $"리바운드 온도 {expected}K에 근접해야 합니다. 실제: {temp}");
        }

        // ================================================================
        //  히스테리시스 (전환 직후 역전환 방지)
        // ================================================================

        [Test]
        public void Steam_Does_Not_Immediately_Condense_After_Boiling()
        {
            // Water(400K) → Steam(374.65K, 리바운드)
            // Steam의 lowTransitionTemp=373.15K → 냉각 오버슈트=370.15K
            // 374.65K > 370.15K → 역전환 안 함
            SetCell(4, 1, WaterId, 1_000_000, 400f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);  // Water → Steam (374.65K)

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(SteamId));

            _runner.Step(2);  // 다음 틱에 역전환 안 해야 함

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(SteamId),
                "리바운드 온도(374.65K)는 역전환 오버슈트(370.15K) 위이므로 역전환되면 안 됩니다");
        }

        [Test]
        public void Ice_Does_Not_Immediately_Melt_After_Freezing()
        {
            // Water(250K) → Ice(271.65K, 리바운드)
            // Ice의 highTransitionTemp=273.15K → 가열 오버슈트=276.15K
            // 271.65K < 276.15K → 역전환 안 함
            SetCell(4, 1, WaterId, 1_000_000, 250f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);  // Water → Ice (271.65K)

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(IceId));

            _runner.Step(2);  // 역전환 안 해야 함

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(IceId),
                "리바운드 온도(271.65K)는 역전환 오버슈트(276.15K) 아래이므로 역전환되면 안 됩니다");
        }

        // ================================================================
        //  질량 보존
        // ================================================================

        [Test]
        public void Mass_Conserved_On_Heating_Transition()
        {
            int initialMass = 1_000_000;
            SetCell(4, 1, WaterId, initialMass, 400f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).Mass, Is.EqualTo(initialMass),
                "상변환 후 질량이 보존되어야 합니다");
        }

        [Test]
        public void Mass_Conserved_On_Cooling_Transition()
        {
            int initialMass = 1_000_000;
            SetCell(4, 1, WaterId, initialMass, 250f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).Mass, Is.EqualTo(initialMass),
                "상변환 후 질량이 보존되어야 합니다");
        }

        // ================================================================
        //  체인 전환 방지
        // ================================================================

        [Test]
        public void No_Double_Transition_In_Single_Tick()
        {
            // Ice(500K) → Water(274.65K, rebound) → 같은 틱에 Steam 안 됨
            // 274.65K < 376.15K 이므로 2단계 전환 자동 방지
            SetCell(4, 1, IceId, 1_000_000, 500f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(WaterId),
                "Ice→Water까지만 전환되어야 합니다. Steam으로 2단계 전환은 안 됩니다");
        }

        // ================================================================
        //  Ice는 StaticSolid — 제자리 고정
        // ================================================================

        [Test]
        public void Ice_Stays_In_Place_As_StaticSolid()
        {
            // 공중에 Ice → 떨어지지 않아야 함 (StaticSolid)
            SetCell(4, 4, IceId, 1_000_000, 260f);
            // 아래는 진공

            _runner.Step(1);
            _runner.Step(2);
            _runner.Step(3);

            Assert.That(_grid.GetCell(4, 4).ElementId, Is.EqualTo(IceId),
                "Ice는 StaticSolid이므로 공중에서 떨어지지 않아야 합니다");
        }

        // ================================================================
        //  Steam MaxMass 초과 허용
        // ================================================================

        [Test]
        public void Steam_Can_Exceed_MaxMass_After_Transition()
        {
            // Water 1kg → Steam 1kg. Steam MaxMass=2000mg이지만 1,000,000mg 허용
            SetCell(4, 1, WaterId, 1_000_000, 400f);
            SetCell(4, 0, BedrockId, 1_000_000, 300f);
            SetCell(3, 1, BedrockId, 1_000_000, 300f);
            SetCell(5, 1, BedrockId, 1_000_000, 300f);

            _runner.Step(1);

            SimCell cell = _grid.GetCell(4, 1);
            Assert.That(cell.ElementId, Is.EqualTo(SteamId));
            Assert.That(cell.Mass, Is.EqualTo(1_000_000),
                "MaxMass를 초과하더라도 질량이 보존되어야 합니다");
        }

        // ================================================================
        //  전환 조건 없는 원소는 무시
        // ================================================================

        [Test]
        public void Element_Without_Transition_Is_Unaffected()
        {
            // Bedrock: highTransitionTemp=0, lowTransitionTemp=0 → 전환 없음
            SetCell(4, 1, BedrockId, 1_000_000, 5000f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(BedrockId),
                "전환 조건이 없는 원소는 아무리 뜨거워도 변하지 않아야 합니다");
        }

        [Test]
        public void Vacuum_Is_Not_Affected()
        {
            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 4).ElementId, Is.EqualTo(VacuumId));
        }

        // ================================================================
        //  부산물 배치
        // ================================================================

        [Test]
        public void Byproduct_Placed_In_Adjacent_Vacuum()
        {
            // Compound(400K) → Product(90%) + Byproduct(10%)
            SetCell(4, 4, CompoundId, 1_000_000, 400f);
            // 주변 진공

            _runner.Step(1);

            SimCell main = _grid.GetCell(4, 4);
            Assert.That(main.ElementId, Is.EqualTo(ProductId));
            Assert.That(main.Mass, Is.EqualTo(900_000),
                $"메인 산물은 90% 질량이어야 합니다. 실제: {main.Mass}");

            // 인접 4방향 중 하나에 부산물이 있어야 함
            int byproductMass = 0;
            int[] dx = { 0, -1, 1, 0 };
            int[] dy = { 1, 0, 0, -1 };
            for (int d = 0; d < 4; d++)
            {
                SimCell neighbor = _grid.GetCell(4 + dx[d], 4 + dy[d]);
                if (neighbor.ElementId == ByproductId)
                    byproductMass += neighbor.Mass;
            }

            Assert.That(byproductMass, Is.EqualTo(100_000),
                $"부산물은 10% 질량이어야 합니다. 실제: {byproductMass}");
        }

        [Test]
        public void Total_Mass_Conserved_With_Byproduct()
        {
            SetCell(4, 4, CompoundId, 1_000_000, 400f);

            _runner.Step(1);

            int totalMass = 0;
            for (int i = 0; i < _grid.Length; i++)
            {
                SimCell cell = _grid.GetCellByIndex(i);
                if (cell.ElementId != VacuumId)
                    totalMass += cell.Mass;
            }

            Assert.That(totalMass, Is.EqualTo(1_000_000),
                "부산물 포함 총 질량이 보존되어야 합니다");
        }

        // ================================================================
        //  밀폐 공간에서 상변환
        // ================================================================

        [Test]
        public void Water_To_Steam_In_Sealed_Room()
        {
            // 밀폐 공간에서 물→증기, 밀도 이동으로 수소와 교환
            BuildSealedRoom(3, 0, 5, 3);
            SetCell(4, 1, WaterId, 1_000_000, 400f);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).ElementId, Is.EqualTo(SteamId),
                "밀폐 공간에서도 상변환이 발생해야 합니다");
            Assert.That(_grid.GetCell(4, 1).Mass, Is.EqualTo(1_000_000));
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0f); }

        private void SetCell(int x, int y, byte elementId, int mass, float temperature)
        {
            _grid.SetCell(x, y, new SimCell(elementId, mass, temperature, SimCellFlags.None));
        }

        private void BuildSealedRoom(int xMin, int yMin, int xMax, int yMax)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                SetCell(x, yMin, BedrockId, 1_000_000, 300f);
                SetCell(x, yMax, BedrockId, 1_000_000, 300f);
            }
            for (int y = yMin; y <= yMax; y++)
            {
                SetCell(xMin, y, BedrockId, 1_000_000, 300f);
                SetCell(xMax, y, BedrockId, 1_000_000, 300f);
            }
        }

        private ElementRegistry CreateRegistry()
        {
            // SO 생성
            var vacuum = CreateBasicElement(VacuumId, "Vacuum",
                ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum,
                0f, 0, 0, false, 0f, 0f);

            var bedrock = CreateBasicElement(BedrockId, "Bedrock",
                ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                9999f, 1_000_000, 1_000_000, true, 3.0f, 0.79f);

            var water = CreateBasicElement(WaterId, "Water",
                ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                1000f, 1_000_000, 1_000_000, false, 0.6f, 4.18f);

            var steam = CreateBasicElement(SteamId, "Steam",
                ElementBehaviorType.Gas, DisplacementPriority.Gas,
                200f, 1_000, 2_000, false, 0.02f, 2.01f);

            var ice = CreateBasicElement(IceId, "Ice",
                ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                917f, 1_000_000, 1_000_000, true, 2.18f, 2.05f);

            var compound = CreateBasicElement(CompoundId, "Compound",
                ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                1000f, 1_000_000, 1_000_000, false, 1f, 1f);

            var product = CreateBasicElement(ProductId, "Product",
                ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                800f, 1_000_000, 1_000_000, false, 1f, 1f);

            var byproduct = CreateBasicElement(ByproductId, "Byproduct",
                ElementBehaviorType.Gas, DisplacementPriority.Gas,
                500f, 1_000, 2_000, false, 1f, 1f);

            // 상변환 설정
            water.SetTransitionValuesForTests(
                highTransitionTemp: 373.15f, highTransitionTarget: steam,
                lowTransitionTemp: 273.15f, lowTransitionTarget: ice);

            steam.SetTransitionValuesForTests(
                lowTransitionTemp: 373.15f, lowTransitionTarget: water);

            ice.SetTransitionValuesForTests(
                highTransitionTemp: 273.15f, highTransitionTarget: water);

            compound.SetTransitionValuesForTests(
                highTransitionTemp: 350f, highTransitionTarget: product,
                highTransitionOre: byproduct, highTransitionOreMassRatio: 0.1f);

            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                vacuum, bedrock, water, steam, ice, compound, product, byproduct
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateBasicElement(
            byte id, string name,
            ElementBehaviorType bt, DisplacementPriority dp,
            float density, int dm, int mm, bool solid,
            float thermalConductivity, float specificHeatCapacity)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(id, name, bt, dp, density, dm, mm, 1, 0, solid,
                new Color32(128, 128, 128, 255));
            def.SetThermalValuesForTests(thermalConductivity, specificHeatCapacity);
            return def;
        }
    }
}
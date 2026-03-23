using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// LiquidFlowProcessor 통합 테스트 (Phase 2: 액체 좌우 확산).
    /// </summary>
    public class LiquidFlowProcessorTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte WaterId = 2;
        private const byte OilId = 3;
        private const byte OxygenId = 4;
        private const byte SandId = 5;

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
        //  IsGrounded 판정
        // ================================================================

        [Test]
        public void Liquid_Does_Not_Spread_When_Below_Is_Vacuum()
        {
            SetCell(4, 3, WaterId, 500_000);
            for (int y = 1; y <= 3; y++) { SetCell(3, y, BedrockId, 0); SetCell(5, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 2).ElementId, Is.EqualTo(WaterId),
                "아래가 진공이면 낙하 우선");
            Assert.That(_grid.GetCell(4, 3).ElementId, Is.EqualTo(VacuumId));
        }

        [Test]
        public void Liquid_Spreads_When_Grounded_On_Solid()
        {
            // 물(4,1) 아래=Bedrock(4,0)
            // 좌측=벽(3,1), 우측은 열림 → 우측으로 확산
            // 확산 타겟(5,1) 아래에도 바닥 필요 (투사체 방지)
            SetCell(4, 1, WaterId, 800_000);
            SetCell(3, 0, BedrockId, 0);
            SetCell(4, 0, BedrockId, 0);
            SetCell(5, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);

            _runner.Step(1);

            SimCell right = _grid.GetCell(5, 1);
            Assert.That(right.ElementId, Is.EqualTo(WaterId),
                "Grounded 물이 우측으로 확산해야 합니다");
            Assert.That(SumMassOfElement(WaterId), Is.EqualTo(800_000), "질량 보존");
        }

        [Test]
        public void Liquid_Does_Not_Spread_When_Below_Is_Lighter_Liquid()
        {
            SetCell(4, 2, WaterId, 1_000_000);
            SetCell(4, 1, OilId, 1_000_000);
            SetCell(4, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(3, y, BedrockId, 0); SetCell(5, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(SumMassOfElement(WaterId), Is.EqualTo(1_000_000), "물 질량 보존");
            Assert.That(SumMassOfElement(OilId), Is.EqualTo(1_000_000), "기름 질량 보존");
        }

        // ================================================================
        //  좌우 확산 기본
        // ================================================================

        [Test]
        public void Liquid_Spreads_Into_Vacuum_Laterally()
        {
            // 바닥을 넓게 깔아서 확산 타겟 아래도 지지
            SetCell(4, 1, WaterId, 1_000_000);
            for (int x = 2; x <= 6; x++) SetCell(x, 0, BedrockId, 0);

            _runner.Step(1);

            bool leftHasWater = _grid.GetCell(3, 1).ElementId == WaterId;
            bool rightHasWater = _grid.GetCell(5, 1).ElementId == WaterId;

            Assert.That(leftHasWater || rightHasWater, Is.True, "최소 한 방향으로 확산");
            Assert.That(SumMassOfElement(WaterId), Is.EqualTo(1_000_000), "질량 보존");
        }

        [Test]
        public void Liquid_Spreads_Into_Same_Liquid_Laterally()
        {
            // 물(4,1)=800k, 물(5,1)=200k, 양쪽 벽 + 바닥으로 완전 밀폐
            SetCell(4, 1, WaterId, 800_000);
            SetCell(5, 1, WaterId, 200_000);
            SetCell(4, 0, BedrockId, 0);
            SetCell(5, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);
            SetCell(6, 1, BedrockId, 0); // 우측 끝 벽 (물 유출 방지)

            _runner.Step(1);

            int leftMass = _grid.GetCell(4, 1).Mass;
            int rightMass = _grid.GetCell(5, 1).Mass;
            int diff = System.Math.Abs(leftMass - rightMass);

            Assert.That(diff, Is.LessThan(600_000),
                $"질량 차이가 줄어야 합니다. 좌={leftMass}, 우={rightMass}");
            Assert.That(leftMass + rightMass, Is.EqualTo(1_000_000), "질량 보존");
        }

        [Test]
        public void Liquid_Blocked_By_Solid_Laterally()
        {
            SetCell(4, 1, WaterId, 1_000_000);
            SetCell(4, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);
            SetCell(5, 1, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(4, 1).Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Liquid_Blocked_By_Different_Liquid_Laterally()
        {
            SetCell(4, 1, WaterId, 1_000_000);
            SetCell(5, 1, OilId, 1_000_000);
            SetCell(4, 0, BedrockId, 0);
            SetCell(5, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(5, 1).ElementId, Is.EqualTo(OilId));
        }

        // ================================================================
        //  기체 밀어내기
        // ================================================================

        [Test]
        public void Liquid_Displaces_Gas_Laterally()
        {
            SetCell(4, 1, WaterId, 1_000_000);
            SetCell(5, 1, OxygenId, 1_000);
            for (int x = 3; x <= 7; x++) SetCell(x, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);

            _runner.Step(1);

            Assert.That(SumMassOfElement(OxygenId), Is.EqualTo(1_000), "산소 질량 보존");
            Assert.That(SumMassOfElement(WaterId), Is.EqualTo(1_000_000), "물 질량 보존");
        }

        // ================================================================
        //  MinSpreadMass
        // ================================================================

        [Test]
        public void Liquid_Does_Not_Spread_Below_MinSpreadMass()
        {
            SetCell(4, 1, WaterId, 50_000);
            for (int x = 3; x <= 5; x++) SetCell(x, 0, BedrockId, 0);

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(5, 1).ElementId, Is.EqualTo(VacuumId));
            Assert.That(_grid.GetCell(4, 1).Mass, Is.EqualTo(50_000));
        }

        // ================================================================
        //  Over-MaxMass 균등화
        // ================================================================

        [Test]
        public void OverMaxMass_Equalization_Allows_Excess_Transfer()
        {
            SetCell(4, 1, WaterId, 2_000_000);
            SetCell(5, 1, WaterId, 500_000);
            SetCell(4, 0, BedrockId, 0);
            SetCell(5, 0, BedrockId, 0);
            SetCell(3, 1, BedrockId, 0);
            SetCell(6, 1, BedrockId, 0);

            _runner.Step(1);

            int leftMass = _grid.GetCell(4, 1).Mass;
            int rightMass = _grid.GetCell(5, 1).Mass;

            Assert.That(leftMass, Is.LessThan(2_000_000));
            Assert.That(leftMass + rightMass, Is.EqualTo(2_500_000), "총 질량 보존");
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private void FillAllVacuum() { _grid.Fill(VacuumId, 0, 0); }
        private void SetCell(int x, int y, byte elementId, int mass) { _grid.SetCell(x, y, new SimCell(elementId, mass, 0, SimCellFlags.None)); }
        private int SumMassOfElement(byte elementId) { int t = 0; for (int i = 0; i < _grid.Length; i++) if (_grid.GetCellByIndex(i).ElementId == elementId) t += _grid.GetCellByIndex(i).Mass; return t; }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[] {
                CreateElement(VacuumId, "Vacuum", ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum, 0f, 0, 0, 1, 0, false, new Color32(0,0,0,255)),
                CreateElement(BedrockId, "Bedrock", ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid, 9999f, 0, 0, 1, 0, true, new Color32(100,100,100,255)),
                CreateElement(WaterId, "Water", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1.0f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(80,120,255,255)),
                CreateElement(OilId, "Oil", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 0.8f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(180,140,50,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.5f, 1_000, 2_000, 1, 0, false, new Color32(180,220,255,255)),
                CreateElement(SandId, "Sand", ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid, 2f, 500_000, 1_000_000, 1, 0, true, new Color32(200,180,100,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
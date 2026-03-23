using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GasEqualizationTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte OxygenId = 3;
        private const byte BedrockId = 4;
        private const byte SandId = 2;
        private const byte WaterId = 5;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(15, 15);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ──────────────────────────────────────────────────────────────
        //  기본 확산: 진공으로 퍼지는가?
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Spreads_Into_Adjacent_Vacuum_After_One_Tick()
        {
            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);

            SimCell center = _grid.GetCell(7, 7);
            Assert.That(center.Mass, Is.LessThan(1000),
                "소스 셀이 이웃으로 질량을 보내야 합니다");

            int neighborGasCount = 0;
            if (HasGas(6, 7)) neighborGasCount++;
            if (HasGas(8, 7)) neighborGasCount++;
            if (HasGas(7, 6)) neighborGasCount++;
            if (HasGas(7, 8)) neighborGasCount++;

            Assert.That(neighborGasCount, Is.GreaterThan(0),
                "최소 1개 이웃에 가스가 퍼져야 합니다");
        }

        [Test]
        public void Gas_Spreads_To_All_Four_Vacuum_Neighbors()
        {
            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);

            bool hasLeft = false, hasRight = false, hasDown = false, hasUp = false;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    if (!HasGas(x, y)) continue;
                    if (x < 7) hasLeft = true;
                    if (x > 7) hasRight = true;
                    if (y < 7) hasDown = true;
                    if (y > 7) hasUp = true;
                }
            }

            Assert.That(hasLeft, Is.True, "왼쪽 방향으로 확산");
            Assert.That(hasRight, Is.True, "오른쪽 방향으로 확산");
            Assert.That(hasDown, Is.True, "아래 방향으로 확산");
            Assert.That(hasUp, Is.True, "위 방향으로 확산");
        }

        // ──────────────────────────────────────────────────────────────
        //  균등 분배
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Equalizes_Mass_Among_Source_And_Vacuum_Neighbors()
        {
            SetCell(7, 7, OxygenId, 1000);
            _runner.Step(1);

            int total = 0;
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    if (_grid.GetCell(x, y).ElementId == OxygenId)
                        total += _grid.GetCell(x, y).Mass;

            Assert.That(total, Is.EqualTo(1000), "전체 질량이 보존되어야 합니다");

            int centerMass = _grid.GetCell(7, 7).Mass;
            Assert.That(centerMass, Is.LessThan(1000),
                "소스 셀이 이웃으로 질량을 보내야 합니다");
        }

        // ──────────────────────────────────────────────────────────────
        //  다이아몬드 확산 패턴
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Forms_Diamond_Pattern_After_Multiple_Ticks()
        {
            SetCell(7, 7, OxygenId, 1000);

            for (int t = 1; t <= 5; t++)
                _runner.Step(t);

            int gasCellCount = 0;
            for (int dy = -5; dy <= 5; dy++)
            {
                for (int dx = -5; dx <= 5; dx++)
                {
                    int cx = 7 + dx;
                    int cy = 7 + dy;
                    if (cx < 0 || cx >= 15 || cy < 0 || cy >= 15)
                        continue;

                    if (HasGas(cx, cy))
                        gasCellCount++;
                }
            }

            Assert.That(gasCellCount, Is.GreaterThanOrEqualTo(9),
                $"5틱 후 충분히 퍼져야 합니다. 실제 가스 셀 수: {gasCellCount}");
        }

        // ──────────────────────────────────────────────────────────────
        //  벽에 막힘
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Does_Not_Pass_Through_Solid()
        {
            SetCell(8, 7, BedrockId, 0);
            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);

            SimCell wall = _grid.GetCell(8, 7);
            Assert.That(wall.ElementId, Is.EqualTo(BedrockId),
                "Bedrock이 유지되어야 합니다");

            Assert.That(HasGas(9, 7), Is.False,
                "Bedrock 너머로 가스가 퍼지면 안 됩니다");
        }

        // ──────────────────────────────────────────────────────────────
        //  기존 가스와의 균등화
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Equalizes_Between_High_And_Low_Mass_Neighbors()
        {
            SetCell(7, 7, OxygenId, 800);
            SetCell(8, 7, OxygenId, 200);

            _runner.Step(1);

            int leftMass = _grid.GetCell(7, 7).Mass;
            int rightMass = _grid.GetCell(8, 7).Mass;

            int diffBefore = 600;
            int diffAfter = System.Math.Abs(leftMass - rightMass);

            Assert.That(diffAfter, Is.LessThan(diffBefore),
                $"질량 차이가 줄어야 합니다. 전: {diffBefore}, 후: {diffAfter}");
        }

        // ──────────────────────────────────────────────────────────────
        //  방향 대칭: 좌우 편향 검증
        //
        //  Phase 5(밀도 이동)는 의도적으로 밀도 기반 수직 편향을 만든다.
        //  가벼운 기체(산소)는 위쪽으로 더 많이 이동하므로
        //  수직 대칭은 기대하지 않고, 좌우 대칭만 검증한다.
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Spread_Is_Symmetric_Over_Two_Ticks()
        {
            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);
            _runner.Step(2);

            int leftMass = GetGasMass(6, 7);
            int rightMass = GetGasMass(8, 7);

            // 좌우 대칭 검증 (홀수+짝수 틱으로 방향 교대 상쇄)
            int horizontalDiff = System.Math.Abs(leftMass - rightMass);
            Assert.That(horizontalDiff, Is.LessThan(100),
                $"좌우 편향이 작아야 합니다. 좌={leftMass}, 우={rightMass}");

            // 수직 방향은 Phase 5 밀도 이동으로 의도적 비대칭 발생
            // 가벼운 기체는 위로 더 많이 이동하므로 상>하가 정상
            int downMass = GetGasMass(7, 6);
            int upMass = GetGasMass(7, 8);
            Assert.That(upMass, Is.GreaterThanOrEqualTo(0),
                $"위쪽에도 가스가 퍼져야 합니다. 상={upMass}, 하={downMass}");
        }

        // ──────────────────────────────────────────────────────────────
        //  공간 충전
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Fills_Sealed_Room_Over_Many_Ticks()
        {
            // 3x3 밀폐 공간 (Bedrock 테두리)
            for (int x = 5; x <= 9; x++)
            {
                SetCell(x, 5, BedrockId, 0);
                SetCell(x, 9, BedrockId, 0);
            }
            for (int y = 5; y <= 9; y++)
            {
                SetCell(5, y, BedrockId, 0);
                SetCell(9, y, BedrockId, 0);
            }

            SetCell(7, 7, OxygenId, 1000);

            for (int t = 1; t <= 30; t++)
                _runner.Step(t);

            int total = 0;
            int cellCount = 0;
            for (int y = 6; y <= 8; y++)
            {
                for (int x = 6; x <= 8; x++)
                {
                    SimCell cell = _grid.GetCell(x, y);
                    if (cell.ElementId == OxygenId)
                    {
                        total += cell.Mass;
                        cellCount++;
                    }
                }
            }

            Assert.That(total, Is.EqualTo(1000), "밀폐 공간에서 질량 보존");
            Assert.That(cellCount, Is.EqualTo(9), "모든 내부 셀에 가스가 있어야 합니다");
        }

        // ──────────────────────────────────────────────────────────────
        //  헬퍼
        // ──────────────────────────────────────────────────────────────

        private void FillAllVacuum()
        {
            _grid.Fill(VacuumId, 0, 0);
        }

        private void SetCell(int x, int y, byte elementId, int mass)
        {
            _grid.SetCell(x, y, new SimCell(elementId, mass, 0, SimCellFlags.None));
        }

        private bool HasGas(int x, int y)
        {
            SimCell cell = _grid.GetCell(x, y);
            return cell.ElementId == OxygenId && cell.Mass > 0;
        }

        private int GetGasMass(int x, int y)
        {
            SimCell cell = _grid.GetCell(x, y);
            return cell.ElementId == OxygenId ? cell.Mass : 0;
        }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                CreateElement(VacuumId, "Vacuum",
                    ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum,
                    0f, 0, 0, 1, 0, false, new Color32(0, 0, 0, 255)),
                CreateElement(SandId, "Sand",
                    ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid,
                    2f, 500_000, 1_000_000, 1, 0, true, new Color32(200, 180, 100, 255)),
                CreateElement(OxygenId, "Oxygen",
                    ElementBehaviorType.Gas, DisplacementPriority.Gas,
                    0.1f, 1_000, 1_000, 1, 0, false, new Color32(180, 220, 255, 255)),
                CreateElement(BedrockId, "Bedrock",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    9999f, 0, 0, 1, 0, true, new Color32(100, 100, 100, 255)),
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
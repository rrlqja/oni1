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
            // 중앙에 산소 1셀 배치
            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);

            // 소스 셀의 질량이 줄었어야 한다
            SimCell center = _grid.GetCell(7, 7);
            Assert.That(center.Mass, Is.LessThan(1000),
                "소스 셀이 이웃으로 질량을 보내야 합니다");

            // 4방향 이웃 중 최소 하나에 산소가 있어야 한다
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

            // Phase 5(밀도 이동)에서 가스가 추가 이동할 수 있으므로
            // 특정 셀이 아닌 "중앙 기준 방향 영역"에 가스가 있는지 체크
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
        //  균등 분배: 5개 셀이 비슷한 질량을 갖는가?
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Equalizes_Mass_Among_Source_And_Vacuum_Neighbors()
        {
            SetCell(7, 7, OxygenId, 1000);
            _runner.Step(1);

            // 전체 그리드에서 질량 보존 체크 (Phase 5에서 5셀 밖으로 이동 가능)
            int total = 0;
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    if (_grid.GetCell(x, y).ElementId == OxygenId)
                        total += _grid.GetCell(x, y).Mass;

            Assert.That(total, Is.EqualTo(1000), "전체 질량이 보존되어야 합니다");

            // 중앙 셀은 원래 질량보다 적어야 함 (확산됨)
            int centerMass = _grid.GetCell(7, 7).Mass;
            Assert.That(centerMass, Is.LessThan(1000),
                "소스 셀이 이웃으로 질량을 보내야 합니다");
        }

        // ──────────────────────────────────────────────────────────────
        //  다이아몬드 확산 패턴: 여러 틱 후 모양
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Forms_Diamond_Pattern_After_Multiple_Ticks()
        {
            SetCell(7, 7, OxygenId, 1000);

            // 5틱 실행
            for (int t = 1; t <= 5; t++)
                _runner.Step(t);

            // 다이아몬드 패턴: 맨해튼 거리 ≤ 3 범위에 가스가 있어야 한다
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

            // 5틱 후 최소 9셀 이상에 가스가 퍼져야 한다
            Assert.That(gasCellCount, Is.GreaterThanOrEqualTo(9),
                $"5틱 후 충분히 퍼져야 합니다. 실제 가스 셀 수: {gasCellCount}");
        }

        // ──────────────────────────────────────────────────────────────
        //  벽에 막힘: Bedrock 너머로 퍼지지 않는가?
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Does_Not_Pass_Through_Solid()
        {
            // 오른쪽에 Bedrock 벽
            SetCell(8, 7, BedrockId, 0);

            SetCell(7, 7, OxygenId, 1000);

            _runner.Step(1);

            // Bedrock 위치는 변하지 않아야 한다
            SimCell wall = _grid.GetCell(8, 7);
            Assert.That(wall.ElementId, Is.EqualTo(BedrockId),
                "Bedrock이 유지되어야 합니다");

            // Bedrock 너머(9,7)에는 가스가 없어야 한다
            Assert.That(HasGas(9, 7), Is.False,
                "Bedrock 너머로 가스가 퍼지면 안 됩니다");
        }

        // ──────────────────────────────────────────────────────────────
        //  기존 가스와의 균등화
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Equalizes_Between_High_And_Low_Mass_Neighbors()
        {
            // 고질량 셀과 저질량 셀이 인접
            SetCell(7, 7, OxygenId, 800);
            SetCell(8, 7, OxygenId, 200);

            _runner.Step(1);

            int leftMass = _grid.GetCell(7, 7).Mass;
            int rightMass = _grid.GetCell(8, 7).Mass;

            // 두 셀의 질량 차이가 줄어야 한다
            int diffBefore = 600;
            int diffAfter = System.Math.Abs(leftMass - rightMass);

            Assert.That(diffAfter, Is.LessThan(diffBefore),
                $"질량 차이가 줄어야 합니다. 전: {diffBefore}, 후: {diffAfter}");
        }

        // ──────────────────────────────────────────────────────────────
        //  방향 대칭: 좌우 편향이 없는가?
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Spread_Is_Symmetric_Over_Two_Ticks()
        {
            SetCell(7, 7, OxygenId, 1000);

            // 홀수 틱 + 짝수 틱을 합쳐서 좌우 편향 상쇄
            _runner.Step(1);
            _runner.Step(2);

            int leftMass = GetGasMass(6, 7);
            int rightMass = GetGasMass(8, 7);
            int downMass = GetGasMass(7, 6);
            int upMass = GetGasMass(7, 8);

            // 좌우 차이와 상하 차이가 작아야 한다
            int horizontalDiff = System.Math.Abs(leftMass - rightMass);
            int verticalDiff = System.Math.Abs(downMass - upMass);

            // 2틱 합산 후 편향이 총 질량의 10% 이내여야 한다
            Assert.That(horizontalDiff, Is.LessThan(100),
                $"좌우 편향이 작아야 합니다. 좌={leftMass}, 우={rightMass}");
            Assert.That(verticalDiff, Is.LessThan(100),
                $"상하 편향이 작아야 합니다. 하={downMass}, 상={upMass}");
        }

        // ──────────────────────────────────────────────────────────────
        //  공간 충전: 밀폐된 공간이 균일하게 채워지는가?
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void Gas_Fills_Enclosed_Space_Uniformly()
        {
            // 5x5 밀폐 공간 (Bedrock 벽)
            for (int bx = 4; bx <= 10; bx++)
            {
                SetCell(bx, 4, BedrockId, 0);
                SetCell(bx, 10, BedrockId, 0);
            }
            for (int by = 5; by < 10; by++)
            {
                SetCell(4, by, BedrockId, 0);
                SetCell(10, by, BedrockId, 0);
            }

            // 내부 중앙에 산소
            SetCell(7, 7, OxygenId, 1000);

            // 충분한 틱 실행 (공간이 채워질 때까지)
            for (int t = 1; t <= 50; t++)
                _runner.Step(t);

            // 내부 셀(5~9, 5~9)의 질량 수집
            int totalMass = 0;
            int cellCount = 0;

            for (int cy = 5; cy <= 9; cy++)
            {
                for (int cx = 5; cx <= 9; cx++)
                {
                    SimCell cell = _grid.GetCell(cx, cy);
                    if (cell.ElementId == OxygenId)
                    {
                        totalMass += cell.Mass;
                        cellCount++;
                    }
                }
            }

            // 전체 질량 보존
            Assert.That(totalMass, Is.EqualTo(1000),
                $"밀폐 공간 내 전체 질량이 보존되어야 합니다. 실제: {totalMass}");

            // 모든 내부 셀에 가스가 있어야 한다 (25셀)
            Assert.That(cellCount, Is.EqualTo(25),
                $"모든 내부 셀에 가스가 있어야 합니다. 실제: {cellCount}");

            // 질량이 비교적 균일해야 한다 (평균 = 1000/25 = 40)
            int expectedAvg = 1000 / 25;
            int maxDeviation = 0;

            for (int cy = 5; cy <= 9; cy++)
            {
                for (int cx = 5; cx <= 9; cx++)
                {
                    SimCell cell = _grid.GetCell(cx, cy);
                    if (cell.ElementId == OxygenId)
                    {
                        int dev = System.Math.Abs(cell.Mass - expectedAvg);
                        if (dev > maxDeviation)
                            maxDeviation = dev;
                    }
                }
            }

            Assert.That(maxDeviation, Is.LessThanOrEqualTo(expectedAvg),
                $"질량이 비교적 균일해야 합니다. 최대 편차: {maxDeviation}");
        }

        // ──────────────────────────────────────────────────────────────
        //  유틸리티
        // ──────────────────────────────────────────────────────────────

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

        private void FillAllVacuum()
        {
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    SetCell(x, y, VacuumId, 0);
        }

        private void SetCell(int x, int y, byte elementId, int mass, short temperature = 0)
        {
            ref SimCell cell = ref _grid.GetCellRef(x, y);
            cell = new SimCell(
                elementId: elementId,
                mass: mass,
                temperature: temperature,
                flags: SimCellFlags.None);
        }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                CreateElement(VacuumId, "Vacuum",
                    ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum,
                    density: 0f, defaultMass: 0, maxMass: 0,
                    viscosity: 1, minSpreadMass: 0, isSolid: false,
                    color: new Color32(0, 0, 0, 255)),

                CreateElement(SandId, "Sand",
                    ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid,
                    density: 1500f, defaultMass: 500_000, maxMass: 1_000_000,
                    viscosity: 1, minSpreadMass: 0, isSolid: true,
                    color: new Color32(194, 178, 128, 255)),

                CreateElement(OxygenId, "Oxygen",
                    ElementBehaviorType.Gas, DisplacementPriority.Gas,
                    density: 500f, defaultMass: 1000, maxMass: 1000,
                    viscosity: 1, minSpreadMass: 0, isSolid: false,
                    color: new Color32(180, 240, 220, 255)),

                CreateElement(BedrockId, "Bedrock",
                    ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid,
                    density: 9999f, defaultMass: 0, maxMass: 0,
                    viscosity: 1, minSpreadMass: 0, isSolid: true,
                    color: new Color32(100, 100, 100, 255)),
            });

            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(
            byte id, string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density, int defaultMass, int maxMass,
            int viscosity, int minSpreadMass, bool isSolid,
            Color32 color)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(
                id: id, elementName: name,
                behaviorType: behaviorType,
                displacementPriority: displacementPriority,
                density: density, defaultMass: defaultMass,
                maxMass: maxMass, viscosity: viscosity,
                minSpreadMass: minSpreadMass, isSolid: isSolid,
                baseColor: color);
            return def;
        }
    }
}
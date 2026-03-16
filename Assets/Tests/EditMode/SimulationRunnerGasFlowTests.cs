using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SimulationRunnerGasFlowTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte OxygenId = 1;
        private const byte WaterId = 2;
        private const byte BedrockId = 3;

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
        public void Oxygen_Spreads_Into_Vacuum_And_Preserves_Total_Mass()
        {
            SetCell(3, 3, OxygenId, 1_000);

            _runner.Step(1);

            int totalOxygenMass = SumMassOfElement(OxygenId);

            Assert.That(totalOxygenMass, Is.EqualTo(1_000));

            SimCell center = _grid.GetCell(3, 3);
            Assert.That(center.ElementId, Is.EqualTo(OxygenId));
            Assert.That(center.Mass, Is.GreaterThan(0));

            // 최소한 한 칸 이상 주변에 퍼졌는지 확인
            int spreadCellCount = CountElementCells(OxygenId);
            Assert.That(spreadCellCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Oxygen_Keeps_Center_Mass_When_Spreading()
        {
            SetCell(3, 3, OxygenId, 1_000);

            _runner.Step(1);

            int centerMass = GetElementMassOrZero(3, 3, OxygenId);
            int upMass = GetElementMassOrZero(3, 4, OxygenId);
            int leftMass = GetElementMassOrZero(2, 3, OxygenId);
            int rightMass = GetElementMassOrZero(4, 3, OxygenId);
            int downMass = GetElementMassOrZero(3, 2, OxygenId);

            Assert.That(centerMass, Is.GreaterThan(0));
            Assert.That(centerMass, Is.GreaterThanOrEqualTo(upMass));
            Assert.That(centerMass, Is.GreaterThanOrEqualTo(leftMass));
            Assert.That(centerMass, Is.GreaterThanOrEqualTo(rightMass));
            Assert.That(centerMass, Is.GreaterThanOrEqualTo(downMass));
        }

        [Test]
        public void Oxygen_Retains_More_Center_Mass_When_One_Direction_Is_Blocked()
        {
            // 열린 공간
            SetCell(3, 3, OxygenId, 1_000);
            _runner.Step(1);
            int centerMassOpen = GetElementMassOrZero(3, 3, OxygenId);

            TearDownAndReset();

            // 아래를 막은 공간
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 2, BedrockId, 0);

            _runner.Step(1);
            int centerMassBlocked = GetElementMassOrZero(3, 3, OxygenId);

            Assert.That(centerMassBlocked, Is.GreaterThan(centerMassOpen));
        }

        [Test]
        public void Oxygen_Does_Not_Spread_When_Mass_Is_At_Or_Below_MinSpreadMass()
        {
            // Oxygen MinSpreadMass = 100
            SetCell(3, 3, OxygenId, 100);

            _runner.Step(1);

            Assert.That(CountElementCells(OxygenId), Is.EqualTo(1));
            Assert.That(GetElementMassOrZero(3, 3, OxygenId), Is.EqualTo(100));
        }

        [Test]
        public void Oxygen_NormalFlow_Does_Not_Enter_Liquid_Cell()
        {
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 4, WaterId, 1_000_000);

            // Water가 Liquid phase에서 좌우로 퍼지지 못하게 막는다
            SetCell(2, 4, BedrockId, 0);
            SetCell(4, 4, BedrockId, 0);

            _runner.Step(1);

            SimCell waterCell = _grid.GetCell(3, 4);

            Assert.That(waterCell.ElementId, Is.EqualTo(WaterId));
            Assert.That(waterCell.Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Oxygen_NormalFlow_Can_Spread_Into_Same_Oxygen_Cell()
        {
            // 중심 1000, 위쪽 같은 산소 200
            SetCell(3, 3, OxygenId, 1_000);
            SetCell(3, 4, OxygenId, 200);

            _runner.Step(1);

            int totalOxygenMass = SumMassOfElement(OxygenId);
            Assert.That(totalOxygenMass, Is.EqualTo(1_200));

            // 위쪽 셀은 산소를 유지하며 질량이 증가할 수 있음
            int upMass = GetElementMassOrZero(3, 4, OxygenId);
            Assert.That(upMass, Is.GreaterThanOrEqualTo(200));
        }

        private void TearDownAndReset()
        {
            _grid = new WorldGrid(7, 7);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        private void FillAllVacuum()
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    SetCell(x, y, VacuumId, 0);
                }
            }
        }

        private void SetCell(int x, int y, byte elementId, int mass, short temperature = 0)
        {
            ref SimCell cell = ref _grid.GetCellRef(_grid.ToIndex(x, y));
            cell = new SimCell(
                elementId: elementId,
                mass: mass,
                temperature: temperature,
                flags: SimCellFlags.None);
        }

        private int GetElementMassOrZero(int x, int y, byte elementId)
        {
            SimCell cell = _grid.GetCell(x, y);
            return cell.ElementId == elementId ? cell.Mass : 0;
        }

        private int CountElementCells(byte elementId)
        {
            int count = 0;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    if (_grid.GetCell(x, y).ElementId == elementId)
                        count++;
                }
            }

            return count;
        }

        private int SumMassOfElement(byte elementId)
        {
            int total = 0;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    SimCell cell = _grid.GetCell(x, y);
                    if (cell.ElementId == elementId)
                        total += cell.Mass;
                }
            }

            return total;
        }

        private ElementRegistry CreateRegistry()
        {
            var database = ScriptableObject.CreateInstance<ElementDatabaseSO>();
            database.SetDefinitionsForTests(new[]
            {
                CreateElement(
                    VacuumId,
                    "Vacuum",
                    ElementBehaviorType.Vacuum,
                    DisplacementPriority.Vacuum,
                    density: 0f,
                    defaultMass: 0,
                    maxMass: 0,
                    viscosity: 1,
                    minSpreadMass: 0,
                    isSolid: false,
                    color: new Color32(0, 0, 0, 255)),

                CreateElement(
                    OxygenId,
                    "Oxygen",
                    ElementBehaviorType.Gas,
                    DisplacementPriority.Gas,
                    density: 0.1f,
                    defaultMass: 1_000,
                    maxMass: 1_000,
                    viscosity: 1,
                    minSpreadMass: 100,
                    isSolid: false,
                    color: new Color32(180, 220, 255, 255)),

                CreateElement(
                    WaterId,
                    "Water",
                    ElementBehaviorType.Liquid,
                    DisplacementPriority.Liquid,
                    density: 1f,
                    defaultMass: 1_000_000,
                    maxMass: 1_000_000,
                    viscosity: 10,
                    minSpreadMass: 100_000,
                    isSolid: false,
                    color: new Color32(80, 120, 255, 255)),

                CreateElement(
                    BedrockId,
                    "Bedrock",
                    ElementBehaviorType.StaticSolid,
                    DisplacementPriority.StaticSolid,
                    density: 9999f,
                    defaultMass: 0,
                    maxMass: 0,
                    viscosity: 1,
                    minSpreadMass: 0,
                    isSolid: true,
                    color: new Color32(100, 100, 100, 255)),
            });

            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(
            byte id,
            string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            float density,
            int defaultMass,
            int maxMass,
            int viscosity,
            int minSpreadMass,
            bool isSolid,
            Color32 color)
        {
            var def = ScriptableObject.CreateInstance<ElementDefinitionSO>();
            def.SetValuesForTests(
                id: id,
                elementName: name,
                behaviorType: behaviorType,
                displacementPriority: displacementPriority,
                density: density,
                defaultMass: defaultMass,
                maxMass: maxMass,
                viscosity: viscosity,
                minSpreadMass: minSpreadMass,
                isSolid: isSolid,
                baseColor: color);
            return def;
        }
    }
}
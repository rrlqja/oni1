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
            SetCell(3, 4, SandId, 500_000);
            SetCell(3, 3, OxygenId, 100_000);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 4);
            SimCell bottom = _grid.GetCell(3, 3);

            Assert.That(top.ElementId, Is.EqualTo(OxygenId));
            Assert.That(bottom.ElementId, Is.EqualTo(SandId));
        }

        [Test]
        public void Sand_Merges_Into_Same_Sand_Below_Until_MaxMass()
        {
            // merge target가 먼저 떨어지지 않도록 아래를 bedrock으로 막는다
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 500_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 2);
            SimCell bottom = _grid.GetCell(3, 1);

            Assert.That(top.ElementId, Is.EqualTo(VacuumId));
            Assert.That(bottom.ElementId, Is.EqualTo(SandId));
            Assert.That(bottom.Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Sand_Merge_Overflow_Remains_In_Source()
        {
            // merge target가 먼저 떨어지지 않도록 아래를 bedrock으로 막는다
            SetCell(3, 2, SandId, 500_000);
            SetCell(3, 1, SandId, 800_000);
            SetCell(3, 0, BedrockId, 0);

            _runner.Step(1);

            SimCell top = _grid.GetCell(3, 2);
            SimCell bottom = _grid.GetCell(3, 1);

            Assert.That(bottom.ElementId, Is.EqualTo(SandId));
            Assert.That(bottom.Mass, Is.EqualTo(1_000_000));
            Assert.That(top.ElementId, Is.EqualTo(SandId));
            Assert.That(top.Mass, Is.EqualTo(300_000));
        }

        [Test]
        public void Water_Falls_Down_First_If_Cell_Below_Is_Vacuum()
        {
            SetCell(3, 4, WaterId, 1_000_000);

            _runner.Step(1);

            SimCell original = _grid.GetCell(3, 4);
            SimCell below = _grid.GetCell(3, 3);

            Assert.That(original.ElementId, Is.EqualTo(VacuumId));
            Assert.That(below.ElementId, Is.EqualTo(WaterId));
            Assert.That(below.Mass, Is.EqualTo(1_000_000));
        }

        [Test]
        public void Water_Fills_Same_Water_Below_Before_Lateral_Spread()
        {
            // 아래 water가 먼저 떨어지거나 좌우로 퍼지지 못하게 완전히 고정
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, WaterId, 400_000);
            SetCell(3, 0, BedrockId, 0);

            // source 좌우 차단
            SetCell(2, 2, BedrockId, 0);
            SetCell(4, 2, BedrockId, 0);

            // below 좌우 차단
            SetCell(2, 1, BedrockId, 0);
            SetCell(4, 1, BedrockId, 0);

            _runner.Step(1);

            SimCell source = _grid.GetCell(3, 2);
            SimCell below = _grid.GetCell(3, 1);

            Assert.That(below.ElementId, Is.EqualTo(WaterId));
            Assert.That(below.Mass, Is.EqualTo(1_000_000));

            Assert.That(source.ElementId, Is.EqualTo(WaterId));
            Assert.That(source.Mass, Is.EqualTo(400_000));
        }

        [Test]
        public void Water_Does_Not_Spread_Sideways_When_Mass_Is_At_Or_Below_MinSpreadMass()
        {
            // Water MinSpreadMass = 100kg (= 100_000g)
            SetCell(3, 2, WaterId, 100_000);
            SetCell(3, 1, WaterId, 1_000_000);

            _runner.Step(1);

            SimCell left = _grid.GetCell(2, 2);
            SimCell center = _grid.GetCell(3, 2);
            SimCell right = _grid.GetCell(4, 2);

            Assert.That(left.ElementId, Is.EqualTo(VacuumId));
            Assert.That(right.ElementId, Is.EqualTo(VacuumId));
            Assert.That(center.ElementId, Is.EqualTo(WaterId));
            Assert.That(center.Mass, Is.EqualTo(100_000));
        }

        [Test]
        public void Water_Spreads_Sideways_When_Below_Is_Blocked_And_Mass_Is_Above_MinSpreadMass()
        {
            SetCell(3, 2, WaterId, 150_000);
            SetCell(3, 1, WaterId, 1_000_000);

            _runner.Step(2);

            SimCell left = _grid.GetCell(2, 2);
            SimCell center = _grid.GetCell(3, 2);
            SimCell right = _grid.GetCell(4, 2);

            int sideMassSum = 0;
            if (left.ElementId == WaterId) sideMassSum += left.Mass;
            if (right.ElementId == WaterId) sideMassSum += right.Mass;

            Assert.That(center.ElementId, Is.EqualTo(WaterId));
            Assert.That(center.Mass, Is.GreaterThanOrEqualTo(100_000));
            Assert.That(sideMassSum, Is.GreaterThan(0));
            Assert.That(center.Mass + sideMassSum, Is.EqualTo(150_000));
        }

        [Test]
        public void Water_LateralSpread_CanBecome_Asymmetric_When_RemainingMass_Is_Limited()
        {
            // MinSpreadMass = 100_000, Viscosity = 10
            // 120_000이면 desiredPerSide = 12_000
            // 첫 번째 방향 12_000, 두 번째는 남은 질량 제한으로 8_000만 가능
            SetCell(3, 2, WaterId, 120_000);
            SetCell(3, 1, WaterId, 1_000_000);

            _runner.Step(1);

            SimCell leftOdd = _grid.GetCell(2, 2);
            SimCell rightOdd = _grid.GetCell(4, 2);

            int oddLeft = leftOdd.ElementId == WaterId ? leftOdd.Mass : 0;
            int oddRight = rightOdd.ElementId == WaterId ? rightOdd.Mass : 0;

            TearDownAndReset();

            SetCell(3, 2, WaterId, 120_000);
            SetCell(3, 1, WaterId, 1_000_000);

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
                    SandId,
                    "Sand",
                    ElementBehaviorType.FallingSolid,
                    DisplacementPriority.FallingSolid,
                    density: 2f,
                    defaultMass: 500_000,
                    maxMass: 1_000_000,
                    viscosity: 1,
                    minSpreadMass: 0,
                    isSolid: true,
                    color: new Color32(200, 180, 100, 255)),

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
                    OxygenId,
                    "Oxygen",
                    ElementBehaviorType.Gas,
                    DisplacementPriority.Gas,
                    density: 0.1f,
                    defaultMass: 100_000,
                    maxMass: 100_000,
                    viscosity: 1,
                    minSpreadMass: 0,
                    isSolid: false,
                    color: new Color32(180, 220, 255, 255)),

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
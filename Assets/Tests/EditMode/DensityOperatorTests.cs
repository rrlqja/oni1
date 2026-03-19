using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DensityOperatorTests
    {
        private static ElementRuntimeDefinition MakeDef(
            byte id, ElementBehaviorType behavior, DisplacementPriority priority,
            float density, int maxMass = 1_000_000)
        {
            return new ElementRuntimeDefinition(
                id: id, name: id.ToString(),
                behaviorType: behavior,
                displacementPriority: priority,
                density: density,
                defaultMass: 0, maxMass: maxMass,
                viscosity: 1, minSpreadMass: 0,
                isSolid: false,
                baseColor: default,
                lateralRetainMass: 0);
        }

        // ── 액체 밀도 이동 ──

        [Test]
        public void Heavy_Liquid_Above_Light_Liquid_Should_Swap()
        {
            // 오염된 물(1050) 위, 물(1000) 아래 → Swap
            var pollutedWater = MakeDef(5, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1050f);
            var water = MakeDef(3, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1000f);

            bool result = DensityOperator.ShouldSwap(
                in pollutedWater, in water, ElementBehaviorType.Liquid);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Light_Liquid_Above_Heavy_Liquid_Should_Not_Swap()
        {
            // 물(1000) 위, 오염된 물(1050) 아래 → 이미 올바른 배치
            var water = MakeDef(3, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1000f);
            var pollutedWater = MakeDef(5, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1050f);

            bool result = DensityOperator.ShouldSwap(
                in water, in pollutedWater, ElementBehaviorType.Liquid);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Same_Liquid_Should_Not_Swap()
        {
            // 같은 원소끼리는 교환 불필요
            var water = MakeDef(3, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1000f);

            bool result = DensityOperator.ShouldSwap(
                in water, in water, ElementBehaviorType.Liquid);

            Assert.That(result, Is.False);
        }

        // ── 기체 밀도 이동 ──

        [Test]
        public void Heavy_Gas_Above_Light_Gas_Should_Swap()
        {
            // 산소(500) 위, 수소(90) 아래 → Swap
            var oxygen = MakeDef(4, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 500f);
            var hydrogen = MakeDef(6, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 90f);

            bool result = DensityOperator.ShouldSwap(
                in oxygen, in hydrogen, ElementBehaviorType.Gas);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Light_Gas_Above_Heavy_Gas_Should_Not_Swap()
        {
            var hydrogen = MakeDef(6, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 90f);
            var oxygen = MakeDef(4, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 500f);

            bool result = DensityOperator.ShouldSwap(
                in hydrogen, in oxygen, ElementBehaviorType.Gas);

            Assert.That(result, Is.False);
        }

        // ── 타입 불일치 → false ──

        [Test]
        public void Liquid_Vs_Gas_Returns_False()
        {
            var water = MakeDef(3, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 1000f);
            var oxygen = MakeDef(4, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 500f);

            bool result = DensityOperator.ShouldSwap(
                in water, in oxygen, ElementBehaviorType.Liquid);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Gas_Checked_As_Liquid_Returns_False()
        {
            var oxygen = MakeDef(4, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 500f);
            var hydrogen = MakeDef(6, ElementBehaviorType.Gas,
                DisplacementPriority.Gas, density: 90f);

            // 기체를 Liquid 타입으로 검사 → false
            bool result = DensityOperator.ShouldSwap(
                in oxygen, in hydrogen, ElementBehaviorType.Liquid);

            Assert.That(result, Is.False);
        }
    }
}
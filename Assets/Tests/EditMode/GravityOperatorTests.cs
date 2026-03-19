using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GravityOperatorTests
    {
        // ── 원소 정의 헬퍼 ──

        private static ElementRuntimeDefinition MakeDef(
            byte id, ElementBehaviorType behavior, DisplacementPriority priority,
            float density = 1f, int maxMass = 1_000_000)
        {
            return new ElementRuntimeDefinition(
                id: id, name: id.ToString(),
                behaviorType: behavior,
                displacementPriority: priority,
                density: density,
                defaultMass: 0, maxMass: maxMass,
                viscosity: 1, minSpreadMass: 0,
                isSolid: behavior == ElementBehaviorType.FallingSolid
                      || behavior == ElementBehaviorType.StaticSolid,
                baseColor: default,
                lateralRetainMass: 0);
        }

        private static readonly ElementRuntimeDefinition VacuumDef =
            MakeDef(0, ElementBehaviorType.Vacuum, DisplacementPriority.Vacuum);

        private static readonly ElementRuntimeDefinition SandDef =
            MakeDef(2, ElementBehaviorType.FallingSolid, DisplacementPriority.FallingSolid,
                density: 2f, maxMass: 1_000_000);

        private static readonly ElementRuntimeDefinition WaterDef =
            MakeDef(3, ElementBehaviorType.Liquid, DisplacementPriority.Liquid,
                density: 1f, maxMass: 1_000_000);

        private static readonly ElementRuntimeDefinition OxygenDef =
            MakeDef(4, ElementBehaviorType.Gas, DisplacementPriority.Gas,
                density: 0.1f, maxMass: 100_000);

        private static readonly ElementRuntimeDefinition BedrockDef =
            MakeDef(1, ElementBehaviorType.StaticSolid, DisplacementPriority.StaticSolid);

        // ── 아래가 진공 → Swap ──

        [Test]
        public void Sand_Above_Vacuum_Returns_Swap()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = SimCell.Vacuum;

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in VacuumDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Swap));
        }

        [Test]
        public void Water_Above_Vacuum_Returns_Swap()
        {
            var upper = new SimCell(WaterDef.Id, 1_000_000, 0);
            var lower = SimCell.Vacuum;

            var result = GravityOperator.Evaluate(
                in upper, in WaterDef, in lower, in VacuumDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Swap));
        }

        // ── 아래가 더 가벼운 원소 → Swap ──

        [Test]
        public void Sand_Above_Gas_Returns_Swap()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = new SimCell(OxygenDef.Id, 1_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in OxygenDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Swap));
        }

        [Test]
        public void Sand_Above_Liquid_Returns_Swap()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = new SimCell(WaterDef.Id, 1_000_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in WaterDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Swap));
        }

        [Test]
        public void Water_Above_Gas_Returns_Swap()
        {
            var upper = new SimCell(WaterDef.Id, 1_000_000, 0);
            var lower = new SimCell(OxygenDef.Id, 1_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in WaterDef, in lower, in OxygenDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Swap));
        }

        // ── 같은 원소 + 용량 남음 → Merge ──

        [Test]
        public void Sand_Above_Same_Sand_With_Capacity_Returns_Merge()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = new SimCell(SandDef.Id, 400_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in SandDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Merge));
        }

        [Test]
        public void Water_Above_Same_Water_With_Capacity_Returns_Merge()
        {
            var upper = new SimCell(WaterDef.Id, 500_000, 0);
            var lower = new SimCell(WaterDef.Id, 400_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in WaterDef, in lower, in WaterDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.Merge));
        }

        // ── 같은 원소 + 용량 꽉 참 → None ──

        [Test]
        public void Sand_Above_Same_Sand_Full_Returns_None()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = new SimCell(SandDef.Id, 1_000_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in SandDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.None));
        }

        // ── 아래가 고체 → None ──

        [Test]
        public void Sand_Above_Bedrock_Returns_None()
        {
            var upper = new SimCell(SandDef.Id, 500_000, 0);
            var lower = new SimCell(BedrockDef.Id, 0, 0);

            var result = GravityOperator.Evaluate(
                in upper, in SandDef, in lower, in BedrockDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.None));
        }

        // ── 같은 우선순위 다른 원소 → None ──

        [Test]
        public void Water_Above_Different_Liquid_Same_Priority_Returns_None()
        {
            // 다른 액체지만 DisplacementPriority가 같으면 중력으로는 교환 불가
            // (밀도 교환은 DensityOperator가 담당)
            var otherLiquidDef = MakeDef(5, ElementBehaviorType.Liquid,
                DisplacementPriority.Liquid, density: 0.5f, maxMass: 1_000_000);

            var upper = new SimCell(WaterDef.Id, 1_000_000, 0);
            var lower = new SimCell(otherLiquidDef.Id, 1_000_000, 0);

            var result = GravityOperator.Evaluate(
                in upper, in WaterDef, in lower, in otherLiquidDef);

            Assert.That(result, Is.EqualTo(GravityOperator.Result.None));
        }
    }
}
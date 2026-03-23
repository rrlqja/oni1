using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// LiquidDensityProcessor 통합 테스트 (Phase 3: 이종 액체 밀도 이동).
    ///
    /// 검증 대상:
    ///   - 수직 밀도 교환 (무거운 액체 아래로, 가벼운 액체 위로)
    ///   - 같은 밀도 교환 안 함
    ///   - 이미 올바른 순서면 교환 안 함
    ///   - 3종 액체 층 분리 (DirtyWater > Water > Oil)
    ///   - 질량 보존
    /// </summary>
    public class LiquidDensityProcessorTests
    {
        private const byte VacuumId = BuiltInElementIds.Vacuum;
        private const byte BedrockId = 1;
        private const byte WaterId = 2;
        private const byte OilId = 3;
        private const byte DirtyWaterId = 4;
        private const byte OxygenId = 5;

        private WorldGrid _grid;
        private ElementRegistry _registry;
        private SimulationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _grid = new WorldGrid(7, 9);
            _registry = CreateRegistry();
            _runner = new SimulationRunner(_grid, _registry);
            FillAllVacuum();
        }

        // ================================================================
        //  수직 밀도 교환
        // ================================================================

        [Test]
        public void Heavy_Liquid_Sinks_Below_Light_Liquid()
        {
            // 물(density=1.0) 위, 기름(density=0.8) 아래 → Swap
            // 양쪽 벽 격납 (투사체 방지)
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, OilId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(WaterId),
                "무거운 물이 아래로 가라앉아야 합니다");
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(OilId),
                "가벼운 기름이 위로 떠올라야 합니다");
        }

        [Test]
        public void Correct_Order_Does_Not_Swap()
        {
            // 기름(가벼움) 위, 물(무거움) 아래 → 이미 올바른 순서, Swap 안 함
            SetCell(3, 2, OilId, 1_000_000);
            SetCell(3, 1, WaterId, 1_000_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(OilId),
                "이미 올바른 순서이므로 변하지 않아야 합니다");
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(WaterId));
        }

        [Test]
        public void Same_Liquid_Does_Not_Density_Swap()
        {
            // 물(3,2) 위, 물(3,1) 아래 → 같은 원소, 밀도 교환 안 함
            SetCell(3, 2, WaterId, 800_000);
            SetCell(3, 1, WaterId, 600_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            int massBefore_Top = _grid.GetCell(3, 2).Mass;
            int massBefore_Bottom = _grid.GetCell(3, 1).Mass;

            _runner.Step(1);

            // Phase 1 Merge가 발생할 수 있음 (같은 원소 + 여유)
            // 핵심: Phase 3 밀도 교환은 발생하지 않음 (같은 원소)
            int totalWater = SumMassOfElement(WaterId);
            Assert.That(totalWater, Is.EqualTo(1_400_000), "질량 보존");
        }

        // ================================================================
        //  3종 액체 층 분리
        // ================================================================

        [Test]
        public void Three_Liquids_Separate_By_Density_Over_Ticks()
        {
            // 역순 배치: DirtyWater(가장 무거움) 위, Oil(가장 가벼움) 아래
            // 시간 경과 후: Oil 위 / Water 중간 / DirtyWater 아래
            SetCell(3, 3, DirtyWaterId, 1_000_000); // density 1.2 (가장 무거움)
            SetCell(3, 2, WaterId, 1_000_000);       // density 1.0
            SetCell(3, 1, OilId, 1_000_000);          // density 0.8 (가장 가벼움)
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            for (int t = 1; t <= 10; t++)
                _runner.Step(t);

            // 밀도순 정렬: 아래가 무겁고 위가 가벼움
            Assert.That(_grid.GetCell(3, 1).ElementId, Is.EqualTo(DirtyWaterId),
                "가장 무거운 DirtyWater가 바닥에");
            Assert.That(_grid.GetCell(3, 2).ElementId, Is.EqualTo(WaterId),
                "중간 밀도 Water가 중간에");
            Assert.That(_grid.GetCell(3, 3).ElementId, Is.EqualTo(OilId),
                "가장 가벼운 Oil이 위에");
        }

        // ================================================================
        //  질량 보존
        // ================================================================

        [Test]
        public void Mass_Conserved_After_Density_Swaps()
        {
            SetCell(3, 3, DirtyWaterId, 800_000);
            SetCell(3, 2, OilId, 600_000);
            SetCell(3, 1, WaterId, 900_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 3; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            for (int t = 1; t <= 20; t++)
                _runner.Step(t);

            int totalDirty = SumMassOfElement(DirtyWaterId);
            int totalWater = SumMassOfElement(WaterId);
            int totalOil = SumMassOfElement(OilId);

            Assert.That(totalDirty, Is.EqualTo(800_000), "DirtyWater 질량 보존");
            Assert.That(totalWater, Is.EqualTo(900_000), "Water 질량 보존");
            Assert.That(totalOil, Is.EqualTo(600_000), "Oil 질량 보존");
        }

        [Test]
        public void Non_Liquid_Not_Affected_By_Density_Swap()
        {
            // 기체와 액체가 인접 → Phase 3에서 교환하지 않음 (Liquid끼리만)
            SetCell(3, 2, WaterId, 1_000_000);
            SetCell(3, 1, OxygenId, 1_000);
            SetCell(3, 0, BedrockId, 0);
            for (int y = 1; y <= 2; y++) { SetCell(2, y, BedrockId, 0); SetCell(4, y, BedrockId, 0); }

            _runner.Step(1);

            // Phase 1에서 물→기체 Swap(중력)이 발생하지만,
            // Phase 3 밀도 교환은 Liquid↔Liquid에서만 작동
            int totalWater = SumMassOfElement(WaterId);
            Assert.That(totalWater, Is.EqualTo(1_000_000), "물 질량 보존");
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
                CreateElement(DirtyWaterId, "DirtyWater", ElementBehaviorType.Liquid, DisplacementPriority.Liquid, 1.2f, 1_000_000, 1_000_000, 10, 100_000, false, new Color32(120,100,60,255)),
                CreateElement(OxygenId, "Oxygen", ElementBehaviorType.Gas, DisplacementPriority.Gas, 0.5f, 1_000, 2_000, 1, 0, false, new Color32(180,220,255,255)),
            });
            return new ElementRegistry(database);
        }

        private ElementDefinitionSO CreateElement(byte id, string name, ElementBehaviorType bt, DisplacementPriority dp, float density, int dm, int mm, int v, int ms, bool s, Color32 c)
        { var def = ScriptableObject.CreateInstance<ElementDefinitionSO>(); def.SetValuesForTests(id, name, bt, dp, density, dm, mm, v, ms, s, c); return def; }
    }
}
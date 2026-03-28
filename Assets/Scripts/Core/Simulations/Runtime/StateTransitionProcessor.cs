using System;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 8: 상태변환 처리기.
    ///
    /// 셀의 온도가 전환점 ± 오버슈트를 넘으면 원소를 교체한다.
    ///
    /// 오버슈트 메커니즘 (ONI 패턴):
    ///   가열: T > highTransitionTemp + OVERSHOOT → 전환
    ///   냉각: T &lt; lowTransitionTemp - OVERSHOOT → 전환
    ///   전환 후 리바운드: 전환점 ± REBOUND
    ///   히스테리시스 = 2 × OVERSHOOT = 6K → 매 틱 진동 방지
    ///
    /// 전환 시 ElementId만 교체 (in-place). 질량 보존.
    /// MaxMass 초과 상태는 허용 — Phase 4/5가 자연스럽게 분배.
    /// 부산물이 있으면 인접 빈 셀에 배치.
    /// </summary>
    public sealed class StateTransitionProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly SimulationSettings _settings;

        public StateTransitionProcessor(WorldGrid grid, ElementRegistry registry, SimulationSettings settings)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Process()
        {
            for (int i = 0; i < _grid.Length; i++)
            {
                ref SimCell cell = ref _grid.GetCellRef(i);

                if (cell.ElementId == BuiltInElementIds.Vacuum || cell.Mass <= 0)
                    continue;

                ref readonly ElementRuntimeDefinition def = ref _registry.Get(cell.ElementId);

                // 가열 전환
                if (def.HighTransitionTemp > 0f &&
                    cell.Temperature > def.HighTransitionTemp + _settings.TransitionOvershoot)
                {
                    TransitionCell(ref cell, i,
                        def.HighTransitionTargetId,
                        def.HighTransitionOreId,
                        def.HighTransitionOreMassRatio,
                        def.HighTransitionTemp + _settings.TransitionRebound);
                    continue;
                }

                // 냉각 전환
                if (def.LowTransitionTemp > 0f &&
                    cell.Temperature < def.LowTransitionTemp - _settings.TransitionOvershoot)
                {
                    TransitionCell(ref cell, i,
                        def.LowTransitionTargetId,
                        def.LowTransitionOreId,
                        def.LowTransitionOreMassRatio,
                        def.LowTransitionTemp - _settings.TransitionRebound);
                }
            }
        }

        private void TransitionCell(
            ref SimCell cell, int cellIndex,
            byte targetId, byte oreId, float oreMassRatio,
            float reboundTemp)
        {
            if (targetId == 0 || targetId == cell.ElementId)
                return;

            // 대상 원소가 레지스트리에 있는지 확인
            if (!_registry.IsRegistered(targetId))
                return;

            int originalMass = cell.Mass;

            // 부산물 처리
            if (oreId != 0 && oreMassRatio > 0f && _registry.IsRegistered(oreId))
            {
                int oreMass = (int)(originalMass * oreMassRatio);
                int mainMass = originalMass - oreMass;

                if (mainMass <= 0)
                    mainMass = 1; // 최소 질량 보장

                cell.ElementId = targetId;
                cell.Mass = mainMass;
                cell.Temperature = reboundTemp;

                if (oreMass > 0)
                    TryPlaceByproduct(cellIndex, oreId, oreMass, reboundTemp);
            }
            else
            {
                // 단순 전환 — 질량 보존, ElementId 교체
                cell.ElementId = targetId;
                cell.Temperature = reboundTemp;
            }
        }

        private void TryPlaceByproduct(int originIndex, byte oreId, int oreMass, float temperature)
        {
            if (oreMass <= 0) return;

            _grid.ToXY(originIndex, out int ox, out int oy);

            // 4방향 탐색: 위 → 좌 → 우 → 아래
            int[] dx = { 0, -1, 1, 0 };
            int[] dy = { 1, 0, 0, -1 };

            for (int d = 0; d < 4; d++)
            {
                int nx = ox + dx[d];
                int ny = oy + dy[d];
                if (!_grid.InBounds(nx, ny)) continue;

                ref SimCell neighbor = ref _grid.GetCellRef(nx, ny);

                // 진공이면 배치
                if (neighbor.ElementId == BuiltInElementIds.Vacuum)
                {
                    neighbor = new SimCell(oreId, oreMass, temperature);
                    return;
                }

                // 동종이면 합류
                if (neighbor.ElementId == oreId)
                {
                    // 온도 가중평균
                    float totalThermal = neighbor.Temperature * neighbor.Mass
                                       + temperature * oreMass;
                    int totalMass = neighbor.Mass + oreMass;
                    neighbor.Mass = totalMass;
                    neighbor.Temperature = totalMass > 0 ? totalThermal / totalMass : temperature;
                    return;
                }
            }

            // 4방향 모두 실패 — 질량 소실 (극히 드문 케이스)
            UnityEngine.Debug.LogWarning(
                $"[StateTransition] Byproduct placement failed at index {originIndex}. " +
                $"OreId={oreId}, Mass={oreMass} lost.");
        }
    }
}
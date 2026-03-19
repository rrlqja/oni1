using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 기체 밀어내기 판정기 (읽기 전용 탐색).
    ///
    /// 액체가 옆 기체를 밀어내려 할 때, 기체가 갈 곳이 있는지 확인한다.
    ///   1순위: 인접 진공 (미사용) → Swap 커맨드
    ///   2순위: 인접 동종 기체 → ForceMergeMass 커맨드 (MaxMass 무시, 압축 합류)
    ///   실패 → return false (해당 방향 차단)
    ///
    /// swapTargetUsed[] 배열로 같은 진공을 두 커맨드가 동시에 지목하는 것을 방지.
    /// </summary>
    public static class DisplaceChecker
    {
        /// <summary>
        /// 지정된 기체 셀이 밀려날 수 있는지 판정한다.
        /// 성공 시 escapeCmd에 Swap 또는 ForceMergeMass 커맨드를 생성한다.
        /// </summary>
        public static bool CanDisplace(
            WorldGrid grid,
            ElementRegistry registry,
            int gasIndex,
            bool[] swapTargetUsed,
            out SimulationCommand escapeCmd)
        {
            escapeCmd = default;

            SimCell gasCell = grid.GetCellByIndex(gasIndex);
            ref readonly ElementRuntimeDefinition gasDef = ref registry.Get(gasCell.ElementId);

            if (gasDef.BehaviorType != ElementBehaviorType.Gas)
                return false;

            grid.ToXY(gasIndex, out int gx, out int gy);

            int bestVacuumIndex = -1;
            int bestMergeIndex = -1;

            TryDirection(grid, gx, gy - 1, gasCell, swapTargetUsed,
                ref bestVacuumIndex, ref bestMergeIndex);
            TryDirection(grid, gx, gy + 1, gasCell, swapTargetUsed,
                ref bestVacuumIndex, ref bestMergeIndex);
            TryDirection(grid, gx - 1, gy, gasCell, swapTargetUsed,
                ref bestVacuumIndex, ref bestMergeIndex);
            TryDirection(grid, gx + 1, gy, gasCell, swapTargetUsed,
                ref bestVacuumIndex, ref bestMergeIndex);

            // 1순위: 진공 → Swap
            if (bestVacuumIndex >= 0)
            {
                swapTargetUsed[bestVacuumIndex] = true;
                escapeCmd = SimulationCommand.CreateSwap(gasIndex, bestVacuumIndex);
                return true;
            }

            // 2순위: 동종 기체 → ForceMergeMass (MaxMass 무시, 압축 합류)
            if (bestMergeIndex >= 0)
            {
                escapeCmd = SimulationCommand.CreateForceMergeMass(gasIndex, bestMergeIndex);
                return true;
            }

            return false;
        }

        private static void TryDirection(
            WorldGrid grid,
            int nx, int ny,
            in SimCell gasCell,
            bool[] swapTargetUsed,
            ref int bestVacuumIndex,
            ref int bestMergeIndex)
        {
            if (!grid.InBounds(nx, ny))
                return;

            int neighborIndex = grid.ToIndex(nx, ny);
            SimCell neighborCell = grid.GetCellByIndex(neighborIndex);

            // 1순위: 진공 + 아직 미사용
            if (neighborCell.ElementId == BuiltInElementIds.Vacuum
                && !swapTargetUsed[neighborIndex])
            {
                if (bestVacuumIndex < 0)
                    bestVacuumIndex = neighborIndex;
                return;
            }

            // 2순위: 동종 기체 (MaxMass 무시 — 압축 합류)
            if (bestMergeIndex < 0
                && neighborCell.ElementId == gasCell.ElementId)
            {
                bestMergeIndex = neighborIndex;
            }
        }
    }
}
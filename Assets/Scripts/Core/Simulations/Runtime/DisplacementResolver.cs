using System;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 원소 밀어내기(Displacement) 핵심 유틸리티.
    ///
    /// 높은 우선순위 원소가 낮은 우선순위 원소를 밀어낼 때,
    /// 밀려나는 원소가 갈 곳을 찾아 이동시킨다.
    ///
    /// 탐색 우선순위:
    ///   1순위: 인접 진공 → 통째 이동
    ///   2순위: 인접 동종 원소 → 강제 질량 합류 (MaxMass 무시)
    ///   3순위: 없음 → 밀어내기 실패
    ///
    /// 사용처:
    ///   - WorldEditService (타일 건설 시)
    /// </summary>
    public static class DisplacementResolver
    {
        /// <summary>
        /// 지정된 셀의 원소를 인접 셀로 밀어낸다.
        /// 성공 시 원래 셀은 진공이 된다.
        /// </summary>
        public static bool TryDisplace(
            WorldGrid grid,
            ElementRegistry registry,
            int cellIndex)
        {
            SimCell cell = grid.GetCellByIndex(cellIndex);

            if (cell.ElementId == BuiltInElementIds.Vacuum || cell.Mass <= 0)
                return true;

            ref readonly ElementRuntimeDefinition element = ref registry.Get(cell.ElementId);

            if (element.IsSolid)
                return false;

            grid.ToXY(cellIndex, out int cx, out int cy);

            // ── 1순위: 진공으로 이동 ──
            int vacuumTarget = FindAdjacentVacuum(grid, cx, cy);
            if (vacuumTarget >= 0)
            {
                ref SimCell targetRef = ref grid.GetCellRef(vacuumTarget);
                targetRef = cell;

                ref SimCell sourceRef = ref grid.GetCellRef(cellIndex);
                sourceRef = SimCell.Vacuum;
                return true;
            }

            // ── 2순위: 동종 원소에 강제 합류 (MaxMass 무시) ──
            int mergeTarget = FindAdjacentSameElement(grid, cx, cy, cell.ElementId);
            if (mergeTarget >= 0)
            {
                ref SimCell targetRef = ref grid.GetCellRef(mergeTarget);

                int transfer = cell.Mass;
                if (transfer > 0)
                {
                    short mergedTemp = ComputeMergedTemperature(
                        cell.Temperature, transfer,
                        targetRef.Temperature, targetRef.Mass);

                    // MaxMass 무시 — 전량 강제 합산
                    targetRef.Mass += transfer;
                    targetRef.Temperature = mergedTemp;
                }

                ref SimCell sourceRef = ref grid.GetCellRef(cellIndex);
                sourceRef = SimCell.Vacuum;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지정된 셀에 새 원소를 배치하면서, 기존 원소를 밀어낸다.
        /// </summary>
        public static bool TryPlaceWithDisplacement(
            WorldGrid grid,
            ElementRegistry registry,
            int cellIndex,
            SimCell newCell)
        {
            SimCell existing = grid.GetCellByIndex(cellIndex);

            if (existing.ElementId == BuiltInElementIds.Vacuum || existing.Mass <= 0)
            {
                grid.GetCellRef(cellIndex) = newCell;
                return true;
            }

            if (TryDisplace(grid, registry, cellIndex))
            {
                grid.GetCellRef(cellIndex) = newCell;
                return true;
            }

            grid.GetCellRef(cellIndex) = newCell;
            return true;
        }

        // ── 탐색 메서드 ──

        private static int FindAdjacentVacuum(WorldGrid grid, int cx, int cy)
        {
            if (TryGetVacuum(grid, cx - 1, cy, out int left)) return left;
            if (TryGetVacuum(grid, cx + 1, cy, out int right)) return right;
            if (TryGetVacuum(grid, cx, cy + 1, out int up)) return up;
            if (TryGetVacuum(grid, cx, cy - 1, out int down)) return down;
            return -1;
        }

        /// <summary>
        /// 인접 동종 원소를 찾는다. MaxMass 제한 없음.
        /// </summary>
        private static int FindAdjacentSameElement(
            WorldGrid grid, int cx, int cy, byte elementId)
        {
            if (TryGetSameElement(grid, cx - 1, cy, elementId, out int left)) return left;
            if (TryGetSameElement(grid, cx + 1, cy, elementId, out int right)) return right;
            if (TryGetSameElement(grid, cx, cy + 1, elementId, out int up)) return up;
            if (TryGetSameElement(grid, cx, cy - 1, elementId, out int down)) return down;
            return -1;
        }

        private static bool TryGetVacuum(WorldGrid grid, int x, int y, out int index)
        {
            index = -1;
            if (!grid.InBounds(x, y)) return false;

            index = grid.ToIndex(x, y);
            SimCell cell = grid.GetCellByIndex(index);
            return cell.ElementId == BuiltInElementIds.Vacuum;
        }

        private static bool TryGetSameElement(
            WorldGrid grid, int x, int y, byte elementId, out int index)
        {
            index = -1;
            if (!grid.InBounds(x, y)) return false;

            index = grid.ToIndex(x, y);
            SimCell cell = grid.GetCellByIndex(index);
            return cell.ElementId == elementId;
        }

        private static short ComputeMergedTemperature(
            short sourceTemp, int sourceMass,
            short targetTemp, int targetMass)
        {
            long total = (long)sourceMass + targetMass;
            if (total <= 0) return targetTemp;

            long result = ((long)sourceTemp * sourceMass + (long)targetTemp * targetMass) / total;
            return (short)result;
        }
    }
}
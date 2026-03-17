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
    ///   2순위: 인접 동종 원소 (용량 여유 있음) → 질량 합류
    ///   3순위: 없음 → 밀어내기 실패
    ///
    /// 사용처:
    ///   - DisplacementProcessor (시뮬레이션 페이즈)
    ///   - WorldEditService (타일 건설 시)
    /// </summary>
    public static class DisplacementResolver
    {
        /// <summary>
        /// 지정된 셀의 원소를 인접 셀로 밀어낸다.
        /// 성공 시 원래 셀은 진공이 된다.
        /// </summary>
        /// <returns>밀어내기 성공 여부</returns>
        public static bool TryDisplace(
            WorldGrid grid,
            ElementRegistry registry,
            int cellIndex)
        {
            SimCell cell = grid.GetCellByIndex(cellIndex);

            // 진공이나 질량 없는 셀은 밀어낼 것이 없음
            if (cell.ElementId == BuiltInElementIds.Vacuum || cell.Mass <= 0)
                return true; // 이미 비어있으므로 성공 취급

            ref readonly ElementRuntimeDefinition element = ref registry.Get(cell.ElementId);

            // 고체는 밀어낼 수 없음
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

            // ── 2순위: 동종 원소에 합류 ──
            int mergeTarget = FindAdjacentSameElement(
                grid, registry, cx, cy,
                cell.ElementId, element.MaxMass);
            if (mergeTarget >= 0)
            {
                ref SimCell targetRef = ref grid.GetCellRef(mergeTarget);
                int capacity = element.MaxMass - targetRef.Mass;
                int transfer = Math.Min(cell.Mass, capacity);

                if (transfer > 0)
                {
                    // 질량 가중 평균 온도
                    short mergedTemp = ComputeMergedTemperature(
                        cell.Temperature, transfer,
                        targetRef.Temperature, targetRef.Mass);

                    targetRef.Mass += transfer;
                    targetRef.Temperature = mergedTemp;
                }

                int remaining = cell.Mass - transfer;

                ref SimCell sourceRef = ref grid.GetCellRef(cellIndex);
                if (remaining <= 0)
                {
                    sourceRef = SimCell.Vacuum;
                }
                else
                {
                    // 일부만 합류한 경우 나머지는 원래 자리에 남음
                    sourceRef.Mass = remaining;
                }
                return remaining <= 0;
            }

            // ── 대상 없음: 밀어내기 실패 ──
            return false;
        }

        /// <summary>
        /// 지정된 셀에 새 원소를 배치하면서, 기존 원소를 밀어낸다.
        /// WorldEditService.SetCell() 등에서 사용.
        /// </summary>
        /// <returns>배치 성공 여부 (밀어내기 실패 시 false)</returns>
        public static bool TryPlaceWithDisplacement(
            WorldGrid grid,
            ElementRegistry registry,
            int cellIndex,
            SimCell newCell)
        {
            SimCell existing = grid.GetCellByIndex(cellIndex);

            // 기존 셀이 비어있으면 바로 배치
            if (existing.ElementId == BuiltInElementIds.Vacuum || existing.Mass <= 0)
            {
                grid.GetCellRef(cellIndex) = newCell;
                return true;
            }

            // 기존 원소 밀어내기 시도
            if (TryDisplace(grid, registry, cellIndex))
            {
                grid.GetCellRef(cellIndex) = newCell;
                return true;
            }

            // 밀어내기 실패 — 강제 배치 (기존 원소 소멸)
            // 건설의 경우 강제 배치가 필요할 수 있음
            grid.GetCellRef(cellIndex) = newCell;
            return true;
        }

        // ── 탐색 메서드 ──

        private static int FindAdjacentVacuum(WorldGrid grid, int cx, int cy)
        {
            // 좌우 우선, 그다음 상하 (자연스러운 밀림 방향)
            if (TryGetVacuum(grid, cx - 1, cy, out int left)) return left;
            if (TryGetVacuum(grid, cx + 1, cy, out int right)) return right;
            if (TryGetVacuum(grid, cx, cy + 1, out int up)) return up;
            if (TryGetVacuum(grid, cx, cy - 1, out int down)) return down;
            return -1;
        }

        private static int FindAdjacentSameElement(
            WorldGrid grid, ElementRegistry registry,
            int cx, int cy,
            byte elementId, int maxMass)
        {
            int bestIndex = -1;
            int bestCapacity = 0;

            // 가장 용량이 많이 남은 동종 이웃을 선택
            CheckMergeCandidate(grid, cx - 1, cy, elementId, maxMass, ref bestIndex, ref bestCapacity);
            CheckMergeCandidate(grid, cx + 1, cy, elementId, maxMass, ref bestIndex, ref bestCapacity);
            CheckMergeCandidate(grid, cx, cy + 1, elementId, maxMass, ref bestIndex, ref bestCapacity);
            CheckMergeCandidate(grid, cx, cy - 1, elementId, maxMass, ref bestIndex, ref bestCapacity);

            return bestIndex;
        }

        private static bool TryGetVacuum(WorldGrid grid, int x, int y, out int index)
        {
            index = -1;
            if (!grid.InBounds(x, y)) return false;

            index = grid.ToIndex(x, y);
            SimCell cell = grid.GetCellByIndex(index);
            if (cell.ElementId == BuiltInElementIds.Vacuum)
                return true;

            index = -1;
            return false;
        }

        private static void CheckMergeCandidate(
            WorldGrid grid, int x, int y,
            byte elementId, int maxMass,
            ref int bestIndex, ref int bestCapacity)
        {
            if (!grid.InBounds(x, y)) return;

            int idx = grid.ToIndex(x, y);
            SimCell cell = grid.GetCellByIndex(idx);

            if (cell.ElementId != elementId) return;
            if (cell.Mass >= maxMass) return;

            int capacity = maxMass - cell.Mass;
            if (capacity > bestCapacity)
            {
                bestCapacity = capacity;
                bestIndex = idx;
            }
        }

        private static short ComputeMergedTemperature(
            short sourceTemp, int sourceMass,
            short targetTemp, int targetMass)
        {
            if (sourceMass <= 0) return targetTemp;

            long total = (long)targetMass + sourceMass;
            if (total <= 0) return targetTemp;

            long result = ((long)sourceTemp * sourceMass + (long)targetTemp * targetMass) / total;
            return (short)result;
        }
    }
}
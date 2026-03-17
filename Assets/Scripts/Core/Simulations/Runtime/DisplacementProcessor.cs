using System;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 물리적 밀어내기(Displacement) 처리기.
    ///
    /// ProcessVerticalGravity():
    ///   액체가 아래 기체와 스왑. 한 틱에 1칸만 낙하.
    ///   Liquid Phase 앞에서 실행 → 스왑 후 즉시 LiquidPlanner가 균등화.
    ///
    /// ProcessHorizontalDisplacement():
    ///   아래가 막힌 액체가 옆 기체를 밀어내고 질량을 나눠 채움.
    ///   Liquid Phase 뒤에서 실행 → LiquidPlanner가 처리 못한 나머지.
    /// </summary>
    public sealed class DisplacementProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly bool[] _acted;

        public DisplacementProcessor(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _acted = new bool[_grid.Length];
        }

        /// <summary>
        /// acted 배열 초기화. 새 Phase 시작 전에 호출.
        /// </summary>
        public void ClearActed()
        {
            Array.Clear(_acted, 0, _acted.Length);
        }

        // ================================================================
        //  수직 중력: 액체↔기체 스왑 (1칸/틱)
        //  SimulationRunner Phase 2에서 호출.
        // ================================================================

        public void ProcessVerticalGravity(int currentTick, bool leftToRight)
        {
            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            // 아래 → 위 순회
            // 이 방향이면 물(y=5)이 기체(y=4)와 스왑 후,
            // 루프가 y=6에 도달할 때 y=5에는 이미 올라온 기체가 있어서
            // y=6의 물도 1칸 내려간다. 결과: 물 기둥 전체가 1칸씩 동시 낙하.
            // (위→아래면 같은 물이 바닥까지 연쇄 이동해버림)
            for (int y = 1; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int upperIndex = _grid.ToIndex(x, y);

                    SimCell upperCell = _grid.GetCellByIndex(upperIndex);
                    ref readonly ElementRuntimeDefinition upperElement =
                        ref _registry.Get(upperCell.ElementId);

                    if (upperElement.BehaviorType != ElementBehaviorType.Liquid)
                        continue;
                    if (upperCell.Mass <= 0)
                        continue;

                    int lowerIndex = _grid.ToIndex(x, y - 1);

                    SimCell lowerCell = _grid.GetCellByIndex(lowerIndex);
                    ref readonly ElementRuntimeDefinition lowerElement =
                        ref _registry.Get(lowerCell.ElementId);

                    ref SimCell upperRef = ref _grid.GetCellRef(upperIndex);
                    ref SimCell lowerRef = ref _grid.GetCellRef(lowerIndex);

                    if (lowerCell.ElementId == BuiltInElementIds.Vacuum)
                    {
                        // ── 케이스 0: 아래가 진공 → 직접 이동 ──
                        lowerRef = upperRef;
                        upperRef = SimCell.Vacuum;

                        _grid.GetTickMetaRef(lowerIndex).MarkActed(currentTick);
                    }
                    else if (lowerElement.BehaviorType == ElementBehaviorType.Gas)
                    {
                        // ── 케이스 A: 아래가 기체 → 직접 스왑 ──
                        SimCell temp = upperRef;
                        upperRef = lowerRef;
                        lowerRef = temp;

                        _grid.GetTickMetaRef(lowerIndex).MarkActed(currentTick);
                    }
                    else if (lowerCell.ElementId == upperCell.ElementId &&
                             lowerCell.Mass < upperElement.MaxMass)
                    {
                        // ── 케이스 B: 아래가 같은 액체 + 용량 있음 → 질량 합류 ──
                        int capacity = upperElement.MaxMass - lowerCell.Mass;
                        int transfer = Math.Min(upperCell.Mass, capacity);

                        if (transfer > 0)
                        {
                            lowerRef.Mass += transfer;
                            upperRef.Mass -= transfer;

                            if (upperRef.Mass <= 0)
                                upperRef = SimCell.Vacuum;
                        }
                    }
                    // 아래가 같은 액체 MaxMass 또는 고체 → 수평 확산이 처리
                }
            }
        }

        // ================================================================
        //  수평 확산: 바닥에 닿은 액체가 옆 기체와 직접 스왑
        //  수직과 동일한 방식: 직접 스왑, DisplacementResolver 불필요
        //  질량 분배는 Phase 3(LiquidPlanner)가 처리
        // ================================================================

        public void ProcessHorizontalDisplacement(bool leftToRight)
        {
            Array.Clear(_acted, 0, _acted.Length);

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int actorIndex = _grid.ToIndex(x, y);
                    if (_acted[actorIndex])
                        continue;

                    SimCell actorCell = _grid.GetCellByIndex(actorIndex);
                    ref readonly ElementRuntimeDefinition actorElement =
                        ref _registry.Get(actorCell.ElementId);

                    if (actorElement.BehaviorType != ElementBehaviorType.Liquid)
                        continue;
                    if (actorCell.Mass <= 0)
                        continue;

                    if (!IsGrounded(x, y, in actorElement))
                        continue;

                    // 좌우 기체와 직접 스왑
                    int leftX = x - 1;
                    int rightX = x + 1;

                    bool canLeft = IsSwappableGas(leftX, y, in actorElement);
                    bool canRight = IsSwappableGas(rightX, y, in actorElement);

                    // 한 틱에 한쪽만 스왑, leftToRight 교대로 편향 제거
                    // 스왑 후 Phase 3(LiquidPlanner)가 질량 균등화 처리
                    if (canLeft && canRight)
                    {
                        int targetX = leftToRight ? leftX : rightX;
                        DoHorizontalSwap(actorIndex, targetX, y);
                        _acted[actorIndex] = true;
                        _acted[_grid.ToIndex(targetX, y)] = true;
                    }
                    else if (canLeft)
                    {
                        DoHorizontalSwap(actorIndex, leftX, y);
                        _acted[actorIndex] = true;
                        _acted[_grid.ToIndex(leftX, y)] = true;
                    }
                    else if (canRight)
                    {
                        DoHorizontalSwap(actorIndex, rightX, y);
                        _acted[actorIndex] = true;
                        _acted[_grid.ToIndex(rightX, y)] = true;
                    }
                }
            }
        }

        private bool IsSwappableGas(int x, int y, in ElementRuntimeDefinition actorElement)
        {
            if (!_grid.InBounds(x, y))
                return false;

            int idx = _grid.ToIndex(x, y);
            if (_acted[idx])
                return false;

            SimCell cell = _grid.GetCellByIndex(idx);
            if (cell.ElementId == BuiltInElementIds.Vacuum)
                return false;

            ref readonly ElementRuntimeDefinition element = ref _registry.Get(cell.ElementId);
            if (element.BehaviorType != ElementBehaviorType.Gas)
                return false;

            return element.DisplacementPriority < actorElement.DisplacementPriority;
        }

        private void DoHorizontalSwap(int sourceIndex, int targetX, int targetY)
        {
            int targetIndex = _grid.ToIndex(targetX, targetY);

            ref SimCell sourceRef = ref _grid.GetCellRef(sourceIndex);
            ref SimCell targetRef = ref _grid.GetCellRef(targetIndex);

            SimCell temp = sourceRef;
            sourceRef = targetRef;
            targetRef = temp;
        }

        /// <summary>
        /// 셀이 "바닥에 닿았는지" 판정.
        /// true = 아래로 더 내려갈 수 없음 → 수평 확산 허용
        /// false = 아래로 이동 가능 → 수평 확산 금지 (낙하 우선)
        /// </summary>
        private bool IsGrounded(int x, int y, in ElementRuntimeDefinition actorElement)
        {
            int belowY = y - 1;
            if (belowY < 0)
                return true; // 월드 바닥

            SimCell below = _grid.GetCell(x, belowY);

            // 진공 → 아래로 갈 수 있음 (LiquidPlanner가 처리)
            if (below.ElementId == BuiltInElementIds.Vacuum)
                return false;

            ref readonly ElementRuntimeDefinition belowElement =
                ref _registry.Get(below.ElementId);

            // 기체 → 다음 틱에 수직 스왑으로 내려감
            if (belowElement.BehaviorType == ElementBehaviorType.Gas)
                return false;

            // 같은 액체인데 용량이 남아있음 → 수직 합류로 내려감
            if (below.ElementId == actorElement.Id && below.Mass < actorElement.MaxMass)
                return false;

            // 고체, 가득 찬 같은 액체, 다른 액체 → 바닥에 닿음
            return true;
        }

    }
}
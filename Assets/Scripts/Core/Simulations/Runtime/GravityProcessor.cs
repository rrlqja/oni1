using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 1: 통합 중력 처리기 — 수직 방향만.
    ///
    /// FallingSolid와 Liquid의 아래 방향 Swap/Merge를 수집한다.
    /// 좌우 확산은 Phase 2(LiquidFlowProcessor)가 담당.
    ///
    /// 방향 분리 원칙:
    ///   Phase 1 = 수직, Phase 2 = 좌우 → 방향이 겹치지 않으므로
    ///   MarkActed 불필요, 2칸 낙하 문제 원천 차단.
    /// </summary>
    public sealed class GravityProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        public GravityProcessor(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 전체 셀을 순회하며 중력 커맨드를 수집한다.
        /// </summary>
        /// <param name="currentTick">현재 틱 (MarkActed용)</param>
        /// <param name="leftToRight">좌우 스캔 방향 교대</param>
        /// <param name="output">수집된 Swap/Merge 커맨드</param>
        public void BuildCommands(
            int currentTick,
            bool leftToRight,
            List<SimulationCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            // 아래 → 위 순회: y=1부터 (y=0은 아래 셀이 없으므로 skip)
            for (int y = 1; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int upperIndex = _grid.ToIndex(x, y);

                    SimCell upperCell = _grid.GetCellByIndex(upperIndex);
                    ref readonly ElementRuntimeDefinition upperDef =
                        ref _registry.Get(upperCell.ElementId);

                    // FallingSolid 또는 Liquid만 대상
                    if (upperDef.BehaviorType != ElementBehaviorType.FallingSolid &&
                        upperDef.BehaviorType != ElementBehaviorType.Liquid)
                        continue;

                    if (upperCell.Mass <= 0)
                        continue;

                    int lowerIndex = _grid.ToIndex(x, y - 1);

                    SimCell lowerCell = _grid.GetCellByIndex(lowerIndex);
                    ref readonly ElementRuntimeDefinition lowerDef =
                        ref _registry.Get(lowerCell.ElementId);

                    GravityOperator.Result result = GravityOperator.Evaluate(
                        in upperCell, in upperDef,
                        in lowerCell, in lowerDef);

                    switch (result)
                    {
                        case GravityOperator.Result.Swap:
                            output.Add(SimulationCommand.CreateSwap(upperIndex, lowerIndex));
                            break;

                        case GravityOperator.Result.Merge:
                            output.Add(SimulationCommand.CreateMergeMass(upperIndex, lowerIndex));
                            break;

                        case GravityOperator.Result.None:
                        default:
                            break;
                    }
                }
            }
        }

    }
}
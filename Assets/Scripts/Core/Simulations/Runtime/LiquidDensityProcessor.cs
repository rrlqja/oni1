using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 3: 액체 밀도 이동 처리기.
    ///
    /// 같은 BehaviorType(Liquid) 내에서 밀도가 역전된 셀을 교환한다.
    /// DensityOperator.ShouldSwap()으로 판정.
    ///
    /// 예:
    ///   오염된 물(밀도 1050) 위, 물(밀도 1000) 아래 → Swap
    ///   물(밀도 1000) 위, 액화수소(밀도 70) 아래 → Swap
    ///
    /// 순회: 아래→위, 좌우 교대 (수직 교환만)
    /// 충돌 방지: 불필요 (하나의 셀 위에는 하나의 셀만 존재)
    /// </summary>
    public sealed class LiquidDensityProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        public LiquidDensityProcessor(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 전체 셀을 순회하며 액체 밀도 교환 Swap 커맨드를 수집한다.
        /// </summary>
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

            // 아래 → 위 순회
            for (int y = 1; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int upperIndex = _grid.ToIndex(x, y);
                    int lowerIndex = _grid.ToIndex(x, y - 1);

                    SimCell upperCell = _grid.GetCellByIndex(upperIndex);
                    SimCell lowerCell = _grid.GetCellByIndex(lowerIndex);

                    if (upperCell.Mass <= 0 || lowerCell.Mass <= 0)
                        continue;

                    ref readonly ElementRuntimeDefinition upperDef =
                        ref _registry.Get(upperCell.ElementId);
                    ref readonly ElementRuntimeDefinition lowerDef =
                        ref _registry.Get(lowerCell.ElementId);

                    if (DensityOperator.ShouldSwap(
                            in upperDef, in lowerDef,
                            ElementBehaviorType.Liquid))
                    {
                        output.Add(SimulationCommand.CreateSwap(upperIndex, lowerIndex));
                    }
                }
            }
        }
    }
}
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
    /// 수직 교환: 매 틱 실행. 무거운 액체가 가벼운 액체 아래로 가라앉는다.
    /// 수평 교환: 해시 기반 확률로 실행. 이종 액체 경계면에서 느린 혼합 효과.
    /// </summary>
    public sealed class LiquidDensityProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly SimulationSettings _settings;

        public LiquidDensityProcessor(WorldGrid grid, ElementRegistry registry, SimulationSettings settings)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void BuildCommands(
            int currentTick,
            bool leftToRight,
            List<SimulationCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            BuildVerticalSwaps(leftToRight, output);
            BuildHorizontalSwaps(currentTick, leftToRight, output);
        }

        // ================================================================
        //  수직 밀도 교환 — 무거운 액체가 가벼운 액체 아래로 (매 틱)
        // ================================================================

        private void BuildVerticalSwaps(
            bool leftToRight,
            List<SimulationCommand> output)
        {
            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

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

        // ================================================================
        //  수평 밀도 교환 — 이종 액체 경계면 느린 혼합 (해시 기반 확률)
        // ================================================================

        private void BuildHorizontalSwaps(
            int currentTick,
            bool leftToRight,
            List<SimulationCommand> output)
        {
            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    // 해시 기반 확률: 셀마다 다른 타이밍에 시도
                    uint hash = MixHash(currentTick, x, y);
                    if (hash % _settings.LiquidHorizontalInterval != 0)
                        continue;

                    int sourceIndex = _grid.ToIndex(x, y);
                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);

                    if (sourceCell.Mass <= 0)
                        continue;

                    ref readonly ElementRuntimeDefinition sourceDef =
                        ref _registry.Get(sourceCell.ElementId);

                    if (sourceDef.BehaviorType != ElementBehaviorType.Liquid)
                        continue;

                    // leftToRight 방향의 다음 셀만 확인 (쌍 중복 방지)
                    int neighborX = x + stepX;
                    if (neighborX < 0 || neighborX >= _grid.Width)
                        continue;

                    int neighborIndex = _grid.ToIndex(neighborX, y);
                    SimCell neighborCell = _grid.GetCellByIndex(neighborIndex);

                    if (neighborCell.Mass <= 0)
                        continue;

                    ref readonly ElementRuntimeDefinition neighborDef =
                        ref _registry.Get(neighborCell.ElementId);

                    if (neighborDef.BehaviorType != ElementBehaviorType.Liquid)
                        continue;

                    if (sourceDef.Id == neighborDef.Id)
                        continue;

                    // 이종 액체면 교환 (밀도 차이 있을 때)
                    if (sourceDef.Density != neighborDef.Density)
                    {
                        output.Add(SimulationCommand.CreateSwap(sourceIndex, neighborIndex));
                    }
                }
            }
        }

        // ================================================================
        //  해시 유틸리티
        // ================================================================

        private static uint MixHash(int tick, int x, int y)
        {
            uint h = (uint)tick;
            h ^= (uint)x * 0x9E3779B1u;
            h ^= (uint)y * 0x517CC1B7u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return h;
        }
    }
}
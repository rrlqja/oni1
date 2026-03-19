using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 시뮬레이션 틱 파이프라인 조율자 (v3).
    ///
    /// 방향 분리 원칙:
    ///   Phase 1: Gravity          — 수직     FallingSolid + Liquid 낙하 (Swap, Merge)
    ///   Phase 2: LiquidFlow       — 좌우     액체 확산 + 기체 밀어내기 (FlowBatch, Swap)
    ///   Phase 3: LiquidDensity    — 수직     이종 액체 밀도 교환 (Swap)
    ///   Phase 4: GasEqualization  — 상하좌우  같은 가스끼리 균등화 (FlowBatch)
    ///   Phase 5: GasDensity       — 상하좌우  밀도 인지 이동 (FlowBatch, Swap)
    ///
    /// Phase 1과 Phase 2의 방향이 겹치지 않으므로
    /// MarkActed 없이 같은 틱에 낙하 + 좌우 확산이 가능.
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private readonly List<SimulationCommand> _commands = new(256);
        private readonly List<SimulationCommand> _displaceCommands = new(64);
        private readonly List<FlowBatchCommand> _flowCommands = new(256);
        private readonly bool[] _swapTargetUsed;

        private readonly GravityProcessor _gravityProcessor;
        private readonly LiquidFlowProcessor _liquidFlowProcessor;
        private readonly LiquidDensityProcessor _liquidDensityProcessor;
        private readonly GasFlowPlanner _gasFlowPlanner;

        private readonly CommandApplier _commandApplier;
        private readonly FlowBatchApplier _flowBatchApplier;

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _swapTargetUsed = new bool[_grid.Length];

            _gravityProcessor = new GravityProcessor(_grid, _registry);
            _liquidFlowProcessor = new LiquidFlowProcessor(_grid, _registry);
            _liquidDensityProcessor = new LiquidDensityProcessor(_grid, _registry);
            _gasFlowPlanner = new GasFlowPlanner(_grid, _registry);

            _commandApplier = new CommandApplier(_grid, _registry);
            _flowBatchApplier = new FlowBatchApplier(_grid, _registry);
        }

        public void Step(int currentTick)
        {
            bool leftToRight = (currentTick & 1) == 0;

            // Phase 1: 중력
            _commands.Clear();
            _grid.ClearAllTickReservations();
            _gravityProcessor.BuildCommands(currentTick, leftToRight, _commands);
            _commandApplier.Apply(_commands);

            // Phase 2: 액체 좌우 확산 + 기체 밀어내기
            //   Phase 1(수직)과 방향이 겹치지 않으므로 MarkActed 불필요.
            //   물이 Phase 1에서 1칸 낙하 → 같은 틱에 Phase 2에서 좌우 확산.
            _flowCommands.Clear();
            _displaceCommands.Clear();
            Array.Clear(_swapTargetUsed, 0, _swapTargetUsed.Length);
            _liquidFlowProcessor.BuildFlowBatches(
                currentTick, leftToRight,
                _flowCommands, _displaceCommands, _swapTargetUsed);
            _commandApplier.Apply(_displaceCommands);   // Swap/Merge 먼저 (길 비움)
            _flowBatchApplier.Apply(_flowCommands);      // FlowBatch 다음 (확산)

            // Phase 3: 액체 밀도 이동 — 이종 액체 밀도 역전 교환
            _commands.Clear();
            _grid.ClearAllTickReservations();
            _liquidDensityProcessor.BuildCommands(currentTick, leftToRight, _commands);
            _commandApplier.Apply(_commands);

            // Phase 4: 기체 균등화
            _flowCommands.Clear();
            _grid.ClearAllTickReservations();
            _gasFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // Phase 5: 기체 밀도 이동
            _flowCommands.Clear();
            _commands.Clear();
            _grid.ClearAllTickReservations();
            _gasFlowPlanner.BuildDensityAwareMovement(
                currentTick, leftToRight, _flowCommands, _commands);
            _flowBatchApplier.Apply(_flowCommands);
            _commandApplier.Apply(_commands);

            _grid.ClearAllTickReservations();
        }
    }
}
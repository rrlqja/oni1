using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 시뮬레이션 틱 파이프라인 조율자.
    /// 각 페이즈의 실행 순서만 관리하며, 구체적인 로직은 프로세서에 위임한다.
    ///
    /// 파이프라인:
    ///   1. FallingSolid  — 고체 낙하, Swap, Merge
    ///   2. Liquid         — 액체 하향 + 좌우 확산
    ///   3. Gas A          — 같은 가스끼리 균등화
    ///   4. Gas B          — 밀도 인지 이동 (drift + swap)
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        // ── 커맨드 버퍼 (재사용) ──
        private readonly List<SimulationCommand> _commands = new(256);
        private readonly List<FlowBatchCommand> _flowCommands = new(256);

        // ── 프로세서 ──
        private readonly FallingSolidProcessor _fallingSolidProcessor;
        private readonly LiquidFlowPlanner _liquidFlowPlanner;
        private readonly GasFlowPlanner _gasFlowPlanner;

        // ── Applier ──
        private readonly CommandApplier _commandApplier;
        private readonly FlowBatchApplier _flowBatchApplier;

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _fallingSolidProcessor = new FallingSolidProcessor(_grid, _registry);
            _liquidFlowPlanner = new LiquidFlowPlanner(_grid, _registry);
            _gasFlowPlanner = new GasFlowPlanner(_grid, _registry);

            _commandApplier = new CommandApplier(_grid, _registry);
            _flowBatchApplier = new FlowBatchApplier(_grid, _registry);
        }

        public void Step(int currentTick)
        {
            bool leftToRight = (currentTick & 1) == 0;

            // 1) FallingSolid — 고체 낙하
            _commands.Clear();
            _grid.ClearAllTickReservations();

            _fallingSolidProcessor.BuildCommands(currentTick, leftToRight, _commands);
            _commandApplier.Apply(_commands);

            // 2) Liquid — 액체 확산
            _flowCommands.Clear();
            _grid.ClearAllTickReservations();

            _liquidFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // 3) Gas A — 균등화 (같은 가스끼리 질량 분배)
            _flowCommands.Clear();
            _grid.ClearAllTickReservations();

            _gasFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // 4) Gas B — 밀도 인지 이동 (진공 drift + 이종 가스 swap)
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
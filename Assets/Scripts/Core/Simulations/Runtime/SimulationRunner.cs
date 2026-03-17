using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 시뮬레이션 틱 파이프라인 조율자.
    ///
    /// 파이프라인:
    ///   1. FallingSolid    — 고체 낙하, Swap, Merge
    ///   2. Displacement↕   — 액체↔기체 수직 스왑 (중력)
    ///   3. Liquid           — 액체 하향 + 좌우 확산 (진공/동종 대상)
    ///   4. Displacement↔   — 액체가 옆 기체를 밀어내고 질량 분할
    ///   5. Gas A            — 같은 가스끼리 균등화
    ///   6. Gas B            — 밀도 인지 이동 (drift + swap)
    ///
    /// Displacement↕를 Liquid 앞에 배치하여:
    ///   - 수직 스왑 후 같은 틱에 LiquidPlanner가 즉시 질량 균등화
    ///   - MaxMass 누적 방지
    ///   - 한 틱에 1칸만 낙하 보장
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private readonly List<SimulationCommand> _commands = new(256);
        private readonly List<FlowBatchCommand> _flowCommands = new(256);

        private readonly FallingSolidProcessor _fallingSolidProcessor;
        private readonly DisplacementProcessor _displacementProcessor;
        private readonly LiquidFlowPlanner _liquidFlowPlanner;
        private readonly GasFlowPlanner _gasFlowPlanner;

        private readonly CommandApplier _commandApplier;
        private readonly FlowBatchApplier _flowBatchApplier;

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _fallingSolidProcessor = new FallingSolidProcessor(_grid, _registry);
            _displacementProcessor = new DisplacementProcessor(_grid, _registry);
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

            // 2) Displacement ↕ — 액체가 기체를 통과해서 가라앉음 (1칸/틱)
            //    MarkActed로 스왑된 셀을 표시 → Phase 3이 건너뜀
            _grid.ClearAllTickReservations();
            _displacementProcessor.ClearActed();
            _displacementProcessor.ProcessVerticalGravity(currentTick, leftToRight);

            // 3) Liquid — 진공/동종 액체 대상 확산 + 하향
            //    주의: ClearAllTickReservations 하지 않음!
            //    Phase 2에서 MarkActed된 셀은 LiquidPlanner가 건너뜀
            //    → 방금 스왑으로 내려온 물이 같은 틱에 또 이동하지 않음
            _flowCommands.Clear();
            _liquidFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // 4) Displacement ↔ — 액체가 옆 기체를 밀어내고 질량 분할
            //    Phase 3에서 처리 못한 잔여 케이스 (옆에 기체만 있을 때)
            _grid.ClearAllTickReservations();
            _displacementProcessor.ProcessHorizontalDisplacement(leftToRight);

            // 5) Gas A — 같은 가스끼리 균등화
            _flowCommands.Clear();
            _grid.ClearAllTickReservations();
            _gasFlowPlanner.BuildNormalFlowBatches(currentTick, leftToRight, _flowCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // 6) Gas B — 밀도 인지 이동 (진공 drift + 이종 가스 swap)
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
using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 시뮬레이션 틱 파이프라인 조율자 (v3 + 투사체 낙하).
    ///
    /// 방향 분리 원칙:
    ///   Phase 0: ProjectileScan   — 투사체 대상 감지 + 그리드 제거
    ///   Phase 1: Gravity          — 수직     FallingSolid + Liquid 셀 낙하
    ///   Phase 2: LiquidFlow       — 좌우     액체 확산 + 기체 밀어내기
    ///   Phase 3: LiquidDensity    — 수직     이종 액체 밀도 교환
    ///   Phase 4: GasEqualization  — 상하좌우  같은 가스끼리 균등화
    ///   Phase 5: GasDensity       — 상하좌우  밀도 인지 이동
    ///   Phase 6: ProjectileMove   — 투사체 이동 + 착지 + 그리드 재생성
    ///   Phase 7: HeatConduction   — 인접 셀 간 열 전도 (NEW)
    ///
    /// Phase 0에서 투사체 대상 셀이 그리드에서 제거되므로,
    /// Phase 1에서 해당 셀과 불필요한 Swap이 발생하지 않는다.
    /// Phase 6에서 착지한 원소가 그리드에 재생성되면,
    /// 다음 틱부터 정상적인 시뮬레이션 대상이 된다.
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        private readonly List<SimulationCommand> _commands = new(256);
        private readonly List<SimulationCommand> _displaceCommands = new(64);
        private readonly List<FlowBatchCommand> _flowCommands = new(256);
        private readonly bool[] _swapTargetUsed;

        private readonly ProjectileScanProcessor _projectileScanProcessor;
        private readonly GravityProcessor _gravityProcessor;
        private readonly LiquidFlowProcessor _liquidFlowProcessor;
        private readonly LiquidDensityProcessor _liquidDensityProcessor;
        private readonly GasFlowPlanner _gasFlowPlanner;
        private readonly TemperatureProcessor _temperatureProcessor;

        private readonly CommandApplier _commandApplier;
        private readonly FlowBatchApplier _flowBatchApplier;
        private readonly FallingEntityManager _fallingEntityManager;

        /// <summary>투사체 엔티티 매니저 (렌더링에서 참조)</summary>
        public FallingEntityManager FallingEntities => _fallingEntityManager;

        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _swapTargetUsed = new bool[_grid.Length];

            _fallingEntityManager = new FallingEntityManager(_grid, _registry);
            _projectileScanProcessor = new ProjectileScanProcessor(_grid, _registry, _fallingEntityManager);
            _gravityProcessor = new GravityProcessor(_grid, _registry);
            _liquidFlowProcessor = new LiquidFlowProcessor(_grid, _registry);
            _liquidDensityProcessor = new LiquidDensityProcessor(_grid, _registry);
            _gasFlowPlanner = new GasFlowPlanner(_grid, _registry);

            _commandApplier = new CommandApplier(_grid, _registry);
            _flowBatchApplier = new FlowBatchApplier(_grid, _registry);

            _temperatureProcessor = new TemperatureProcessor(_grid, _registry);
        }

        public void Step(int currentTick)
        {
            bool leftToRight = (currentTick & 1) == 0;

            // Phase 0: 투사체 스캔 — 대상 셀 감지 + 그리드에서 제거 + 엔티티 생성
            // Phase 1 전에 실행: 투사체 대상이 먼저 빠져야 Phase 1에서 Swap 안 함.
            _projectileScanProcessor.Scan(currentTick, leftToRight);

            // Phase 1: 중력 (셀 낙하) — 투사체 대상이 아닌 셀만 처리
            _commands.Clear();
            _grid.ClearAllTickReservations();
            _gravityProcessor.BuildCommands(currentTick, leftToRight, _commands);
            _commandApplier.Apply(_commands);

            // Phase 2: 액체 좌우 확산 + 기체 밀어내기
            _flowCommands.Clear();
            _displaceCommands.Clear();
            Array.Clear(_swapTargetUsed, 0, _swapTargetUsed.Length);
            _liquidFlowProcessor.BuildFlowBatches(
                currentTick, leftToRight,
                _flowCommands, _displaceCommands, _swapTargetUsed);
            _commandApplier.Apply(_displaceCommands);
            _flowBatchApplier.Apply(_flowCommands);

            // Phase 2.5: 투사체 후속 스캔
            //   Phase 2에서 좌우 확산으로 가장자리에 새로 생성된 액체를 감지.
            //   없으면 비용 무시할 수준 (빈 candidates).
            _projectileScanProcessor.Scan(currentTick, leftToRight);

            // Phase 3: 액체 밀도 이동
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

            // Phase 6: 투사체 이동 + 착지 — 다른 Phase 완료 후 실행
            //   착지한 원소는 그리드에 재생성 → 다음 틱부터 정상 시뮬레이션 대상
            _fallingEntityManager.ProcessTick();
            _grid.ClearAllTickReservations();

            // Phase 7: 열 전도 — 인접 셀 간 열 교환
            _temperatureProcessor.Process();
        }
    }
}
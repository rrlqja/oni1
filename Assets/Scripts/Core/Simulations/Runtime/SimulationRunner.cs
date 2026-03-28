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
    /// 방향 분리 원칙:
    ///   Phase 0: ProjectileScan   — 투사체 대상 감지 + 그리드 제거
    ///   Phase 1: Gravity          — 수직     FallingSolid + Liquid 셀 낙하
    ///   Phase 2: LiquidFlow       — 좌우     액체 확산 + 기체 밀어내기
    ///   Phase 2.5: ProjectileScan — 후속 스캔 (가장자리 액체)
    ///   Phase 3: LiquidDensity    — 수직     이종 액체 밀도 교환
    ///   Phase 4: GasEqualization  — 상하좌우  같은 가스끼리 균등화
    ///   Phase 5: GasDensity       — 상하좌우  밀도 인지 이동
    ///   Phase 6: ProjectileMove   — 투사체 이동 + 착지
    ///   Phase 7: HeatConduction   — 인접 셀 간 열 전도
    ///   Phase 8: StateTransition  — 온도 기반 원소 전환
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly SimulationSettings _settings;

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

        private readonly StateTransitionProcessor _stateTransitionProcessor;

        /// <summary>투사체 엔티티 매니저 (렌더링에서 참조)</summary>
        public FallingEntityManager FallingEntities => _fallingEntityManager;

        /// <summary>현재 시뮬레이션 설정 (읽기 전용 접근)</summary>
        public SimulationSettings Settings => _settings;

        /// <summary>
        /// 프로덕션 생성자. SimulationSettingsSO에서 모든 튜닝 값을 읽는다.
        /// </summary>
        public SimulationRunner(WorldGrid grid, ElementRegistry registry, SimulationSettings settings)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = settings ?? new SimulationSettings(null);

            _swapTargetUsed = new bool[_grid.Length];

            _fallingEntityManager = new FallingEntityManager(_grid, _registry, _settings);
            _projectileScanProcessor = new ProjectileScanProcessor(_grid, _registry, _fallingEntityManager, _settings);
            _gravityProcessor = new GravityProcessor(_grid, _registry);
            _liquidFlowProcessor = new LiquidFlowProcessor(_grid, _registry, _settings);
            _liquidDensityProcessor = new LiquidDensityProcessor(_grid, _registry, _settings);
            _gasFlowPlanner = new GasFlowPlanner(_grid, _registry, _settings);

            _commandApplier = new CommandApplier(_grid, _registry);
            _flowBatchApplier = new FlowBatchApplier(_grid, _registry);

            _temperatureProcessor = new TemperatureProcessor(_grid, _registry, _settings);
            _stateTransitionProcessor = new StateTransitionProcessor(_grid, _registry, _settings);
        }

        /// <summary>
        /// 테스트 호환 생성자. 기본 설정값 사용.
        /// </summary>
        public SimulationRunner(WorldGrid grid, ElementRegistry registry)
            : this(grid, registry, new SimulationSettings(null))
        {
        }

        public void Step(int currentTick)
        {
            bool leftToRight = (currentTick & 1) == 0;

            // Phase 0: 투사체 스캔
            _projectileScanProcessor.Scan(currentTick, leftToRight);

            // Phase 1: 중력 (셀 낙하)
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

            // Phase 6: 투사체 이동 + 착지
            _fallingEntityManager.ProcessTick();
            _grid.ClearAllTickReservations();

            // Phase 7: 열 전도
            _temperatureProcessor.Process();

            // Phase 8: 상태변환
            _stateTransitionProcessor.Process();
        }
    }
}
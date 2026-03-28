using System;
using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 0: 투사체 전환 스캔.
    ///
    /// 투사체의 목적: 빈 공간(진공/기체)을 Swap 없이 빠르게 통과하는 것.
    ///
    /// 트리거 조건:
    ///   FallingSolid: 아래가 고체가 아닌 경우 (진공, 기체, 액체 모두 통과)
    ///   Liquid: 아래가 진공 또는 기체이고, 양쪽이 고체벽으로 격납되지 않은 경우
    ///           (양쪽 고체벽 = 1칸 너비 통로, 셀 낙하로 처리)
    ///           (그 외 = 투사체, 절벽 가장자리 물 포함)
    /// </summary>
    public sealed class ProjectileScanProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly FallingEntityManager _fallingEntityManager;
        private readonly SimulationSettings _settings;

        private readonly List<ProjectileCandidate> _candidates = new(64);
        private readonly bool[] _solidColumnUsed;

        private struct ProjectileCandidate
        {
            public int CellIndex;
            public int X;
            public int Y;
            public byte ElementId;
            public int Mass;
            public float Temperature;
            public int FallSpeed;
        }

        public ProjectileScanProcessor(
            WorldGrid grid,
            ElementRegistry registry,
            FallingEntityManager fallingEntityManager, 
            SimulationSettings settings = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _fallingEntityManager = fallingEntityManager
                ?? throw new ArgumentNullException(nameof(fallingEntityManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _solidColumnUsed = new bool[_grid.Width];
        }

        public void Scan(int currentTick, bool leftToRight)
        {
            _candidates.Clear();
            Array.Clear(_solidColumnUsed, 0, _solidColumnUsed.Length);

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            // ── 수집 단계 ──
            for (int y = 1; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int index = _grid.ToIndex(x, y);

                    SimCell cell = _grid.GetCellByIndex(index);
                    if (cell.Mass <= 0)
                        continue;

                    ref readonly ElementRuntimeDefinition def =
                        ref _registry.Get(cell.ElementId);

                    if (def.BehaviorType == ElementBehaviorType.FallingSolid)
                    {
                        if (_solidColumnUsed[x])
                            continue;

                        if (ShouldProjectileFallSolid(x, y, in cell, in def))
                        {
                            _solidColumnUsed[x] = true;
                            _candidates.Add(new ProjectileCandidate
                            {
                                CellIndex = index, X = x, Y = y,
                                ElementId = cell.ElementId,
                                Mass = cell.Mass,
                                Temperature = cell.Temperature,
                                FallSpeed = _settings.ProjectileFallSpeedSolid
                            });
                        }
                    }
                    else if (def.BehaviorType == ElementBehaviorType.Liquid)
                    {
                        if (ShouldProjectileFallLiquid(x, y, in cell, in def))
                        {
                            _candidates.Add(new ProjectileCandidate
                            {
                                CellIndex = index, X = x, Y = y,
                                ElementId = cell.ElementId,
                                Mass = cell.Mass,
                                Temperature = cell.Temperature,
                                FallSpeed = _settings.ProjectileFallSpeedLiquid
                            });
                        }
                    }
                }
            }

            // ── 적용 단계 ──
            for (int i = 0; i < _candidates.Count; i++)
            {
                ProjectileCandidate c = _candidates[i];

                SimCell current = _grid.GetCellByIndex(c.CellIndex);
                if (current.ElementId != c.ElementId || current.Mass != c.Mass)
                    continue;

                _fallingEntityManager.Spawn(
                    c.ElementId, c.Mass, c.Temperature,
                    c.X, c.Y, c.FallSpeed);

                _grid.GetCellRef(c.CellIndex) = SimCell.Vacuum;
            }
        }

        // ================================================================
        //  FallingSolid 판정
        // ================================================================

        private bool ShouldProjectileFallSolid(
            int x, int y,
            in SimCell cell,
            in ElementRuntimeDefinition def)
        {
            int belowY = y - 1;
            if (belowY < 0)
                return false;

            SimCell below = _grid.GetCell(x, belowY);
            ref readonly ElementRuntimeDefinition belowDef =
                ref _registry.Get(below.ElementId);

            if (below.ElementId == cell.ElementId && below.Mass < belowDef.MaxMass)
                return false;

            if (belowDef.IsSolid)
                return false;

            return true;
        }

        // ================================================================
        //  Liquid 판정
        // ================================================================

        /// <summary>
        /// Liquid가 투사체로 전환되어야 하는지 판정.
        ///
        /// 조건:
        ///   아래가 진공 또는 기체
        ///   AND 양쪽이 고체벽으로 격납되지 않음
        ///
        /// 양쪽 고체벽 격납 = 1칸 너비 통로. 이 경우 셀 낙하(Phase 1)로 처리.
        /// 그 외(절벽 가장자리, 넓은 공간 등) = 투사체.
        /// </summary>
        private bool ShouldProjectileFallLiquid(
            int x, int y,
            in SimCell cell,
            in ElementRuntimeDefinition def)
        {
            int belowY = y - 1;
            if (belowY < 0)
                return false;

            SimCell below = _grid.GetCell(x, belowY);
            ref readonly ElementRuntimeDefinition belowDef =
                ref _registry.Get(below.ElementId);

            // 아래가 고체 → 정지 (좌우 확산 대상)
            if (belowDef.IsSolid)
                return false;

            // 아래가 같은 액체 → 셀 낙하 또는 정지
            if (below.ElementId == cell.ElementId)
                return false;

            // 아래가 다른 종류 액체 → Phase 3 밀도 이동이 처리
            if (belowDef.BehaviorType == ElementBehaviorType.Liquid)
                return false;

            // 아래가 진공 또는 기체 → 투사체 후보
            if (belowDef.BehaviorType != ElementBehaviorType.Vacuum &&
                belowDef.BehaviorType != ElementBehaviorType.Gas)
                return false;

            // 격납 확인: 양쪽이 모두 고체이면 셀 낙하 (1칸 너비 통로)
            if (IsContainedBySolid(x, y))
                return false;

            return true;
        }

        /// <summary>
        /// 양쪽(좌, 우)이 모두 고체 또는 월드 경계인지 확인.
        /// true이면 1칸 너비 통로에 격납된 상태 → 셀 낙하로 처리.
        /// </summary>
        private bool IsContainedBySolid(int x, int y)
        {
            bool leftSolid;
            if (x <= 0)
            {
                leftSolid = true; // 월드 좌측 경계
            }
            else
            {
                ref readonly ElementRuntimeDefinition leftDef =
                    ref _registry.Get(_grid.GetCell(x - 1, y).ElementId);
                leftSolid = leftDef.IsSolid;
            }

            bool rightSolid;
            if (x >= _grid.Width - 1)
            {
                rightSolid = true; // 월드 우측 경계
            }
            else
            {
                ref readonly ElementRuntimeDefinition rightDef =
                    ref _registry.Get(_grid.GetCell(x + 1, y).ElementId);
                rightSolid = rightDef.IsSolid;
            }

            return leftSolid && rightSolid;
        }
    }
}
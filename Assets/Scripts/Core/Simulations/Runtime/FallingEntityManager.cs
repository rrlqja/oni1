using System;
using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 투사체 낙하 엔티티 관리자.
    ///
    /// 착지 규칙:
    ///   FallingSolid: 진공/기체/액체를 모두 통과. 고체 위에서만 착지.
    ///                 착지점에 액체가 있으면 밀어내고 그 자리에 배치.
    ///   Liquid: 진공/기체를 통과. 고체 또는 액체 표면 위에 착지.
    /// </summary>
    public sealed class FallingEntityManager
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly List<FallingEntity> _entities = new(64);

        public IReadOnlyList<FallingEntity> ActiveEntities => _entities;
        public int Count => _entities.Count;

        public FallingEntityManager(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Spawn(byte elementId, int mass, short temperature,
            int cellX, int cellY, int fallSpeed)
        {
            if (mass <= 0)
                return;

            _entities.Add(new FallingEntity(
                elementId, mass, temperature,
                cellX, cellY, fallSpeed));
        }

        public void ProcessTick()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                FallingEntity entity = _entities[i];

                if (!entity.IsActive)
                {
                    _entities.RemoveAt(i);
                    continue;
                }

                bool landed = TryMoveAndLand(ref entity);

                if (landed)
                    _entities.RemoveAt(i);
                else
                    _entities[i] = entity;
            }
        }

        public void Clear()
        {
            _entities.Clear();
        }

        // ================================================================
        //  이동 + 착지
        // ================================================================

        private bool TryMoveAndLand(ref FallingEntity entity)
        {
            int x = entity.CellX;
            int startY = (int)entity.CurrentY;
            int targetY = startY - entity.FallSpeed;

            if (targetY < 0)
                targetY = 0;

            ref readonly ElementRuntimeDefinition entityDef =
                ref _registry.Get(entity.ElementId);

            bool isSolid = entityDef.BehaviorType == ElementBehaviorType.FallingSolid;

            for (int y = startY - 1; y >= targetY; y--)
            {
                if (y < 0)
                    return LandAtPosition(ref entity, x, 0, isSolid);

                if (!_grid.InBounds(x, y))
                    continue;

                SimCell belowCell = _grid.GetCell(x, y);
                ref readonly ElementRuntimeDefinition belowDef =
                    ref _registry.Get(belowCell.ElementId);

                LandingResult result = EvaluateLanding(
                    in entityDef, in entity,
                    in belowDef, in belowCell,
                    isSolid);

                switch (result)
                {
                    case LandingResult.LandAbove:
                        return LandAtPosition(ref entity, x, y + 1, isSolid);

                    case LandingResult.Merge:
                        return MergeInto(ref entity, x, y, isSolid);

                    case LandingResult.PassThrough:
                        continue;
                }
            }

            entity.CurrentY = targetY;
            return false;
        }

        // ================================================================
        //  착지 판정
        // ================================================================

        private enum LandingResult : byte
        {
            PassThrough,
            LandAbove,
            Merge,
        }

        private LandingResult EvaluateLanding(
            in ElementRuntimeDefinition entityDef,
            in FallingEntity entity,
            in ElementRuntimeDefinition belowDef,
            in SimCell belowCell,
            bool entityIsSolid)
        {
            // 진공 → 통과
            if (belowDef.BehaviorType == ElementBehaviorType.Vacuum)
                return LandingResult.PassThrough;

            // 기체 → 통과
            if (belowDef.BehaviorType == ElementBehaviorType.Gas)
                return LandingResult.PassThrough;

            // ── 고체 만남 ──
            if (belowDef.IsSolid)
            {
                if (belowCell.ElementId == entity.ElementId &&
                    belowCell.Mass < belowDef.MaxMass)
                    return LandingResult.Merge;

                return LandingResult.LandAbove;
            }

            // ── 액체 만남 ──
            if (belowDef.BehaviorType == ElementBehaviorType.Liquid)
            {
                if (entityIsSolid)
                {
                    // FallingSolid → 액체를 통과 (바닥까지 가라앉음)
                    return LandingResult.PassThrough;
                }

                // Liquid → Liquid
                if (belowCell.ElementId == entity.ElementId)
                {
                    if (belowCell.Mass < belowDef.MaxMass)
                        return LandingResult.Merge;

                    return LandingResult.LandAbove;
                }

                // 다른 액체 → 위에 착지
                return LandingResult.LandAbove;
            }

            return LandingResult.LandAbove;
        }

        // ================================================================
        //  착지 실행
        // ================================================================

        /// <summary>
        /// 지정 위치에 착지한다.
        /// FallingSolid: 착지점에 액체/기체가 있으면 밀어내고 배치.
        /// Liquid: 빈 셀을 찾아 배치.
        /// </summary>
        private bool LandAtPosition(ref FallingEntity entity, int x, int y, bool isSolid)
        {
            if (!_grid.InBounds(x, y))
            {
                entity.IsActive = false;
                return true;
            }

            if (isSolid)
                return LandSolid(ref entity, x, y);
            else
                return LandLiquid(ref entity, x, y);
        }

        /// <summary>
        /// FallingSolid 착지: 착지점에 뭐가 있든 밀어내고 배치.
        /// 모래가 물속 바닥에 도달하면 물을 밀어내고 그 자리에 안착한다.
        /// </summary>
        private bool LandSolid(ref FallingEntity entity, int x, int y)
        {
            int index = _grid.ToIndex(x, y);
            SimCell existing = _grid.GetCellByIndex(index);

            // 빈 셀 → 바로 배치
            if (existing.ElementId == BuiltInElementIds.Vacuum)
            {
                PlaceEntity(ref entity, x, y);
                return true;
            }

            // 같은 원소 + 여유 → Merge
            if (existing.ElementId == entity.ElementId)
            {
                ref readonly ElementRuntimeDefinition def = ref _registry.Get(existing.ElementId);
                if (existing.Mass < def.MaxMass)
                    return MergeInto(ref entity, x, y, true);
            }

            // 그 외 (액체, 기체 등) → 밀어내고 배치
            // DisplacementResolver로 기존 원소를 인접 셀로 이동
            if (DisplacementResolver.TryDisplace(_grid, _registry, index))
            {
                PlaceEntity(ref entity, x, y);
                return true;
            }

            // 밀어내기 실패 → 위로 올라가며 빈 셀 찾기 (폴백)
            for (int tryY = y + 1; tryY < _grid.Height; tryY++)
            {
                SimCell cell = _grid.GetCell(x, tryY);
                if (cell.ElementId == BuiltInElementIds.Vacuum)
                {
                    PlaceEntity(ref entity, x, tryY);
                    return true;
                }

                ref readonly ElementRuntimeDefinition cellDef = ref _registry.Get(cell.ElementId);
                if (!cellDef.IsSolid)
                {
                    int idx = _grid.ToIndex(x, tryY);
                    if (DisplacementResolver.TryDisplace(_grid, _registry, idx))
                    {
                        PlaceEntity(ref entity, x, tryY);
                        return true;
                    }
                }
            }

            // 완전 실패
            entity.IsActive = false;
            UnityEngine.Debug.LogWarning(
                $"[FallingEntity] Solid failed to land at ({x},{y}). Mass lost!");
            return true;
        }

        /// <summary>
        /// Liquid 착지: 빈 셀을 찾거나 기체를 밀어내고 배치.
        /// </summary>
        private bool LandLiquid(ref FallingEntity entity, int x, int y)
        {
            for (int tryY = y; tryY < _grid.Height; tryY++)
            {
                if (!_grid.InBounds(x, tryY))
                    break;

                SimCell existing = _grid.GetCell(x, tryY);

                // 빈 셀 → 배치
                if (existing.ElementId == BuiltInElementIds.Vacuum)
                {
                    PlaceEntity(ref entity, x, tryY);
                    return true;
                }

                // 같은 원소 + 여유 → Merge
                if (existing.ElementId == entity.ElementId)
                {
                    ref readonly ElementRuntimeDefinition def = ref _registry.Get(existing.ElementId);
                    if (existing.Mass < def.MaxMass)
                        return MergeInto(ref entity, x, tryY, false);
                }

                // 기체 → 밀어내고 배치
                ref readonly ElementRuntimeDefinition existingDef = ref _registry.Get(existing.ElementId);
                if (existingDef.BehaviorType == ElementBehaviorType.Gas)
                {
                    int idx = _grid.ToIndex(x, tryY);
                    if (DisplacementResolver.TryDisplace(_grid, _registry, idx))
                    {
                        PlaceEntity(ref entity, x, tryY);
                        return true;
                    }
                }
            }

            // 폴백: 강제 배치
            if (_grid.InBounds(x, y))
            {
                PlaceEntity(ref entity, x, y);
                return true;
            }

            entity.IsActive = false;
            return true;
        }

        // ================================================================
        //  배치/합류
        // ================================================================

        private void PlaceEntity(ref FallingEntity entity, int x, int y)
        {
            ref SimCell cell = ref _grid.GetCellRef(x, y);
            cell = new SimCell(
                elementId: entity.ElementId,
                mass: entity.Mass,
                temperature: entity.Temperature,
                flags: SimCellFlags.None);

            entity.IsActive = false;
        }

        private bool MergeInto(ref FallingEntity entity, int x, int y, bool isSolid)
        {
            ref SimCell target = ref _grid.GetCellRef(x, y);
            ref readonly ElementRuntimeDefinition targetDef =
                ref _registry.Get(target.ElementId);

            int capacity = targetDef.MaxMass - target.Mass;
            int transfer = Math.Min(entity.Mass, capacity);

            if (transfer > 0)
            {
                long totalThermal = (long)target.Temperature * target.Mass
                    + (long)entity.Temperature * transfer;
                int totalMass = target.Mass + transfer;

                target.Mass = totalMass;
                target.Temperature = totalMass > 0
                    ? (short)(totalThermal / totalMass)
                    : (short)0;
            }

            entity.Mass -= transfer;

            if (entity.Mass <= 0)
            {
                entity.IsActive = false;
                return true;
            }

            // 넘치는 분량 → 위에 배치
            return LandAtPosition(ref entity, x, y + 1, isSolid);
        }
    }
}
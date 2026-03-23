using System;
using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 투사체 낙하 엔티티 관리자.
    ///
    /// 중력 가속도: 매 틱 velocity += gravity.
    ///   초기 velocity = 0 → 첫 틱 이동거리 0.5셀 → 점점 빨라짐.
    ///   maxVelocity로 상한 제한 (터미널 벨로시티).
    ///
    /// 착지 규칙:
    ///   FallingSolid: 진공/기체/액체 통과. 고체 위 착지. 액체 밀어내기.
    ///   Liquid: 진공/기체 통과. 고체/액체 표면 위 착지.
    /// </summary>
    public sealed class FallingEntityManager
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly List<FallingEntity> _entities = new(64);

        /// <summary>중력 가속도 (셀/틱²). 매 틱 velocity에 더해진다.</summary>
        private const float GRAVITY = 0.5f;

        /// <summary>최대 낙하 속도 (셀/틱).</summary>
        private const float MAX_VELOCITY = 6f;

        public IReadOnlyList<FallingEntity> ActiveEntities => _entities;
        public int Count => _entities.Count;

        public FallingEntityManager(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Spawn(byte elementId, int mass, float temperature,
            int cellX, int cellY, int fallSpeed)
        {
            // fallSpeed 파라미터는 하위호환용, 실제론 가속도 사용
            if (mass <= 0)
                return;

            _entities.Add(new FallingEntity(
                elementId, mass, temperature,
                cellX, cellY));
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
            // 이전 Y 저장 (렌더링 보간용)
            entity.PreviousY = entity.CurrentY;

            // 중력 가속: velocity 증가
            entity.Velocity += GRAVITY;
            if (entity.Velocity > MAX_VELOCITY)
                entity.Velocity = MAX_VELOCITY;

            int x = entity.CellX;
            int startY = (int)entity.CurrentY;

            // 이번 틱 이동 거리 (실수 → 정수 셀 수)
            float moveDistance = entity.Velocity;
            int cellsToMove = Math.Max(1, (int)moveDistance);

            int targetY = startY - cellsToMove;
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
                        entity.CurrentY = y + 1;
                        return LandAtPosition(ref entity, x, y + 1, isSolid);

                    case LandingResult.Merge:
                        entity.CurrentY = y;
                        return MergeInto(ref entity, x, y, isSolid);

                    case LandingResult.PassThrough:
                        continue;
                }
            }

            // 착지점 없음 → 계속 낙하
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
            if (belowDef.BehaviorType == ElementBehaviorType.Vacuum)
                return LandingResult.PassThrough;

            if (belowDef.BehaviorType == ElementBehaviorType.Gas)
                return LandingResult.PassThrough;

            if (belowDef.IsSolid)
            {
                if (belowCell.ElementId == entity.ElementId &&
                    belowCell.Mass < belowDef.MaxMass)
                    return LandingResult.Merge;

                return LandingResult.LandAbove;
            }

            if (belowDef.BehaviorType == ElementBehaviorType.Liquid)
            {
                if (entityIsSolid)
                    return LandingResult.PassThrough;

                if (belowCell.ElementId == entity.ElementId)
                {
                    if (belowCell.Mass < belowDef.MaxMass)
                        return LandingResult.Merge;

                    return LandingResult.LandAbove;
                }

                return LandingResult.LandAbove;
            }

            return LandingResult.LandAbove;
        }

        // ================================================================
        //  착지 실행
        // ================================================================

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

        private bool LandSolid(ref FallingEntity entity, int x, int y)
        {
            int index = _grid.ToIndex(x, y);
            SimCell existing = _grid.GetCellByIndex(index);

            if (existing.ElementId == BuiltInElementIds.Vacuum)
            {
                PlaceEntity(ref entity, x, y);
                return true;
            }

            if (existing.ElementId == entity.ElementId)
            {
                ref readonly ElementRuntimeDefinition def = ref _registry.Get(existing.ElementId);
                if (existing.Mass < def.MaxMass)
                    return MergeInto(ref entity, x, y, true);
            }

            if (DisplacementResolver.TryDisplace(_grid, _registry, index))
            {
                PlaceEntity(ref entity, x, y);
                return true;
            }

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

            entity.IsActive = false;
            UnityEngine.Debug.LogWarning(
                $"[FallingEntity] Solid failed to land at ({x},{y}). Mass lost!");
            return true;
        }

        private bool LandLiquid(ref FallingEntity entity, int x, int y)
        {
            for (int tryY = y; tryY < _grid.Height; tryY++)
            {
                if (!_grid.InBounds(x, tryY))
                    break;

                SimCell existing = _grid.GetCell(x, tryY);

                if (existing.ElementId == BuiltInElementIds.Vacuum)
                {
                    PlaceEntity(ref entity, x, tryY);
                    return true;
                }

                if (existing.ElementId == entity.ElementId)
                {
                    ref readonly ElementRuntimeDefinition def = ref _registry.Get(existing.ElementId);
                    if (existing.Mass < def.MaxMass)
                        return MergeInto(ref entity, x, tryY, false);
                }

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
                float totalThermal = target.Temperature * target.Mass
                    + entity.Temperature * transfer;
                int totalMass = target.Mass + transfer;

                target.Mass = totalMass;
                target.Temperature = totalMass > 0
                    ? totalThermal / totalMass
                    : 0f;
            }

            entity.Mass -= transfer;

            if (entity.Mass <= 0)
            {
                entity.IsActive = false;
                return true;
            }

            return LandAtPosition(ref entity, x, y + 1, isSolid);
        }
    }
}
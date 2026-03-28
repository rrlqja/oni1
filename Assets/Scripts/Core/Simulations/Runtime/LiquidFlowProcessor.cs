using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 2: 액체 좌우 확산 처리기.
    ///
    /// 수직 낙하는 Phase 1(GravityProcessor)이 담당.
    /// 이 프로세서는 좌우 방향만 처리한다.
    ///
    /// 확산 판정:
    ///   좌우: 진공/동종 → FlowBatch 대상
    ///         기체(탈출 가능) → 기체 탈출 ForceMergeMass + FlowBatch
    ///         기체(탈출 불가, grounded) → 직접 Swap (폴백)
    ///         고체/다른액체 → 차단
    ///
    /// 방향 분리:
    ///   Phase 1 = 수직, Phase 2 = 좌우 → MarkActed 불필요, 2칸 낙하 원천 차단.
    ///   물이 Phase 1에서 1칸 낙하한 뒤, 같은 틱에 Phase 2에서 즉시 좌우 확산.
    ///
    /// [개선 2] Over-MaxMass 균등화: 소스 질량이 타겟보다 많으면
    /// MaxMass를 초과하더라도 차이만큼 확산을 허용한다.
    /// </summary>
    public sealed class LiquidFlowProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly SimulationSettings _settings;

        public LiquidFlowProcessor(WorldGrid grid, ElementRegistry registry, SimulationSettings settings)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// 액체 좌우 확산 커맨드를 수집한다.
        /// </summary>
        public void BuildFlowBatches(
            int currentTick,
            bool leftToRight,
            List<FlowBatchCommand> flowOutput,
            List<SimulationCommand> displaceOutput,
            bool[] swapTargetUsed)
        {
            if (flowOutput == null)
                throw new ArgumentNullException(nameof(flowOutput));
            if (displaceOutput == null)
                throw new ArgumentNullException(nameof(displaceOutput));
            if (swapTargetUsed == null)
                throw new ArgumentNullException(nameof(swapTargetUsed));

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int sourceIndex = _grid.ToIndex(x, y);

                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);
                    ref readonly ElementRuntimeDefinition sourceDef =
                        ref _registry.Get(sourceCell.ElementId);

                    if (sourceDef.BehaviorType != ElementBehaviorType.Liquid)
                        continue;
                    if (sourceCell.Mass <= 0)
                        continue;

                    // MinSpreadMass 이하면 좌우 확산 안 함
                    if (sourceCell.Mass <= sourceDef.MinSpreadMass)
                        continue;

                    TryBuildLateralBatch(
                        x, y, sourceIndex,
                        in sourceCell, in sourceDef,
                        currentTick, leftToRight,
                        flowOutput, displaceOutput, swapTargetUsed);
                }
            }
        }

        private void TryBuildLateralBatch(
            int x, int y, int sourceIndex,
            in SimCell sourceCell, in ElementRuntimeDefinition sourceDef,
            int currentTick, bool leftToRight,
            List<FlowBatchCommand> flowOutput,
            List<SimulationCommand> displaceOutput,
            bool[] swapTargetUsed)
        {
            // 아래로 더 내려갈 수 있으면 좌우 확산하지 않음 (낙하 우선)
            if (!IsGrounded(x, y, in sourceDef))
                return;

            var buffer = new TransferPlanBuffer();
            int currentRemainingMass = sourceCell.Mass;
            int directSwapGasIndex = -1;

            int desiredPerSide = currentRemainingMass / sourceDef.Viscosity;
            desiredPerSide = Math.Min(desiredPerSide, _settings.MaxLateralTransfer);

            if (desiredPerSide <= 0)
                return;

            int firstSideX = leftToRight ? x - 1 : x + 1;
            int secondSideX = leftToRight ? x + 1 : x - 1;

            TryPlanLateral(
                firstSideX, y,
                in sourceCell, in sourceDef,
                ref currentRemainingMass, desiredPerSide,
                ref buffer, swapTargetUsed, displaceOutput,
                ref directSwapGasIndex);

            TryPlanLateral(
                secondSideX, y,
                in sourceCell, in sourceDef,
                ref currentRemainingMass, desiredPerSide,
                ref buffer, swapTargetUsed, displaceOutput,
                ref directSwapGasIndex);

            // FlowBatch 결과 출력
            if (buffer.Count > 0)
            {
                EmitBatch(sourceIndex, in sourceCell, currentTick, buffer, flowOutput);
                return;
            }

            // 폴백: FlowBatch 대상 없음 + 기체 이웃 있음 → 직접 Swap
            //   산소로 가득 찬 공간에서 기체가 탈출 불가일 때,
            //   grounded 액체가 기체와 직접 위치 교환한다.
            if (directSwapGasIndex >= 0 && IsGrounded(x, y, in sourceDef))
            {
                if (!swapTargetUsed[directSwapGasIndex])
                {
                    swapTargetUsed[directSwapGasIndex] = true;
                    displaceOutput.Add(SimulationCommand.CreateSwap(sourceIndex, directSwapGasIndex));
                }
            }
        }

        // ================================================================
        //  좌우 확산 시도
        // ================================================================

        private void TryPlanLateral(
            int targetX, int targetY,
            in SimCell sourceCell, in ElementRuntimeDefinition sourceDef,
            ref int currentRemainingMass, int desiredPerSide,
            ref TransferPlanBuffer buffer,
            bool[] swapTargetUsed,
            List<SimulationCommand> displaceOutput,
            ref int directSwapGasIndex)
        {
            if (!_grid.InBounds(targetX, targetY))
                return;

            int targetIndex = _grid.ToIndex(targetX, targetY);
            SimCell targetCell = _grid.GetCellByIndex(targetIndex);

            LateralResult result = ClassifyTarget(
                sourceCell.ElementId, in sourceDef, in targetCell,
                targetIndex, swapTargetUsed, displaceOutput,
                ref directSwapGasIndex);

            if (result == LateralResult.Flow || result == LateralResult.Displaced)
            {
                // [개선 2] sourceMass를 전달하여 over-MaxMass 균등화 허용
                int capacity = (result == LateralResult.Displaced)
                    ? sourceDef.MaxMass
                    : GetFlowCapacity(sourceCell.ElementId, sourceCell.Mass, sourceDef.MaxMass, in targetCell);
                if (capacity <= 0)
                    return;

                int allowedByMinSpread = currentRemainingMass - sourceDef.MinSpreadMass;
                if (allowedByMinSpread <= 0)
                    return;

                int planned = Math.Min(desiredPerSide, capacity);
                planned = Math.Min(planned, allowedByMinSpread);

                if (planned > 0)
                {
                    buffer.Add(targetIndex, planned);
                    currentRemainingMass -= planned;
                }
            }
        }

        // ================================================================
        //  타겟 분류
        // ================================================================

        private enum LateralResult : byte
        {
            Blocked,
            Flow,
            Displaced,
            DirectSwapCandidate,
        }

        private LateralResult ClassifyTarget(
            byte sourceElementId,
            in ElementRuntimeDefinition sourceDef,
            in SimCell targetCell,
            int targetIndex,
            bool[] swapTargetUsed,
            List<SimulationCommand> displaceOutput,
            ref int directSwapGasIndex)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return LateralResult.Flow;

            if (targetCell.ElementId == sourceElementId)
                return LateralResult.Flow;

            ref readonly ElementRuntimeDefinition targetDef =
                ref _registry.Get(targetCell.ElementId);

            if (targetDef.BehaviorType == ElementBehaviorType.Gas &&
                targetDef.DisplacementPriority < sourceDef.DisplacementPriority)
            {
                if (DisplaceChecker.CanDisplace(
                    _grid, _registry, targetIndex, swapTargetUsed,
                    out SimulationCommand escapeCmd))
                {
                    displaceOutput.Add(escapeCmd);
                    return LateralResult.Displaced;
                }

                if (directSwapGasIndex < 0 && !swapTargetUsed[targetIndex])
                {
                    directSwapGasIndex = targetIndex;
                }
                return LateralResult.DirectSwapCandidate;
            }

            return LateralResult.Blocked;
        }

        // ================================================================
        //  Grounded 판정
        // ================================================================

        private bool IsGrounded(int x, int y, in ElementRuntimeDefinition actorDef)
        {
            int belowY = y - 1;
            if (belowY < 0)
                return true;

            SimCell below = _grid.GetCell(x, belowY);

            if (below.ElementId == BuiltInElementIds.Vacuum)
                return false;

            ref readonly ElementRuntimeDefinition belowDef =
                ref _registry.Get(below.ElementId);

            if (belowDef.BehaviorType == ElementBehaviorType.Gas)
                return false;

            if (below.ElementId == actorDef.Id && below.Mass < actorDef.MaxMass)
                return false;

            // 아래가 더 가벼운 액체 → 밀도 교환(Phase 3)으로 가라앉을 수 있음
            if (belowDef.BehaviorType == ElementBehaviorType.Liquid
                && belowDef.Id != actorDef.Id
                && actorDef.Density > belowDef.Density)
                return false;

            return true;
        }

        // ================================================================
        //  유틸리티
        // ================================================================

        /// <summary>
        /// 타겟 셀에 보낼 수 있는 최대 질량을 계산한다.
        ///
        /// [개선 2] Over-MaxMass 균등화 허용:
        /// 일반 케이스(타겟 MaxMass 미만) → 기존대로 headroom 반환.
        /// Over-MaxMass 케이스(타겟이 MaxMass 이상) → 소스가 타겟보다 많으면
        /// 차이만큼 허용하여 자연스러운 균등화를 가능하게 한다.
        /// </summary>
        private static int GetFlowCapacity(
            byte sourceElementId, int sourceMass, int maxMass, in SimCell targetCell)
        {
            if (targetCell.ElementId == BuiltInElementIds.Vacuum)
                return maxMass;

            if (targetCell.ElementId == sourceElementId)
            {
                // 일반 케이스: 타겟에 MaxMass 미만 여유 있음
                int headroom = maxMass - targetCell.Mass;
                if (headroom > 0)
                    return headroom;

                // Over-MaxMass 균등화: 소스가 타겟보다 많으면 차이만큼 허용
                int excess = sourceMass - targetCell.Mass;
                return Math.Max(0, excess);
            }

            return 0;
        }

        private void EmitBatch(
            int sourceIndex,
            in SimCell sourceCell,
            int currentTick,
            in TransferPlanBuffer buffer,
            List<FlowBatchCommand> output)
        {
            FlowBatchCommand batch = buffer.ToBatch(
                sourceIndex,
                sourceCell.ElementId,
                sourceCell.Temperature,
                FlowBatchMode.Normal);

            output.Add(batch);
        }
    }
}
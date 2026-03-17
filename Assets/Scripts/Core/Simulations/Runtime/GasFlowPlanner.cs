using System;
using System.Collections.Generic;
using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// ONI 스타일 기체 시뮬레이션 — 2-Phase 모델
    ///
    /// Phase A (균등화):
    ///   같은 가스끼리 질량 평균을 맞추며 확산한다.
    ///   FlowBatchCommand로 처리.
    ///
    /// Phase B (밀도 인지 이동):
    ///   모든 가스 셀이 밀도 기반 방향 가중치에 따라 이동을 시도한다.
    ///   - 진공 인접 → 통째 이동 (drift)
    ///   - 다른 가스 인접 + 밀도 역전 → 스왑
    ///   방향별 확률: 무거운 가스는 아래로, 가벼운 가스는 위로 편향.
    ///   좌우는 중립. 결과적으로 시간이 지나면 밀도순 층 분리.
    /// </summary>
    public sealed class GasFlowPlanner
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;

        /// <summary>
        /// 밀도 이동 주기: N틱마다 한번 이동을 시도한다.
        /// </summary>
        private const int MOVEMENT_INTERVAL = 2;

        /// <summary>
        /// 방향 가중치 계산에 사용하는 최대 밀도 기준값.
        /// Phase B 밀도 인지 이동에서 사용.
        /// </summary>
        private const float MAX_GAS_DENSITY = 2000f;

        /// <summary>
        /// 균등화 방향 차단 기준 밀도.
        /// 이 값 이상 → "무거운 가스" → 아래 + 좌우로만 균등화.
        /// 이 값 미만 → "가벼운 가스" → 위 + 좌우로만 균등화.
        /// Hydrogen=90, Oxygen=500 기준으로 300이면 H₂는 위로, O₂는 아래로.
        /// </summary>
        private const float DENSITY_THRESHOLD = 300f;

        public GasFlowPlanner(WorldGrid grid, ElementRegistry registry)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // ================================================================
        //  Phase A: 균등화 — 같은 가스끼리 질량 분배
        // ================================================================

        public void BuildNormalFlowBatches(
            int currentTick,
            bool leftToRight,
            List<FlowBatchCommand> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int sourceIndex = _grid.ToIndex(x, y);

                    ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
                    if (sourceMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);
                    ref readonly ElementRuntimeDefinition sourceElement =
                        ref _registry.Get(sourceCell.ElementId);

                    if (sourceElement.BehaviorType != ElementBehaviorType.Gas)
                        continue;

                    if (sourceCell.Mass <= 0)
                        continue;

                    if (TryBuildEqualizationBatch(
                            x, y, sourceIndex,
                            in sourceCell, in sourceElement,
                            leftToRight,
                            out FlowBatchCommand batch))
                    {
                        sourceMeta.MarkActed(currentTick);
                        output.Add(batch);
                    }
                }
            }
        }

        // ================================================================
        //  Phase B: 밀도 인지 이동
        //
        //  드리프트(진공 이동)와 부력 스왑(이종 가스 교환)을 통합.
        //  4방향 모두 고려하며, 밀도에 따라 방향 확률이 달라진다.
        //
        //  출력:
        //    - 진공 이동 → FlowBatchCommand (flowOutput)
        //    - 이종 스왑 → SimulationCommand (swapOutput)
        // ================================================================

        public void BuildDensityAwareMovement(
            int currentTick,
            bool leftToRight,
            List<FlowBatchCommand> flowOutput,
            List<SimulationCommand> swapOutput)
        {
            if (flowOutput == null)
                throw new ArgumentNullException(nameof(flowOutput));
            if (swapOutput == null)
                throw new ArgumentNullException(nameof(swapOutput));

            int startX = leftToRight ? 0 : _grid.Width - 1;
            int endX = leftToRight ? _grid.Width : -1;
            int stepX = leftToRight ? 1 : -1;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int sourceIndex = _grid.ToIndex(x, y);

                    ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
                    if (sourceMeta.HasActedThisTick(currentTick))
                        continue;

                    SimCell sourceCell = _grid.GetCellByIndex(sourceIndex);
                    ref readonly ElementRuntimeDefinition sourceElement =
                        ref _registry.Get(sourceCell.ElementId);

                    if (sourceElement.BehaviorType != ElementBehaviorType.Gas)
                        continue;

                    if (sourceCell.Mass <= 0)
                        continue;

                    // 이동 주기 제어 (셀마다 엇갈린 타이밍)
                    uint timingHash = MixHash(currentTick, x, y);
                    if (timingHash % MOVEMENT_INTERVAL != 0)
                        continue;

                    TryDensityMove(
                        x, y, sourceIndex,
                        in sourceCell, in sourceElement,
                        currentTick, leftToRight,
                        flowOutput, swapOutput);
                }
            }
        }

        // ────────────────────────────────────────────────────
        //  밀도 인지 이동 — 핵심 로직
        // ────────────────────────────────────────────────────

        private void TryDensityMove(
            int x, int y,
            int sourceIndex,
            in SimCell sourceCell,
            in ElementRuntimeDefinition sourceElement,
            int currentTick,
            bool leftToRight,
            List<FlowBatchCommand> flowOutput,
            List<SimulationCommand> swapOutput)
        {
            float density = sourceElement.Density;

            // ── 1) 4방향 가중치 계산 ──
            // 상하: 이진 차단 — 무거우면 아래만, 가벼우면 위만
            // 좌우: 약하게 허용 — 자연스러운 옆 퍼짐
            bool isHeavy = density >= DENSITY_THRESHOLD;

            float wUp = isHeavy ? 0f : 10f;
            float wDown = isHeavy ? 10f : 0f;
            float wLeft = 1f;
            float wRight = 1f;

            // ── 2) 4방향 후보 수집 ──
            // 각 방향: (neighborIndex, actionType, directionWeight)
            //   actionType: 0=불가, 1=drift(진공이동), 2=swap(이종교환)

            int fhx = leftToRight ? x - 1 : x + 1;
            int shx = leftToRight ? x + 1 : x - 1;

            // 방향 순서: left, right, up, down
            // leftToRight 교대로 좌우 우선순위를 바꿔 편향 제거
            int dir0X = fhx, dir0Y = y; float dir0W = (fhx < x) ? wLeft : wRight;
            int dir1X = shx, dir1Y = y; float dir1W = (shx < x) ? wLeft : wRight;
            int dir2X = x, dir2Y = y + 1; float dir2W = wUp;
            int dir3X = x, dir3Y = y - 1; float dir3W = wDown;

            // 후보 배열 (인라인, 힙 할당 없음)
            int c0Idx = -1, c1Idx = -1, c2Idx = -1, c3Idx = -1;
            byte c0Act = 0, c1Act = 0, c2Act = 0, c3Act = 0;
            float c0Wt = 0f, c1Wt = 0f, c2Wt = 0f, c3Wt = 0f;
            int candidateCount = 0;

            EvaluateDirection(dir0X, dir0Y, dir0W, sourceCell.ElementId, density, currentTick,
                ref candidateCount, ref c0Idx, ref c0Act, ref c0Wt,
                ref c1Idx, ref c1Act, ref c1Wt,
                ref c2Idx, ref c2Act, ref c2Wt,
                ref c3Idx, ref c3Act, ref c3Wt);

            EvaluateDirection(dir1X, dir1Y, dir1W, sourceCell.ElementId, density, currentTick,
                ref candidateCount, ref c0Idx, ref c0Act, ref c0Wt,
                ref c1Idx, ref c1Act, ref c1Wt,
                ref c2Idx, ref c2Act, ref c2Wt,
                ref c3Idx, ref c3Act, ref c3Wt);

            EvaluateDirection(dir2X, dir2Y, dir2W, sourceCell.ElementId, density, currentTick,
                ref candidateCount, ref c0Idx, ref c0Act, ref c0Wt,
                ref c1Idx, ref c1Act, ref c1Wt,
                ref c2Idx, ref c2Act, ref c2Wt,
                ref c3Idx, ref c3Act, ref c3Wt);

            EvaluateDirection(dir3X, dir3Y, dir3W, sourceCell.ElementId, density, currentTick,
                ref candidateCount, ref c0Idx, ref c0Act, ref c0Wt,
                ref c1Idx, ref c1Act, ref c1Wt,
                ref c2Idx, ref c2Act, ref c2Wt,
                ref c3Idx, ref c3Act, ref c3Wt);

            if (candidateCount == 0)
                return;

            // ── 3) 가중치 기반 랜덤 선택 ──
            float totalWeight = c0Wt + c1Wt + c2Wt + c3Wt;
            if (totalWeight <= 0f)
                return;

            uint dirHash = MixHash(currentTick, x * 7, y * 13);
            float roll = (dirHash % 10000) / 10000f * totalWeight;

            int chosenIdx;
            byte chosenAct;

            float cumulative = c0Wt;
            if (roll < cumulative && c0Act > 0)
            {
                chosenIdx = c0Idx;
                chosenAct = c0Act;
            }
            else
            {
                cumulative += c1Wt;
                if (roll < cumulative && c1Act > 0)
                {
                    chosenIdx = c1Idx;
                    chosenAct = c1Act;
                }
                else
                {
                    cumulative += c2Wt;
                    if (roll < cumulative && c2Act > 0)
                    {
                        chosenIdx = c2Idx;
                        chosenAct = c2Act;
                    }
                    else if (c3Act > 0)
                    {
                        chosenIdx = c3Idx;
                        chosenAct = c3Act;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            if (chosenIdx < 0)
                return;

            // ── 4) 실행 ──
            ref TickMeta sourceMeta = ref _grid.GetTickMetaRef(sourceIndex);
            ref TickMeta targetMeta = ref _grid.GetTickMetaRef(chosenIdx);

            sourceMeta.MarkActed(currentTick);
            targetMeta.MarkActed(currentTick);

            if (chosenAct == 1)
            {
                // Drift: 전체 질량을 진공으로 이동
                sourceMeta.AddReservation(TickReservationMask.SourceReserved);
                targetMeta.AddReservation(TickReservationMask.TargetReserved);

                FlowTransferPlan transfer = new FlowTransferPlan(chosenIdx, sourceCell.Mass);
                flowOutput.Add(new FlowBatchCommand(
                    sourceIndex,
                    sourceCell.ElementId,
                    sourceCell.Temperature,
                    FlowBatchMode.Normal,
                    1, transfer, default, default, default));
            }
            else if (chosenAct == 2)
            {
                // Swap: 이종 가스와 셀 교환
                sourceMeta.AddReservation(TickReservationMask.SourceReserved);
                targetMeta.AddReservation(TickReservationMask.TargetReserved);

                swapOutput.Add(SimulationCommand.CreateSwap(sourceIndex, chosenIdx));
            }
        }

        // ────────────────────────────────────────────────────
        //  방향 평가: 해당 방향이 이동 가능한지, 어떤 종류인지
        // ────────────────────────────────────────────────────

        private void EvaluateDirection(
            int nx, int ny,
            float directionWeight,
            byte sourceElementId,
            float sourceDensity,
            int currentTick,
            ref int count,
            ref int c0Idx, ref byte c0Act, ref float c0Wt,
            ref int c1Idx, ref byte c1Act, ref float c1Wt,
            ref int c2Idx, ref byte c2Act, ref float c2Wt,
            ref int c3Idx, ref byte c3Act, ref float c3Wt)
        {
            if (nx < 0 || nx >= _grid.Width || ny < 0 || ny >= _grid.Height)
                return;

            int idx = _grid.ToIndex(nx, ny);

            ref TickMeta meta = ref _grid.GetTickMetaRef(idx);
            if (meta.HasActedThisTick(currentTick))
                return;
            if (meta.ReservationMask != 0)
                return;

            SimCell cell = _grid.GetCellByIndex(idx);
            ref readonly ElementRuntimeDefinition element = ref _registry.Get(cell.ElementId);

            byte actionType = 0; // 0=불가

            if (cell.ElementId == BuiltInElementIds.Vacuum)
            {
                // 진공 → drift 가능
                actionType = 1;
            }
            else if (element.BehaviorType == ElementBehaviorType.Gas &&
                     cell.ElementId != sourceElementId &&
                     cell.Mass > 0)
            {
                // 다른 가스 → swap 가능 (항상. 방향 가중치가 알아서 처리)
                actionType = 2;
            }

            if (actionType == 0)
                return;

            // 후보 등록
            switch (count)
            {
                case 0: c0Idx = idx; c0Act = actionType; c0Wt = directionWeight; break;
                case 1: c1Idx = idx; c1Act = actionType; c1Wt = directionWeight; break;
                case 2: c2Idx = idx; c2Act = actionType; c2Wt = directionWeight; break;
                default: c3Idx = idx; c3Act = actionType; c3Wt = directionWeight; break;
            }
            count++;
        }

        // ================================================================
        //  균등화 배치 생성 (Phase A 내부)
        // ================================================================

        private bool TryBuildEqualizationBatch(
            int x, int y,
            int sourceIndex,
            in SimCell sourceCell,
            in ElementRuntimeDefinition sourceElement,
            bool leftToRight,
            out FlowBatchCommand batch)
        {
            batch = default;

            int sourceMass = sourceCell.Mass;
            byte sourceElId = sourceCell.ElementId;
            int maxMass = sourceElement.MaxMass;

            int n0Idx = -1, n1Idx = -1, n2Idx = -1, n3Idx = -1;
            int n0Mass = 0, n1Mass = 0, n2Mass = 0, n3Mass = 0;

            int participantCount = 1;
            long totalMass = sourceMass;

            int firstHX = leftToRight ? x - 1 : x + 1;
            int secondHX = leftToRight ? x + 1 : x - 1;

            // n0, n1 = 좌우 (수평)
            // n2 = 위 (y+1)
            // n3 = 아래 (y-1)
            GatherNeighbor(firstHX, y, sourceElId, maxMass,
                ref participantCount, ref totalMass, ref n0Idx, ref n0Mass);
            GatherNeighbor(secondHX, y, sourceElId, maxMass,
                ref participantCount, ref totalMass, ref n1Idx, ref n1Mass);
            GatherNeighbor(x, y + 1, sourceElId, maxMass,
                ref participantCount, ref totalMass, ref n2Idx, ref n2Mass);
            GatherNeighbor(x, y - 1, sourceElId, maxMass,
                ref participantCount, ref totalMass, ref n3Idx, ref n3Mass);

            if (participantCount <= 1)
                return false;

            int average = (int)(totalMass / participantCount);
            int sourceExcess = sourceMass - average;
            if (sourceExcess <= 0)
                return false;

            // ── 균등화는 방향 중립 ──
            // 같은 가스 또는 진공만 대상이므로 밀도 분리에 영향 없음.
            // 모든 방향으로 균등하게 확산해야 자연스러운 퍼짐이 가능.
            // (밀도 방향 제어는 Phase B에서만 처리)
            float w = 1f;

            FlowTransferPlan t0 = default, t1 = default, t2 = default, t3 = default;
            byte transferCount = 0;
            int totalPlanned = 0;

            PlanWeightedTransfer(n0Idx, n0Mass, average, maxMass,
                sourceExcess, w, ref totalPlanned, ref transferCount,
                ref t0, ref t1, ref t2, ref t3);
            PlanWeightedTransfer(n1Idx, n1Mass, average, maxMass,
                sourceExcess, w, ref totalPlanned, ref transferCount,
                ref t0, ref t1, ref t2, ref t3);
            PlanWeightedTransfer(n2Idx, n2Mass, average, maxMass,
                sourceExcess, w, ref totalPlanned, ref transferCount,
                ref t0, ref t1, ref t2, ref t3);
            PlanWeightedTransfer(n3Idx, n3Mass, average, maxMass,
                sourceExcess, w, ref totalPlanned, ref transferCount,
                ref t0, ref t1, ref t2, ref t3);

            if (transferCount == 0)
                return false;

            batch = new FlowBatchCommand(
                sourceIndex, sourceCell.ElementId, sourceCell.Temperature,
                FlowBatchMode.Normal, transferCount,
                t0, t1, t2, t3);
            return true;
        }

        // ================================================================
        //  유틸리티 메서드
        // ================================================================

        private void GatherNeighbor(
            int nx, int ny,
            byte sourceElementId, int maxMass,
            ref int participantCount, ref long totalMass,
            ref int outIndex, ref int outMass)
        {
            if (nx < 0 || nx >= _grid.Width || ny < 0 || ny >= _grid.Height)
                return;

            int idx = _grid.ToIndex(nx, ny);
            SimCell cell = _grid.GetCellByIndex(idx);

            if (cell.ElementId == BuiltInElementIds.Vacuum)
            {
                outIndex = idx;
                outMass = 0;
                participantCount++;
                return;
            }

            if (cell.ElementId != sourceElementId)
                return;

            outIndex = idx;
            outMass = cell.Mass;
            participantCount++;
            totalMass += cell.Mass;
        }

        private static void PlanWeightedTransfer(
            int neighborIndex, int neighborMass,
            int average, int maxMass, int sourceExcess,
            float directionWeight,
            ref int totalPlanned, ref byte transferCount,
            ref FlowTransferPlan t0, ref FlowTransferPlan t1,
            ref FlowTransferPlan t2, ref FlowTransferPlan t3)
        {
            if (neighborIndex < 0) return;

            int deficit = average - neighborMass;
            if (deficit <= 0) return;

            int targetCapacity = maxMass - neighborMass;
            if (targetCapacity <= 0) return;

            int remainingExcess = sourceExcess - totalPlanned;
            if (remainingExcess <= 0) return;

            // 방향 가중치 적용: deficit에 가중치를 곱해서
            // 선호 방향으로는 더 많이, 비선호 방향으로는 더 적게 보낸다
            int weightedDeficit = (int)(deficit * directionWeight);
            if (weightedDeficit <= 0) return;

            int planned = Math.Min(weightedDeficit, Math.Min(targetCapacity, remainingExcess));
            if (planned <= 0) return;

            totalPlanned += planned;
            FlowTransferPlan plan = new FlowTransferPlan(neighborIndex, planned);
            switch (transferCount)
            {
                case 0: t0 = plan; break;
                case 1: t1 = plan; break;
                case 2: t2 = plan; break;
                default: t3 = plan; break;
            }
            transferCount++;
        }

        /// <summary>
        /// 결정적 비트 믹싱 해시.
        /// 같은 (tick, x, y)는 항상 같은 결과 → 리플레이 재현 가능.
        /// 인접 좌표도 완전히 다른 결과 → 패턴 없는 분포.
        /// </summary>
        private static uint MixHash(int tick, int x, int y)
        {
            uint h = (uint)tick;
            h ^= (uint)x * 0x9E3779B9u;
            h ^= (uint)y * 0x517CC1B7u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
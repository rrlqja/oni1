using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 중력 판정 연산자 (순수 함수).
    ///
    /// FallingSolid + Liquid의 아래 방향 상호작용을 하나의 판정으로 통합한다.
    /// Move는 별도 커맨드가 아니라 Swap with Vacuum으로 처리된다.
    ///
    /// 판정:
    ///   아래 = 진공           → Swap
    ///   아래 = 더 가벼운 원소  → Swap (DisplacementPriority 비교)
    ///   아래 = 같은 원소+용량  → Merge
    ///   그 외                 → None
    /// </summary>
    public static class GravityOperator
    {
        public enum Result : byte
        {
            None = 0,
            Swap = 1,
            Merge = 2,
        }

        /// <summary>
        /// 위 셀(upper)이 아래 셀(lower)에 대해 중력 행동을 판정한다.
        /// upper는 FallingSolid 또는 Liquid여야 한다 (호출자가 보장).
        /// </summary>
        public static Result Evaluate(
            in SimCell upper, in ElementRuntimeDefinition upperDef,
            in SimCell lower, in ElementRuntimeDefinition lowerDef)
        {
            // 아래가 진공 → Swap (= Move)
            if (lowerDef.BehaviorType == ElementBehaviorType.Vacuum)
                return Result.Swap;

            // 아래가 더 낮은 DisplacementPriority → Swap (밀어내기)
            if (lowerDef.DisplacementPriority < upperDef.DisplacementPriority)
                return Result.Swap;

            // 아래가 같은 원소 + 용량 남음 → Merge
            if (upper.ElementId == lower.ElementId && lower.Mass < lowerDef.MaxMass)
                return Result.Merge;

            return Result.None;
        }
    }
}
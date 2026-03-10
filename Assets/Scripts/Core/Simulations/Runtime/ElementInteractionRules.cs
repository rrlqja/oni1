using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    public static class ElementInteractionRules
    {
        public static bool IsFallingSolid(in ElementRuntimeDefinition element)
        {
            return element.BehaviorType == ElementBehaviorType.FallingSolid;
        }

        public static DownInteractionResult EvaluateDownInteraction(
            in ElementRuntimeDefinition actor,
            in ElementRuntimeDefinition target)
        {
            // 완전 빈칸이면 그냥 이동
            if (target.Id == BuiltInElementIds.Vacuum)
                return DownInteractionResult.Move;

            // 더 낮은 치환 우선순위라면 아래 칸으로 진입 가능한 후보
            // 예: FallingSolid(3) -> Liquid(2), Gas(1)
            if (target.DisplacementPriority < actor.DisplacementPriority)
                return DownInteractionResult.Replace;

            return DownInteractionResult.Blocked;
        }
    }
}
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
            in SimCell actorCell,
            in ElementRuntimeDefinition actorElement,
            in SimCell targetCell,
            in ElementRuntimeDefinition targetElement)
        {
            if (targetElement.BehaviorType == ElementBehaviorType.Vacuum)
                return DownInteractionResult.Move;

            if (targetElement.BehaviorType == ElementBehaviorType.Gas ||
                targetElement.BehaviorType == ElementBehaviorType.Liquid)
            {
                return DownInteractionResult.Swap;
            }

            if (targetElement.BehaviorType == ElementBehaviorType.FallingSolid &&
                actorCell.ElementId == targetCell.ElementId &&
                targetCell.Mass < targetElement.MaxMass)
            {
                return DownInteractionResult.Merge;
            }

            return DownInteractionResult.Blocked;
        }
    }
}
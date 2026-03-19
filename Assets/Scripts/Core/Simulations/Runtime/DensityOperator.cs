using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 밀도 기반 교환 판정 연산자 (순수 함수).
    ///
    /// 같은 BehaviorType 내에서 밀도가 역전된 두 셀의 Swap 여부를 판정한다.
    /// 액체 밀도 이동(Phase 3)과 기체 밀도 이동(Phase 5) 모두에서 재사용.
    ///
    /// 판정:
    ///   위 셀과 아래 셀이 같은 requiredType이고,
    ///   서로 다른 원소이며,
    ///   위 셀의 Density가 더 크면 → Swap (무거운 것이 아래로)
    /// </summary>
    public static class DensityOperator
    {
        /// <summary>
        /// 위 셀(upper)이 아래 셀(lower)보다 무거워서 교환해야 하는지 판정한다.
        /// </summary>
        /// <param name="upperDef">위 셀의 원소 정의</param>
        /// <param name="lowerDef">아래 셀의 원소 정의</param>
        /// <param name="requiredType">비교 대상 BehaviorType (Liquid 또는 Gas)</param>
        /// <returns>교환 필요 여부</returns>
        public static bool ShouldSwap(
            in ElementRuntimeDefinition upperDef,
            in ElementRuntimeDefinition lowerDef,
            ElementBehaviorType requiredType)
        {
            if (upperDef.BehaviorType != requiredType) return false;
            if (lowerDef.BehaviorType != requiredType) return false;
            if (upperDef.Id == lowerDef.Id) return false;
            return upperDef.Density > lowerDef.Density;
        }
    }
}
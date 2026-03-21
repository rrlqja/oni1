using System.Collections.Generic;
using Core.Simulation.Runtime;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 렌더링 레이어의 공통 인터페이스.
    /// 각 레이어(고체, 액체, 기체, 배경)가 이 인터페이스를 구현한다.
    /// GridRenderManager가 이 인터페이스를 통해 모든 레이어를 조율한다.
    /// </summary>
    public interface IGridLayerRenderer
    {
        /// <summary>
        /// SimulationWorld를 받아 초기 상태를 구성한다.
        /// </summary>
        void Initialize(SimulationWorld world);

        /// <summary>
        /// 전체 렌더링을 갱신한다. (전체 순회)
        /// </summary>
        void Refresh();

        /// <summary>
        /// 변경된 셀만 부분 갱신한다.
        /// 최적화를 지원하지 않는 렌더러는 내부에서 Refresh()를 호출하면 된다.
        /// </summary>
        /// <param name="dirtyIndices">변경된 셀의 1D 인덱스 목록</param>
        /// <param name="gridWidth">인덱스 → (x, y) 변환에 필요한 그리드 너비</param>
        void RefreshDirty(IReadOnlyList<int> dirtyIndices, int gridWidth);

        /// <summary>
        /// 레이어가 사용 중인 리소스를 해제한다.
        /// </summary>
        void Cleanup();
    }
}
using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Runtime;

namespace Core.Simulation.Rendering
{
    /// <summary>
    /// 이전 프레임의 셀 스냅샷과 현재 데이터를 비교하여 변경된 셀을 감지한다.
    ///
    /// 시뮬레이션 코어를 수정하지 않고 렌더링 측에서 독립적으로 동작한다.
    /// 스냅샷은 ElementId + Mass만 저장하여 메모리를 최소화한다.
    /// (200×200 = 40,000 셀 × 3바이트 = 120KB)
    ///
    /// 사용 흐름:
    ///   1. DetectDirty(grid) → dirty 인덱스 목록 반환
    ///   2. 렌더러가 dirty 목록으로 부분 갱신
    ///   3. 다음 틱에서 다시 DetectDirty 호출
    /// </summary>
    public sealed class DirtyTracker
    {
        private byte[] _prevElementIds;
        private int[] _prevMasses;
        private int _cellCount;
        private bool _firstFrame;

        // dirty 비율이 이 값을 넘으면 전체 갱신이 더 효율적
        private const float FullRefreshThreshold = 0.3f;

        // dirty 인덱스 목록 (재사용)
        private readonly List<int> _dirtyIndices = new List<int>(256);

        /// <summary>
        /// 마지막 DetectDirty 호출에서 감지된 dirty 셀 인덱스 목록.
        /// </summary>
        public IReadOnlyList<int> DirtyIndices => _dirtyIndices;

        /// <summary>
        /// dirty 셀 수가 전체의 30% 이상이면 true.
        /// 이 경우 부분 갱신보다 전체 갱신이 효율적이다.
        /// </summary>
        public bool ShouldFullRefresh { get; private set; }

        /// <summary>
        /// 변경된 셀이 하나도 없으면 true.
        /// </summary>
        public bool IsClean => _dirtyIndices.Count == 0;

        public DirtyTracker(int cellCount)
        {
            _cellCount = cellCount;
            _prevElementIds = new byte[cellCount];
            _prevMasses = new int[cellCount];
            _firstFrame = true;
        }

        /// <summary>
        /// 현재 그리드와 이전 스냅샷을 비교하여 dirty 셀을 감지한다.
        /// 호출 후 DirtyIndices와 ShouldFullRefresh를 읽을 수 있다.
        /// </summary>
        public void DetectDirty(WorldGrid grid)
        {
            int total = grid.Width * grid.Height;

            // 크기 변경 대응
            if (total != _cellCount)
            {
                _cellCount = total;
                _prevElementIds = new byte[total];
                _prevMasses = new int[total];
                _firstFrame = true;
            }

            _dirtyIndices.Clear();

            if (_firstFrame)
            {
                // 첫 프레임: 모든 셀이 dirty
                for (int i = 0; i < total; i++)
                    _dirtyIndices.Add(i);

                ShouldFullRefresh = true;
                _firstFrame = false;
                SaveSnapshot(grid, total);
                return;
            }

            // 비교
            for (int i = 0; i < total; i++)
            {
                SimCell cell = grid.GetCellByIndex(i);

                if (cell.ElementId != _prevElementIds[i] ||
                    cell.Mass != _prevMasses[i])
                {
                    _dirtyIndices.Add(i);
                }
            }

            ShouldFullRefresh = _dirtyIndices.Count > (int)(total * FullRefreshThreshold);
            SaveSnapshot(grid, total);
        }

        private void SaveSnapshot(WorldGrid grid, int total)
        {
            for (int i = 0; i < total; i++)
            {
                SimCell cell = grid.GetCellByIndex(i);
                _prevElementIds[i] = cell.ElementId;
                _prevMasses[i] = cell.Mass;
            }
        }
    }
}
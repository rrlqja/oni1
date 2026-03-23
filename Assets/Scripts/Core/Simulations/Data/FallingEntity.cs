namespace Core.Simulation.Data
{
    /// <summary>
    /// 투사체 낙하 엔티티.
    /// 그리드에서 제거된 후 시뮬레이션 외부에서 낙하하는 원소 데이터.
    /// 착지 시 그리드에 재생성된다.
    ///
    /// 중력 가속도: 매 틱 velocity가 gravity만큼 증가하여
    /// 처음엔 느리다가 점점 빨라지는 자연스러운 낙하.
    /// </summary>
    public struct FallingEntity
    {
        /// <summary>원소 ID</summary>
        public byte ElementId;

        /// <summary>질량 (mg)</summary>
        public int Mass;

        /// <summary>온도</summary>
        public float Temperature;

        /// <summary>X 셀 좌표 (정수, 수직 낙하이므로 변하지 않음)</summary>
        public int CellX;

        /// <summary>현재 Y 위치 (실수, 렌더링 보간 + 이동 계산용)</summary>
        public float CurrentY;

        /// <summary>이전 틱의 Y 위치 (렌더링 보간용)</summary>
        public float PreviousY;

        /// <summary>현재 낙하 속도 (셀/틱, 실수). 매 틱 gravity만큼 증가.</summary>
        public float Velocity;

        /// <summary>유효한 엔티티인지 (풀링용)</summary>
        public bool IsActive;

        public FallingEntity(
            byte elementId, int mass, float temperature,
            int cellX, float startY)
        {
            ElementId = elementId;
            Mass = mass;
            Temperature = temperature;
            CellX = cellX;
            CurrentY = startY;
            PreviousY = startY;
            Velocity = 0f; // 초기 속도 0, 중력 가속도로 증가
            IsActive = true;
        }
    }
}
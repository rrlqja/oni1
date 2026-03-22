namespace Core.Simulation.Data
{
    /// <summary>
    /// 투사체 낙하 엔티티.
    /// 그리드에서 제거된 후 시뮬레이션 외부에서 낙하하는 원소 데이터.
    /// 착지 시 그리드에 재생성된다.
    /// </summary>
    public struct FallingEntity
    {
        /// <summary>원소 ID</summary>
        public byte ElementId;

        /// <summary>질량 (mg)</summary>
        public int Mass;

        /// <summary>온도</summary>
        public short Temperature;

        /// <summary>X 셀 좌표 (정수, 수직 낙하이므로 변하지 않음)</summary>
        public int CellX;

        /// <summary>현재 Y 위치 (실수, 렌더링 보간용)</summary>
        public float CurrentY;

        /// <summary>틱당 낙하 속도 (셀/틱)</summary>
        public int FallSpeed;

        /// <summary>유효한 엔티티인지 (풀링용)</summary>
        public bool IsActive;

        public FallingEntity(
            byte elementId, int mass, short temperature,
            int cellX, float startY, int fallSpeed)
        {
            ElementId = elementId;
            Mass = mass;
            Temperature = temperature;
            CellX = cellX;
            CurrentY = startY;
            FallSpeed = fallSpeed;
            IsActive = true;
        }
    }
}
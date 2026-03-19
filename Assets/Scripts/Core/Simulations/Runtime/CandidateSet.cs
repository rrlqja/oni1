namespace Core.Simulation.Runtime
{
    /// <summary>
    /// 방향 후보 하나.
    /// Index: 대상 셀 인덱스
    /// ActionType: 0 = FlowBatch(진공 확장), 1 = Swap(이종 교환)
    /// Weight: 방향 가중치 (밀도 기반)
    /// </summary>
    public struct DirectionCandidate
    {
        public int Index;
        public byte ActionType;
        public float Weight;

        public DirectionCandidate(int index, byte actionType, float weight)
        {
            Index = index;
            ActionType = actionType;
            Weight = weight;
        }

        /// <summary>FlowBatch 대상 (진공 확장)</summary>
        public const byte ActionFlow = 0;

        /// <summary>Swap 대상 (이종 교환)</summary>
        public const byte ActionSwap = 1;
    }

    /// <summary>
    /// 최대 4방향 후보를 힙 할당 없이 수집하는 고정 크기 버퍼.
    /// 기체 밀도 이동(Phase 5) 등에서 방향 가중치 기반 선택에 사용.
    /// </summary>
    public struct CandidateSet
    {
        public DirectionCandidate C0;
        public DirectionCandidate C1;
        public DirectionCandidate C2;
        public DirectionCandidate C3;
        public int Count;

        public void Add(int index, byte actionType, float weight)
        {
            switch (Count)
            {
                case 0: C0 = new DirectionCandidate(index, actionType, weight); break;
                case 1: C1 = new DirectionCandidate(index, actionType, weight); break;
                case 2: C2 = new DirectionCandidate(index, actionType, weight); break;
                case 3: C3 = new DirectionCandidate(index, actionType, weight); break;
                default: return;
            }
            Count++;
        }

        public DirectionCandidate Get(int i)
        {
            switch (i)
            {
                case 0: return C0;
                case 1: return C1;
                case 2: return C2;
                case 3: return C3;
                default: return default;
            }
        }

        /// <summary>
        /// 가중치 합계를 반환한다. 확률 기반 선택에 사용.
        /// </summary>
        public float TotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
                total += Get(i).Weight;
            return total;
        }

        /// <summary>
        /// 정규화된 랜덤 값(0~1)으로 가중치 기반 후보를 선택한다.
        /// </summary>
        public DirectionCandidate SelectByWeight(float normalizedRandom)
        {
            float total = TotalWeight();
            if (total <= 0f)
                return Count > 0 ? C0 : default;

            float threshold = normalizedRandom * total;
            float accumulated = 0f;

            for (int i = 0; i < Count; i++)
            {
                DirectionCandidate c = Get(i);
                accumulated += c.Weight;
                if (accumulated >= threshold)
                    return c;
            }

            // 부동소수점 오차 대비: 마지막 후보 반환
            return Get(Count - 1);
        }
    }
}
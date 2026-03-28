using System;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    /// <summary>
    /// Phase 7: 열 전도 처리기.
    ///
    /// 인접 셀 간 열 교환을 수집-적용 패턴으로 처리한다.
    ///
    /// 핵심 공식:
    ///   k_eff = 2 × (kA × kB) / (kA + kB)        조화평균 전도율
    ///   Q = k_eff × (T_hot - T_cold) × SCALE      열 교환량
    ///   ΔTA = -Q / (massA × cA)                    뜨거운 쪽 감소
    ///   ΔTB = +Q / (massB × cB)                    차가운 쪽 증가
    ///
    /// 조화평균: 한쪽 전도율이 낮으면 전체 전달이 느려짐.
    ///   증기(0.02) ↔ 화강암(3.0) → k_eff=0.04 (극히 느림)
    ///   마그마(2.0) ↔ 화강암(3.0) → k_eff=2.4 (빠름)
    ///
    /// 수집-적용: _deltaTemp[] 배열에 누적 후 일괄 적용.
    ///   스캔 순서에 의한 비대칭 방지.
    ///
    /// 진공(Vacuum)은 건너뜀: 물질이 없으므로 열 교환 불가.
    /// </summary>
    public sealed class TemperatureProcessor
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly float[] _deltaTemp;
        private readonly SimulationSettings _settings;

        public TemperatureProcessor(WorldGrid grid, ElementRegistry registry, SimulationSettings settings = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _deltaTemp = new float[_grid.Length];
        }

        /// <summary>
        /// 인접 셀 간 열 전도를 수집-적용 패턴으로 처리한다.
        /// </summary>
        public void Process()
        {
            // ── 수집 단계: 모든 인접 셀 쌍의 열 교환량 계산 ──
            Array.Clear(_deltaTemp, 0, _deltaTemp.Length);

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int idx = _grid.ToIndex(x, y);
                    SimCell cell = _grid.GetCellByIndex(idx);

                    // 진공이거나 질량 없으면 건너뜀
                    if (cell.ElementId == BuiltInElementIds.Vacuum || cell.Mass <= 0)
                        continue;

                    // 우측 이웃 (각 쌍을 한 번만 처리)
                    if (x + 1 < _grid.Width)
                        AccumulateHeatExchange(idx, _grid.ToIndex(x + 1, y));

                    // 상단 이웃
                    if (y + 1 < _grid.Height)
                        AccumulateHeatExchange(idx, _grid.ToIndex(x, y + 1));
                }
            }

            // ── 적용 단계: 누적된 온도 변화를 일괄 적용 ──
            for (int i = 0; i < _grid.Length; i++)
            {
                float dt = _deltaTemp[i];
                if (dt == 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                    continue;

                ref SimCell cell = ref _grid.GetCellRef(i);
                if (cell.ElementId == BuiltInElementIds.Vacuum)
                    continue;

                cell.Temperature += dt;

                // 절대영도 이하 방지
                if (cell.Temperature < TemperatureConstants.ABSOLUTE_ZERO)
                    cell.Temperature = TemperatureConstants.ABSOLUTE_ZERO;
            }
        }

        private void AccumulateHeatExchange(int idxA, int idxB)
        {
            SimCell a = _grid.GetCellByIndex(idxA);
            SimCell b = _grid.GetCellByIndex(idxB);

            // 상대가 진공이거나 질량 없으면 교환 불가
            if (b.ElementId == BuiltInElementIds.Vacuum || b.Mass <= 0)
                return;

            float tempDiff = a.Temperature - b.Temperature;
            if (Math.Abs(tempDiff) < _settings.MinHeatExchange)
                return;

            ref readonly ElementRuntimeDefinition defA = ref _registry.Get(a.ElementId);
            ref readonly ElementRuntimeDefinition defB = ref _registry.Get(b.ElementId);

            float kA = defA.ThermalConductivity;
            float kB = defB.ThermalConductivity;

            // 어느 한쪽이 0이면 열 전달 불가 (완벽한 단열)
            if (kA <= 0f || kB <= 0f)
                return;

            // 조화평균 전도율
            float kEff = 2f * kA * kB / (kA + kB);

            // 열 교환량 (양수면 A→B 방향)
            float q = kEff * tempDiff * _settings.ConductivityScale;

            // 열 용량 = 질량 × 비열
            float capacityA = (a.Mass * 0.001f) * defA.SpecificHeatCapacity;
            float capacityB = (b.Mass * 0.001f) * defB.SpecificHeatCapacity;

            const float MIN_CAPACITY = 0.01f;
            if (capacityA < MIN_CAPACITY || capacityB < MIN_CAPACITY) return;

            float dtA = -q / capacityA;
            float dtB = q / capacityB;

            // ── 역전 방지 클램프 ──
            // 열 교환 후 온도가 역전되면 안 됨.
            // 가중평균 온도(열적 평형점)를 넘지 않도록 제한.
            float midpoint = (a.Temperature * capacityA + b.Temperature * capacityB)
                           / (capacityA + capacityB);

            if (tempDiff > 0f)
            {
                // A가 뜨거움: A는 midpoint 이하로 내려가면 안 됨, B는 midpoint 이상으로 올라가면 안 됨
                float newA = a.Temperature + dtA;
                float newB = b.Temperature + dtB;

                if (newA < midpoint)
                {
                    dtA = midpoint - a.Temperature;
                    dtB = midpoint - b.Temperature;
                }
                else if (newB > midpoint)
                {
                    dtA = midpoint - a.Temperature;
                    dtB = midpoint - b.Temperature;
                }
            }
            else
            {
                // B가 뜨거움: 반대 방향
                float newA = a.Temperature + dtA;
                float newB = b.Temperature + dtB;

                if (newA > midpoint)
                {
                    dtA = midpoint - a.Temperature;
                    dtB = midpoint - b.Temperature;
                }
                else if (newB < midpoint)
                {
                    dtA = midpoint - a.Temperature;
                    dtB = midpoint - b.Temperature;
                }
            }

            _deltaTemp[idxA] += dtA;
            _deltaTemp[idxB] += dtB;
        }
    }
}
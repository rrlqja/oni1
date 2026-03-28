using System;
using System.Collections.Generic;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using UnityEngine;

namespace Core.Simulation.Runtime.WorldGeneration
{
    /// <summary>
    /// 맵 생성기.
    ///
    /// Step 1: Voronoi + Lloyd's Relaxation + 노이즈 경계
    ///   - 격자+지터 초기 배치 → Relaxation으로 균일화 → 노이즈로 경계 흔들기
    ///   - 결과: 크기가 균일한 유기적 바이옴 영역
    ///
    /// Step 2~4: 미구현 (향후 단계별 추가)
    /// </summary>
    public sealed class WorldGenerator
    {
        private readonly WorldGrid _grid;
        private readonly ElementRegistry _registry;
        private readonly WorldGenProfileSO _profile;
        private readonly SeededRandom _rng;
        private readonly int _seed;

        private readonly int[] _biomeMap;
        private readonly List<BiomePoint> _biomePoints = new(200);
        private readonly List<BiomeSO> _biomeList = new(20);

        private struct BiomePoint
        {
            public float X;
            public float Y;
            public int BiomeIndex;
        }

        public int[] BiomeMap => _biomeMap;
        public IReadOnlyList<BiomeSO> BiomeList => _biomeList;

        public WorldGenerator(WorldGrid grid, ElementRegistry registry, WorldGenProfileSO profile)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            _seed = profile.Seed;
            _rng = new SeededRandom(_seed);
            _biomeMap = new int[_grid.Length];
        }

        public void Generate()
        {
            ClearGrid();
            GenerateBiomeLayout();
            CreateBorder();

            Debug.Log($"[WorldGen] Step 1 완료. 시드={_seed}, " +
                      $"포인트={_biomePoints.Count}, " +
                      $"바이옴={_biomeList.Count}, " +
                      $"relaxation={_profile.RelaxationIterations}회");
        }

        // ================================================================
        //  Step 0: 초기화
        // ================================================================

        private void ClearGrid()
        {
            _grid.Fill(BuiltInElementIds.Vacuum, 0, 0f);
            _grid.ClearAllTickReservations();
        }

        // ================================================================
        //  Step 1: 바이옴 영역 분할
        // ================================================================

        private void GenerateBiomeLayout()
        {
            BuildBiomeList();
            PlaceBiomePoints();
            RelaxPoints();
            AssignCellsToBiomes();
        }

        private void BuildBiomeList()
        {
            _biomeList.Clear();

            if (_profile.SpawnBiome != null)
                _biomeList.Add(_profile.SpawnBiome);

            if (_profile.AvailableBiomes != null)
            {
                foreach (var biome in _profile.AvailableBiomes)
                {
                    if (biome != null)
                        _biomeList.Add(biome);
                }
            }
        }

        /// <summary>
        /// 초기 포인트 배치: 격자 + 지터.
        /// 순수 랜덤보다 초기 분포가 균일하여 Relaxation 수렴이 빠르다.
        /// </summary>
        private void PlaceBiomePoints()
        {
            _biomePoints.Clear();

            int w = _grid.Width;
            int h = _grid.Height;
            int border = _profile.BorderThickness;

            float innerMinX = border + 1;
            float innerMinY = border + 1;
            float innerW = w - (border + 1) * 2;
            float innerH = h - (border + 1) * 2;

            int targetCount = _profile.BiomePointCount;

            // 격자 크기 계산
            float cellArea = (innerW * innerH) / targetCount;
            float cellSize = Mathf.Sqrt(cellArea);
            int cols = Mathf.Max(1, Mathf.RoundToInt(innerW / cellSize));
            int rows = Mathf.Max(1, Mathf.RoundToInt(innerH / cellSize));

            float stepX = innerW / cols;
            float stepY = innerH / rows;

            // 스폰 포인트 (중앙 고정)
            if (_biomeList.Count > 0)
            {
                _biomePoints.Add(new BiomePoint
                {
                    X = innerMinX + innerW * 0.5f,
                    Y = innerMinY + innerH * 0.5f,
                    BiomeIndex = 0
                });
            }

            // 격자 + 지터
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (_biomePoints.Count >= targetCount)
                        break;

                    float baseX = innerMinX + (col + 0.5f) * stepX;
                    float baseY = innerMinY + (row + 0.5f) * stepY;

                    float jitterX = (_rng.NextFloat() - 0.5f) * stepX * 0.8f;
                    float jitterY = (_rng.NextFloat() - 0.5f) * stepY * 0.8f;

                    float px = Mathf.Clamp(baseX + jitterX, innerMinX, innerMinX + innerW);
                    float py = Mathf.Clamp(baseY + jitterY, innerMinY, innerMinY + innerH);

                    // 스폰 포인트와 너무 가까우면 스킵
                    if (_biomePoints.Count > 0)
                    {
                        float dx = px - _biomePoints[0].X;
                        float dy = py - _biomePoints[0].Y;
                        if (dx * dx + dy * dy < cellSize * cellSize * 0.25f)
                            continue;
                    }

                    int biomeIdx = _biomeList.Count > 1
                        ? _rng.NextInt(1, _biomeList.Count)
                        : 0;

                    _biomePoints.Add(new BiomePoint
                    {
                        X = px, Y = py,
                        BiomeIndex = biomeIdx
                    });
                }
            }
        }

        /// <summary>
        /// Lloyd's Relaxation: 각 포인트를 Voronoi 셀 중심(centroid)으로 이동.
        /// 반복할수록 셀 크기가 균일해진다.
        /// 스폰 포인트(인덱스 0)는 중앙 고정.
        /// </summary>
        private void RelaxPoints()
        {
            int iterations = _profile.RelaxationIterations;
            if (iterations <= 0 || _biomePoints.Count <= 1)
                return;

            int w = _grid.Width;
            int h = _grid.Height;
            int border = _profile.BorderThickness;

            float innerMinX = border + 1;
            float innerMinY = border + 1;
            float innerMaxX = w - border - 2;
            float innerMaxY = h - border - 2;

            float[] sumX = new float[_biomePoints.Count];
            float[] sumY = new float[_biomePoints.Count];
            int[] count = new int[_biomePoints.Count];

            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Clear(sumX, 0, sumX.Length);
                Array.Clear(sumY, 0, sumY.Length);
                Array.Clear(count, 0, count.Length);

                // 1. 각 셀을 가장 가까운 포인트에 할당 (순수 거리)
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float minDist = float.MaxValue;
                        int closest = 0;

                        for (int i = 0; i < _biomePoints.Count; i++)
                        {
                            float dx = x - _biomePoints[i].X;
                            float dy = y - _biomePoints[i].Y;
                            float dist = dx * dx + dy * dy;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closest = i;
                            }
                        }

                        sumX[closest] += x;
                        sumY[closest] += y;
                        count[closest]++;
                    }
                }

                // 2. centroid로 이동
                for (int i = 0; i < _biomePoints.Count; i++)
                {
                    if (i == 0) continue; // 스폰 고정
                    if (count[i] == 0) continue;

                    var pt = _biomePoints[i];
                    pt.X = Mathf.Clamp(sumX[i] / count[i], innerMinX, innerMaxX);
                    pt.Y = Mathf.Clamp(sumY[i] / count[i], innerMinY, innerMaxY);
                    _biomePoints[i] = pt;
                }
            }
        }

        /// <summary>
        /// 최종 셀 할당: 노이즈로 경계 흔들기.
        /// </summary>
        private void AssignCellsToBiomes()
        {
            if (_biomePoints.Count == 0) return;

            int w = _grid.Width;
            int h = _grid.Height;
            float noiseStrength = _profile.BoundaryNoise;
            float noiseScale = _profile.BoundaryNoiseScale;
            int noiseSeed = _seed + 7919;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = x;
                    float ny = y;

                    if (noiseStrength > 0f)
                    {
                        nx += SeededRandom.Noise2D(
                            x * noiseScale, y * noiseScale, noiseSeed) * noiseStrength;
                        ny += SeededRandom.Noise2D(
                            x * noiseScale + 100f, y * noiseScale + 100f, noiseSeed + 1) * noiseStrength;
                    }

                    float minDist = float.MaxValue;
                    int closestBiome = 0;

                    for (int i = 0; i < _biomePoints.Count; i++)
                    {
                        float dx = nx - _biomePoints[i].X;
                        float dy = ny - _biomePoints[i].Y;
                        float dist = dx * dx + dy * dy;

                        if (dist < minDist)
                        {
                            minDist = dist;
                            closestBiome = _biomePoints[i].BiomeIndex;
                        }
                    }

                    _biomeMap[y * w + x] = closestBiome;
                }
            }
        }

        // ================================================================
        //  Bedrock 테두리
        // ================================================================

        private void CreateBorder()
        {
            if (!_registry.IsRegistered(BuiltInElementIds.Bedrock))
                return;

            ref readonly var bedrock = ref _registry.Get(BuiltInElementIds.Bedrock);
            int t = _profile.BorderThickness;
            int w = _grid.Width;
            int h = _grid.Height;

            SimCell bedrockCell = new SimCell(
                elementId: bedrock.Id,
                mass: bedrock.DefaultMass,
                temperature: bedrock.DefaultTemperature);

            for (int y = 0; y < t; y++)
                for (int x = 0; x < w; x++)
                {
                    _grid.SetCell(x, y, bedrockCell);
                    _grid.SetCell(x, h - 1 - y, bedrockCell);
                }

            for (int y = t; y < h - t; y++)
                for (int x = 0; x < t; x++)
                {
                    _grid.SetCell(x, y, bedrockCell);
                    _grid.SetCell(w - 1 - x, y, bedrockCell);
                }
        }

        // ================================================================
        //  바이옴 색상 조회
        // ================================================================

        public Color GetBiomeColor(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= _biomeMap.Length)
                return Color.black;

            int biomeIdx = _biomeMap[cellIndex];

            if (biomeIdx < 0 || biomeIdx >= _biomeList.Count)
                return Color.black;

            return _biomeList[biomeIdx].BackgroundColor;
        }
    }
}
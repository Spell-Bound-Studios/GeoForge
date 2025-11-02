// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    [CreateAssetMenu(menuName = "Spellbound/MarchingCubes/TerrainSettings")]
    public class TerrainConfig : ScriptableObject {
        [Header("Marching Cubes Settings")] public int resolution = 1;

        [Range(8, 32)] public int cubesPerMarch = 16;

        [Range(100, 1000)] public int viewDistance = 700;

        [Range(2f, 4f)] public float lodRangeScale = 2f;

        [Range(0, 1)] public float marchingCubeDensityThreshold = 0.5f;

        [Space(50), Header("Advance/Derived Settings")]
        public int chunkSize = 128;

        public int lods = 3;

        public Vector2[] lodRanges;

        [Space(50)] public bool useBuiltInMap = true;
        public BuiltInMap builtInMap;

        [Serializable]
        public class BuiltInMap {
            public Vector3Int mapSize = new(384, 128, 384);
            public MapStartState startState = MapStartState.HalfFull;
            public MapEdgeConstraints top = MapEdgeConstraints.Open;
            public MapEdgeConstraints bottom = MapEdgeConstraints.Closed;
            public MapEdgeConstraints sides = MapEdgeConstraints.Closed;
        }

        private void OnValidate() {
            if (cubesPerMarch % 2 != 0)
                cubesPerMarch += 1;

            ComputeLods(out chunkSize, out lods, out lodRanges);
        }

        private void ComputeLods(out int chunkSize, out int lodCount, out Vector2[] lodRadii) {
            lodCount = Mathf.FloorToInt(
                Mathf.Log(viewDistance / (lodRangeScale * cubesPerMarch) + 1, 2)
            );
            chunkSize = cubesPerMarch * (int)Mathf.Pow(2, lodCount - 1);
            var radiiList = new List<Vector2>();

            for (var n = 0; n < lodCount; n++) {
                var currentChunkSize = cubesPerMarch * Mathf.Pow(2, n);

                // clamp to maximum allowed chunk size
                if (currentChunkSize > this.chunkSize) currentChunkSize = this.chunkSize;

                var radius = lodRangeScale * cubesPerMarch * Mathf.Pow(2, n + 1);
                if (radius > viewDistance) radius = viewDistance;

                var minRadius = radiiList.Count == 0 ? 0 : radiiList[^1].y;
                radiiList.Add(new Vector2(minRadius, radius));

                if (radius >= viewDistance - 0.01f)
                    break;
            }

            lodRadii = radiiList.ToArray();
        }

        public enum MapEdgeConstraints {
            Open,
            Closed,
            Unconstrained
        }

        public enum MapStartState {
            Full,
            HalfFull,
            Empty
        }
    }
}
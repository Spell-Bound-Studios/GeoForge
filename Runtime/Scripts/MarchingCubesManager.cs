// Copyright 2025 Spellbound Studio Inc.

using Unity.Entities;
using UnityEngine;
using McHelper = SpellBound.MarchingCubes.McStaticHelper;

namespace SpellBound.MarchingCubes {
    public class MarchingCubesManager : MonoBehaviour {
        public static MarchingCubesManager Instance { get; private set; }

        public BlobAssetReference<MCTablesBlobAsset> McTablesBlob;
        [SerializeField] public GameObject octreePrefab;

        [Range(300f, 1000f), SerializeField] public float viewDistance = 350;

        //This MUST have a length of MaxLevelOfDetail + 1
        [SerializeField] public Vector2[] lodRanges = {
            new(0, 80),
            new(60, 120),
            new(120, 250),
            new(200, 350)
        };

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);

                return;
            }

            Instance = this;

            McTablesBlob = MCTablesBlobCreator.CreateMCTablesBlobAsset();
        }

        private void OnValidate() {
            lodRanges = new Vector2[McHelper.MaxLevelOfDetail + 1];

            for (var i = 0; i < lodRanges.Length; i++) {
                var div = Mathf.Pow(2, lodRanges.Length - 1 - i);

                if (i == 0) {
                    lodRanges[i] = new Vector2(0, Mathf.Clamp(viewDistance, 0, viewDistance / div));

                    continue;
                }

                lodRanges[i] = new Vector2(lodRanges[i - 1].y, Mathf.Clamp(viewDistance, 0, viewDistance / div));
            }
        }

        private void OnDestroy() {
            if (McTablesBlob.IsCreated)
                McTablesBlob.Dispose();
        }
    }
}
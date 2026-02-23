// Copyright 2026 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Console;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        private Dictionary<int, DenseVoxelData> _denseVoxelDataDict = new();

        public NativeArray<VoxelData> GetOrUnpackVoxelArray(
            int dataSizeKey,
            BaseChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                Debug.LogError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");

                return new DenseVoxelData().DenseVoxelArray;
            }

            ref var config = ref chunk.ParentVolume.ConfigBlob.Value;

            if (denseVoxelData.IsArrayInUse) {
                if (chunk != denseVoxelData.CurrentChunk) {
                    Debug.LogError(
                        $"GetOrUnpackVoxelArray - Trying to unpack voxel array while another unpacked voxel array  is in use");

                    return denseVoxelData.DenseVoxelArray;
                }

                Debug.LogError(
                    $"GetOrUnpackVoxelArray - Trying to unpack voxel array but array is in use for the same chunk. This is unexpected and bad.");

                return denseVoxelData.DenseVoxelArray;
            }

            if (chunk == denseVoxelData.CurrentChunk) {
                // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - No need to unpack. Getting voxel array for {coord}, sparseVoxels length is {sparseData.Length}.");
                denseVoxelData.IsArrayInUse = true;

                return denseVoxelData.DenseVoxelArray;
            }

            // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - Unpacking voxel array for {coord}, sparseVoxels length is {sparseData.Length}");
            denseVoxelData.IsArrayInUse = true;
            denseVoxelData.CurrentChunk = chunk;

            denseVoxelData.DensityRange[0] = new DensityRange(byte.MaxValue, byte.MinValue, config.DensityThreshold);

            var unpackJob = new SparseToDenseVoxelDataJob {
                ConfigBlob = chunk.ParentVolume.ConfigBlob,
                Voxels = denseVoxelData.DenseVoxelArray,
                SparseVoxels = sparseData,
                DensityRange = denseVoxelData.DensityRange
            };
            var jobHandle = unpackJob.Schedule(config.ChunkDataWidthSize, 1);
            jobHandle.Complete();

            return denseVoxelData.DenseVoxelArray;
        }

        public void PackVoxelArray(int dataSizeKey) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                Debug.LogError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");
            }

            if (denseVoxelData.CurrentChunk == null) {
                Debug.LogError(
                    $"PackVoxelArray - Trying to pack but CurrentChunk is null");

                return;
            }

            if (!denseVoxelData.IsArrayInUse) {
                Debug.LogError(
                    $"PackVoxelArray - Trying to pack but _isArrayInUse is false which is unexpected and bad");
            }

            var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

            var packJob = new DenseToSparseVoxelDataJob {
                Voxels = denseVoxelData.DenseVoxelArray,
                SparseVoxels = sparseData
            };
            var jobHandle = packJob.Schedule();
            jobHandle.Complete();

            // ConsoleLogger.PrintToConsole($"PackVoxelArray - Packing voxel array for {_currentCoord}, sparseVoxels length is {sparseData.Length}");

            denseVoxelData.CurrentChunk.UpdateVoxelData(sparseData, denseVoxelData.DensityRange[0]);
            sparseData.Dispose();
        }

        public void ReleaseVoxelArray(int dataSizeKey) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                ConsoleLogger.PrintError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");

                return;
            }

            denseVoxelData.IsArrayInUse = false;
        }

        public class DenseVoxelData : IDisposable {
            public NativeArray<VoxelData> DenseVoxelArray;
            public NativeArray<DensityRange> DensityRange;
            public Dictionary<int, List<Vector3Int>> SharedIndicesAcrossChunks;
            public bool IsArrayInUse;
            public BaseChunk CurrentChunk;

            public DenseVoxelData(
                int chunkSize, BaseChunk currentChunk = null, Allocator allocator = Allocator.Persistent) {
                var cs = chunkSize + 3;
                DenseVoxelArray = new NativeArray<VoxelData>(cs * cs * cs, allocator);
                DensityRange = new NativeArray<DensityRange>(1, allocator);
                SharedIndicesAcrossChunks = InitializeSharedIndicesLookup(chunkSize);
                IsArrayInUse = false;
                CurrentChunk = null;
            }

            public DenseVoxelData() {
                DenseVoxelArray = default;
                DensityRange = default;
                IsArrayInUse = false;
                CurrentChunk = null;
            }

            private Dictionary<int, List<Vector3Int>> InitializeSharedIndicesLookup(int chunkSize) {
                var sharedIndices = new Dictionary<int, List<Vector3Int>>();
                var cs = chunkSize + 3;
                List<Vector3Int> neighborCoords = new();

                for (var dx = -1; dx <= 1; dx++) {
                    for (var dy = -1; dy <= 1; dy++) {
                        for (var dz = -1; dz <= 1; dz++) {
                            var coordDelta = new Vector3Int(dx, dy, dz);

                            if (coordDelta == Vector3Int.zero)
                                continue;

                            neighborCoords.Add(new Vector3Int(dx, dy, dz));
                        }
                    }
                }

                var chunkBounds = new BoundsInt(
                    0,
                    0,
                    0,
                    chunkSize + 3,
                    chunkSize + 3,
                    chunkSize + 3
                );

                for (var i = 0; i < cs * cs * cs; i++) {
                    GfStaticHelper.IndexToInt3(i, cs * cs, cs, out var x, out var y,
                        out var z);
                    var localPos = new Vector3Int(x, y, z);

                    foreach (var coord in neighborCoords) {
                        var localPosNeighbor = localPos - coord * chunkSize;

                        if (!chunkBounds.Contains(localPosNeighbor))
                            continue;

                        if (!sharedIndices.TryGetValue(i, out var coordsSharingIndex)) {
                            coordsSharingIndex = new List<Vector3Int>();
                            sharedIndices[i] = coordsSharingIndex;
                        }

                        coordsSharingIndex.Add(coord);
                    }
                }

                return sharedIndices;
            }

            public void Dispose() {
                if (DenseVoxelArray.IsCreated)
                    DenseVoxelArray.Dispose();

                if (DensityRange.IsCreated)
                    DensityRange.Dispose();
            }
        }
    }
}
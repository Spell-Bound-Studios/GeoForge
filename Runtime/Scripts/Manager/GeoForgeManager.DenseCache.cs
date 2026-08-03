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
            GeoChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                // No entry means RegisterVoxelVolume was never called for this chunk size - a
                // setup/lifecycle bug, not a normal runtime condition. Throw immediately instead
                // of handing back an uncreated NativeArray, which would only surface as a
                // confusing "array not allocated" exception far away at the first index into it.
                throw new InvalidOperationException(
                    $"GetOrUnpackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            ref var config = ref chunk.ParentGeoVolume.ConfigBlob.Value;

            if (denseVoxelData.IsArrayInUse) {
                if (chunk != denseVoxelData.CurrentChunk) {
                    Debug.LogError(
                        $"GetOrUnpackVoxelArray - Trying to unpack voxel array while another unpacked voxel array  is in use");

                    return denseVoxelData.DenseVoxelArray;
                }

                Debug.LogError(
                    $"GetOrUnpackVoxelArray - Trying to unpack voxel array but array is in use for the same geoChunk. This is unexpected and bad.");

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

            // DensityRange is no longer touched by the unpack job - it's only ever computed by the
            // pack job (DenseToSparseVoxelDataJob), which is single-threaded and always runs after
            // edits are written into the dense array. No seed/reset needed here; whatever value is
            // sitting in denseVoxelData.DensityRange[0] is leftover from a previous pack and gets
            // overwritten the next time PackVoxelArray runs for this pooled slot.
            var unpackJob = new SparseToDenseVoxelDataJob {
                ConfigBlob = chunk.ParentGeoVolume.ConfigBlob,
                Voxels = denseVoxelData.DenseVoxelArray,
                SparseVoxels = sparseData
            };
            var jobHandle = unpackJob.Schedule(config.ChunkDataWidthSize, 1);
            jobHandle.Complete();

            return denseVoxelData.DenseVoxelArray;
        }

        public void PackVoxelArray(int dataSizeKey) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                // Same misuse case as GetOrUnpackVoxelArray - throw immediately rather than
                // falling through and dereferencing a null denseVoxelData on the next line.
                throw new InvalidOperationException(
                    $"PackVoxelArray: no denseVoxelData registered for chunk size {dataSizeKey}. " +
                    "Was RegisterVoxelVolume called for this volume's chunk size?");
            }

            if (denseVoxelData.CurrentChunk == null) {
                Debug.LogError(
                    $"PackVoxelArray - Trying to pack but CurrentChunk is null");

                return;
            }

            if (!denseVoxelData.IsArrayInUse) {
                // Was falling through and packing anyway even though nothing currently has this
                // array checked out - same "log then continue as if nothing happened" pattern as
                // the two misuse cases above. Stop here instead.
                Debug.LogError(
                    $"PackVoxelArray - Trying to pack but _isArrayInUse is false which is unexpected and bad");

                return;
            }

            var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

            // DensityRange is computed fresh here, single-threaded, from the dense array as it
            // currently stands - which already has any pending edits written into it by the time
            // PackVoxelArray is called. This is the only place DensityRange gets computed.
            var packJob = new DenseToSparseVoxelDataJob {
                Voxels = denseVoxelData.DenseVoxelArray,
                SparseVoxels = sparseData,
                DensityRange = denseVoxelData.DensityRange
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
            public GeoChunk CurrentChunk;

            public DenseVoxelData(
                int chunkSize, GeoChunk currentChunk = null, Allocator allocator = Allocator.Persistent) {
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
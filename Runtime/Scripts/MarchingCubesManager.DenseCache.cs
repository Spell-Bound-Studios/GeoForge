// Copyright 2025 Spellbound Studio Inc.

using System;
using System.Collections.Generic;
using Spellbound.Core.Console;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public partial class MarchingCubesManager : MonoBehaviour {
        private Dictionary<int, DenseVoxelData> _denseVoxelDataDict = new();

        public NativeArray<VoxelData> GetOrUnpackVoxelArray(
            int dataSizeKey,
            Vector3Int coord,
            VoxChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                Debug.LogError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");

                return new DenseVoxelData().DenseVoxelArray;
            }
            if (denseVoxelData.IsArrayInUse) {
                if (denseVoxelData.CurrentCoord.HasValue && denseVoxelData.CurrentCoord.Value != coord) {
                    Debug.LogError(
                        $"GetOrUnpackVoxelArray - Trying to unpack voxel array for {coord} while another unpacked voxel array for {denseVoxelData.CurrentCoord.Value} is in use");

                    return denseVoxelData.DenseVoxelArray;
                }

                Debug.LogError(
                    $"GetOrUnpackVoxelArray - Trying to unpack voxel array for {coord} but array is in use for the same coord. This is unexpected and bad.");

                return denseVoxelData.DenseVoxelArray;
            }

            if (denseVoxelData.CurrentCoord.HasValue && denseVoxelData.CurrentCoord.Value == coord && chunk == denseVoxelData.CurrentChunk) {
                // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - No need to unpack. Getting voxel array for {coord}, sparseVoxels length is {sparseData.Length}.");
                denseVoxelData.IsArrayInUse = true;

                return denseVoxelData.DenseVoxelArray;
            }

            // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - Unpacking voxel array for {coord}, sparseVoxels length is {sparseData.Length}");
            denseVoxelData.IsArrayInUse = true;
            denseVoxelData.CurrentCoord = coord;
            denseVoxelData.CurrentChunk = chunk;

            denseVoxelData.DensityRange[0] = new DensityRange(byte.MaxValue, byte.MinValue, McConfigBlob.Value.DensityThreshold);

            var unpackJob = new SparseToDenseVoxelDataJob {
                ConfigBlob = McConfigBlob,
                Voxels = denseVoxelData.DenseVoxelArray,
                SparseVoxels = sparseData,
                DensityRange = denseVoxelData.DensityRange
            };
            var jobHandle = unpackJob.Schedule(McConfigBlob.Value.ChunkDataWidthSize, 1);
            jobHandle.Complete();

            return denseVoxelData.DenseVoxelArray;
        }

        public void PackVoxelArray(int dataSizeKey) {
            if (!_denseVoxelDataDict.TryGetValue(dataSizeKey, out var denseVoxelData)) {
                Debug.LogError(
                    $"MarchingCubes Manager does not have a denseVoxelData Array of this size");
            }
            if (!denseVoxelData.CurrentCoord.HasValue) {
                Debug.LogError(
                    $"PackVoxelArray - Trying to pack but CurrentCoord is null");

                return;
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
            public Vector3Int? CurrentCoord;
            public bool IsArrayInUse;
            public VoxChunk CurrentChunk;
            
            public DenseVoxelData(int voxelCount, Vector3Int? coord = null, VoxChunk currentChunk = null, Allocator allocator = Allocator.Persistent) {
                DenseVoxelArray = new NativeArray<VoxelData>(voxelCount, allocator);
                DensityRange = new NativeArray<DensityRange>(1, allocator);
                CurrentCoord = null;
                IsArrayInUse = false;
                CurrentChunk = null;
            }
            
            public DenseVoxelData() {
                DenseVoxelArray = default;
                DensityRange = default;
                CurrentCoord = null;
                IsArrayInUse = false;
                CurrentChunk = null;
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
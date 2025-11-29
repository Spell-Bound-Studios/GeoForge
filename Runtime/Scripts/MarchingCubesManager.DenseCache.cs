// Copyright 2025 Spellbound Studio Inc.

using Spellbound.Core.Console;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public partial class MarchingCubesManager : MonoBehaviour {
        private NativeArray<VoxelData> _denseVoxelArray;
        private NativeArray<DensityRange> _densityRange;
        private Vector3Int? _currentCoord;
        private bool _isArrayInUse;
        private VoxChunk _currentChunk;

        private void AllocateArrays(int arraySize) {
            _denseVoxelArray = new NativeArray<VoxelData>(arraySize, Allocator.Persistent);
            _densityRange = new NativeArray<DensityRange>(1, Allocator.Persistent);
        }

        public NativeArray<VoxelData> GetOrUnpackVoxelArray(
            Vector3Int coord,
            VoxChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            if (_isArrayInUse) {
                if (_currentCoord.HasValue && _currentCoord.Value != coord) {
                    ConsoleLogger.PrintError(
                        $"GetOrUnpackVoxelArray - Trying to unpack voxel array for {coord} while another unpacked voxel array for {_currentCoord.Value} is in use");

                    return _denseVoxelArray;
                }

                ConsoleLogger.PrintError(
                    $"GetOrUnpackVoxelArray - Trying to unpack voxel array for {coord} but array is in use for the same coord. This is unexpected and bad.");

                return _denseVoxelArray;
            }

            if (_currentCoord.HasValue && _currentCoord.Value == coord && chunk == _currentChunk) {
                // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - No need to unpack. Getting voxel array for {coord}, sparseVoxels length is {sparseData.Length}.");
                _isArrayInUse = true;

                return _denseVoxelArray;
            }

            // ConsoleLogger.PrintToConsole($"GetOrUnpackVoxelArray - Unpacking voxel array for {coord}, sparseVoxels length is {sparseData.Length}");
            _isArrayInUse = true;
            _currentCoord = coord;
            _currentChunk = chunk;

            _densityRange[0] = new DensityRange(byte.MaxValue, byte.MinValue, McConfigBlob.Value.DensityThreshold);

            var unpackJob = new SparseToDenseVoxelDataJob {
                ConfigBlob = McConfigBlob,
                Voxels = _denseVoxelArray,
                SparseVoxels = sparseData,
                DensityRange = _densityRange
            };
            var jobHandle = unpackJob.Schedule(McConfigBlob.Value.ChunkDataWidthSize, 1);
            jobHandle.Complete();

            return _denseVoxelArray;
        }

        public void PackVoxelArray() {
            if (!_currentCoord.HasValue || _currentChunk == null) {
                ConsoleLogger.PrintError(
                    $"PackVoxelArray - Trying to pack but chunk or coord is null");

                return;
            }

            if (!_isArrayInUse) {
                ConsoleLogger.PrintError(
                    $"PackVoxelArray - Trying to pack but _isArrayInUse is false which is unexpected and bad");
            }

            var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);

            var packJob = new DenseToSparseVoxelDataJob {
                Voxels = _denseVoxelArray,
                SparseVoxels = sparseData
            };
            var jobHandle = packJob.Schedule();
            jobHandle.Complete();

            // ConsoleLogger.PrintToConsole($"PackVoxelArray - Packing voxel array for {_currentCoord}, sparseVoxels length is {sparseData.Length}");

            _currentChunk.UpdateVoxelData(sparseData, _densityRange[0]);
            sparseData.Dispose();
        }

        public void ReleaseVoxelArray() => _isArrayInUse = false;

        private void DisposeArrays() {
            if (_denseVoxelArray.IsCreated)
                _denseVoxelArray.Dispose();

            if (_densityRange.IsCreated)
                _densityRange.Dispose();
        }
    }
}
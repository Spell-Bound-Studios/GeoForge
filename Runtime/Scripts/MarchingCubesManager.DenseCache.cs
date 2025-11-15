// Copyright 2025 Spellbound Studio Inc.

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public partial class MarchingCubesManager : MonoBehaviour {
        
        private NativeArray<VoxelData> _denseVoxelArray;
        private Vector3Int? _currentCoord;
        private IVoxelTerrainChunk _currentChunk;

        private void AllocateDenseBuffer(int arraySize) {
            _denseVoxelArray = new NativeArray<VoxelData>(arraySize, Allocator.Persistent);
        }
        
        public NativeArray<VoxelData> GetOrUnpackVoxelArray(
            Vector3Int coord, 
            IVoxelTerrainChunk chunk,
            NativeList<SparseVoxelData> sparseData) {
            
            if (_currentCoord.HasValue) {
                if (_currentCoord.Value != coord) {
                    Debug.LogError("Trying to unpack voxel array while another unpacked voxel array is in use");
                    
                }
                return _denseVoxelArray;
            }
            
            var densityRangeArray = new NativeArray<DensityRange>(1, Allocator.TempJob);
            densityRangeArray[0] = new DensityRange(byte.MaxValue, byte.MinValue, McConfigBlob.Value.DensityThreshold);

            var unpackJob = new SparseToDenseVoxelDataJob {
                ConfigBlob = McConfigBlob,
                Voxels = _denseVoxelArray,
                SparseVoxels = sparseData,
                DensityRange = densityRangeArray
            };
            var jobHandle = unpackJob.Schedule(McConfigBlob.Value.ChunkDataWidthSize, 1);
            jobHandle.Complete();
            
            chunk.SetDensityRange(unpackJob.DensityRange[0]);
            unpackJob.DensityRange.Dispose();
            
            _currentCoord = coord;
            _currentChunk = chunk;

            return _denseVoxelArray;
        }
        
        public void PackVoxelArray() {
            if (!_currentCoord.HasValue) return;
            
            if (_currentChunk != null) {
                var sparseData = new NativeList<SparseVoxelData>(Allocator.TempJob);
                
                var packJob = new DenseToSparseVoxelDataJob {
                    Voxels = _denseVoxelArray,
                    SparseVoxels = sparseData
                };
                var jobHandle = packJob.Schedule();
                jobHandle.Complete();

                _currentChunk.UpdateVoxelData(sparseData);
                sparseData.Dispose();
            }
        }

        public void ReleaseVoxelArray() {
            _currentCoord = null;
            _currentChunk = null;
        }

        private void DisposeDenseBuffer() {
            PackVoxelArray();

            if (_denseVoxelArray.IsCreated) {
                _denseVoxelArray.Dispose();
            }
        }
    }
}
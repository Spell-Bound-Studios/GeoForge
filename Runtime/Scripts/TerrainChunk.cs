using System.Collections.Generic;
using Spellbound.Core;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.MarchingCubes {
    public class TerrainChunk : MonoBehaviour, IVoxelTerrainChunk
    {
        private Vector3Int _chunkCoord;
        private Bounds _bounds;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private Dictionary<int, VoxelEdit> _voxelEdits;
        private OctreeNode _rootNode;
        private Bounds _editBounds;
        private bool _hasEdits;
        private DensityRange _densityRange;
        private MarchingCubesManager _mcManager;
        private IVoxelTerrainChunkManager _chunkManager;
        private bool _isDirty;

        
        public NativeArray<VoxelData> GetVoxelData() => _mcManager.GetOrCreate(_chunkCoord, this, _sparseVoxels);

        public DensityRange GetDensityRange() => _densityRange;

        public void SetDensityRange(DensityRange densityRange) => _densityRange = densityRange;
        public Vector3Int GetChunkCoord() => _chunkCoord;

        public Transform GetChunkTransform() => transform;

        public void InitializeVoxelData(NativeList<SparseVoxelData> voxels) {
            if (_sparseVoxels.IsCreated) {
                Debug.LogError($"_sparseVoxels is already created for this chunkCoord {_chunkCoord}.");
            }

            if (voxels.IsCreated) {
                Debug.LogError($"_sparseVoxels being initialized with a List is already created for this chunkCoord {_chunkCoord}.");
            }
            
            _sparseVoxels = new NativeList<SparseVoxelData>(voxels.Length, Allocator.Persistent);
            _sparseVoxels.AddRange(voxels.AsArray());
        }

        public void UpdateVoxelData(NativeList<SparseVoxelData> voxels) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _isDirty = false;
        }

        public bool IsDirty() => _isDirty;

        public void BroadcastNewLeaf(OctreeNode newLeaf, Vector3 pos, int index) {
            if (_bounds.Contains(pos)) {
                _rootNode?.ValidateTransition(newLeaf, pos, McStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = McStaticHelper.GetNeighborCoord(index, _chunkCoord);

            var neighborChunk = _chunkManager.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;


            neighborChunk.BroadcastNewLeaf(newLeaf, pos, index);
        }

        public void AddToVoxelEdits(List<VoxelEdit> newVoxelEdits) {
            var voxelArray = _mcManager.GetOrCreate(_chunkCoord, this, _sparseVoxels);

            foreach (var voxelEdit in newVoxelEdits) {
                var index = voxelEdit.index;

                if (voxelEdit.density == voxelArray[index].Density &&
                    voxelEdit.MaterialType == voxelArray[index].MaterialType)
                    return;
                
                voxelArray[index] = new VoxelData(voxelEdit.density, voxelEdit.MaterialType);
                McStaticHelper.IndexToInt3(index, out var x, out var y, out var z);
                var localPos = new Vector3Int(x, y, z);

                if (!_hasEdits) {
                    _editBounds = new Bounds(localPos, Vector3.zero);
                    _hasEdits = true;
                    _isDirty = true;
                }
                else
                    _editBounds.Encapsulate(localPos);

                _densityRange.Encapsulate(voxelArray[index].Density);
                ValidateOctreeEdits(_editBounds);
                
            }
        }

        public VoxelData GetVoxelData(int index) {
            var voxels = _mcManager.GetOrCreate(_chunkCoord, this, _sparseVoxels);

            return voxels[index];
        }

        public VoxelData GetVoxelData(Vector3Int position) {
            var localPos = position - _chunkCoord * SpellboundStaticHelper.ChunkSize;
            var index = McStaticHelper.Coord3DToIndex(localPos.x, localPos.y, localPos.z);

            return GetVoxelData(index);
        }

        public bool HasVoxelData() => _sparseVoxels.IsCreated;

        // TODO: Null checking twice is weird.
        public void ValidateOctreeEdits(Bounds bounds) {
            if (_rootNode == null)
                _rootNode = new  OctreeNode(Vector3Int.zero, McStaticHelper.MaxLevelOfDetail, this);

            var worldBounds = new Bounds(bounds.center + _chunkCoord * SpellboundStaticHelper.ChunkSize, bounds.size);
            _rootNode.ValidateOctreeEdits(worldBounds);
        }
   
    }
}


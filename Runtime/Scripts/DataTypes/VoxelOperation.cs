// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Mathematics;

namespace Spellbound.GeoForge {
    public struct VoxelDensityDelta {
        public int Index;
        public short DensityDelta;

        public VoxelDensityDelta(int index, short densityDelta) {
            Index = index;
            DensityDelta = densityDelta;
        }
    }
    
    public struct VoxelEditOperation {
        public byte MaterialIndex;
        public VoxelDensityDelta[] Deltas;
        public uint4 AllowedMaterialsMask;
        
        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas, uint4 allowedMaterialsMask) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = allowedMaterialsMask;
        }

        public VoxelEditOperation(byte materialIndex, List<VoxelDensityDelta> deltas) {
            MaterialIndex = materialIndex;
            Deltas = deltas.ToArray();
            AllowedMaterialsMask = new uint4(uint.MaxValue);
        }

        public bool IsAllowed(byte materialIndex) {
            var lane = (materialIndex / 32) switch {
                0 => AllowedMaterialsMask.x,
                1 => AllowedMaterialsMask.y,
                2 => AllowedMaterialsMask.z,
                _ => AllowedMaterialsMask.w
            };
 
            return (lane & (1u << (materialIndex % 32))) != 0;
        }
    }
}
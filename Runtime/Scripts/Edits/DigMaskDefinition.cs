// Copyright 2026 Spellbound Studio Inc.

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    /// Authoring asset for a tool's dig permission mask. Defines a default policy (diggable or
    /// impervious) applied to every material, plus a list of exceptions that flip specific
    /// materials against that default. Call GetMask() to get the resolved uint4 for use in a
    /// VoxelEditOperation - the mask is built lazily once and cached until the asset is edited.
    /// </summary>
    [CreateAssetMenu(menuName = "Spellbound/GeoForge/Dig Mask", fileName = "DigMaskDefinition")]
    public class DigMaskDefinition : ScriptableObject {
        /// <summary>
        /// How materials NOT explicitly listed in Exceptions are treated.
        /// </summary>
        public enum DefaultPolicy {
            DiggableByDefault,
            ImperviousByDefault
        }

        [SerializeField] private DefaultPolicy defaultPolicy = DefaultPolicy.DiggableByDefault;

        [Tooltip("Materials that deviate from the default policy above. If the default is " +
                 "DiggableByDefault, these are treated as impervious; if the default is " +
                 "ImperviousByDefault, these are treated as diggable.")]
        [SerializeField] private List<byte> exceptions = new();

        private bool _isBuilt;
        private uint4 _cachedMask;

        /// <summary>
        /// Returns the resolved dig mask, building and caching it on first use (or after the
        /// asset's fields have been edited in the inspector).
        /// </summary>
        public uint4 GetMask() {
            if (!_isBuilt)
                Build();

            return _cachedMask;
        }

        private void Build() {
            _cachedMask = defaultPolicy == DefaultPolicy.DiggableByDefault
                    ? new uint4(uint.MaxValue)
                    : uint4.zero;

            foreach (var materialIndex in exceptions) {
                var bit = 1u << (materialIndex % 32);
                var lane = materialIndex / 32;

                // Exceptions always flip AGAINST the default: if the default already allows this
                // material, an exception here means "block it" (clear the bit); if the default
                // already blocks it, an exception here means "allow it" (set the bit).
                var settingBit = defaultPolicy == DefaultPolicy.ImperviousByDefault;

                switch (lane) {
                    case 0:
                        if (settingBit) _cachedMask.x |= bit;
                        else _cachedMask.x &= ~bit;

                        break;
                    case 1:
                        if (settingBit) _cachedMask.y |= bit;
                        else _cachedMask.y &= ~bit;

                        break;
                    case 2:
                        if (settingBit) _cachedMask.z |= bit;
                        else _cachedMask.z &= ~bit;

                        break;
                    default:
                        if (settingBit) _cachedMask.w |= bit;
                        else _cachedMask.w &= ~bit;

                        break;
                }
            }

            _isBuilt = true;
        }

#if UNITY_EDITOR
        // Invalidate the cache whenever the asset is edited in the inspector, so play-mode
        // testing (or a domain reload skip) doesn't hold onto a stale mask.
        private void OnValidate() {
            _isBuilt = false;
        }
#endif
    }
}
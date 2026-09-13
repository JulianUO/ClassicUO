// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections;
using System.IO;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Manages per-map block discovery state for the Fog of War system.
    /// Only active when UltimaLive is running on the server.
    /// Discovery state is persisted to disk so it survives disconnects/relogs.
    /// </summary>
    internal sealed class FogOfWarManager
    {
        // File format: magic (4) + totalBlocks (4) + bitfield bytes
        private const uint FILE_MAGIC = 0x464F4731; // "FOG1"

        private static FogOfWarManager _instance;
        public static FogOfWarManager Instance => _instance ??= new FogOfWarManager();

        // _discovered[mapId] is a BitArray indexed by block number (blockX * heightInBlocks + blockY).
        // null means "no data for this map" (will be allocated on first use).
        private BitArray[] _discovered = new BitArray[sbyte.MaxValue];

        private string _shardFolder;

        private readonly object[] _saveLocks = new object[sbyte.MaxValue];

        private FogOfWarManager()
        {
            for (int i = 0; i < _saveLocks.Length; i++)
                _saveLocks[i] = new object();
        }

        /// <summary>
        /// True when the fog system is active (UltimaLive connected and shard folder set).
        /// </summary>
        public bool IsActive => !string.IsNullOrEmpty(_shardFolder);

        /// <summary>
        /// Called when UltimaLive sends the login-confirmation packet (0x02).
        /// Initialises the state for every valid map and loads persisted data from disk.
        /// </summary>
        public void Initialise(string shardFolder, System.Collections.Generic.List<int> validMaps, int[,] mapBlockSizes)
        {
            _shardFolder = shardFolder;
            _discovered = new BitArray[sbyte.MaxValue];

            foreach (int mapId in validMaps)
            {
                int totalBlocks = mapBlockSizes[mapId, 0] * mapBlockSizes[mapId, 1];
                _discovered[mapId] = new BitArray(totalBlocks, false);
                LoadDiscoveredState(mapId);
            }

            Log.Trace($"[FogOfWar] Initialised for shard '{shardFolder}', {validMaps.Count} maps.");
        }

        /// <summary>Resets state (e.g. on disconnect).</summary>
        public void Reset()
        {
            _shardFolder = null;
            _discovered = new BitArray[sbyte.MaxValue];
        }

        /// <summary>Returns true if the block has been received from the server at least once.</summary>
        public bool IsBlockDiscovered(int mapId, int block)
        {
            if (!IsActive || (uint)mapId >= (uint)_discovered.Length)
                return false;

            var arr = _discovered[mapId];
            if (arr == null || (uint)block >= (uint)arr.Length)
                return false;

            return arr[block];
        }

        /// <summary>
        /// Marks a block as discovered and persists the updated state asynchronously.
        /// Called from UltimaLive packet handlers (terrain 0x40 and statics 0x00).
        /// </summary>
        public void MarkBlockDiscovered(int mapId, int block)
        {
            if (!IsActive || (uint)mapId >= (uint)_discovered.Length)
                return;

            var arr = _discovered[mapId];
            if (arr == null || (uint)block >= (uint)arr.Length)
                return;

            if (!arr[block])
            {
                arr[block] = true;
                // Persist async — fire-and-forget so it doesn't stall the game loop.
                System.Threading.Tasks.Task.Run(() => SaveDiscoveredState(mapId));
            }
        }

        /// <summary>
        /// Resets the fog of war discovered blocks for a map.
        /// </summary>
        public void ResetFog(int mapId)
        {
            if ((uint)mapId >= (uint)_discovered.Length) return;

            var arr = _discovered[mapId];
            if (arr != null)
            {
                arr.SetAll(false);
                if (IsActive)
                {
                    System.Threading.Tasks.Task.Run(() => SaveDiscoveredState(mapId));
                }
            }
        }


        // ── Persistence ─────────────────────────────────────────────────────────

        private string GetFilePath(int mapId)
            => Path.Combine(_shardFolder, $"fog_map{mapId}.bin");

        private void SaveDiscoveredState(int mapId)
        {
            if (!IsActive || (uint)mapId >= (uint)_saveLocks.Length) return;

            lock (_saveLocks[mapId])
            {
                try
                {
                    var arr = _discovered[mapId];
                    if (arr == null) return;

                    int totalBlocks = arr.Length;
                    byte[] bits = new byte[(totalBlocks + 7) / 8];
                    arr.CopyTo(bits, 0);

                    string path = GetFilePath(mapId);
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    using var bw = new BinaryWriter(fs);
                    bw.Write(FILE_MAGIC);
                    bw.Write(totalBlocks);
                    bw.Write(bits);
                }
                catch (Exception ex)
                {
                    Log.Warn($"[FogOfWar] Failed to save fog state for map {mapId}: {ex.Message}");
                }
            }
        }

        private void LoadDiscoveredState(int mapId)
        {
            if (!IsActive) return;

            string path = GetFilePath(mapId);
            if (!File.Exists(path)) return;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                uint magic = br.ReadUInt32();
                if (magic != FILE_MAGIC)
                {
                    Log.Warn($"[FogOfWar] Invalid fog file for map {mapId} (bad magic).");
                    return;
                }

                int savedBlocks = br.ReadInt32();
                byte[] bits = br.ReadBytes((savedBlocks + 7) / 8);

                var arr = _discovered[mapId];
                if (arr == null) return;

                // Only load bits that fit the current map size.
                int blocksToLoad = Math.Min(savedBlocks, arr.Length);
                var loaded = new BitArray(bits);
                for (int i = 0; i < blocksToLoad; i++)
                    arr[i] = loaded[i];

                Log.Trace($"[FogOfWar] Loaded fog state for map {mapId} ({savedBlocks} blocks).");
            }
            catch (Exception ex)
            {
                Log.Warn($"[FogOfWar] Failed to load fog state for map {mapId}: {ex.Message}");
            }
        }
    }
}

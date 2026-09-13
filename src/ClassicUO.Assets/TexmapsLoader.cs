// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.IO;
using ClassicUO.Utility;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClassicUO.Assets
{
    public sealed class TexmapsLoader : UOFileLoader
    {
        private UOFile _file;

        public const int MAX_LAND_TEXTURES_DATA_INDEX_COUNT = 0x4000;

        public TexmapsLoader(UOFileManager fileManager) : base(fileManager) { }

        public UOFile File => _file;

        public override void Load()
        {
            string path = FileManager.GetUOFilePath("texmaps.mul");
            string pathidx = FileManager.GetUOFilePath("texidx.mul");

            FileSystemHelper.EnsureFileExists(path);
            FileSystemHelper.EnsureFileExists(pathidx);

            _file = new UOFileMul(path, pathidx);
            _file.FillEntries();
            string pathdef = FileManager.GetUOFilePath("TexTerr.def");

            if (System.IO.File.Exists(pathdef))
            {
                using (DefReader defReader = new DefReader(pathdef))
                {
                    while (defReader.Next())
                    {
                        int index = defReader.ReadInt();

                        if (index < 0 || index >= _file.Entries.Length)
                        {
                            continue;
                        }

                        int[] group = defReader.ReadGroup();

                        if (group == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < group.Length; i++)
                        {
                            int checkindex = group[i];

                            if (checkindex < 0 || checkindex >= _file.Entries.Length)
                            {
                                continue;
                            }

                            _file.Entries[index] = _file.Entries[checkindex];
                        }
                    }
                }
            }
        }

        public TexmapInfo GetTexmap(uint idx)
        {
            ref UOFileIndex entry = ref _file.GetValidRefEntry((int)idx);

            if (entry.Length <= 0)
            {
                return default;
            }

            _file.Seek(entry.Offset, SeekOrigin.Begin);
            var size = entry.Length == 0x2000 ? 64 : 128;
            var data = new uint[size * size];

            for (int i = 0; i < size; ++i)
            {
                int pos = i * size;

                for (int j = 0; j < size; ++j)
                {
                    ushort c16 = _file.ReadUInt16();
                    if (c16 == 0x7C1F || c16 == 0xF81F)
                    {
                        data[pos + j] = 0xFF_00_00_00;
                    }
                    else
                    {
                        data[pos + j] = HuesHelper.Color16To32(c16) | 0xFF_00_00_00;
                    }
                }
            }

            return new TexmapInfo()
            {
                Pixels = data,
                Width = size,
                Height = size
            };
        }

        public bool TryGetTexmapSize(uint idx, out int width, out int height)
        {
            ref UOFileIndex entry = ref _file.GetValidRefEntry((int)idx);

            if (entry.Length <= 0)
            {
                width = 0;
                height = 0;
                return false;
            }

            width = height = entry.Length == 0x2000 ? 64 : 128;
            return true;
        }
    }

    public ref struct TexmapInfo
    {
        public Span<uint> Pixels;
        public int Width;
        public int Height;
    }
}

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Maploader.World
{
    public class BlockCoord
    {
        public BlockCoord(BlockData block, int x, int y, int z)
        {
            this.Block = block;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.XZ = x * 256 + z;
        }

        public BlockData Block { get; set; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int XZ { get; }

        public override string ToString()
        {
            return $"{this.X} {this.Y} {this.Z} {this.Block}";
        }
    }

    public class BlockData
    {
        public BlockData(string id, Dictionary<string, object> data)
        {
            this.Id = id ?? throw new ArgumentNullException(nameof(id));
            this.Data = data;
            this.Version = 0;
        }

        [NotNull]
        public string Id { get; set; }

        public Dictionary<string, object> Data { get; set; }

        public int Version { get; set; }

        public override string ToString()
        {
            return string.Format($"{this.Id}:{this.Data} ({this.Version})");
        }

        public void Reset()
        {
            this.Id = "minecraft:air";
            this.Data.Clear();
            this.Version = 0;
        }
    }
}
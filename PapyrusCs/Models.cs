using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PapyrusCs
{
    public enum Dimension : int
    {
        Unknown = -1,
        Overworld = 0,
        Nether = 1,
        End = 2
    }

    public struct ChunkKey
    {
        public ulong Key;
        public int X;
        public int Z;
    }

    [DebuggerDisplay("{CenterX} {CenterY} {CenterZ}")]
    public class Village
    {
        public int X0 { get; set; }
        public int X1 { get; set; }
        public int Y0 { get; set; }
        public int Y1 { get; set; }
        public int Z0 { get; set; }
        public int Z1 { get; set; }

        public int XSize => Math.Abs(this.X0 - this.X1);
        public int YSize => Math.Abs(this.Y0 - this.Y1);
        public int ZSize => Math.Abs(this.Z0 - this.Z1);

        public int CenterX => this.X0 + (this.XSize / 2);
        public int CenterZ => this.Z0 + (this.ZSize / 2);
        public int CenterY => this.Y0 + (this.YSize / 2);

        public readonly List<Villager> Villagers = new List<Villager>();
    }

    [DebuggerDisplay("{Name} {X} {Y} {Z}")]
    public class Villager
    {
        public long VillagerID { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    [DebuggerDisplay("{Dimension} {X} {Y} {Z}")]
    public class Portal
    {
        public Dimension Dimension { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    [DebuggerDisplay("{Name} {X} {Y} {Z}")]
    public class Player
    {
        public string UniqueID { get; set; }
        public Dimension Dimension { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}

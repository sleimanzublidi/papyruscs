using System;
using System.Diagnostics;
using Maploader.World;

namespace PapyrusCs.Database
{
    [DebuggerDisplay("{X} {Z} {SubChunkId} [{KeyType}]")]
    public struct LevelDbWorldKey2
    {
        public byte[] Key { get; }

        public bool Equals(LevelDbWorldKey2 other)
        {
            return (this.SubChunkId == 0xFF || other.SubChunkId == 0xFF) //  I will go to the nether for this
                ? this.KeyType == other.KeyType && this.X == other.X && this.Z == other.Z //  I will go to the nether for this
                : this.KeyType == other.KeyType && this.X == other.X && this.Z == other.Z && this.SubChunkId == other.SubChunkId;
        }

        public override bool Equals(object obj)
        {
            return obj is LevelDbWorldKey2 other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = this.KeyType.GetHashCode();
                hashCode = (hashCode * 397) ^ this.X;
                hashCode = (hashCode * 397) ^ this.Z;
                //hashCode = (hashCode * 397) ^ SubChunkId.GetHashCode(); //  I will go to the nether for this
                return hashCode;
            }
        }

        public static bool operator ==(LevelDbWorldKey2 left, LevelDbWorldKey2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LevelDbWorldKey2 left, LevelDbWorldKey2 right)
        {
            return !left.Equals(right);
        }

        public LevelDbWorldKey2(byte[] key)
        {
            this.Key = key;
            this.X = (key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            this.Z = (key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            if (key.Length == 10)
            {
                this.KeyType = key[8];
                this.SubChunkId = key[9];
            }
            else if (key.Length == 14)
            {
                this.KeyType = key[12];
                this.SubChunkId = key[13];
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public LevelDbWorldKey2(int x, int z)
        {
            this.Key = new byte[] { 0 };
            this.X = x;
            this.Z = z;
            this.KeyType = 47;
            this.SubChunkId = 0xFF;
        }

        public byte KeyType { get; }
        public Int32 X { get; }
        public Int32 Z { get; }
        public UInt64 XZ => (((UInt64)this.X) << 32) | (UInt32)this.Z;
        public byte SubChunkId { get; set; }

        public UInt64 GetXZGroup(int chunkPerDimension)
        {
            UInt64 result = ((UInt64)CoordHelpers.GetGroupedCoordinate(this.X, chunkPerDimension)) << 32;
            result |= (UInt32)CoordHelpers.GetGroupedCoordinate(this.Z, chunkPerDimension);
            return result;
        }
    }
}
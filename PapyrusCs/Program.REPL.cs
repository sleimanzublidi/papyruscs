using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Maploader.Core;
using Maploader.World;
using PapyrusCs.Database;
using SixLabors.Primitives;

namespace PapyrusCs
{
    public partial class Program
    {
        private static void RunRepl(ReplOptions options)
        {
            var world = new World
            {
                ChunkPool = new ChunkPool()
            };

            try
            {
                Console.WriteLine("Opening world...");
                world.Open(options.MinecraftWorld);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open world at '{options.MinecraftWorld}'!. Did you specify the .../db folder?");
                Console.WriteLine("The reason was:");
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine();
            Console.WriteLine("REPL Ready");

            while (true)
            {
                Console.WriteLine();
                Console.Write("> ");

                var command = Console.ReadLine().Trim() + " ";

                if (command.StartsWith("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (command.StartsWith("find", StringComparison.OrdinalIgnoreCase) && command.Length > 4)
                {
                    Find(world, command.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }

                if (command.StartsWith("dump", StringComparison.OrdinalIgnoreCase))
                {
                    Dump(world, command.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        private static void Find(World world, string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return;
            }

            if (!FindById(world, tokens[0], tokens))
            {
                Console.WriteLine();
                Console.WriteLine("usage: find {what} {dimension}");
            }
        }

        private static bool FindById(World world, string blockId, string[] tokens)
        {
            var runTimeId = world.Table.RunTimeIds.FirstOrDefault(r => r.name == blockId.ToLower() || r.name == "minecraft:" + blockId.ToLower());
            if (runTimeId == null)
            {
                Console.WriteLine($"'{blockId}' is not a valid block id.");
                return false;
            }
            blockId = runTimeId.name;

            var dimension = GetDimension(tokens);
            if (dimension == Dimension.Unknown)
            {
                return false;
            }

            var chunkKeys = GetChunkKeys(world, dimension);
            if (!chunkKeys.Any())
            {
                return false;
            }

            int centerX = 0;
            int centerZ = 0;

            if (tokens.Any(t => t.Contains(',')))
            {
                var values = tokens.First(t => t.Contains(',')).Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length == 2)
                {
                    int.TryParse(values[0], out centerX);
                    int.TryParse(values[1], out centerZ);
                }
            }

            var time = Stopwatch.StartNew();
            int i = 0;
            int nextout = 30;

            var sortedChunks = chunkKeys
                .OrderBy(chunk => PointDistance(chunk.X * 16, chunk.Z * 16, centerX, centerZ))
                .ToList();

            foreach (var key in sortedChunks)
            {
                i++;

                var chunkData = world.GetChunkData(key.X, key.Z);
                var chunk = world.GetChunk(chunkData.X, chunkData.Z, chunkData);

                var blocks = chunk.Blocks
                    .Where(x => x.Value.Block.Id == blockId)
                    .OrderBy(b => PointDistance(b.Value.X, b.Value.Z, centerX, centerZ));

                foreach (var b in blocks)
                {
                    Console.WriteLine($"{b.Value.Block.Id} {b.Value.X + chunk.X * 16} {b.Value.Z + chunk.Z * 16} {b.Value.Y} ");
                    i++;
                }

                if (i > nextout)
                {
                    nextout += 100;
                    Console.WriteLine($"{time.Elapsed} Press C to continue.");
                    if (Console.ReadKey(true).KeyChar == 'c' ||
                        Console.ReadKey(true).KeyChar == 'C')
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return true;
        }

        private static bool Dump(World world, string[] tokens)
        {
            var dimension = GetDimension(tokens);
            if (dimension == Dimension.Unknown)
            {
                return false;
            }

            var chunkKeys = GetChunkKeys(world, dimension);
            if (!chunkKeys.Any())
            {
                return false;
            }

            var time = Stopwatch.StartNew();

            foreach (var key in chunkKeys)
            {
                var chunkData = world.GetChunkData(key.X, key.Z);
                var chunk = world.GetChunk(chunkData.X, chunkData.Z, chunkData);
                var blocks = chunk.Blocks;

                foreach (var b in blocks)
                {
                    Console.WriteLine($"Chunk {chunk.X} {chunk.Z} -- Block {b.Value.X + chunk.X * 16} {b.Value.Z + chunk.Z * 16} {b.Value.Y} {b.Value.Block.Id}");
                }
            }

            Console.WriteLine($"{time.Elapsed}");

            return true;
        }

        private static Dimension GetDimension(string[] tokens)
        {
            var token = tokens.FirstOrDefault(t => t.StartsWith("dim")) ?? "dim0";
            if (token.StartsWith("dim0", StringComparison.OrdinalIgnoreCase))
            {
                return Dimension.Overworld;
            }
            else if (token.StartsWith("dim1", StringComparison.OrdinalIgnoreCase))
            {
                return Dimension.Nether;
            }
            else if (token.StartsWith("dim2", StringComparison.OrdinalIgnoreCase))
            {
                return Dimension.End;
            }

            Console.WriteLine($"'{token}' is not a valid dimension.");
            return Dimension.Unknown;
        }

        private static List<ChunkKey> GetChunkKeys(World world, Dimension dimension)
        {
            var chuckKeys = world.GetDimension((int)dimension)
                .Select(x => new LevelDbWorldKey2(x)).GroupBy(x => x.XZ).Select(x => x.Key)
                .Select(key => new ChunkKey { Key = key, X = (int)((ulong)key >> 32), Z = (int)((ulong)key & 0xffffffff) })
                .ToList();

            Console.WriteLine($"{chuckKeys.Count()} chunks found in the {dimension}.");

            return chuckKeys;
        }

        private static double PointDistance(double x, double z, double centerX, double centerZ)
        {
            return Math.Sqrt(((x - centerX) * (x - centerX)) + ((z - centerZ) * (z - centerZ)));
        }
    }

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
}

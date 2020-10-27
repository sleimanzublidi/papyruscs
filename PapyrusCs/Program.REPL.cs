using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Maploader.Core;
using Maploader.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PapyrusCs.Database;

namespace PapyrusCs
{
    public partial class Program
    {
        private static int RunRepl(ReplOptions options)
        {
            var world = new World
            {
                ChunkPool = new ChunkPool()
            };

            try
            {
                Console.WriteLine("Opening world...");
                Console.WriteLine(options.MinecraftWorld);
                world.Open(options.MinecraftWorld);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open world at '{options.MinecraftWorld}'!. Did you specify the .../db folder?");
                Console.WriteLine("The reason was:");
                Console.WriteLine(ex.Message);
                return -1;
            }

            Console.WriteLine();
            Console.WriteLine("REPL Ready");

            while (true)
            {
                Console.WriteLine();
                Console.Write("> ");

                var command = Console.ReadLine().Trim() + " ";

                if (command.StartsWith("exit", StringComparison.OrdinalIgnoreCase) ||
                    command.StartsWith("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                else if(command.StartsWith("find", StringComparison.OrdinalIgnoreCase) && command.Length > 4)
                {
                    Find(world, command.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    //FindPortals(world, "".Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            return 0;
        }

        private static void Find(World world, string[] tokens)
        {
            if (tokens.Length > 0)
            {
                if (tokens[0].StartsWith("village", StringComparison.OrdinalIgnoreCase))
                {
                    FindVillages(world, tokens);
                }
                else if (tokens[0].StartsWith("portal", StringComparison.OrdinalIgnoreCase))
                {
                    FindPortals(world, tokens);
                }
                else
                {
                    FindBlockById(world, tokens[0], tokens);
                }

                return;
            }

            Console.WriteLine();
            Console.WriteLine("usage: find {what} {where:dimension} {where:position}");
            Console.WriteLine();
            Console.WriteLine("   ex: find diamond overworld 0,0");
        }

        private static List<Village> FindVillages(World world, string[] tokens)
        {
            var villageBytes = "VILLAGE".Select(x => (byte)x).ToArray();
            var villages = new List<Village>();
            {
                var village = new Village();
                var villager = new Villager();

                foreach (var key in world.Keys)
                {
                    if (key.Length > 8 && key[8] == (int)KeyType.SubChunkPrefix)
                    {
                        continue;
                    }

                    if (key.Locate(villageBytes).Length == 0)
                        continue;

                    var data = world.GetData(key);
                    if (data != null && data.Length > 0)
                    {
                        var ms = new MemoryStream(data);
                        var nbt = new fNbt.NbtReader(ms, false);

                        var tags = ReadTags(nbt);
                        if (tags.Contains("X0"))
                        {
                            village = new Village();
                            villages.Add(village);

                            foreach (var tag in tags)
                            {
                                switch (tag.Name)
                                {
                                    case "X0":
                                        village.X0 = Convert.ToInt32(tag.Value);
                                        break;
                                    case "X1":
                                        village.X1 = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Y0":
                                        village.Y0 = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Y1":
                                        village.Y1 = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Z0":
                                        village.Z0 = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Z1":
                                        village.Z1 = Convert.ToInt32(tag.Value);
                                        break;
                                    default:
                                        break;
                                }

                            }
                        }

                        if (tags.Contains("VillagerID"))
                        {
                            foreach (var tag in tags)
                            {
                                switch (tag.Name)
                                {
                                    case "VillagerID":
                                        villager = new Villager
                                        {
                                            VillagerID = Convert.ToInt64(tag.Value)
                                        };
                                        village.Villagers.Add(villager);
                                        break;
                                    case "Name":
                                        villager.Name = Convert.ToString(tag.Value);
                                        break;
                                    case "X":
                                        villager.X = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Y":
                                        villager.Y = Convert.ToInt32(tag.Value);
                                        break;
                                    case "Z":
                                        villager.Z = Convert.ToInt32(tag.Value);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

                        ms.Dispose();
                    }
                }
            }

            ParseCenterPoint(tokens, out var center, out var centerX, out var centerZ);

            var sortedVillages = villages
                .OrderBy(village => PointDistance(village.CenterX, village.CenterZ, centerX, centerZ))
                .ToList();

            foreach (var village in sortedVillages)
            {
                Console.WriteLine();
                Console.WriteLine($"Village Center: {village.CenterX} {village.CenterZ}");
                if (center)
                {
                    Console.WriteLine($"      Distance: {PointDistance(village.CenterX, village.CenterZ, centerX, centerZ)}");
                }
                Console.WriteLine($"     Villagers: {village.Villagers.Count}");
                foreach (var villager in village.Villagers)
                {
                    Console.WriteLine($"                {villager.Name} {villager.X} {villager.Z}");
                }
            }

            if (tokens.Contains("json"))
            {
                const string jsonTemplate = "var villageData = { villages: [] }";
                var portalData = JObject.Parse(jsonTemplate.Substring(jsonTemplate.IndexOf('=') + 1).Trim().TrimEnd(';'));

                foreach (var village in sortedVillages)
                {
                    ((JArray)portalData["villages"]).Add(JObject.FromObject(new
                    {
                        name = "Village",
                        icon = "\uf4d9",
                        dimensionId = 0,
                        position = new[] { village.CenterX, village.CenterY, village.CenterZ},
                        color = "Black",
                        visible = true
                    }));
                }

                var json = JsonConvert.SerializeObject(portalData, Formatting.Indented);
                File.WriteAllText("villageData.js",
                    jsonTemplate.Replace("{ villages: [] }", json),
                    Encoding.UTF8);
            }

            return villages;
        }

        private static List<Portal> FindPortals(World world, string[] tokens)
        {
            var portalBytes = "portal".Select(x => (byte)x).ToArray();

            var portals = new List<Portal>();
            {
                var portal = new Portal();

                foreach (var key in world.Keys)
                {
                    if (key.Length > 8 && key[8] == (int)KeyType.SubChunkPrefix)
                    {
                        continue;
                    }

                    if (key.Locate(portalBytes).Length == 0)
                        continue;

                    var data = world.GetData(key);
                    if (data != null && data.Length > 0)
                    {
                        var ms = new MemoryStream(data);
                        var nbt = new fNbt.NbtReader(ms, false);

                        var tags = ReadTags(nbt);
                        if (tags.Contains("TpX"))
                        {
                            foreach (var tag in tags)
                            {
                                switch (tag.Name)
                                {
                                    case "DimId":
                                        portal = new Portal();
                                        portals.Add(portal);

                                        portal.Dimension = (Dimension)Convert.ToInt32(tag.Value);
                                        break;

                                    case "TpX":
                                        portal.X = Convert.ToInt32(tag.Value);
                                        break;

                                    case "TpY":
                                        portal.Y = Convert.ToInt32(tag.Value);
                                        break;

                                    case "TpZ":
                                        portal.Z = Convert.ToInt32(tag.Value);
                                        break;

                                    default:
                                        break;
                                }

                            }
                        }

                        ms.Dispose();
                    }
                }
            }

            var dimension = GetDimension(tokens);
            if (dimension != Dimension.Unknown)
            {
                portals = portals.Where(p => p.Dimension == dimension)
                                 .ToList();
            }

            ParseCenterPoint(tokens, out var center, out var centerX, out var centerZ);

            var sortedPortals = portals
                .OrderBy(portal => portal.Dimension)
                .ThenBy(portal =>
                     portal.Dimension == Dimension.Nether ?
                     PointDistance(portal.X * 16, portal.Z * 16, centerX, centerZ) :
                     PointDistance(portal.X, portal.Z, centerX, centerZ)
                    )
                .ToList();

            foreach (var portal in sortedPortals)
            {
                Console.WriteLine();
                Console.Write($"{portal.Dimension} {portal.X} {portal.Y} {portal.Z}");
                if (portal.Dimension == Dimension.Nether)
                {
                    Console.WriteLine($" ({portal.X * 16} {portal.Y * 16} {portal.Z * 16})");
                }
                else
                {
                    Console.WriteLine($" ({portal.X / 16} {portal.Y / 16} {portal.Z / 16})");
                }
            }

            if (tokens.Contains("json"))
            {
                const string jsonTemplate = "var portalData = { portals: [] }";
                var portalData = JObject.Parse(jsonTemplate.Substring(jsonTemplate.IndexOf('=') + 1).Trim().TrimEnd(';'));

                foreach (var portal in sortedPortals)
                {
                    ((JArray)portalData["portals"]).Add(JObject.FromObject(new
                    {
                        name = "Portal",
                        icon = "\uf52a",
                        dimensionId = (int)portal.Dimension,
                        position = new[] { portal.X, portal.Y, portal.Z },
                        color = "Black",
                        visible = true
                    }));
                }

                var json = JsonConvert.SerializeObject(portalData, Formatting.Indented);
                File.WriteAllText("portalData.js",
                    jsonTemplate.Replace("{ portals: [] }", json),
                    Encoding.UTF8);
            }

            return portals;
        }

        private static void FindBlockById(World world, string blockId, string[] tokens)
        {
            var runTimeId = world.Table.RunTimeIds.FirstOrDefault(r => r.name == blockId.ToLower() || r.name == "minecraft:" + blockId.ToLower());
            if (runTimeId == null)
            {
                Console.WriteLine($"'{blockId}' is not a valid block id.");
                return;
            }
            blockId = runTimeId.name;

            var dimension = GetDimension(tokens);
            if (dimension == Dimension.Unknown)
            {
                return;
            }

            ParseCenterPoint(tokens, out var center, out var centerX, out var centerZ);

            var chunkKeys = world.GetDimension((int)dimension)
                .Select(x => new LevelDbWorldKey2(x)).GroupBy(x => x.XZ).Select(x => x.Key)
                .Select(key => new ChunkKey { Key = key, X = (int)((ulong)key >> 32), Z = (int)((ulong)key & 0xffffffff) })
                .ToList();

            Console.WriteLine($"{chunkKeys.Count()} chunks found in the {dimension}.");
            if (!chunkKeys.Any())
            {
                return;
            }

            int i = 0;
            int nextOut = 100;

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

                if (i > nextOut)
                {
                    nextOut += 100;
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
        }

        private static Dimension GetDimension(string[] tokens)
        {
            if (tokens.Any(t => t.Equals("0") ||
                tokens.Any(t => t.StartsWith("dim0", StringComparison.OrdinalIgnoreCase))) ||
                tokens.Any(t => t.Equals("overworld", StringComparison.OrdinalIgnoreCase)))
            {
                return Dimension.Overworld;
            }
            else if (tokens.Any(t => t.Equals("1") ||
                     tokens.Any(t => t.StartsWith("dim1", StringComparison.OrdinalIgnoreCase))) ||
                     tokens.Any(t => t.Equals("nether", StringComparison.OrdinalIgnoreCase)))
            {
                return Dimension.Nether;
            }
            else if (tokens.Any(t => t.Equals("2") ||
                     tokens.Any(t => t.StartsWith("dim2", StringComparison.OrdinalIgnoreCase))) ||
                     tokens.Any(t => t.Equals("end", StringComparison.OrdinalIgnoreCase)))
            {
                return Dimension.End;
            }

            return Dimension.Unknown;
        }

        private static void ParseCenterPoint(string[] tokens, out bool defined, out int centerX, out int centerZ)
        {
            defined = false;
            centerX = 0;
            centerZ = 0;
            if (tokens.Any(t => t.Contains(',')))
            {
                var values = tokens.First(t => t.Contains(',')).Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length == 2)
                {
                    defined = int.TryParse(values[0], out centerX) &&
                              int.TryParse(values[1], out centerZ);

                    if (!defined)
                    {
                        centerX = 0;
                        centerZ = 0;
                    }
                    else if (GetDimension(tokens) == Dimension.Nether)
                    {
                        centerX = centerX * 16;
                        centerZ = centerZ * 16;
                    }
                }
            }
        }

        private static TagList ReadTags(fNbt.NbtReader nbt)
        {
            var tags = new TagList();

            var result = nbt.ReadToFollowing();
            if (result && nbt.IsCompound)
            {
                result = nbt.ReadToFollowing();
                while (result)
                {
                    if (nbt.HasName)
                    {
                        var space = new string(' ', nbt.Depth * 2);
                        var tagName = nbt.TagName;

                        if (nbt.HasValue)
                        {
                            var tagValue = nbt.ReadValue();
                            tags.Add(tagName, tagValue);
                            //Debug.WriteLine($"{space}{tagName}:{tagValue}");
                        }
                        else if (nbt.HasLength)
                        {
                            //Debug.WriteLine($"{space}{tagName} ({nbt.TagLength})");
                        }
                        else
                        {
                            //Debug.WriteLine($"{space}{tagName}");
                        }
                    }
                    result = nbt.ReadToFollowing();
                }
            }

            return tags;
        }

        private static int PointDistance(double x, double z, double centerX, double centerZ)
        {
            return (int)Math.Sqrt(((x - centerX) * (x - centerX)) + ((z - centerZ) * (z - centerZ)));
        }

        private static int Angle(double x, double z, double centerX, double centerZ)
        {
            var radian = Math.Atan2((z - centerZ), (x - centerX));
            var angle = (radian * (180 / Math.PI) + 360) % 360;
            return (int)angle;
        }
    }

    public enum KeyType : int
    {
        // Biomes and elevation
        Data2D = 45,
        // Terrain for a 16×16×16 subchunk
        SubChunkPrefix = 47,
        // Block entity data
        BlockEntity = 49,
        // Entity data
        Entity = 50,
        // Bounding boxes for structure spawns stored in binary format
        HardCodedSpawnAreas = 57
    }

    public struct ChunkKey
    {
        public ulong Key;
        public int X;
        public int Z;
    }
}

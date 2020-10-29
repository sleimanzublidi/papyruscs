using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using fNbt.Tags;
using Maploader.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PapyrusCs.Database;

namespace PapyrusCs
{
    public partial class Program
    {
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
                else if (tokens[0].StartsWith("map", StringComparison.OrdinalIgnoreCase))
                {
                    FindMaps(world, tokens);
                }
                else if (tokens[0].StartsWith("players", StringComparison.OrdinalIgnoreCase))
                {
                    FindPlayers(world, tokens);
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
            //var villageIndicator = Encoding.UTF8.GetBytes("VILLAGE");
            //var villageKey = world.Keys
            //    .Where(key => key.Take(villageIndicator.Length).SequenceEqual(villageIndicator))
            //    .Select(k => Encoding.UTF8.GetString(k))
            //    .ToList();

            //const string village_dweller_regex = "VILLAGE_[0-9a-f\\-]+_DWELLERS";
            //const string village_info_regex = "VILLAGE_[0-9a-f\\-]+_INFO";
            //const string village_player_regex = "VILLAGE_[0-9a-f\\-]+_PLAYERS";
            //const string village_poi_regex = "VILLAGE_[0-9a-f\\-]+_POI";

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

                        var tags = nbt.ReadTags();
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
                        position = new[] { village.CenterX, village.CenterY, village.CenterZ },
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
            var portalIndicator = Encoding.UTF8.GetBytes("portals");
            var portalsKey = world.Keys
                .Where(key => key.Take(portalIndicator.Length).SequenceEqual(portalIndicator))
                .FirstOrDefault();

            var portals = new List<Portal>();
            if (portalsKey == null)
            {
                return portals;
            }

            var data = world.GetData(portalsKey);
            using (var memoryStream = new MemoryStream(data))
            {
                // portals
                // --data
                // ----PortalRecords

                var nbtReader = new fNbt.NbtReader(memoryStream, false);
                if (nbtReader.ReadToFollowing("PortalRecords"))
                {
                    var tagList = nbtReader.ReadAsTag() as NbtList;
                    foreach (var tag in tagList)
                    {
                        var portal = new Portal
                        {
                            Dimension = (Dimension)tag["DimId"].IntValue,
                            X = tag["TpX"].IntValue,
                            Y = tag["TpY"].IntValue,
                            Z = tag["TpZ"].IntValue
                        };

                        portals.Add(portal);
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

        private static List<Map> FindMaps(World world, string[] tokens)
        {
            var mapIndicator = Encoding.UTF8.GetBytes("map_");
            var mapKeys = world.Keys
                .Where(key => key.Take(mapIndicator.Length).SequenceEqual(mapIndicator))
                .ToList();

            var maps = new List<Map>();
            if (mapKeys.Count == 0)
            {
                return maps;
            }

            foreach (var key in mapKeys)
            {
                //Console.WriteLine(Encoding.UTF8.GetString(key));

                var data = world.GetData(key);
                using var memoryStream = new MemoryStream(data);
                var nbtReader = new fNbt.NbtReader(memoryStream, false);
                var tags = nbtReader.ReadAsTag();

                var map = new Map
                {
                    MapId = tags["mapId"].LongValue,
                    ParentMapId = tags["parentMapId"].LongValue,
                    Dimension = (Dimension)tags["dimension"].IntValue,
                    CenterX = tags["xCenter"].IntValue,
                    CenterZ = tags["zCenter"].IntValue,
                    IsLocked = tags["mapLocked"].IntValue == 1,
                    IsFullyExplored = tags["fullyExplored"].IntValue == 1,
                    UnlimitedTracking = tags["unlimitedTracking"].IntValue == 1,
                    Scale = tags["scale"].IntValue,
                    Height = tags["height"].IntValue,
                    Width = tags["width"].IntValue,
                };

                var colorBytes = tags["colors"].ByteArrayValue;
                map.Colors = new Color[colorBytes.Length / 4];
                for (int i = 0; i < colorBytes.Length; i += 4)
                {
                    byte r = colorBytes[i + 0];
                    byte g = colorBytes[i + 1];
                    byte b = colorBytes[i + 2];
                    byte a = colorBytes[i + 3];

                    map.Colors[i / 4] = Color.FromArgb(a, r, g, b);
                    var hex = r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2");
                }

                maps.Add(map);
            }

            var nMaps = maps.Where(m => !m.IsEmpty)
                            .OrderBy(m => m.IsFullyExplored)
                            .ThenByDescending(m => m.Scale)
                            .ToList();

            Console.WriteLine($"{nMaps.Count} maps found.");

            if (tokens.Contains("png"))
            {
                foreach (var map in nMaps)
                {
                    if (map.Colors[0] == Color.Black)
                        continue;

                    const int tileSize = 16;
                    using var bitmap = new Bitmap(map.Width * tileSize, map.Height * tileSize);
                    using (var graphic = Graphics.FromImage(bitmap))
                    {
                        int c = 0;
                        for (int y = 0; y < map.Height; y++)
                            for (int x = 0; x < map.Width; x++)
                            {
                                var brush = new SolidBrush(map.Colors[c]);
                                var rect = new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize);
                                graphic.FillRectangle(brush, rect);
                                c++;
                            }
                    }
                    bitmap.Save(filename: $"map_{map.MapId}.Png", System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            if (tokens.Contains("json"))
            {
                var json = JsonConvert.SerializeObject(nMaps, Formatting.Indented);
                File.WriteAllText("maps.json", json, Encoding.UTF8);
            }

            return maps;
        }

        private static List<Player> FindPlayers(World world, string[] tokens)
        {
            var serverPlayerKeyIndicator = Encoding.UTF8.GetBytes("player_server");
            var playerKeys = world.Keys
                .Where(key => key.Take(serverPlayerKeyIndicator.Length).SequenceEqual(serverPlayerKeyIndicator))
                .ToList();

            var localPlayerKeyIndicator = Encoding.UTF8.GetBytes("~local_player");
            var localPlayerKey = world.Keys
                .Where(key => key.Take(localPlayerKeyIndicator.Length).SequenceEqual(localPlayerKeyIndicator))
                .FirstOrDefault();

            if (localPlayerKey != null)
            {
                playerKeys.Insert(0, localPlayerKey);
            }

            var players = new List<Player>();

            foreach (var playerKey in playerKeys)
            {
                var data = world.GetData(playerKey);
                var memoryStream = new MemoryStream(data);
                var nbtReader = new fNbt.NbtReader(memoryStream, false);

                var playerTag = nbtReader.ReadAsTag();

                var player = new Player
                {
                    UniqueID = playerTag["UniqueID"].LongValue.ToString(),
                    Dimension = (Dimension)playerTag["DimensionId"].IntValue,
                    X = (int)playerTag["Pos"][0].FloatValue,
                    Y = (int)playerTag["Pos"][1].FloatValue,
                    Z = (int)playerTag["Pos"][2].FloatValue
                };

                players.Add(player);

                memoryStream.Dispose();
            }

            foreach (var player in players)
            {
                Console.WriteLine();
                Console.Write($"{player.UniqueID} {player.Dimension} {player.X} {player.Y} {player.Z}");
            }

            if (tokens.Contains("json"))
            {
                const string jsonTemplate = "var playersData = { players: [] }";
                var playersData = JObject.Parse(jsonTemplate.Substring(jsonTemplate.IndexOf('=') + 1).Trim().TrimEnd(';'));

                foreach (var player in players)
                {
                    ((JArray)playersData["players"]).Add(JObject.FromObject(new
                    {
                        name = "Player " + player.UniqueID,
                        icon = "\uf183",
                        dimensionId = (int)player.Dimension,
                        position = new[] { player.X, player.Y, player.Z },
                        color = "Black",
                        visible = true
                    }));
                }

                var json = JsonConvert.SerializeObject(playersData, Formatting.Indented);
                File.WriteAllText("playersData.js",
                    jsonTemplate.Replace("{ players: [] }", json),
                    Encoding.UTF8);
            }

            return players;
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
}


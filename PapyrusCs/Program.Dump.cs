using System;
using System.Collections.Generic;
using System.Text;
using Maploader.World;
using PapyrusCs.Database;

namespace PapyrusCs
{
    public partial class Program
    {
        // An example implementation of a Bedrock level-db parser can be found in
        // https://github.com/mmccoo/minecraft_mmccoo/blob/master/parse_bedrock.cpp

        private static void Dump(World world, string[] tokens)
        {
            var list = new List<string>();

            // Overworld
            // | x: 32 | y: 32 | tag: 8 | (optional)subchunk id: 8 |

            // Other dimension
            // | x: 32 | y: 32 | dimension: 32 | tag: 8 | (optional) subchunk id: 8 |

            Console.WriteLine("Writing Keys");
            foreach (var key in world.Keys)
            {
                var sKey = Encoding.UTF8.GetString(key);
                if (sKey == "Nether" || sKey == "portals")
                {
                    // 1 of each
                    continue;
                }

                int dimension = -1;

                // The first eight bytes are chunk X and Z
                int chunkX = key.Length > 4 ? GetInt(key, 0) : 0;
                int chunkZ = key.Length > 8 ? GetInt(key, 4) : 0;
                int chunkY = -1;

                // Overworld
                if (key.Length == 10 && key[8] == 0x2f)
                {
                    dimension = 0;
                    chunkY = GetInt(key, 8);
                }
                else if (key.Length == 9 && key[8] == 0x36)
                {
                    // finalized state
                }

                // Nether(1) | End(2)
                else if (key.Length == 14 && key[8] == 0x2f)
                {
                    dimension = GetInt(key, 8);
                    chunkY = GetInt(key, 11);
                }
                else if (key.Length == 13 && key[12] == 0x36)
                {
                    dimension = GetInt(key, 8);
                    // finalized state
                }

                // Biomes and Elevation
                else if(key.Length == 9 && key[8] == 0x2d)
                {
                }

                // Blocks
                else if (key.Length == 13 && key[8] == 0x31)
                {
                }
                else if (key.Length == 13 && key[12] == 0x31)
                {
                }

                // Entities
                else if (key.Length == 9 && key[8] == 0x32)
                {
                }
                else if (key.Length == 13 && key[12] == 0x32)
                {
                }

                if (key.Length > 8)
                {
                    int tag = (int)key[8];
                    switch (tag)
                    {
                        case (int)KeyType.Data2D:
                            // Lenght: 9
                            break;

                        case (int)KeyType.SubChunkPrefix:
                            new LevelDbWorldKey2(key);
                            break;

                        case (int)KeyType.BlockEntity:
                            break;

                        case (int)KeyType.Entity:
                            break;

                        case (int)KeyType.HardCodedSpawnAreas:
                            break;

                        default:
                            break;
                    }
                    continue;
                }
                else
                {

                }


                list.Add(sKey);
            }
        }

        private static int GetInt(byte[] bytes, uint offset)
        {
            int value = 0;

            for (int i = 0; i < 4; i++)
            {
                // if I don't do the static cast, the top bit will be sign extended.
                value |= ((bytes[offset + i]) << i * 8);
            }

            return value;
        }
    }
}

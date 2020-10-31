﻿using System.Diagnostics;
using System.Linq;

namespace PapyrusCs
{
    [DebuggerDisplay("{Name} {Id}")]
    public struct Biome
    {
        public Biome(string name, byte id, int[] rgb)
        {
            this.Name = name;
            this.Id = id;
            this.RGB = rgb;
        }

        public string Name;
        public byte Id;
        public int[] RGB;
    }

    public static class Biomes
    {
        public static Biome Get(int id)
        {
            if (All.Any(b => b.Id == id))
            {
                return All.First(b => b.Id == id);
            }
            return All.Last();
        }

        public static Biome[] All = {
            new Biome("Ocean", 0,  new[]{2,0,107}),
            new Biome("Plains", 1, new[]{140,178,100}),
            new Biome("Desert", 2, new[]{247,146,42}),
            new Biome("Mountains", 3, new[]{95,95,95}),
            new Biome("Forest", 4, new[]{21,101,41}),
            new Biome("Taiga", 5, new[]{24,101,89}),
            new Biome("Swamp", 6, new[]{46,249,181}),
            new Biome("River", 7, new[]{0,0,250}),
            new Biome("Nether", 8, new[]{252,0,1}),
            new Biome("TheEnd", 9, new[]{125,121,251}),
            new Biome("FrozenOcean", 0xa, new[]{141,141,156}),
            new Biome("FrozenRiver", 0xb, new[]{158,155,252}),
            new Biome("SnowyTundra", 0xc, new[]{254,254,254}),
            new Biome("SnowyMountains", 0xd, new[]{156,157,156}),
            new Biome("MushroomFields", 0xe, new[]{251,0,249}),
            new Biome("MushroomFieldShore", 0xf, new[]{156,0,249}),
            new Biome("Beach", 0x10, new[]{249,222,93}),
            new Biome("DesertHills", 0x11, new[]{207,94,31}),
            new Biome("WoodedHills", 0x12, new[]{39,85,35}),
            new Biome("TaigaHills",  0x13, new[]{28,58,53}),
            new Biome("MountainEdge", 0x14, new[]{122,127,157}),
            new Biome("Jungle", 0x15, new[]{84,122,30}),
            new Biome("JungleHills", 0x16, new[]{48,68,19}),
            new Biome("JungleEdge", 0x17, new[]{98,138,40}),
            new Biome("DeepOcean", 0x18, new[]{3,2,49}),
            new Biome("StoneShore", 0x19, new[]{158,159,130}),
            new Biome("SnowyBeach", 0x1a, new[]{249,239,191}),
            new Biome("BirchForest", 0x1b, new[]{52,115,70}),
            new Biome("BirchForestHills", 0x1c, new[]{39,96,56}),
            new Biome("DarkForest", 0x1d, new[]{67,83,36}),
            new Biome("SnowyTaiga", 0x1e, new[]{52,85,74}),
            new Biome("SnowyTaigaHills", 0x1f, new[]{41,65,57}),
            new Biome("GiantTreeTaiga", 0x20, new[]{88,101,81}),
            new Biome("GiantTreeTaigaHills", 0x21, new[]{84,94,64}),
            new Biome("WoodedMountains", 0x22, new[]{80,111,81}),
            new Biome("Savanna", 0x23, new[]{187,178,104}),
            new Biome("SavannaPlateau", 0x24, new[]{164,155,101}),
            new Biome("Badlands", 0x25, new[]{213,67,29}),
            new Biome("WoodedBadlandsPlateau", 0x26, new[]{174,150,103}),
            new Biome("BadlandsPlateau", 0x27, new[]{198,137,101}),
            new Biome("Unknown28", 0x28, new[]{0,0,0}),
            new Biome("Unknown2a", 0x2a, new[]{0,0,0}),
            new Biome("Unknown2b", 0x2b, new[]{0,0,0}),
            new Biome("WarmOcean", 0x2c, new[]{76,35,183}),
            new Biome("LukewarmOcean", 0x2d, new[]{87,58,168}),
            new Biome("ColdOcean", 0x2e, new[]{69,116,214}),
            new Biome("DeepWarmOcean", 0x2f, new[]{100,53,227}),
            new Biome("DeepLukewarmOcean", 0x30, new[]{105,55,238}),
            new Biome("DeepColdOcean", 0x31, new[]{29,72,164}),
            new Biome("DeepFrozenOcean", 0x32, new[]{158,184,237}),
            new Biome("Void", 0x7f, new[]{3,3,3}),
            new Biome("SunflowerPlains", 0x81, new[]{181,220,140}),
            new Biome("DesertLakes", 0x82, new[]{253,186,75}),
            new Biome("GravellyMountains", 0x83, new[]{133,133,133}),
            new Biome("FlowerForest", 0x84, new[]{105,115,46}),
            new Biome("TaigaMountains", 0x85, new[]{88,101,81}),
            new Biome("SwampHills", 0x86, new[]{65,254,218}),
            new Biome("IceSpikes", 0x8c, new[]{178,218,218}),
            new Biome("ModifiedJungle", 0x95, new[]{122,162,59}),
            new Biome("ModifiedJungleEdge", 0x97, new[]{138,178,71}),
            new Biome("TallBirchForest", 0x9b, new[]{89,154,108}),
            new Biome("TallBirchHills", 0x9c, new[]{79,137,96}),
            new Biome("DarkForestHills", 0x9d, new[]{104,121,71}),
            new Biome("SnowyTaigaMountains", 0x9e, new[]{89,123,112}),
            new Biome("GiantSpruceTaiga", 0xa0, new[]{106,95,78}),
            new Biome("GiantSprucetaigaHills", 0xa1, new[]{106,116,100}),
            new Biome("GravellyMountainsPlus", 0xa2, new[]{119,150,119}),
            new Biome("ShatteredSavanna", 0xa3, new[]{227,217,136}),
            new Biome("ShatteredsavannaPlateau", 0xa4, new[]{204,195,140}),
            new Biome("ErodedBadlands", 0xa5, new[]{251,107,65}),
            new Biome("ModifiedWoodedBadlandsPlateau", 0xa6, new[]{213,189,141}),
            new Biome("ModifiedBadlandsPlateau", 0xa7, new[]{239,176,139}),
            new Biome("BambooJungle", 0xa8, new[]{113,230,52}),
            new Biome("BambooJungleHills", 0xa9, new[]{96,197,45}),
            new Biome("UnknownBiome", 0xff, new[]{255, 0, 0})
        };
    }
}

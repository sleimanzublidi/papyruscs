namespace PapyrusCs
{
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
}

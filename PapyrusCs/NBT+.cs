using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

    [DebuggerDisplay("{Name}:{Value}")]
    public struct Tag
    {
        public string Name;
        public object Value;
    }

    public class TagList : List<Tag>
    {
        public void Add(string name, object value)
        {
            this.Add(new Tag { Name = name, Value = value });
        }

        public IEnumerable<string> Names => this.Select(t => t.Name);

        public bool Contains(string tagName) => this.Names.Contains(tagName);
    }

    public static class NbtExtensions
    {
        public static TagList ReadTags(this fNbt.NbtReader nbt)
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
    }
}

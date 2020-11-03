using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using Maploader.Extensions;
using Maploader.Renderer.Heightmap;
using Maploader.Renderer.Imaging;
using Maploader.Renderer.Texture;
using Maploader.World;

namespace Maploader.Renderer
{
    public class ChunkRenderer<TImage> where TImage : class
    {
        private readonly TextureFinder<TImage> textureFinder;
        private readonly IGraphicsApi<TImage> graphics;
        private readonly RenderSettings renderSettings;
        private readonly Brillouin brillouin;

        public ChunkRenderer([NotNull] TextureFinder<TImage> textureFinder, IGraphicsApi<TImage> graphics, RenderSettings settings = null)
        {
            this.textureFinder = textureFinder ?? throw new ArgumentNullException(nameof(textureFinder));
            this.graphics = graphics;
            this.renderSettings = settings ?? new RenderSettings();

            this.brillouin = new Brillouin(this.renderSettings.BrillouinJ, this.renderSettings.BrillouinDivider);
        }

        public List<string> MissingTextures { get; } = new List<string>();

        public void RenderChunk(TImage dest, Chunk chunk, int xOffset, int zOffset)
        {
            var xzColumns = chunk.Blocks.GroupBy(x => x.Value.XZ);
            var blocksOrderedByXZ = xzColumns.OrderBy(x => x.Key.GetLeByte(0)).ThenBy(x => x.Key.GetLeByte(1));
            var brightnessOffset = Math.Min(this.renderSettings.BrillouinOffset, this.renderSettings.YMax);
            if (brightnessOffset < 0)
                brightnessOffset = this.renderSettings.BrillouinOffset;

            foreach (var blocks in blocksOrderedByXZ)
            {
                var blocksToRender = new Stack<BlockCoord>();

                List<KeyValuePair<uint, BlockCoord>> blocksFromSkyToBedrock = blocks
                    .Where(x => x.Value.Block.Id != "minecraft:air")
                    .OrderByDescending(x => x.Value.Y)
                    .ToList();

                if (this.renderSettings.YMax > 0)
                {
                    blocksFromSkyToBedrock = blocksFromSkyToBedrock
                        .Where(x => x.Value.Y <= this.renderSettings.YMax)
                        .ToList();
                }

                if (this.renderSettings.TrimCeiling)
                {
                    int start = -1;
                    for (int i = 1; i < blocksFromSkyToBedrock.Count; i++)
                    {
                        if (Math.Abs(blocksFromSkyToBedrock[i].Value.Y - blocksFromSkyToBedrock[i - 1].Value.Y) > 4)
                        {
                            start = i;
                            break;
                        }
                    }

                    if (start != -1)
                    {
                        blocksFromSkyToBedrock.RemoveRange(0, start);
                    }
                }

                switch (this.renderSettings.Profile)
                {
                    case "underground":
                        {
                            var lastYValue = 300;
                            var isRendering = false;
                            var state = "goingthroughtoplevelsky";

                            foreach (var blockColumn in blocksFromSkyToBedrock)
                            {
                                var block = blockColumn.Value;

                                if (!isRendering)
                                {
                                    var skyBlocksSkipped = lastYValue - block.Y - 1;

                                    switch (state)
                                    {
                                        case "goingthroughtoplevelsky":
                                            if (this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id) || block.Block.Id.Contains("water") || block.Block.Id.Contains("kelp"))
                                            {
                                                continue;
                                            }

                                            lastYValue = block.Y;

                                            if (skyBlocksSkipped > 0)
                                            {
                                                state = "goingthroughground";
                                            }
                                            break;
                                        case "goingthroughground":
                                            lastYValue = block.Y;

                                            if (this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id) || block.Block.Id.Contains("water") || block.Block.Id.Contains("kelp"))
                                            {
                                                isRendering = true;
                                            }

                                            if (skyBlocksSkipped > 0)
                                            {
                                                isRendering = true;
                                            }
                                            break;
                                    }
                                }

                                if (isRendering)
                                {
                                    blocksToRender.Push(block);
                                    if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        break;

                    case "aquatic":
                        {
                            bool isWater = false;

                            foreach (var blockColumn in blocksFromSkyToBedrock)
                            {
                                var block = blockColumn.Value;
                                if (block.Block.Id.Contains("water"))
                                {
                                    isWater = true;
                                    continue;
                                }

                                if (!isWater)
                                {
                                    // stop if we hit a solid block first
                                    if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                                    {
                                        break;
                                    }
                                    continue;
                                }

                                blocksToRender.Push(block);
                                if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                                {
                                    break;
                                }
                            }
                        }
                        break;

                    case "ore":
                        {
                            SearchForOres(blocksToRender, blocksFromSkyToBedrock);
                        }
                        break;

                    case "stronghold":
                        {
                            var lastYValue = 300;
                            var isRendering = false;
                            var state = "goingthroughtoplevelsky";

                            foreach (var blockColumn in blocksFromSkyToBedrock)
                            {
                                var block = blockColumn.Value;

                                if (!isRendering)
                                {
                                    var skyBlocksSkipped = lastYValue - block.Y - 1;

                                    switch (state)
                                    {
                                        case "goingthroughtoplevelsky":
                                            if (this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id) || block.Block.Id.Contains("water") || block.Block.Id.Contains("kelp"))
                                            {
                                                continue;
                                            }

                                            lastYValue = block.Y;

                                            if (skyBlocksSkipped > 0)
                                            {
                                                state = "goingthroughground";
                                            }
                                            break;
                                        case "goingthroughground":
                                            lastYValue = block.Y;

                                            if (this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id) || block.Block.Id.Contains("water") || block.Block.Id.Contains("kelp"))
                                            {
                                                isRendering = true;
                                            }

                                            if (skyBlocksSkipped > 0)
                                            {
                                                isRendering = true;
                                            }
                                            break;
                                    }
                                }

                                if (isRendering)
                                {
                                    if (block.Block.Id.Contains("cobblestone") ||
                                        block.Block.Id.Contains("brick") ||
                                        block.Block.Id.Contains("end") ||
                                        block.Block.Id.Contains("iron_bars") ||
                                        block.Block.Id.Contains("spawn") ||
                                        block.Block.Id.Contains("egg") ||
                                        block.Block.Id.Contains("bookshelf") ||
                                        block.Block.Id.Contains("cobweb") ||
                                        block.Block.Id.Contains("oak_planks") ||
                                        block.Block.Id.Contains("chest") ||
                                        block.Block.Id.Contains("door"))
                                    {
                                        blocksToRender.Push(block);
                                        if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case "elevation":
                        foreach (var blockColumn in blocksFromSkyToBedrock) // Look for transparent blocks in single y column
                        {
                            var block = blockColumn.Value;
                            if (block.Block.Id.Contains("water"))
                            {
                                continue;
                            }

                            blocksToRender.Push(block);
                            if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                            {
                                break;
                            }
                        }
                        break;

                    default:
                        {
                            foreach (var blockColumn in blocksFromSkyToBedrock) // Look for transparent blocks in single y column
                            {
                                var block = blockColumn.Value;

                                blocksToRender.Push(block);
                                if (!this.textureFinder.TransparentBlocks.ContainsKey(block.Block.Id))
                                {
                                    break;
                                }
                            }
                        }
                        break;
                }

                foreach (var block in blocksToRender)
                {
                    if (SkipSpecialBlockRender(block.Block))
                    {
                        continue;
                    }

                    if (this.renderSettings.Profile == "elevation")
                    {
                        var x = xOffset + block.X * 16;
                        var z = zOffset + block.Z * 16;

                        this.graphics.DrawImageWithBrightness(dest, this.GetElevationTexture(block.Y) as TImage, x, z, 1);
                        continue;
                    }

                    var textures = this.textureFinder.FindTexturePath(block.Block.Id, block.Block.Data, block.X, block.Z, block.Y);
                    if (textures == null)
                    {
                        Console.WriteLine($"\nMissing Texture(2): {block.ToString().PadRight(30)}");
                        this.MissingTextures.Add($"ID: {block.Block.Id}");
                        continue;
                    }

                    foreach (var texture in textures.Infos)
                    {
                        var bitmapTile = this.textureFinder.GetTextureImage(texture);
                        if (bitmapTile != null)
                        {
                            var x = xOffset + block.X * 16;
                            var z = zOffset + block.Z * 16;

                            if (this.renderSettings.RenderMode == RenderMode.Heightmap)
                            {
                                this.graphics.DrawImageWithBrightness(dest, bitmapTile, x, z, this.brillouin.GetBrightness(block.Y - brightnessOffset));
                            }
                            else
                            {
                                this.graphics.DrawImageWithBrightness(dest, bitmapTile, x, z, 1);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"\nMissing Texture(1): {block.ToString().PadRight(30)} -- {texture.Filename}");
                            this.MissingTextures.Add($"ID: {block.Block.Id}, {texture.Filename}");
                            continue;
                        }
                    }
                }
            }

            if (this.renderSettings.RenderCoordinateStrings)
            {
                this.graphics.DrawString(dest, $"{chunk.X * 1}, {chunk.Z * 1}", new Font(FontFamily.GenericSansSerif, 10), Brushes.Black, xOffset, zOffset);
            }
        }

        private readonly Bitmap[] elevationTextures = new Bitmap[256];

        private Bitmap GetElevationTexture(int elevation)
        {
            if (this.elevationTextures[elevation] != null)
                return this.elevationTextures[elevation];

            var bitmap = new Bitmap(16, 16);
            using (var graphic = Graphics.FromImage(bitmap))
            {
                var brush = new SolidBrush(Color.FromArgb(elevation, Color.Black));
                var rect = new Rectangle(0, 0, 16, 16);
                graphic.FillRectangle(brush, rect);
            }

            this.elevationTextures[elevation] = bitmap;
            return elevationTextures[elevation];
        }

        private static void SearchForOres(Stack<BlockCoord> blocksToRender, List<KeyValuePair<uint, BlockCoord>> blocksFromSkyToBedrock)
        {
            var orePriority = new[]
            {
                // Overworld
                "minecraft:diamond_ore",
                "minecraft:emerald_ore",
                "minecraft:redstone_ore",
                "minecraft:gold_ore",
                "minecraft:iron_ore",
                "minecraft:lapis_ore",
                "minecraft:coal_ore",
                // Nether
                "minecraft:ancient_debris",
                "minecraft:nether_gold_ore",
                "minecraft:quartz_ore",
            };

            foreach (var target in orePriority)
            {
                bool foundOre = false;

                foreach (var blockColumn in blocksFromSkyToBedrock)
                {
                    var block = blockColumn.Value;
                    if (block.Block.Id == target)
                    {
                        blocksToRender.Push(block);
                        foundOre = true;
                        break;
                    }
                }

                if (foundOre)
                {
                    break;
                }
            }
        }

        private static void SearchForEndBlocks(Stack<BlockCoord> blocksToRender, List<KeyValuePair<uint, BlockCoord>> blocksFromSkyToBedrock)
        {
            foreach (var blockColumn in blocksFromSkyToBedrock)
            {
                var block = blockColumn.Value;

                if (block.Block.Id.Contains("end"))
                {
                    blocksToRender.Push(block);
                    break;
                }
            }
        }

        private static bool SkipSpecialBlockRender(BlockData block)
        {
            switch (block.Id)
            {
                case "minecraft:light_block":
                    return true;
            }
            return false;
        }
    }
}

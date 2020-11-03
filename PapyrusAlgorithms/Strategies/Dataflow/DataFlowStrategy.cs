using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Maploader.Core;
using Maploader.Extensions;
using Maploader.Renderer;
using Maploader.Renderer.Imaging;
using Maploader.Renderer.Texture;
using Maploader.World;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PapyrusAlgorithms.Data;
using PapyrusAlgorithms.Database;
using PapyrusCs.Database;

namespace PapyrusAlgorithms.Strategies.Dataflow
{
    public class DataFlowStrategy<TImage> : IRenderStrategy where TImage : class
    {
        private readonly IGraphicsApi<TImage> graphics;
        private PapyrusContext db;
        private ImmutableDictionary<LevelDbWorldKey2, KeyAndCrc> renderedSubchunks;
        private bool isUpdate;

        public DataFlowStrategy(IGraphicsApi<TImage> graphics)
        {
            this.graphics = graphics;
        }

        public int XMin { get; set; }
        public int XMax { get; set; }
        public int ZMin { get; set; }
        public int ZMax { get; set; }
        public string OutputPath { get; set; }
        public Dictionary<string, Texture> TextureDictionary { get; set; }
        public string TexturePath { get; set; }
        public int ChunkSize { get; set; }
        public int ChunksPerDimension { get; set; }
        public int TileSize { get; set; }
        public World World { get; set; }
        public int TotalChunkCount { get; set; }
        public int InitialZoomLevel { get; set; }
        public ConcurrentBag<string> MissingTextures { get; }
        public List<Exception> Exceptions { get; }
        public RenderSettings RenderSettings { get; set; }
        public int InitialDiameter { get; set; }
        public HashSet<LevelDbWorldKey2> AllWorldKeys { get; set; }
        public string FileFormat { get; set; }
        public int FileQuality { get; set; }
        public int Dimension { get; set; }
        public string Profile { get; set; }

        public bool IsUpdate => this.isUpdate;
        public bool DeleteExistingUpdateFolder { get; set; }
        public int NewInitialZoomLevel { get; set; }
        public int NewLastZoomLevel { get; set; }

        private string pathToDb;
        private string pathToDbUpdate;
        private string pathToDbBackup;
        private string pathToMapUpdate;
        private string pathToMap;

        public void RenderInitialLevel()
        {
            this.World.ChunkPool = new ChunkPool();
            this.graphics.DefaultQuality = this.FileQuality;

            Console.Write("Grouping subchunks... ");

            var chunkKeys = this.AllWorldKeys
                .Where(c => c.X <= this.XMax && c.X >= this.XMin && c.Z <= this.ZMax && c.Z >= this.ZMin)
                .GroupBy(x => x.XZ)
                .Select(x => new GroupedChunkSubKeys(x))
                .ToList();

            Console.WriteLine(chunkKeys.Count);

            this.AllWorldKeys.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var numberOfThreadsUsed = Math.Max(1, this.RenderSettings.MaxNumberOfThreads);

            var getOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Math.Min(2 * numberOfThreadsUsed, this.RenderSettings.MaxNumberOfQueueEntries),
                EnsureOrdered = false,
                MaxDegreeOfParallelism = 1
            };

            var bitmapOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Math.Min(2 * numberOfThreadsUsed, this.RenderSettings.MaxNumberOfQueueEntries),
                EnsureOrdered = false,
                MaxDegreeOfParallelism = numberOfThreadsUsed
            };

            var threadsUsedForSaving = this.FileFormat == "webp" ? numberOfThreadsUsed : 2;
            var saveOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Math.Min(2 * numberOfThreadsUsed, this.RenderSettings.MaxNumberOfQueueEntries),
                EnsureOrdered = false,
                MaxDegreeOfParallelism = threadsUsedForSaving
            };

            var dbOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Math.Min(2 * numberOfThreadsUsed,
                this.RenderSettings.MaxNumberOfQueueEntries),
                EnsureOrdered = false,
                MaxDegreeOfParallelism = 1
            };

            var groupedToTiles = chunkKeys
                .GroupBy(x => x.Subchunks.First().Value.GetXZGroup(this.ChunksPerDimension))
                .ToList();

            Console.WriteLine($"Grouped by {this.ChunksPerDimension} to {groupedToTiles.Count} tiles");

            var average = groupedToTiles.Average(x => x.Count());
            Console.WriteLine($"Average of {average:0.0} chunks per tile");

            var getDataBlock = new GetDataBlock(this.World, this.renderedSubchunks, getOptions, this.ForceOverwrite);

            var createAndRender = new CreateChunkAndRenderBlock<TImage>(this.World, this.TextureDictionary, this.TexturePath, this.RenderSettings, this.graphics, this.ChunkSize, this.ChunksPerDimension, bitmapOptions);

            var saveBitmapBlock = new SaveBitmapBlock<TImage>(this.isUpdate ? this.pathToMapUpdate : this.pathToMap, this.NewInitialZoomLevel, this.FileFormat, saveOptions, this.graphics);

            var batchBlock = new BatchBlock<IEnumerable<SubChunkData>>(128, new GroupingDataflowBlockOptions { BoundedCapacity = 128 * 8, EnsureOrdered = false });

            // Todo, put in own class
            var inserts = 0;
            var updates = 0;
            var r = new Random();
            var dbBLock = new ActionBlock<IEnumerable<IEnumerable<SubChunkData>>>(data =>
            {
                if (data == null)
                    return;

                var datas = data.Where(x => x != null).SelectMany(x => x).ToList();
                try
                {
                    /*
                    if (r.Next(100) == 0)
                    {
                        throw new ArgumentOutOfRangeException("Test Error in dbBLock");
                    }*/

                    var toInsert = datas.Where(x => x.FoundInDb == false)
                        .Select(x => new Checksum { Crc32 = x.Crc32, LevelDbKey = x.Key, Profile = Profile }).ToList();

                    if (toInsert.Count > 0)
                    {
                        this.db.BulkInsert(toInsert);
                        inserts += toInsert.Count;
                    }

                    var toUpdate = datas.Where(x => x.FoundInDb).Select(x => new Checksum()
                    { Id = x.ForeignDbId, Crc32 = x.Crc32, LevelDbKey = x.Key, Profile = Profile }).ToList();
                    if (toUpdate.Count > 0)
                    {
                        this.db.BulkUpdate(toUpdate);
                        updates += toUpdate.Count;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in CreateChunkAndRenderBlock: " + ex.Message);
                }

            }, dbOptions);

            createAndRender.ChunksRendered += (sender, args) => ChunksRendered?.Invoke(sender, args);

            getDataBlock.Block.LinkTo(createAndRender.Block, new DataflowLinkOptions { PropagateCompletion = true, });
            createAndRender.Block.LinkTo(saveBitmapBlock.Block, new DataflowLinkOptions { PropagateCompletion = true });
            saveBitmapBlock.Block.LinkTo(batchBlock, new DataflowLinkOptions { PropagateCompletion = true });
            batchBlock.LinkTo(dbBLock, new DataflowLinkOptions { PropagateCompletion = true });

            int postCount = 0;
            foreach (var groupedToTile in groupedToTiles)
            {
                if (getDataBlock.Block.Post(groupedToTile))
                {
                    postCount++;
                    continue;
                }

                postCount++;
                getDataBlock.Block.SendAsync(groupedToTile).Wait();
                if (postCount > 1000)
                {
                    postCount = 0;
                    Console.WriteLine($"\nQueue Stat: GetData {getDataBlock.InputCount} Render {createAndRender.InputCount} Save {saveBitmapBlock.InputCount} Db {dbBLock.InputCount}");
                }
            }

            Console.WriteLine("Post complete");

            getDataBlock.Block.Complete();
            while (!dbBLock.Completion.Wait(1000))
            {
                Console.WriteLine($"\nQueue Stat: GetData {getDataBlock.InputCount} Render {createAndRender.InputCount} Save {saveBitmapBlock.InputCount} Db {dbBLock.InputCount}");
            }
            Console.WriteLine("DbUpdate complete");


            Console.WriteLine($"\n{inserts}, {updates}");
            Console.WriteLine($"\n{getDataBlock.ProcessedCount} {createAndRender.ProcessedCount}  {saveBitmapBlock.ProcessedCount}");
        }

        protected Func<IEnumerable<int>, ParallelOptions, Action<int>, ParallelLoopResult> OuterLoopStrategy
            => Parallel.ForEach;

        public bool ForceOverwrite { get; set; }

        public void RenderZoomLevels()
        {
            var sourceZoomLevel = this.NewInitialZoomLevel;
            var sourceDiameter = this.InitialDiameter;

            var sourceLevelXmin = this.XMin / this.ChunksPerDimension;
            var sourceLevelXmax = this.XMax / this.ChunksPerDimension;
            var sourceLevelZmin = this.ZMin / this.ChunksPerDimension;
            var sourceLevelZmax = this.ZMax / this.ChunksPerDimension;


            while (sourceZoomLevel > this.NewLastZoomLevel)
            {
                // Force garbage collection (may not be necessary)
                GC.Collect();
                GC.WaitForPendingFinalizers();

                var destDiameter = sourceDiameter / 2;
                var sourceZoom = sourceZoomLevel;
                var destZoom = sourceZoomLevel - 1;
                var linesRendered = 0;


                if (sourceLevelXmin.IsOdd()) // always start at an even coordinate
                    sourceLevelXmin--;

                if (sourceLevelXmax.IsOdd())
                    sourceLevelXmax++;

                if (sourceLevelZmin.IsOdd()) // always start at an even coordinate
                    sourceLevelZmin--;

                if (sourceLevelZmax.IsOdd())
                    sourceLevelZmax++;


                Console.WriteLine(
                    $"\nRendering Level {destZoom} with source coordinates X {sourceLevelXmin} to {sourceLevelXmax}, Z {sourceLevelZmin} to {sourceLevelZmax}");

                this.OuterLoopStrategy(BetterEnumerable.SteppedRange(sourceLevelXmin, sourceLevelXmax, 2),
                    new ParallelOptions() { MaxDegreeOfParallelism = this.RenderSettings.MaxNumberOfThreads },
                    x =>
                    {
                        for (int z = sourceLevelZmin; z < sourceLevelZmax; z += 2)
                        {
                            var b1 = this.LoadBitmap(sourceZoom, x, z, this.isUpdate);
                            var b2 = this.LoadBitmap(sourceZoom, x + 1, z, this.isUpdate);
                            var b3 = this.LoadBitmap(sourceZoom, x, z + 1, this.isUpdate);
                            var b4 = this.LoadBitmap(sourceZoom, x + 1, z + 1, this.isUpdate);

                            if (b1 != null || b2 != null || b3 != null || b4 != null)
                            {
                                var bfinal = this.graphics.CreateEmptyImage(this.TileSize, this.TileSize);
                                {
                                    b1 = b1 ?? this.LoadBitmap(sourceZoom, x, z, false);
                                    b2 = b2 ?? this.LoadBitmap(sourceZoom, x + 1, z, false);
                                    b3 = b3 ?? this.LoadBitmap(sourceZoom, x, z + 1, false);
                                    b4 = b4 ?? this.LoadBitmap(sourceZoom, x + 1, z + 1, false);

                                    var halfTileSize = this.TileSize / 2;

                                    if (b1 != null)
                                    {
                                        this.graphics.DrawImage(bfinal, b1, 0, 0, halfTileSize, halfTileSize);
                                    }

                                    if (b2 != null)
                                    {
                                        this.graphics.DrawImage(bfinal, b2, halfTileSize, 0, halfTileSize, halfTileSize);
                                    }

                                    if (b3 != null)
                                    {
                                        this.graphics.DrawImage(bfinal, b3, 0, halfTileSize, halfTileSize, halfTileSize);
                                    }

                                    if (b4 != null)
                                    {
                                        this.graphics.DrawImage(bfinal, b4, halfTileSize, halfTileSize, halfTileSize,
                                            halfTileSize);
                                    }

                                    this.SaveBitmap(destZoom, x / 2, z / 2, this.isUpdate, bfinal);
                                }

                                // Dispose of any bitmaps, releasing memory
                                foreach (var bitmap in new[] { b1, b2, b3, b4, bfinal }.OfType<IDisposable>())
                                {
                                    bitmap.Dispose();
                                }
                            }
                        }

                        Interlocked.Add(ref linesRendered, 2);

                        ZoomLevelRenderd?.Invoke(this,
                            new ZoomRenderedEventArgs(linesRendered, sourceDiameter, destZoom));
                    });

                sourceLevelZmin /= 2;
                sourceLevelZmax /= 2;
                sourceLevelXmin /= 2;
                sourceLevelXmax /= 2;

                sourceDiameter = destDiameter;
                sourceZoomLevel = destZoom;
            }

        }

        private TImage LoadBitmap(int zoom, int x, int z, bool isUpdate)
        {
            var mapPath = isUpdate ? this.pathToMapUpdate : this.pathToMap;

            var path = Path.Combine(mapPath, $"{zoom}", $"{x}");
            var filepath = Path.Combine(path, $"{z}.{this.FileFormat}");
            if (File.Exists(filepath))
            {
                try
                {
                    return this.graphics.LoadImage(filepath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Loading tile at {filepath}, because {ex}");
                    return null;
                }
            }

            return null;
        }

        private void SaveBitmap(int zoom, int x, int z, bool isUpdate, TImage b)
        {
            var mapPath = isUpdate ? this.pathToMapUpdate : this.pathToMap;

            var path = Path.Combine(mapPath, $"{zoom}", $"{x}");
            var filepath = Path.Combine(path, $"{z}.{this.FileFormat}");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            this.graphics.SaveImage(b, filepath);
        }


        public event EventHandler<ChunksRenderedEventArgs> ChunksRendered;
        public event EventHandler<ZoomRenderedEventArgs> ZoomLevelRenderd;

        public void Init()
        {
            this.pathToDb = Path.Combine(this.OutputPath, "chunks.sqlite");
            this.pathToDbUpdate = Path.Combine(this.OutputPath, "chunks-update.sqlite");
            this.pathToDbBackup = Path.Combine(this.OutputPath, "chunks-backup.sqlite");

            this.pathToMapUpdate = Path.Combine(this.OutputPath, "update", "dim" + this.Dimension + (string.IsNullOrEmpty(this.Profile) ? "" : $"_{this.Profile}"));
            this.pathToMap = Path.Combine(this.OutputPath, "map", "dim" + this.Dimension + (string.IsNullOrEmpty(this.Profile) ? "" : $"_{this.Profile}"));

            this.isUpdate = File.Exists(this.pathToDb);

            this.NewInitialZoomLevel = 20;
            this.NewLastZoomLevel = this.NewInitialZoomLevel - this.InitialZoomLevel;

            if (this.isUpdate)
            {
                Console.WriteLine("Found chunks.sqlite, this must be an update of the map");

                if (File.Exists(this.pathToDbUpdate))
                {
                    Console.WriteLine($"Deleting {this.pathToDbUpdate} old update database file");
                    File.Delete(this.pathToDbUpdate);
                    File.Delete(this.pathToDbUpdate + "-wal");
                    File.Delete(this.pathToDbUpdate + "-shm");
                }

                File.Copy(this.pathToDb, this.pathToDbUpdate);

                if (Directory.Exists(this.pathToMapUpdate) && this.DeleteExistingUpdateFolder)
                {
                    Console.WriteLine("Deleting old update in {0}", this.pathToMapUpdate);
                    DirectoryInfo di = new DirectoryInfo(this.pathToMapUpdate);
                    var files = di.EnumerateFiles("*.*", SearchOption.AllDirectories);
                    var fileInfos = files.ToList();
                    if (fileInfos.Any(x => x.Extension != "." + this.FileFormat))
                    {
                        Console.WriteLine("Can not delete the update folder, because there are files in it not generated by PapyrusCs");
                        foreach (var f in fileInfos.Where(x => x.Extension != "." + this.FileFormat))
                        {
                            Console.WriteLine("Unknown file {0}", f.FullName);
                        }
                        throw new InvalidOperationException("Can not delete the update folder, because there are files in it not generated by PapyrusCs");
                    }

                    foreach (var f in fileInfos)
                    {
                        Console.WriteLine("Deleting update file {0}", f.FullName);
                        f.Delete();
                    }
                }
            }

            var c = new DbCreator();
            this.db = c.CreateDbContext(this.pathToDbUpdate, true);
            this.db.Database.Migrate();

            var settings = this.db.Settings.FirstOrDefault(x => x.Dimension == this.Dimension && x.Profile == this.Profile);
            if (settings != null)
            {
                this.FileFormat = settings.Format;
                this.FileQuality = settings.Quality;
                this.ChunksPerDimension = settings.ChunksPerDimension;
                Console.WriteLine("Overriding settings with: Format {0}, Quality {1} ChunksPerDimension {2}", this.FileFormat, this.FileQuality, this.ChunksPerDimension);

                settings.MaxZoom = this.NewInitialZoomLevel;
                settings.MinZoom = this.NewLastZoomLevel;
                Console.WriteLine("Setting Zoom levels to {0} down to {1}", this.NewInitialZoomLevel, this.NewLastZoomLevel);
                this.db.SaveChanges();
            }
            else
            {

                settings = new Settings()
                {
                    Dimension = Dimension,
                    Profile = Profile,
                    Quality = FileQuality,
                    Format = FileFormat,
                    MaxZoom = this.NewInitialZoomLevel,
                    MinZoom = this.NewLastZoomLevel,
                    ChunksPerDimension = this.db.Settings.FirstOrDefault()?.ChunksPerDimension ?? this.ChunksPerDimension
                };
                this.db.Add(settings);
                this.db.SaveChanges();
            }

            this.renderedSubchunks = this.db.Checksums.Where(x => x.Profile == this.Profile).ToImmutableDictionary(
                x => new LevelDbWorldKey2(x.LevelDbKey), x => new KeyAndCrc(x.Id, x.Crc32));
            Console.WriteLine($"Found {this.renderedSubchunks.Count} subchunks which are already rendered");
        }

        public void Finish()
        {
            if (string.IsNullOrWhiteSpace(this.pathToDbUpdate))
                return;
            if (string.IsNullOrWhiteSpace(this.pathToDb))
                return;
            if (string.IsNullOrWhiteSpace(this.pathToDbBackup))
                return;

            this.db.Database.CloseConnection();
            this.db.Dispose();

            if (File.Exists(this.pathToDbUpdate))
            {
                if (File.Exists(this.pathToDbBackup))
                {
                    Console.WriteLine("Deleting old chunks.sqlite backup...");
                    File.Delete(this.pathToDbBackup);
                }

                if (File.Exists(this.pathToDb))
                {
                    Console.WriteLine("Creating new chunks.sqlite backup...");
                    File.Move(this.pathToDb, this.pathToDbBackup);
                }

                Console.WriteLine("Updating chunks.sqlite...");
                File.Move(this.pathToDbUpdate, this.pathToDb);
            }

            if (Directory.Exists(this.pathToMapUpdate))
            {
                var filesToCopy = Directory.EnumerateFiles(this.pathToMapUpdate, "*." + this.FileFormat, SearchOption.AllDirectories);
                Console.WriteLine($"Copying {filesToCopy.Count()} to {this.pathToMap}");
                foreach (var f in filesToCopy)
                {
                    var newPath = f.Replace(this.pathToMapUpdate, this.pathToMap);
                    FileInfo fi = new FileInfo(newPath);
                    fi.Directory?.Create();
                    File.Copy(f, newPath, true);
                }
            }
        }

        public Settings[] GetSettings()
        {
            return this.db.Settings.ToArray();
        }
    }
}

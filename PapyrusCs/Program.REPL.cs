using System;
using System.Diagnostics;
using System.Linq;
using Maploader.Core;
using Maploader.World;

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

            //Dump(world, new string[] { });

            Console.WriteLine();
            Console.WriteLine(world.WorldName + " ready");

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
                else if (command.StartsWith("dump", StringComparison.OrdinalIgnoreCase) && command.Length > 4)
                {
                    Dump(world, command.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    //FindPortals(world, "".Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            world.Close();

            if (Debugger.IsAttached)
            {
                Console.ReadKey();
            }

            return 0;
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
    }
}

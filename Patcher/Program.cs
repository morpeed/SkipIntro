using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Patcher
{
    public class Program
    {
        // Last updated July 9th, 2025
        const string unpatchedHash = "C682FFD429F26426077CD3A97CDE72692F0D9D05CDD8C50796F68C2A7F648771";
        const string patchedHash = "55843E87358D9ED5F5D476A2DC47581C2F3B697282B715981645EC8A7BADC580";

        static string GetHash(string input)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        }

        public static void Main(string[] args)
        {
            string path = string.Empty;
            bool hash = false;
            if (args.Length == 1 && File.Exists(args[0]))
            {
                path = args[0];
            }
            else
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();

                    if (arg == "-path" && i + 1 < args.Length)
                    {
                        path = args[i + 1];
                        Console.WriteLine($"Path set to: {path}");
                        i += 1;
                    }
                    else if (arg == "-hash")
                    {
                        hash = true;
                    }
                }

                if (path == string.Empty)
                {
                    Console.WriteLine("Please provide the path to the assembly to patch.");
                    path = Console.ReadLine();
                }
            }

            path ??= string.Empty;
            path = path.Replace("\"", "");

            if (File.Exists(path))
            {
                Console.WriteLine($"Patching {path}...");
                try
                {
                    var module = ModuleDefMD.Load(File.ReadAllBytes(path));
                    Console.WriteLine($"Loaded module: {module.Name}");

                    var main = module.Types.First(t => t.Name == "Main");
                    if (main == null)
                    {
                        Console.WriteLine("Main not found");
                        return;
                    }

                    var drawSplash = main.Methods.First(m => m.Name == "DrawSplash");
                    if (drawSplash == null)
                    {
                        Console.WriteLine("DrawSplash not found");
                        return;
                    }

                    if (drawSplash.HasBody)
                    {
                        string methodHash = GetHash(string.Concat(drawSplash.Body.Instructions));

                        if (hash)
                        {
                            Console.WriteLine(methodHash);
                            return;
                        }
                        else if (methodHash == patchedHash)
                        {
                            Console.WriteLine("This dll has already been patched.");
                            return;
                        }
                        else if (methodHash != unpatchedHash)
                        {
                            Console.WriteLine("This dll is invalid.");
                            return;
                        }

                        Instruction[] instructions =
                        [
                            Instruction.Create(OpCodes.Ldsfld, main.Fields.First(f => f.Name == "_isAsyncLoadComplete")),
                            Instruction.Create(OpCodes.Brfalse, drawSplash.Body.Instructions[52]),
                            Instruction.Create(OpCodes.Ldarg_0),
                            Instruction.Create(OpCodes.Call, main.Methods.First(m => m.Name == "Initialize_AlmostEverything")),
                            Instruction.Create(OpCodes.Call, main.Methods.First(m => m.Name == "PostContentLoadInitialize")),
                            Instruction.Create(OpCodes.Ldc_I4_0),
                            Instruction.Create(OpCodes.Stsfld, main.Fields.First(f => f.Name == "showSplash")),
                            Instruction.Create(OpCodes.Ldc_I4_0),
                            Instruction.Create(OpCodes.Stsfld, main.Fields.First(f => f.Name == "fadeCounter")),
                            Instruction.Create(OpCodes.Ldsfld, main.Fields.First(f => f.Name == "splashTimer")),
                            Instruction.Create(OpCodes.Callvirt, module.Import(typeof(Stopwatch).GetMethod("Stop"))),
                            Instruction.Create(OpCodes.Br, drawSplash.Body.Instructions[197])
                        ];

                        for (int i = 0; i < instructions.Length; i++)
                        {
                            drawSplash.Body.Instructions.Insert(52 + i, instructions[i]);
                        }

                        try
                        {
                            Console.WriteLine("Writing changes to file...");
                            module.Write(path);
                            Console.WriteLine("Changes written successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred, possibly incorrect file: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("File doesn't exist.");
            }
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using static System.Console;

namespace RESExt
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            string v = $"{version.Major}.{version.Minor}.{version.Build}";

            WriteLine($"### Resident Evil Survivor Extractor v{v} ###\n");

            if (args.Length < 2 || args.Length > 3)
            {
                ShowHelp();
                return;
            }

            switch (args[0])
            {
                case "-e":
                    Extract(args[1], args.Length == 2 ? null : args[2]);
                    break;

                case "-i":
                    Insert(args[1], args.Length == 2 ? null : args[2]);
                    break;

                default:
                    ShowHelp();
                    return;
            }
        }

        static void ShowHelp()
        {
            WriteLine("Usage:");
            WriteLine("  Extract: -e <SourceFile> [<OutputFolder>]");
            WriteLine("   Insert: -i <SourceFolder> [<OutputFile>]\n");
        }

        static void Extract(string sourceFile, string? outputFolder)
        {
            if (string.IsNullOrEmpty(sourceFile))
            {
                WriteLine("Error: SourceFile is not valid.");
                return;
            }

            if (!File.Exists(sourceFile))
            {
                WriteLine("Error: SourceFile does not exist.");
                return;
            }

            if (string.IsNullOrEmpty(outputFolder)) outputFolder = Path.ChangeExtension(sourceFile, null);
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            using (FileStream fs = File.OpenRead(sourceFile))
            using (BinaryReader br = new BinaryReader(fs))
            {
                uint fileCount = br.ReadUInt32();
                uint[] pointers = new uint[fileCount];

                for (int f = 0; f < fileCount; f++)
                {
                    pointers[f] = br.ReadUInt32();
                }

                for (int f = 0; f < fileCount; f++)
                {
                    uint length = f < fileCount - 1 ? pointers[f + 1] - pointers[f] : (uint)fs.Length - pointers[f];

                    fs.Position = pointers[f];

                    // Figure out if it's a TIM file

                    string extension = string.Empty;

                    uint b1 = br.ReadUInt32();
                    uint b2 = br.ReadUInt32();

                    if ((b1 == 0x00000010) && (b2 == 0x00000000 || b2 == 0x00000001 || b2 == 0x00000008 || b2 == 0x00000009))
                    {
                        extension = ".TIM";
                    }

                    fs.Position = pointers[f];

                    byte[] data = new byte[length];
                    int bytesRead = fs.Read(data, 0, (int)length);

                    if (bytesRead != length)
                    {
                        WriteLine("Error: Reached the end of file unexpectedly.");
                        return;
                    }

                    File.WriteAllBytes(Path.Combine(outputFolder, f.ToString("D8") + extension), data);
                }
            }
        }

        static void Insert(string sourceFolder, string? outputFile)
        {
            if (string.IsNullOrEmpty(sourceFolder))
            {
                WriteLine("Error: SourceFolder is not valid.");
                return;
            }

            if (!Directory.Exists(sourceFolder))
            {
                WriteLine("Error: SourceFolder does not exist.");
                return;
            }

            if (outputFile == null) outputFile = sourceFolder + ".BIN";
            string[] fileNames = Directory.GetFiles(sourceFolder);

            using (FileStream fs = File.Create(outputFile))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((uint)fileNames.Length);

                uint pointerPosition = 4;
                uint dataPosition = ((uint)fileNames.Length * 4) + 4;

                for (int n = 0; n < fileNames.Length; n++)
                {
                    fs.Position = pointerPosition;
                    bw.Write(dataPosition);
                    pointerPosition += 4;

                    fs.Position = dataPosition;

                    using (FileStream fs2 = File.OpenRead(fileNames[n]))
                    {
                        fs2.CopyTo(fs);
                        dataPosition += Pad((uint)fs2.Length, 4);
                    }
                }
            }
        }

        static uint Pad(uint value, uint paddingSize)
        {
            uint n = value % paddingSize;
            return n == 0 ? value : value + (paddingSize - n);
        }
    }
}
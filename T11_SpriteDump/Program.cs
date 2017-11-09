using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

using SevenZip.Compression.LZMA;

namespace T11_SpriteDump
{
	class Program
	{
		private static string _assetsPath;
		private static string _dumpToPath;
		
		private static MemoryStream _spriteBuffer;
		
		private static UInt32 _dumpedCount;
		
		
		[STAThread]
		public static void Main(string[] args)
		{
			_spriteBuffer = new MemoryStream(0x100000);
			
			FolderBrowserDialog folderDialog = new FolderBrowserDialog();
			
			_assetsPath = "";
			_dumpToPath = Environment.CurrentDirectory;
			
			folderDialog.RootFolder = Environment.SpecialFolder.Desktop;
			folderDialog.SelectedPath = _dumpToPath;
			
			Console.Write("Client Assets Path: ");
			if (folderDialog.ShowDialog() == DialogResult.OK)
			{
				_assetsPath = folderDialog.SelectedPath;
				if (_assetsPath.EndsWith("\\") == false) _assetsPath = _assetsPath + "\\";
			}
			Console.WriteLine(_assetsPath);
			
			Console.Write("Dump To Path: ");
			
			if (folderDialog.ShowDialog() == DialogResult.OK) _dumpToPath = folderDialog.SelectedPath;
			if (_dumpToPath.EndsWith("\\") == false) _dumpToPath = _dumpToPath + "\\";
			Console.WriteLine(_dumpToPath);
			
			if (_assetsPath == "" || File.Exists(_assetsPath + "catalog-content.json") == false)
			{
				Console.WriteLine("Error - Invalid assets path!");
			}
			else
			{
				Console.WriteLine("Press any key to dump sprite sheets, this may take a while...");
				Console.ReadKey(true);
				
				if (Directory.Exists(_dumpToPath) == false) Directory.CreateDirectory(_dumpToPath);
				
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();
				
				_dumpedCount = 0;
				
				// Read in each line of the catalog JSON file to an array so we can determine the sprite sheets to dump
				string[] assets = File.ReadAllLines(_assetsPath + "catalog-content.json");
				
				/*
					Instead of parsing the full file just loop through each line to find sprite definitions and parse out the
					desired fields (file, first ID, last ID) assuming they are stored in the following order:
					
						|  "type":"sprite",
						|  "file":"",
						|  "spritetype":0,
						|  "firstspriteid":0,
						|  "lastspriteid":0,
						|  "area":0
				*/
				Int32 count = assets.Length;
				for (Int32 index = 0; index < count; ++index)
				{
					if (assets[index] == "  \"type\":\"sprite\",")
					{
						
						string file = assets[++index];
						file = file.Substring(10, file.Length - 12);
						
						++index;
						
						string firsttID = assets[++index];
						firsttID = firsttID.Substring(18, firsttID.Length - 19);
						
						string lastID = assets[++index];
						lastID = lastID.Substring(17, lastID.Length - 18);
						
						DumpSpriteSheet(file, firsttID, lastID);
					}
				}
				
				stopwatch.Stop();
				Console.WriteLine("Dumped {0} sprite sheets in {1} minutes.", _dumpedCount, stopwatch.Elapsed.ToString(@"mm\:ss\:fff"));
			}
			
			Console.Write("Press any key to continue...");
			Console.ReadKey(true);
		}
		
		private static void DumpSpriteSheet(string file, string firstID, string lastID)
		{
			string filePath = String.Format("{0}{1}.lzma", _assetsPath, file);
			if (File.Exists(filePath) == false)
			{
				Console.WriteLine("Skipping '{0}', doesn't exist!", file);
				
				return;
			}
			
			Console.WriteLine("Dumping '{0}.lzma' to 'Sprites {1}-{2}.png'.", file, firstID, lastID);
			
			MemoryStream spriteBuffer = _spriteBuffer;
			
			Decoder decoder = new Decoder();
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
			{
				Stream input = reader.BaseStream;
				
				// CIP's header
				input.Position = 30; // Skip past 30 (value for tibia 11.49, 11.50) initial constant bytes
				while ((reader.ReadByte() & 0x80) == 0x80) { } // LZMA size, 7-bit integer where MSB = flag for next byte used
				
				// LZMA file
				decoder.SetDecoderProperties(reader.ReadBytes(5));
				reader.ReadUInt64();
				
				// Disabled arithmetic underflow/overflow check in debug mode so this won't cause an exception
				spriteBuffer.Position = 0;
				decoder.Code(input, spriteBuffer, input.Length - input.Position, 0x100000, null);
			}
			
			spriteBuffer.Position = 0;
			Image image = Image.FromStream(spriteBuffer);
			image.Save(String.Format("{0}Sprites {1}-{2}.png", _dumpToPath, firstID, lastID), ImageFormat.Png);
			
			++_dumpedCount;
		}
	}
}

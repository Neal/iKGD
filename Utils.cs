using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using RemoteZip;
using PlistCS;

namespace iKGD
{
	static class Utils
	{
		public static void ConsoleWrite(string str, ConsoleColor color)
		{
			ConsoleColor origColor = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.Write(str);
			Console.ForegroundColor = origColor;
		}
		public static void ConsoleWriteLine(string str, ConsoleColor color)
		{
			ConsoleWrite(str + "\n", color);
		}
		
		public static void Delay(double SecondsToDelay)
		{
			DateAndTime.Now.AddSeconds(1.1574074074074073E-05);
			DateTime time = DateAndTime.Now.AddSeconds(1.1574074074074073E-05).AddSeconds(SecondsToDelay);
			while (DateTime.Compare(DateAndTime.Now, time) <= 0) { }
		}

		public static void CheckResources()
		{
			FileIO.Directory_Create(iKGD.TempDir);
			FileIO.Directory_Create(iKGD.Resources);
			if (!FileIO.File_Exists(iKGD.Resources + "dmg.exe"))
			{
				FileIO.SaveResourceToDisk("Resources.zip", iKGD.TempDir + "Resources.zip");
				UnzipAll(iKGD.TempDir + "Resources.zip", iKGD.Resources);
			}
			FileIO.Directory_Delete(iKGD.IPSWdir);
			FileIO.File_Delete(iKGD.TempDir + "Resources.zip");
		}

		public static void PwnDevice(string board)
		{
			Console.Write("Exploiting device with limera1n...");
			irecovery("-e");
			Delay(1);
			if (!FileIO.File_Exists(iKGD.TempDir + board + ".iBSS"))
			{
				Console.Write("\nDownloading iBSS for " + board + "...");
				Remote.DownloadImage("iBSS", board, iKGD.TempDir + board + ".iBSS");
			}
			Console.Write("\nUploading iBSS...");
			irecovery_file(iKGD.TempDir + board + ".iBSS");
			Console.Write("\nWaiting for iBSS...");
			while (!SearchDeviceInMode("iBoot")) { }; Delay(1);
			Console.Write("\nUploading iKGD iBSS payload...");
			irecovery_file(iKGD.Resources + board + ".ibss.payload");
			Console.WriteLine("\nExecuting iKGD iBSS payload...");
			irecovery_cmd("go");
			irecovery_cmd("go fbclear");
			irecovery_cmd("go nvram set iKGD true");
			irecovery_file(iKGD.Resources + "applelogo.img3");
			irecovery_cmd("setpicture 0");
			irecovery_cmd("bgcolor 0 0 0");
		}

		public static bool DeviceIsCompatible(string board)
		{
			switch (board)
			{
				case "n72ap":
				case "n18ap":
				case "n81ap":
				case "n88ap":
				case "n90ap":
				case "k48ap":
				case "k66ap":
					return true;
				default:
					return false;
			}
		}

		public static bool SearchDeviceInMode(string mode, bool libirecovery = false)
		{
			string str = "";
			if (!libirecovery)
			{
				ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity WHERE Description = 'Apple Recovery (" + mode + ") USB Driver'");
				foreach (ManagementObject obj in searcher.Get())
				{
					str += obj["Description"];
				}
				return str.Contains(mode);
			}
			else
			{
				str = ExecuteCommandAndGetOutput(iKGD.Resources + "irecovery.exe -find");
				return !str.Contains("No device") && str.Contains(mode);
			}
		}

		public static string dmg_extract(string _in, string _out, string key)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "dmg.exe extract \"" + _in + "\" \"" + _out + "\" -k \"" + key + "\"");
		}
		public static string genpass(string platform, string ramdisk, string filesystem)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "genpass.exe -p \"" + platform + "\" -r \"" + ramdisk + "\" -f \"" + filesystem + "\"");
		}
		public static string hfsplus_extractall(string image, string path, string dest)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "hfsplus.exe \"" + image + "\" extractall \"" + path + "\" \"" + dest + "\"");
		}
		public static string irecovery(string cmd)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "irecovery.exe " + cmd);
		}
		public static string irecovery_cmd(string cmd)
		{
			return irecovery("-c \"" + cmd + "\"");
		}
		public static string irecovery_file(string file)
		{
			return irecovery("-f \"" + file + "\"");
		}
		public static string irecovery_getenv(string var)
		{
			return irecovery("-g \"" + var + "\"");
		}
		public static string irecovery_fbecho(string str)
		{
			return irecovery("-c \"go fbecho " + str + "\"");
		}
		public static string xpwntool(string infile, string outfile)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "xpwntool.exe \"" + infile + "\" \"" + outfile + "\"");
		}
		public static string xpwntool(string infile, string outfile, string iv, string key)
		{
			return ExecuteCommandAndGetOutput(iKGD.Resources + "xpwntool.exe \"" + infile + "\" \"" + outfile + "\" -iv " + iv + " -k " + key);
		}

		public static void UnzipFile(string ZipFile, string TargetDir, string FileInZip, ulong BytesToExtract)
		{
			try
			{
				ZipStorer zip = ZipStorer.Open(ZipFile, FileAccess.Read);
				foreach (ZipStorer.ZipFileEntry entry in (List<ZipStorer.ZipFileEntry>)zip.ReadCentralDir())
				{
					if (entry.FilenameInZip == FileInZip)
					{
						zip.ExtractFile(entry, TargetDir + Path.GetFileName(FileInZip), BytesToExtract);
					}
				}
				zip.Close();
			}
			catch (Exception) { }
		}
		public static void UnzipFile(string ZipFile, string TargetDir, string FileInZip)
		{
			UnzipFile(ZipFile, TargetDir, FileInZip, 0);
		}
		public static void UnzipAll(string ZipFile, string TargetDir)
		{
			try
			{
				ZipStorer zip = ZipStorer.Open(ZipFile, FileAccess.Read);
				foreach (ZipStorer.ZipFileEntry entry in (List<ZipStorer.ZipFileEntry>)zip.ReadCentralDir())
				{
					zip.ExtractFile(entry, TargetDir + Path.GetFileName(entry.FilenameInZip));
				}
				zip.Close();
			}
			catch (Exception) { }
		}

		public static bool SearchAndReplace(string file, string searchFor, string replaceWith)
		{
			try
			{
				StreamReader reader = new StreamReader(file);
				string contents = reader.ReadToEnd();
				reader.Close();
				reader.Dispose();
				contents = System.Text.RegularExpressions.Regex.Replace(contents, searchFor, replaceWith);
				StreamWriter writer = new StreamWriter(file);
				writer.Write(contents);
				writer.Close();
				writer.Dispose();
				return true;
			}
			catch (Exception) { }
			return false;
		}

		public static string GetFirmwareURL(string device, string firmwarebuild)
		{
			try
			{
				string url = "http://api.ios.icj.me/v2/DEVICE/FWBUILD/url/dl".Replace("DEVICE", device).Replace("FWBUILD", firmwarebuild);
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
				request.Timeout = 5000;
				request.Method = "HEAD";
				request.UserAgent = "iKGD/" + iKGD.Version;
				return request.GetResponse().ResponseUri.ToString();
			}
			catch (Exception) { }
			return "";
		}

		public static string ParseBuildManifestInfo(string key, string BuildManifestPath)
		{
			try
			{
				Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist(BuildManifestPath);
				Dictionary<string, object> BuildIdentities = (Dictionary<string, object>)((List<object>)dict["BuildIdentities"])[0];
				Dictionary<string, object> Info = (Dictionary<string, object>)BuildIdentities["Info"];
				return Info[key].ToString();
			}
			catch (Exception) { }
			return "";
		}
		public static string GetImagePathFromBuildManifest(string image, string BuildManifestPath)
		{
			try
			{
				Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist(BuildManifestPath);
				Dictionary<string, object> BuildIdentities = (Dictionary<string, object>)((List<object>)dict["BuildIdentities"])[0];
				Dictionary<string, object> Manifest = (Dictionary<string, object>)BuildIdentities["Manifest"];
				if (Manifest.ContainsKey(image))
					return (string)((Dictionary<string, object>)((Dictionary<string, object>)Manifest[image])["Info"])["Path"];
			}
			catch (Exception) { }
			return "";
		}
		public static void ParseRestorePlist(string RestorePlistPath)
		{
			try
			{
				Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist(RestorePlistPath);
				Dictionary<string, object> DeviceMap = (Dictionary<string, object>)((List<object>)dict["DeviceMap"])[0];
				Dictionary<string, object> RestoreRamDisks = (Dictionary<string, object>)dict["RestoreRamDisks"];
				Dictionary<string, object> SystemRestoreImages = (Dictionary<string, object>)dict["SystemRestoreImages"];
				iKGD.Device = GetValueforKeyFromDict(dict, "ProductType");
				iKGD.Firmware = GetValueforKeyFromDict(dict, "ProductVersion");
				iKGD.BuildID = GetValueforKeyFromDict(dict, "ProductBuildVersion");
				iKGD.Platform = GetValueforKeyFromDict(DeviceMap, "Platform");
				iKGD.BoardConfig = GetValueforKeyFromDict(DeviceMap, "BoardConfig");
				iKGD.RootFileSystem = GetValueforKeyFromDict(SystemRestoreImages, "User");
				iKGD.RestoreRamdisk = GetValueforKeyFromDict(RestoreRamDisks, "User");
				iKGD.UpdateRamdisk = GetValueforKeyFromDict(RestoreRamDisks, "Update");
			}
			catch (Exception) { }
		}

		public static string GetValueforKeyFromDict(Dictionary<string, object> dict, string key)
		{
			return dict.ContainsKey(key) ? (string)dict[key] : "";
		}

		public static string ExecuteCommandAndGetOutput(string command)
		{
			try
			{
				ProcessStartInfo info = new ProcessStartInfo("cmd", "/c " + command)
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardOutput = true
				};
				Process process = new Process { StartInfo = info };
				process.Start();
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return output;
			}
			catch (Exception) { }
			return "";
		}
		public static void ExecuteCommand(object command)
		{
			ExecuteCommandAndGetOutput((string)command);
		}
		public static void ExecuteCommandAsync(string command)
		{
			try
			{
				Thread objThread = new Thread(new ParameterizedThreadStart(ExecuteCommand));
				objThread.Priority = ThreadPriority.AboveNormal;
				objThread.IsBackground = true;
				objThread.Start(command);
			}
			catch (ThreadStartException) { }
			catch (ThreadAbortException) { }
			catch (Exception) { }
		}

		public static void MakeTheiPhoneWikiFile(string fileLocation)
		{
			using (StreamWriter writer = new StreamWriter(fileLocation))
			{
				writer.WriteLine("{{keys");
				writer.WriteLine(" | version             = " + iKGD.Firmware);
				writer.WriteLine(" | build               = " + iKGD.BuildID);
				writer.WriteLine(" | device              = " + iKGD.Device.ToLower().Replace(@",", ""));
				writer.WriteLine(" | codename            = " + iKGD.Codename);
				if (iKGD.BasebandExists)
					writer.WriteLine(" | baseband            = " + iKGD.Baseband);
				if (!string.IsNullOrEmpty(iKGD.DownloadURL.Trim()))
					writer.WriteLine(" | downloadurl         = " + iKGD.DownloadURL);
				writer.WriteLine();
				writer.WriteLine(" | rootfsdmg           = " + iKGD.RootFileSystem.Replace(".dmg", ""));
				writer.WriteLine(" | rootfskey           = " + iKGD.VFDecryptKey);
				if (!iKGD.UpdateRamdiskExists)
					writer.WriteLine("\n | noupdateramdisk     = true");
				if (!iKGD.UpdateRamdiskIsEncrypted)
				{
					if (iKGD.UpdateRamdiskExists) writer.WriteLine();
					writer.WriteLine(" | ramdisknotencrypted = true");
				}
				writer.WriteLine();
				if (iKGD.UpdateRamdiskExists)
				{
					writer.WriteLine(" | updatedmg           = " + iKGD.UpdateRamdisk.Replace(".dmg", ""));
					if (iKGD.UpdateRamdiskIsEncrypted)
					{
						writer.WriteLine(" | updateiv            = " + iKGD.iv[0]);
						writer.WriteLine(" | updatekey           = " + iKGD.key[0]);
					}
					writer.WriteLine();
				}
				writer.WriteLine(" | restoredmg          = " + iKGD.RestoreRamdisk.Replace(".dmg", ""));
				if (iKGD.RestoreRamdiskIsEncrypted)
				{
					writer.WriteLine(" | restoreiv           = " + iKGD.iv[1]);
					writer.WriteLine(" | restorekey          = " + iKGD.key[1]);
				}
				for (int i = 2; i < iKGD.images.Length; i++)
				{
					writer.WriteLine();
					string str = iKGD.images[i] + "IV";
					for (int j = 0; j < 20 - str.Length + j; j++)
					{
						str = str.Insert(str.Length, " ");
					}
					writer.WriteLine(" | " + str + "= " + iKGD.iv[i]);
					writer.WriteLine(" | " + str.Replace("IV ", "Key") + "= " + iKGD.key[i]);
				}
				writer.WriteLine("}}");
				writer.WriteLine();
			}
			SearchAndReplace(fileLocation, "BatteryChargingIV   =", "GlyphChargingIV     =");
			SearchAndReplace(fileLocation, "BatteryChargingKey  =", "GlyphChargingKey    =");
			SearchAndReplace(fileLocation, "BatteryPluginIV     =", "GlyphPluginIV       =");
			SearchAndReplace(fileLocation, "BatteryPluginKey    =", "GlyphPluginKey      =");
			SearchAndReplace(fileLocation, "KernelCacheIV       =", "KernelcacheIV       =");
			SearchAndReplace(fileLocation, "KernelCacheKey      =", "KernelcacheKey      =");
		}

		public static void MakeOpensn0wPlist(string fileLocation)
		{
			Dictionary<string, object> dict = new Dictionary<string, object>();
			Dictionary<string, object> FirmwareKeys = new Dictionary<string, object>();
			Dictionary<string, object> FirmwareInfo = new Dictionary<string, object>();
			FirmwareKeys.Add("iBSS", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBSS", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[13] }, { "Key", iKGD.key[13] } });
			FirmwareKeys.Add("DeviceTree", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("DeviceTree", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[8] }, { "Key", iKGD.key[8] } });
			FirmwareKeys.Add("BatteryCharging1", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryCharging1", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[4] }, { "Key", iKGD.key[4] } });
			FirmwareKeys.Add("GlyphCharging", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("GlyphCharging", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[9] }, { "Key", iKGD.key[9] } });
			FirmwareKeys.Add("iBoot", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBoot", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[12] }, { "Key", iKGD.key[12] } });
			FirmwareKeys.Add("BatteryCharging0", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryCharging0", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[3] }, { "Key", iKGD.key[3] } });
			FirmwareKeys.Add("BatteryLow0", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryLow0", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[6] }, { "Key", iKGD.key[6] } });
			FirmwareKeys.Add("LLB", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("LLB", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[16] }, { "Key", iKGD.key[16] } });
			FirmwareKeys.Add("iBEC", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBEC", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[11] }, { "Key", iKGD.key[11] } });
			FirmwareKeys.Add("KernelCache", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("KernelCache", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[14] }, { "Key", iKGD.key[14] } });
			FirmwareKeys.Add("FileSystem", new Dictionary<string, object> { { "VFDecryptKey", iKGD.VFDecryptKey }, { "FileName", GetImagePathFromBuildManifest("OS", iKGD.IPSWdir + "BuildManifest.plist") } });
			FirmwareKeys.Add("AppleLogo", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("AppleLogo", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[2] }, { "Key", iKGD.key[2] } });
			if (iKGD.UpdateRamdiskExists)
				FirmwareKeys.Add("UpdateRamdisk", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("UpdateRamdisk", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[0] }, { "Key", iKGD.key[0] } });
			FirmwareKeys.Add("RestoreRamdisk", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("RestoreRamdisk", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[1] }, { "Key", iKGD.key[1] } });
			FirmwareKeys.Add("GlyphPlugin", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("GlyphPlugin", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[10] }, { "Key", iKGD.key[10] } });
			FirmwareKeys.Add("BatteryLow1", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryLow1", iKGD.IPSWdir + "BuildManifest.plist") }, { "IV", iKGD.iv[7] }, { "Key", iKGD.key[7] } });
			FirmwareInfo.Add("URL", iKGD.DownloadURL);
			FirmwareInfo.Add("Version", iKGD.Firmware);
			dict.Add("FirmwareKeys", FirmwareKeys);
			dict.Add("FirmwareInfo", FirmwareInfo);
			Plist.writeXml(dict, fileLocation);
		}

		public static long GetFileSizeOnDisk(string file)
		{
			FileInfo info = new FileInfo(file);
			uint dummy, sectorsPerCluster, bytesPerSector;
			int result = GetDiskFreeSpaceW(info.Directory.Root.FullName, out sectorsPerCluster, out bytesPerSector, out dummy, out dummy);
			if (result == 0) throw new Win32Exception();
			uint clusterSize = sectorsPerCluster * bytesPerSector;
			uint hosize;
			uint losize = GetCompressedFileSizeW(file, out hosize);
			long size;
			size = (long)hosize << 32 | losize;
			return ((size + clusterSize - 1) / clusterSize) * clusterSize;
		}

		[DllImport("kernel32.dll")]
		static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
		   [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

		[DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
		static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
		   out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
		   out uint lpTotalNumberOfClusters);

	}
}

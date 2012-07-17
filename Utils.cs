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
				FileIO.Directory_Delete(iKGD.Resources);
				FileIO.SaveResourceToDisk("Resources.zip", iKGD.TempDir + "Resources.zip");
				UnzipAll(iKGD.TempDir + "Resources.zip", iKGD.Resources);
				FileIO.File_Delete(iKGD.TempDir + "Resources.zip");
			}
			FileIO.Directory_Delete(iKGD.IPSWdir);
		}

		public static void PwnDevice(string board)
		{
			string iBSS = iKGD.TempDir + board + ".iBSS";
			Console.Write("Exploiting device with {0}...", (iKGD.Platform.Contains("8720")) ? "steaks4uce" : "limera1n");
			irecovery("-e");
			if (!FileIO.File_Exists(iBSS))
			{
				Console.Write("\nDownloading iBSS for " + board + "...");
				Remote.DownloadImage("iBSS", board, iBSS);
				if (!FileIO.File_Exists(iBSS))
				{
					Console.Write("\nERROR: Unable to download iBSS.");
					Environment.Exit((int)iKGD.ExitCode.NoInternetConnection);
				}
			}
			else Delay(1);
			Console.Write("\nUploading iBSS...");
			irecovery_file(iBSS);
			Console.Write("\nWaiting for iBSS...");
			while (!SearchDeviceInMode("iBoot")) { };
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

		public static string GetTheiPhoneWikiDeviceName(string board)
		{
			switch (board)
			{
				case "n72ap": return "iPod_touch_2G";
				case "n18ap": return "iPod_touch_3G";
				case "n81ap": return "iPod_touch_4G";
				case "n88ap": return "iPhone_3GS";
				case "n90ap": return "iPhone_4";
				case "n92ap": return "iPhone_4_CDMA";
				case "k48ap": return "iPad";
				case "k66ap": return "Apple_TV_2G";
				default:
					return "";
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
						break;
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

		public static bool HasInternetConnection()
		{
			try
			{
				using (var client = new WebClient())
				using (var stream = client.OpenRead("http://www.google.com"))
				{
					return true;
				}
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
				request.Timeout = 4000;
				request.Method = "HEAD";
				request.UserAgent = "iKGD/" + iKGD.Version;
				return request.GetResponse().ResponseUri.ToString();
			}
			catch (Exception) { }
			return "TODO";
		}

		public static string ParseBuildManifestInfo(string Key)
		{
			try
			{
				Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist(iKGD.IPSWdir + "BuildManifest.plist");
				Dictionary<string, object> BuildIdentities = (Dictionary<string, object>)((List<object>)dict["BuildIdentities"])[0];
				Dictionary<string, object> Info = (Dictionary<string, object>)BuildIdentities["Info"];
				return Info[Key].ToString();
			}
			catch (Exception) { }
			return "";
		}

		public static string GetImagePathFromBuildManifest(string image)
		{
			try
			{
				Dictionary<string, object> dict = (Dictionary<string, object>)Plist.readPlist(iKGD.IPSWdir + "BuildManifest.plist");
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
				iKGD.Device = GetValueByKey(dict, "ProductType");
				iKGD.Firmware = GetValueByKey(dict, "ProductVersion");
				iKGD.BuildID = GetValueByKey(dict, "ProductBuildVersion");
				iKGD.Platform = GetValueByKey(DeviceMap, "Platform");
				iKGD.BoardConfig = GetValueByKey(DeviceMap, "BoardConfig");
				iKGD.UpdateRamdisk = GetValueByKey(RestoreRamDisks, "Update");
				iKGD.RestoreRamdisk = GetValueByKey(RestoreRamDisks, "User");
				iKGD.RootFileSystem = GetValueByKey(SystemRestoreImages, "User");
				iKGD.Codename = ParseBuildManifestInfo("BuildTrain");
			}
			catch (Exception) { }
		}

		public static string GetValueByKey(Dictionary<string, object> dict, string key)
		{
			return dict.ContainsKey(key) ? (string)dict[key] : "";
		}

		public static void MakeKeysFileForTheiPhoneWiki(string fileLocation)
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
					writer.WriteLine(writer.NewLine + " | noupdateramdisk     = true");
				if (!iKGD.RestoreRamdiskIsEncrypted)
					writer.WriteLine(writer.NewLine + " | ramdisknotencrypted = true");
				if (iKGD.UpdateRamdiskExists)
				{
					writer.WriteLine(writer.NewLine + " | updatedmg           = " + iKGD.UpdateRamdisk.Replace(".dmg", ""));
					if (iKGD.UpdateRamdiskIsEncrypted)
					{
						writer.WriteLine(" | updateiv            = " + iKGD.iv[(int)iKGD.FirmwareItems.UpdateRamdisk]);
						writer.WriteLine(" | updatekey           = " + iKGD.key[(int)iKGD.FirmwareItems.UpdateRamdisk]);
					}
				}
				writer.WriteLine(writer.NewLine + " | restoredmg          = " + iKGD.RestoreRamdisk.Replace(".dmg", ""));
				if (iKGD.RestoreRamdiskIsEncrypted)
				{
					writer.WriteLine(" | restoreiv           = " + iKGD.iv[(int)iKGD.FirmwareItems.RestoreRamdisk]);
					writer.WriteLine(" | restorekey          = " + iKGD.key[(int)iKGD.FirmwareItems.RestoreRamdisk]);
				}
				for (int i = (int)iKGD.FirmwareItems.AppleLogo; i < iKGD.TotalFirmwareItems; i++)
				{
					writer.WriteLine();
					string str = (iKGD.FirmwareItem[i] + "IV").Replace("BatteryChargingIV", "GlyphChargingIV").Replace("BatteryPluginIV", "GlyphPluginIV").Replace("KernelCacheIV", "KernelcacheIV");
					for (int j = 0; j < 20 - str.Length + j; j++)
						str = str.Insert(str.Length, " ");
					writer.WriteLine(" | " + str + "= " + iKGD.iv[i]);
					writer.WriteLine(" | " + str.Replace("IV ", "Key") + "= " + iKGD.key[i]);
				}
				writer.WriteLine("}}");
			}
		}

		public static void MakeKeysFileForOpensn0w(string fileLocation)
		{
			Dictionary<string, object> dict = new Dictionary<string, object>();
			Dictionary<string, object> FirmwareKeys = new Dictionary<string, object>();
			Dictionary<string, object> FirmwareInfo = new Dictionary<string, object>();
			FirmwareKeys.Add("iBSS", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBSS") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.iBSS] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.iBSS] } });
			FirmwareKeys.Add("DeviceTree", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("DeviceTree") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.DeviceTree] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.DeviceTree] } });
			FirmwareKeys.Add("BatteryCharging1", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryCharging1") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryCharging1] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryCharging1] } });
			FirmwareKeys.Add("GlyphCharging", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("GlyphCharging") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryCharging] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryCharging] } });
			FirmwareKeys.Add("iBoot", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBoot") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.iBoot] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.iBoot] } });
			FirmwareKeys.Add("BatteryCharging0", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryCharging0") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryCharging0] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryCharging0] } });
			FirmwareKeys.Add("BatteryLow0", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryLow0") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryLow0] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryLow0] } });
			FirmwareKeys.Add("LLB", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("LLB") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.LLB] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.LLB] } });
			FirmwareKeys.Add("iBEC", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("iBEC") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.iBEC] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.iBEC] } });
			FirmwareKeys.Add("KernelCache", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("KernelCache") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.KernelCache] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.KernelCache] } });
			FirmwareKeys.Add("FileSystem", new Dictionary<string, object> { { "VFDecryptKey", iKGD.VFDecryptKey }, { "FileName", GetImagePathFromBuildManifest("OS") } });
			FirmwareKeys.Add("AppleLogo", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("AppleLogo") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.AppleLogo] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.AppleLogo] } });
			if (iKGD.UpdateRamdiskIsEncrypted && iKGD.UpdateRamdiskExists)
				FirmwareKeys.Add("UpdateRamdisk", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("UpdateRamdisk") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.UpdateRamdisk] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.UpdateRamdisk] } });
			if (iKGD.RestoreRamdiskIsEncrypted && iKGD.RestoreRamdiskExists)
				FirmwareKeys.Add("RestoreRamdisk", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("RestoreRamdisk") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.RestoreRamdisk] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.RestoreRamdisk] } });
			FirmwareKeys.Add("GlyphPlugin", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("GlyphPlugin") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryPlugin] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryPlugin] } });
			FirmwareKeys.Add("BatteryLow1", new Dictionary<string, object> { { "FileName", GetImagePathFromBuildManifest("BatteryLow1") }, { "IV", iKGD.iv[(int)iKGD.FirmwareItems.BatteryLow1] }, { "Key", iKGD.key[(int)iKGD.FirmwareItems.BatteryLow1] } });
			if (!string.IsNullOrEmpty(iKGD.DownloadURL))
				FirmwareInfo.Add("URL", iKGD.DownloadURL);
			FirmwareInfo.Add("Version", iKGD.Firmware);
			dict.Add("FirmwareKeys", FirmwareKeys);
			dict.Add("FirmwareInfo", FirmwareInfo);
			Plist.writeXml(dict, fileLocation);
		}

		public static void MakeKeysFileForPastie(string fileLocation)
		{
			using (StreamWriter writer = new StreamWriter(fileLocation))
			{
				writer.WriteLine("## iOS {0} ({1}) keys/ivs for {2}.  [plain_text]", iKGD.Firmware, iKGD.BuildID, iKGD.Device);
				writer.WriteLine();
				writer.WriteLine("------------------------------");
				writer.WriteLine("{0} [RootFS]", iKGD.RootFileSystem);
				writer.WriteLine("------------------------------");
				writer.WriteLine("VFDecryptKey: " + iKGD.VFDecryptKey);
				writer.WriteLine("------------------------------");
				if (iKGD.UpdateRamdiskIsEncrypted)
				{
					writer.WriteLine("{0} [UpdateRD]", iKGD.UpdateRamdisk);
					writer.WriteLine("------------------------------");
					writer.WriteLine("IV: " + iKGD.iv[(int)iKGD.FirmwareItems.UpdateRamdisk]);
					writer.WriteLine("Key: " + iKGD.key[(int)iKGD.FirmwareItems.UpdateRamdisk]);
					writer.WriteLine("------------------------------");
				}
				if (iKGD.RestoreRamdiskIsEncrypted)
				{
					writer.WriteLine("{0} [RestoreRD]", iKGD.RestoreRamdisk);
					writer.WriteLine("------------------------------");
					writer.WriteLine("IV: " + iKGD.iv[(int)iKGD.FirmwareItems.RestoreRamdisk]);
					writer.WriteLine("Key: " + iKGD.key[(int)iKGD.FirmwareItems.RestoreRamdisk]);
					writer.WriteLine("------------------------------");
				}
				for (int i = (int)iKGD.FirmwareItems.AppleLogo; i < iKGD.TotalFirmwareItems; i++)
				{
					writer.WriteLine(iKGD.FirmwareItem[i]);
					writer.WriteLine("------------------------------");
					writer.WriteLine("IV: " + iKGD.iv[i]);
					writer.WriteLine("Key: " + iKGD.key[i]);
					writer.WriteLine("------------------------------");
				}
				writer.WriteLine();
			}
		}

		public static void SetClipboardDataObject(object data)
		{
			for (int i = 0; i < 10; i++)
			{
				try
				{
					System.Windows.Forms.Clipboard.SetDataObject(data);
					return;
				}
				catch { }
				System.Threading.Thread.Sleep(100);
			}
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
		static extern void Sleep(double milliseconds);

		[DllImport("kernel32.dll")]
		static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
		   [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

		[DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
		static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
		   out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
		   out uint lpTotalNumberOfClusters);

	}
}

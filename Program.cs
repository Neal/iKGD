using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using RemoteZip;
using PlistCS;
using XGetoptCS;

namespace iKGD
{
	internal sealed class iKGD
	{
		public static string Version = "1.0";
		public static string TempDir = Path.GetTempPath() + @"iKGD\";
		public static string IPSWdir = TempDir + @"IPSW\";
		public static string Resources = TempDir + @"Resources\";
		public static string KeysDir = @"C:\IPSW\Keys\";
		public static string CurrentProcessName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
		public static string DropboxHostDBFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox\\host.db");
		public static string DropboxDir = (FileIO.File_Exists(DropboxHostDBFilePath)) ? ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(
			File.ReadAllLines(DropboxHostDBFilePath)[1])) + "\\" : Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\iKGD\\";
		public static string RemoteFileLocation = DropboxDir + "share\\";

		public static bool RunningRemotelyServer = false, RunningRemotelyHome = false, KeepRemoteFiles = false, FirmwareIsBeta = false;
		public static bool RebootDevice = true;
		public static bool Verbose = false;

		public static string IPSWLocation = "", IPSWurl = "", ReqDevice = "", ReqFirmware = "", RootFileSystem = "", RestoreRamdisk = "", UpdateRamdisk = "";
		public static string DecryptedRootFS = "DEC-RootFS.dmg", DecryptedRestoreRamdisk = "DEC-RestoreRD.dmg", DecryptedUpdateRamdisk = "DEC-UpdateRD.dmg";
		public static bool RestoreRamdiskIsEncrypted, RestoreRamdiskExists, UpdateRamdiskIsEncrypted, UpdateRamdiskExists, ExtractFullRootFS = false;

		public static string Device, Firmware, BuildID, Codename, Platform, BoardConfig, PluggedInDevice, VFDecryptKey, DownloadURL = "", Baseband = "";
		public static bool BasebandExists = false;

		public enum FirmwareItems : int
		{
			UpdateRamdisk = 0,
			RestoreRamdisk = 1,
			AppleLogo = 2,
			BatteryCharging0 = 3,
			BatteryCharging1 = 4,
			BatteryFull = 5,
			BatteryLow0 = 6,
			BatteryLow1 = 7,
			DeviceTree = 8,
			BatteryCharging = 9,
			BatteryPlugin = 10,
			iBEC = 11,
			iBoot = 12,
			iBSS = 13,
			KernelCache = 14,
			LLB = 15,
			RecoveryMode = 16
		}

		public static int TotalFirmwareItems = Enum.GetValues(typeof(FirmwareItems)).Length;
		public static string[] FirmwareItem = Enum.GetNames(typeof(FirmwareItems));
		public static string[] kbag = new string[TotalFirmwareItems];
		public static string[] iv = new string[TotalFirmwareItems];
		public static string[] key = new string[TotalFirmwareItems];

		static void Main(string[] args)
		{
			Console.WriteLine("\nInitializing iKGD v" + Version);

			char c;
			XGetopt g = new XGetopt();
			while ((c = g.Getopt(args.Length, args, "evi:u:d:f:rSHK")) != '\0')
			{
				switch (c)
				{
					case 'i': IPSWLocation = g.Optarg; break;
					case 'u': IPSWurl = g.Optarg; break;
					case 'd': ReqDevice = g.Optarg; break;
					case 'f': ReqFirmware = g.Optarg; break;
					case 'k': KeysDir = g.Optarg; break;
					case 'r': RebootDevice = false; break;
					case 'e': ExtractFullRootFS = true; break;
					case 'S': RunningRemotelyServer = true; break;
					case 'H': RunningRemotelyHome = true; break;
					case 'R': RemoteFileLocation = g.Optarg; break;
					case 'K': KeepRemoteFiles = true; break;
					case 'v': Verbose = true; break;
				}
			}


			if (RunningRemotelyHome || RunningRemotelyServer)
			{
				if (!FileIO.Directory_Exists(RemoteFileLocation))
				{
					Console.WriteLine("Directory {0} does not exist!\nUse -R to manually specify a different location.", RemoteFileLocation);
					Environment.Exit((int)ExitCode.InvalidRemoteFileLocation);
				}
				if (RunningRemotelyHome) RemoteModeHome();
			}
			if (!string.IsNullOrEmpty(IPSWLocation))
			{
				if (!FileIO.File_Exists(IPSWLocation))
				{
					Console.WriteLine("File {0} does not exist!", IPSWLocation);
					Environment.Exit((int)ExitCode.InvalidIPSWLocation);
				}
			}
			else if (!string.IsNullOrEmpty(IPSWurl))
			{
				ExtractFullRootFS = false;
				if (!Remote.isURLaFirmware(IPSWurl))
				{
					Console.WriteLine("The url specified is not a valid iOS firmware.");
					Environment.Exit((int)ExitCode.URLisNotFirmware);
				}
			}
			else if (!string.IsNullOrEmpty(ReqDevice) && !string.IsNullOrEmpty(ReqFirmware))
			{
				Console.Write("Fetching link...");
				IPSWurl = Utils.GetFirmwareURL(ReqDevice, ReqFirmware);
				if (!Remote.isURLaFirmware(IPSWurl))
				{
					Console.WriteLine("\tInvalid URL!\n\nPlease double check if the specified firmware exists for the device.");
					Environment.Exit((int)ExitCode.InvalidURL);
				}
				Console.WriteLine();
			}
			else
			{
				PrintUsage(CurrentProcessName);
			}

			Console.WriteLine("Checking resources...");
			Utils.CheckResources();

			Stopwatch timer = new Stopwatch();
			timer.Start();

			if (!string.IsNullOrEmpty(IPSWLocation))
				ExtractIPSW();

			if (!string.IsNullOrEmpty(IPSWurl))
				DownloadIPSW();

			CheckRamdisks();

			GrabKBAGS();

			if (RunningRemotelyServer)
			{
				RemoteModeServer();
			}
			else
			{
				MakeDeviceReady();
				GrabKeys();
				if (RebootDevice) Utils.irecovery("-kick");
			}

			DecryptRamdisks();

			GetVFDecryptKey();

			GetBaseband();

			FetchFirmwareURL();

			MakeFilesForKeys();

			CopyKeysToKeysDir();

			DecryptRootFS();

			timer.Stop();
			Console.WriteLine("Elapsed for {0} seconds", (double) timer.ElapsedMilliseconds / 1000);

			Environment.Exit((int)ExitCode.Success);
		}

		public static void ExtractIPSW()
		{
			Console.WriteLine("Firmware: " + Path.GetFileNameWithoutExtension(IPSWLocation));
			Console.Write("Extracting essential files from zip...");
			Utils.UnzipFile(IPSWLocation, IPSWdir, "Restore.plist");
			Utils.UnzipFile(IPSWLocation, IPSWdir, "BuildManifest.plist");
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			Console.Write("Parsing Restore.plist...");
			Utils.ParseRestorePlist(IPSWdir + "Restore.plist");
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			Console.Write("Extracting images...");
			for (int i = (int)FirmwareItems.AppleLogo; i < TotalFirmwareItems; i++)
			{
				Utils.UnzipFile(IPSWLocation, IPSWdir, Utils.GetImagePathFromBuildManifest(FirmwareItem[i], IPSWdir + "BuildManifest.plist"));
			}
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			Console.Write("Extracting ramdisks and root filesystem...");
			if (ExtractFullRootFS) Utils.UnzipFile(IPSWLocation, IPSWdir, RootFileSystem);
			if (!ExtractFullRootFS) Utils.UnzipFile(IPSWLocation, IPSWdir, RootFileSystem, 122880);
			Utils.UnzipFile(IPSWLocation, IPSWdir, UpdateRamdisk);
			Utils.UnzipFile(IPSWLocation, IPSWdir, RestoreRamdisk);
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
		}

		public static void DownloadIPSW()
		{
			try
			{
				FileIO.Directory_Create(IPSWdir);
				Console.WriteLine("Firmware: " + Path.GetFileNameWithoutExtension(IPSWurl));
				Console.Write("Downloading essential files...");
				Remote.DownloadFileFromZip(IPSWurl, "Restore.plist", IPSWdir + "Restore.plist");
				Remote.DownloadFileFromZip(IPSWurl, "BuildManifest.plist", IPSWdir + "BuildManifest.plist");
				Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
				Console.WriteLine("Parsing Restore.plist");
				Utils.ParseRestorePlist(IPSWdir + "Restore.plist");
				Console.Write("Downloading images...");
				for (int i = (int)FirmwareItems.AppleLogo; i < TotalFirmwareItems; i++)
				{
					string img = Utils.GetImagePathFromBuildManifest(FirmwareItem[i], IPSWdir + "BuildManifest.plist");
					if (Verbose) Console.WriteLine("\r[v] Downloading " + Path.GetFileName(img));
					Remote.DownloadFileFromZip(IPSWurl, img, IPSWdir + Path.GetFileName(img));
				}
				if (!Verbose) Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
				Console.Write("Downloading ramdisks and root filesystem...");
				if (Verbose) Console.WriteLine("\r[v] Downloading root filesystem");
				Remote.DownloadFileFromZipInBackground(IPSWurl, RootFileSystem, IPSWdir + RootFileSystem, 125829120);
				if (Verbose) Console.WriteLine("[v] Downloading update ramdisk");
				Remote.DownloadFileFromZip(IPSWurl, UpdateRamdisk, IPSWdir + UpdateRamdisk);
				if (Verbose) Console.WriteLine("[v] Downloading restore ramdisk");
				Remote.DownloadFileFromZip(IPSWurl, RestoreRamdisk, IPSWdir + RestoreRamdisk);
				if (!Verbose) Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			}
			catch (Exception e)
			{
				if (Verbose) Console.WriteLine(e);
			}
		}

		public static void CheckRamdisks()
		{
			UpdateRamdiskExists = (!string.IsNullOrEmpty(UpdateRamdisk));
			RestoreRamdiskExists = (!string.IsNullOrEmpty(RestoreRamdisk));
			Console.Write("Checking if ramdisks are encrypted...");
			kbag[(int)FirmwareItems.UpdateRamdisk] = Utils.xpwntool(IPSWdir + UpdateRamdisk, TempDir + DecryptedUpdateRamdisk).Trim();
			kbag[(int)FirmwareItems.RestoreRamdisk] = Utils.xpwntool(IPSWdir + RestoreRamdisk, TempDir + DecryptedRestoreRamdisk).Trim();
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			UpdateRamdiskIsEncrypted = (kbag[(int)FirmwareItems.UpdateRamdisk].Length != 0) && UpdateRamdiskExists;
			RestoreRamdiskIsEncrypted = (kbag[(int)FirmwareItems.RestoreRamdisk].Length != 0) && RestoreRamdiskExists;
			Console.WriteLine("Update ramdisk: " + (UpdateRamdiskExists ? (UpdateRamdiskIsEncrypted ? "encrypted" : "decrypted") : "not found"));
			Console.WriteLine("Restore ramdisk: " + (RestoreRamdiskExists ? (RestoreRamdiskIsEncrypted ? "encrypted" : "decrypted") : "not found"));
		}

		public static void GrabKBAGS()
		{
			Console.Write("Grabbing kbags...");
			for (int i = (int)FirmwareItems.AppleLogo; i < TotalFirmwareItems; i++)
			{
				string kbagStr = Utils.xpwntool(IPSWdir + Path.GetFileName(Utils.GetImagePathFromBuildManifest(FirmwareItem[i], IPSWdir + "BuildManifest.plist")), "/dev/null");
				kbag[i] = kbagStr.Substring(0, kbagStr.IndexOf(Environment.NewLine));
			}
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
		}

		public static void MakeDeviceReady()
		{
			Utils.irecovery("-killitunes");
			if (!Utils.irecovery_getenv("iKGD").Contains("true"))
			{
				int count = 1;
				Console.Write("Waiting for device in DFU mode...");
				while (!Utils.SearchDeviceInMode("DFU"))
				{
					Console.CursorLeft = 0;
					Console.Write("Waiting for device in DFU mode...  [{0}]", count);
					Utils.Delay(1);
					count++;
				}
				PluggedInDevice = Utils.irecovery("-getboardid").Trim();
				Console.CursorLeft = 0;
				Console.WriteLine("Waiting for device in DFU mode...   [Found {0}]", PluggedInDevice);
				if (!Utils.DeviceIsCompatible(PluggedInDevice))
				{
					if (string.IsNullOrEmpty(PluggedInDevice)) PluggedInDevice = "The device you plugged in ";
					Console.WriteLine("\nERROR: {0} is not compatible with iKGD yet!\n", PluggedInDevice);
					Environment.Exit((int)ExitCode.IncompatibleDevice);
				}
				if ((Utils.irecovery("-platform").Trim() != Platform) && (!string.IsNullOrEmpty(Platform)))
				{
					Console.WriteLine("\nERROR: Plugged in device is not the same platform as the ipsw!");
					Console.WriteLine("\nYou plugged in a {0} while you're trying to get keys for {1}.\n", Utils.irecovery("-platform").Trim(), Platform);
					Environment.Exit((int)ExitCode.PlatformNotSame);
				}
				Utils.PwnDevice(PluggedInDevice);
			}
			else
			{
				if ((Utils.irecovery("-platform").Trim() != Platform) && (!string.IsNullOrEmpty(Platform)))
				{
					Console.WriteLine("\nERROR: Plugged in device is not the same platform as the ipsw!");
					Console.WriteLine("\nYou plugged in a {0} while you're trying to get keys for {1}.\n", Utils.irecovery("-platform").Trim(), Platform);
					Environment.Exit((int)ExitCode.PlatformNotSame);
				}
				Console.WriteLine("Found device running iKGD payload");
				Utils.irecovery_cmd("go fbclear");
			}
			irecv_fbechoikgd();
		}

		public static void GrabKeys()
		{
			Console.Write("Grabbing keys...");
			for (int i = (int)FirmwareItems.UpdateRamdisk; i < TotalFirmwareItems; i++)
			{
				if ((UpdateRamdiskIsEncrypted && i == (int)FirmwareItems.UpdateRamdisk) || 
					(RestoreRamdiskIsEncrypted && i == (int)FirmwareItems.RestoreRamdisk) || (i >= (int)FirmwareItems.AppleLogo))
				{
					Utils.irecovery_fbecho(FirmwareItem[i]);
					Utils.irecovery_cmd("go aes dec " + kbag[i]);
					iv[i] = Utils.irecovery_getenv("iv").Trim();
					key[i] = Utils.irecovery_getenv("key").Trim();
					Utils.irecovery_fbecho("IV: " + iv[i]);
					Utils.irecovery_fbecho("Key: " + key[i]);
					Utils.irecovery_fbecho("=========================");
				}
			}
			Utils.ConsoleWriteLine((iv[9].Contains("0x") || string.IsNullOrEmpty(iv[9]) ? "   [FAILED]" : "   [DONE]"), ConsoleColor.DarkGray);
		}

		public static void DecryptRamdisks()
		{
			Console.Write("Decrypting ramdisks...");
			if (UpdateRamdiskIsEncrypted) Utils.xpwntool(IPSWdir + UpdateRamdisk, TempDir + DecryptedUpdateRamdisk, iv[(int)FirmwareItems.UpdateRamdisk], key[(int)FirmwareItems.UpdateRamdisk]);
			if (RestoreRamdiskIsEncrypted) Utils.xpwntool(IPSWdir + RestoreRamdisk, TempDir + DecryptedRestoreRamdisk, iv[(int)FirmwareItems.RestoreRamdisk], key[(int)FirmwareItems.RestoreRamdisk]);
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
		}

		public static void GetVFDecryptKey(bool UseUpdateRamdisk = false)
		{
			Console.Write("Getting vfdecryptkey...");
			try
			{
				string[] vf = Utils.genpass(Platform, TempDir + DecryptedRestoreRamdisk, IPSWdir + RootFileSystem).Split(new string[] { "vfdecrypt key: " }, StringSplitOptions.RemoveEmptyEntries);
				VFDecryptKey = vf[1].Trim();
				if ((UseUpdateRamdisk || string.IsNullOrEmpty(VFDecryptKey)) && UpdateRamdiskExists)
				{
					vf = Utils.genpass(Platform, TempDir + DecryptedUpdateRamdisk, IPSWdir + RootFileSystem).Split(new string[] { "vfdecrypt key: " }, StringSplitOptions.RemoveEmptyEntries);
					VFDecryptKey = vf[1].Trim();
				}
			}
			catch (Exception) { }
			Utils.ConsoleWriteLine(string.IsNullOrEmpty(VFDecryptKey) ? "   [FAILED]" : "   [DONE]", ConsoleColor.DarkGray);
		}

		public static void GetBaseband()
		{
			Console.Write("Getting baseband...");
			try
			{
				switch (Device)
				{
					case "iPhone2,1":
						BasebandExists = true;
						if (!string.IsNullOrEmpty(IPSWLocation))
							Utils.UnzipFile(IPSWLocation, IPSWdir, @"Firmware/ICE2.Release.bbfw");
						else if (!string.IsNullOrEmpty(IPSWurl))
							Remote.DownloadFileFromZip(IPSWurl, @"Firmware/ICE2.Release.bbfw", IPSWdir + "ICE2.Release.bbfw");
						ZipStorer zip = ZipStorer.Open(IPSWdir + "ICE2.Release.bbfw", FileAccess.Read);
						Baseband = Path.GetFileNameWithoutExtension(((List<ZipStorer.ZipFileEntry>)zip.ReadCentralDir())[0].FilenameInZip).Replace("ICE2_", "");
						zip.Close();
						break;

					case "iPhone3,1":
						BasebandExists = true;
						Baseband = Path.GetFileName(Utils.GetImagePathFromBuildManifest("BasebandFirmware", IPSWdir + "BuildManifest.plist")).Split('_')[1];
						break;

					case "iPhone3,3":
						BasebandExists = true;
						Baseband = Path.GetFileName(Utils.GetImagePathFromBuildManifest("BasebandFirmware", IPSWdir + "BuildManifest.plist")).Replace(".Release.bbfw", "").Replace("Phoenix-", "");
						break;

					case "iPad1,1":
						BasebandExists = true;
						FileIO.Directory_Create(TempDir + @"bb\");
						Utils.hfsplus_extractall(TempDir + DecryptedUpdateRamdisk, "/usr/local/standalone/firmware/", TempDir + "bb");
						Utils.UnzipAll((Directory.GetFiles(TempDir + @"bb\", "*.bbfw"))[0], TempDir + @"bb\");
						Baseband = Path.GetFileNameWithoutExtension((Directory.GetFiles(TempDir + @"bb\", "*.eep"))[1]).Replace("ICE2_", "");
						FileIO.Directory_Delete(TempDir + @"bb\");
						break;
				}
			}
			catch (Exception e)
			{
				if (Verbose) Console.Error.WriteLine(e);
			}
			Utils.ConsoleWriteLine(BasebandExists && !string.IsNullOrEmpty(Baseband) ? "   [DONE]" : BasebandExists ? "   [FAILED]" : "   [No Baseband Found]", ConsoleColor.DarkGray);
		}

		public static void FetchFirmwareURL()
		{
			Codename = Utils.ParseBuildManifestInfo(IPSWdir + "BuildManifest.plist", "BuildTrain");
			FirmwareIsBeta = Utils.ParseBuildManifestInfo(IPSWdir + "BuildManifest.plist", "Variant").Contains("Developer");
			if (FirmwareIsBeta)
			{
				Console.WriteLine("Firmware {0} is a beta firmware, can not fetch url for it.", BuildID);
				return;
			}
			Console.WriteLine("Fetching url for " + Device + " and " + BuildID);
			DownloadURL = Utils.GetFirmwareURL(Device, BuildID);
			if (string.IsNullOrEmpty(DownloadURL))
				Console.WriteLine("Unable to find url. Perhaps " + BuildID + " is a beta firmware?");
		}

		public static void MakeFilesForKeys()
		{
			Console.Write("Making The iPhone Wiki Keys file...");
			Utils.MakeTheiPhoneWikiFile(TempDir + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt");
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);

			Console.Write("Making opensn0w Keys plist...");
			Utils.MakeOpensn0wPlist(TempDir + Device + "_" + Firmware + "_" + BuildID + ".plist");
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
		}

		public static void CopyKeysToKeysDir()
		{
			Console.Write("Copying keys to the keys directory");
			if (FileIO.Directory_Create(KeysDir))
			{
				FileIO.File_Copy(TempDir + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt", KeysDir + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt", true);
				FileIO.File_Copy(TempDir + Device + "_" + Firmware + "_" + BuildID + ".plist", KeysDir + Device + "_" + Firmware + "_" + BuildID + ".plist", true);
			}
			Utils.ConsoleWriteLine(FileIO.File_Exists(KeysDir + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt") ? "   [DONE]" : "   [FAILED]", ConsoleColor.DarkGray);

			if (RunningRemotelyServer)
			{
				Console.Write("Copying keys to " + RemoteFileLocation);
				FileIO.File_Copy(TempDir + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt", RemoteFileLocation + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt", true);
				FileIO.File_Copy(TempDir + Device + "_" + Firmware + "_" + BuildID + ".plist", RemoteFileLocation + Device + "_" + Firmware + "_" + BuildID + ".plist", true);
				Utils.ConsoleWriteLine(FileIO.File_Exists(RemoteFileLocation + Device + "_" + Firmware + "_" + BuildID + "_Keys.txt") ? "   [DONE]" : "   [FAILED]", ConsoleColor.DarkGray);
			}
		}

		public static void DecryptRootFS()
		{
			if (ExtractFullRootFS)
			{
				Console.Write("Decrypting the Root FileSystem...");
				Utils.dmg_extract(IPSWdir + RootFileSystem, TempDir + DecryptedRootFS, VFDecryptKey);
				if (Utils.GetFileSizeOnDisk(TempDir + DecryptedRootFS) == 0)
				{
					GetVFDecryptKey(true);
					Utils.dmg_extract(IPSWdir + RootFileSystem, TempDir + DecryptedRootFS, VFDecryptKey);
				}
				Utils.ConsoleWriteLine((Utils.GetFileSizeOnDisk(TempDir + DecryptedRootFS) != 0) ? "   [DONE]" : "   [FAILED]", ConsoleColor.DarkGray);
			}
		}

		public static void RemoteModeHome()
		{
			Console.Write("Waiting for KBAGS from server...");
			while (!FileIO.File_Exists(RemoteFileLocation + "iKGD-RemoteServer.plist")) { };
			Console.Write("\nGetting KBAGS...");
			Dictionary<string, object> RemoteServerDict = (Dictionary<string, object>)Plist.readPlist(RemoteFileLocation + "iKGD-RemoteServer.plist");
			Dictionary<string, object> FirmwareInfo = (Dictionary<string, object>)RemoteServerDict["FirmwareInfo"];
			Dictionary<string, object> KBAGS = (Dictionary<string, object>)RemoteServerDict["KBAGS"];
			Device = Utils.GetValueByKey(FirmwareInfo, "Device");
			Firmware = Utils.GetValueByKey(FirmwareInfo, "Firmware");
			BuildID = Utils.GetValueByKey(FirmwareInfo, "BuildID");
			Platform = Utils.GetValueByKey(FirmwareInfo, "Platform");
			UpdateRamdiskIsEncrypted = FirmwareInfo.ContainsKey("UpdateRamdiskEncrypted") ? (bool)FirmwareInfo["UpdateRamdiskEncrypted"] : false;
			RestoreRamdiskIsEncrypted = FirmwareInfo.ContainsKey("RestoreRamdiskEncrypted") ? (bool)FirmwareInfo["RestoreRamdiskEncrypted"] : false;
			for (int i = (int)FirmwareItems.UpdateRamdisk; i < TotalFirmwareItems; i++)
			{
				kbag[i] = Utils.GetValueByKey(KBAGS, FirmwareItem[i]);
			}
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			Console.WriteLine("Checking resources...");
			Utils.CheckResources();
			MakeDeviceReady();
			GrabKeys();
			Console.Write("Writing keys plist for remote server...");
			Dictionary<string, object> RemoteHomeDict = new Dictionary<string, object>();
			Dictionary<string, object> IVs = new Dictionary<string, object>();
			Dictionary<string, object> Keys = new Dictionary<string, object>();
			for (int i = (int)FirmwareItems.UpdateRamdisk; i < TotalFirmwareItems; i++)
			{
				IVs.Add(FirmwareItem[i], iv[i]);
				Keys.Add(FirmwareItem[i], key[i]);
			}
			RemoteHomeDict.Add("FirmwareInfo", FirmwareInfo);
			RemoteHomeDict.Add("IVs", IVs);
			RemoteHomeDict.Add("Keys", Keys);
			Plist.writeXml(RemoteHomeDict, RemoteFileLocation + "iKGD-RemoteHome.plist");
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
			Environment.Exit((int)ExitCode.Success);
		}

		public static void RemoteModeServer()
		{
			if (!KeepRemoteFiles)
			{
				FileIO.File_Delete(RemoteFileLocation + "iKGD-RemoteHome.plist");
				FileIO.File_Delete(RemoteFileLocation + "iKGD-RemoteServer.plist");
			}
			Dictionary<string, object> RemoteServerDict = new Dictionary<string, object>();
			Dictionary<string, object> KBAGS = new Dictionary<string, object>();
			Dictionary<string, object> FirmwareInfo = new Dictionary<string, object>();
			FirmwareInfo.Add("Device", Device);
			FirmwareInfo.Add("Firmware", Firmware);
			FirmwareInfo.Add("BuildID", BuildID);
			FirmwareInfo.Add("Platform", Platform);
			FirmwareInfo.Add("UpdateRamdiskEncrypted", UpdateRamdiskIsEncrypted);
			FirmwareInfo.Add("RestoreRamdiskEncrypted", RestoreRamdiskIsEncrypted);
			if (UpdateRamdiskIsEncrypted) KBAGS.Add(FirmwareItem[0], kbag[0]);
			if (RestoreRamdiskIsEncrypted) KBAGS.Add(FirmwareItem[1], kbag[1]);
			for (int i = 2; i < TotalFirmwareItems; i++)
			{
				KBAGS.Add(FirmwareItem[i], kbag[i]);
			}
			RemoteServerDict.Add("FirmwareInfo", FirmwareInfo);
			RemoteServerDict.Add("KBAGS", KBAGS);
			Plist.writeXml(RemoteServerDict, RemoteFileLocation + "iKGD-RemoteServer.plist");
			Console.Write("Waiting for IVs and Keys...");
			while (!FileIO.File_Exists(RemoteFileLocation + "iKGD-RemoteHome.plist")) { };
			Console.Write("\nGetting Keys...");
			Dictionary<string, object> RemoteHomeDict = (Dictionary<string, object>)Plist.readPlist(RemoteFileLocation + "iKGD-RemoteHome.plist");
			Dictionary<string, object> IVs = (Dictionary<string, object>)RemoteHomeDict["IVs"];
			Dictionary<string, object> Keys = (Dictionary<string, object>)RemoteHomeDict["Keys"];
			for (int i = 0; i < TotalFirmwareItems; i++)
			{
				iv[i] = Utils.GetValueByKey(IVs, FirmwareItem[i]);
				key[i] = Utils.GetValueByKey(Keys, FirmwareItem[i]);
			}
			if (!KeepRemoteFiles)
			{
				FileIO.File_Delete(RemoteFileLocation + "iKGD-RemoteHome.plist");
				FileIO.File_Delete(RemoteFileLocation + "iKGD-RemoteServer.plist");
			}
			Utils.ConsoleWriteLine("   [DONE]", ConsoleColor.DarkGray);
		}

		private static void PrintUsage(string CurrentProcessName)
		{
			Console.WriteLine();
			Console.WriteLine("Usage: " + CurrentProcessName + " <args> [options]");
			Console.WriteLine("  -i <ipswlocation>     Local path to the iOS firmware");
			Console.WriteLine("  -u <ipswurl>          Remote firmware URL to download files from");
			Console.WriteLine("  -d <device>           Device boardid as n90ap to fetch URL (use with -f)");
			Console.WriteLine("  -f <firmwarebuild>    Firmware build as 9A334 to fetch URL (use with -d)");
			Console.WriteLine("  -k <keysdir>          Path to dir to store keys (default \"{0}\"", KeysDir);
			Console.WriteLine("  -S                    Running on server (also run -H at home)");
			Console.WriteLine("  -H                    Use with -S to get keys from home");
			Console.WriteLine("  -R                    Manually specify dir used by Remote mode (-S or -H)");
			Console.WriteLine("  -e                    Extract full root filesystem (only with -i)");
			Console.WriteLine("  -r                    Don't reboot device.");
			Console.WriteLine("  -v                    Verbose");
			Console.WriteLine();
			Console.WriteLine(" eg. {0} -ei \"C:\\iPod4,1_5.0_9A334_Restore.ipsw\"", CurrentProcessName);
			Console.WriteLine("     {0} -v -u \"http://apple.com/iPod4,1_5.0_9A334_Restore.ipsw\"", CurrentProcessName);
			Console.WriteLine("     {0} -d iPod4,1 -f 9A334 -v", CurrentProcessName);
			Console.WriteLine();
			Environment.Exit((int)ExitCode.Usage);
		}

		public static void irecv_fbechoikgd()
		{
			Utils.irecovery_fbecho("________________________________________________________________________________________________________");
			Utils.irecovery_fbecho("________________________________________________________________________________________________________");
			Utils.irecovery_fbecho("__________iiii_______KKKKKKKKK____KKKKKKK_____________GGGGGGGGGGGGG_______DDDDDDDDDDDDD_________________");
			Utils.irecovery_fbecho("_________i::::i______K:::::::K____K:::::K__________GGG::::::::::::G_______D::::::::::::DDD______________");
			Utils.irecovery_fbecho("__________iiii_______K:::::::K____K:::::K________GG:::::::::::::::G_______D:::::::::::::::DD____________");
			Utils.irecovery_fbecho("_____________________K:::::::K___K::::::K_______G:::::GGGGGGGG::::G_______DDD:::::DDDDD:::::D___________");
			Utils.irecovery_fbecho("________iiiiiii______KK::::::K__K:::::KKK______G:::::G_______GGGGGG_________D:::::D____D:::::D__________");
			Utils.irecovery_fbecho("________i:::::i________K:::::K_K:::::K________G:::::G_______________________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i________K::::::K:::::K_________G:::::G_______________________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i________K:::::::::::K__________G:::::G____GGGGGGGGGG_________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i________K:::::::::::K__________G:::::G____G::::::::G_________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i________K::::::K:::::K_________G:::::G____GGGGG::::G_________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i________K:::::K_K:::::K________G:::::G________G::::G_________D:::::D_____D:::::D_________");
			Utils.irecovery_fbecho("_________i::::i______KK::::::K__K:::::KKK______G:::::G_______G::::G_________D:::::D____D:::::D__________");
			Utils.irecovery_fbecho("________i::::::i_____K:::::::K___K::::::K_______G:::::GGGGGGGG::::G_______DDD:::::DDDDD:::::D___________");
			Utils.irecovery_fbecho("________i::::::i_____K:::::::K____K:::::K________GG:::::::::::::::G_______D:::::::::::::::DD____________");
			Utils.irecovery_fbecho("________i::::::i_____K:::::::K____K:::::K__________GGG::::::GGG:::G_______D::::::::::::DDD______________");
			Utils.irecovery_fbecho("________iiiiiiii_____KKKKKKKKK____KKKKKKK_____________GGGGGG___GGGG_______DDDDDDDDDDDDD_________________");
			Utils.irecovery_fbecho("--------------------------------------------------------------------------------------------------------");
			Utils.irecovery_fbecho("========================================================================================================");
			Utils.irecovery_fbecho(":: iKGD v" + Version + " initialized!");
			Utils.irecovery_fbecho("=====================================");
			Utils.irecovery_fbecho(":: " + Device + " - iOS " + Firmware + " [" + BuildID + "]");
			Utils.irecovery_fbecho("=====================================");
		}

		enum ExitCode : int
		{
			Success = 0,
			Usage = 1,
			InvalidURL = 2,
			InvalidIPSWLocation = 3,
			IncompatibleDevice = 4,
			PlatformNotSame = 5,
			URLisNotFirmware = 6,
			InvalidRemoteFileLocation = 7,
			UnknownError = 10
		}
	}
}

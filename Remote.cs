using System;
using System.Collections;
using System.IO;
using System.Net;
using RemoteZip;
using ICSharpCode.SharpZipLib.Zip;

namespace iKGD
{
    static class Remote
	{
		public static void CopyStream(Stream input, Stream output)
		{
			int num = 0;
			byte[] buffer = new byte[0x2000];
			while (InlineAssignHelper<int>(ref num, input.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write(buffer, 0, num);
			}
		}

		private static T InlineAssignHelper<T>(ref T target, T value)
		{
			target = value;
			return value;
		}

		public static void DownloadFileFromZip(string ZipURL, string FilePathInZip, string LocalPath)
		{
			RemoteZipFile file = new RemoteZipFile();
			if (file.Load(ZipURL))
			{
				try
				{
					IEnumerator enumerator = file.GetEnumerator();
					while (enumerator.MoveNext())
					{
						ZipEntry current = (ZipEntry)enumerator.Current;
						if (current.Name == FilePathInZip)
						{
							FileStream output = new FileStream(LocalPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
							CopyStream(file.GetInputStream(current), output);
							output.Close();
						}
					}
					if (enumerator is IDisposable) (enumerator as IDisposable).Dispose();
				}
				catch (Exception e)
				{
					if (iKGD.Verbose) Console.Error.WriteLine(e);
				}
			}
		}

		public static bool isURLaFirmware(string url)
		{
			RemoteZipFile file = new RemoteZipFile();
			try
			{
				if (file.Load(url))
				{
					IEnumerator enumerator = file.GetEnumerator();
					while (enumerator.MoveNext())
					{
						ZipEntry current = (ZipEntry)enumerator.Current;
						if (current.Name == "Restore.plist")
						{
							return true;
						}
					}
					(enumerator as IDisposable).Dispose();
				}
			}
			catch (Exception) { }
			return false;
		}

		public static void DownloadFileFromZipInBackground(string ZipURL, string FilePathInZip, string LocalPath)
		{
			try
			{
				Utils.ExecuteCommandAsync("START /B \"Downloading file from zip\" " + iKGD.Resources + "PartialZip.exe \"" + ZipURL + "\" \"" + FilePathInZip + "\" \"" + LocalPath + "\" >NUL 2>&1");
			}
			catch (Exception) { }
		}
		public static void DownloadFileFromZipInBackground(string ZipURL, string FilePathInZip, string LocalPath, long bytesToDownload)
		{
			try
			{
				Utils.ExecuteCommandAsync("START /B \"Downloading file from zip\" " + iKGD.Resources + "PartialZip.exe \"" + FilePathInZip + "\" \"" + LocalPath + "\" >NUL 2>&1");
				while (FileIO.File_Exists(LocalPath)) { }
				while (Utils.GetFileSizeOnDisk(LocalPath) <= bytesToDownload) { }
				Utils.ExecuteCommand("taskkill /F /IM RemoteZip.exe >NUL");
			}
			catch (Exception) { }
		}

		public static void DownloadImage(string Image, string DeviceBoardConfig, string TargetPath)
		{
			string url = "";
			switch (DeviceBoardConfig)
			{
				case "n72ap":
					url = "http://appldnld.apple.com/iPhone4/061-7937.20100908.ghj4f/iPod2,1_4.1_8B117_Restore.ipsw"; break;
				case "n18ap":
					url = "http://appldnld.apple.com/iPhone4/061-7941.20100908.sV9KE/iPod3,1_4.1_8B117_Restore.ipsw"; break;
				case "n81ap":
					url = "http://appldnld.apple.com/iPhone4/061-8490.20100901.hyjtR/iPod4,1_4.1_8B117_Restore.ipsw"; break;
				case "n88ap":
					url = "http://appldnld.apple.com/iPhone4/061-7938.20100908.F3rCk/iPhone2,1_4.1_8B117_Restore.ipsw"; break;
				case "n90ap":
					url = "http://appldnld.apple.com/iPhone4/061-7939.20100908.Lcyg3/iPhone3,1_4.1_8B117_Restore.ipsw"; break;
				case "n92ap":
					url = ""; break;
				case "k48ap":
					url = "http://appldnld.apple.com/iPad/061-8801.20100811.CvfR5/iPad1,1_3.2.2_7B500_Restore.ipsw"; break;
				case "k66ap":
					url = "http://appldnld.apple.com/AppleTV/061-8940.20100926.Tvtnz/AppleTV2,1_4.1_8M89_Restore.ipsw"; break;
			}
			if (!string.IsNullOrEmpty(url))
			{
				if (Image == "iBSS")
					DownloadFileFromZip(url, "Firmware/dfu/iBSS." + DeviceBoardConfig + ".RELEASE.dfu", TargetPath);
				if (Image == "iBoot")
					DownloadFileFromZip(url, "Firmware/all_flash/all_flash." + DeviceBoardConfig + ".production/iBoot." + DeviceBoardConfig + ".RELEASE.img3", TargetPath);
			}
		}
    }
}
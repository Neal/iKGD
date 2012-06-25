using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace iKGD
{
	internal sealed class FileIO
	{		
		public static bool Directory_Exists(string DirName)
		{
			if (string.IsNullOrEmpty(DirName))
				return false;

			return Directory.Exists(DirName);
		}

		public static bool Directory_Create(string DirName)
		{
			if (!string.IsNullOrEmpty(DirName))
			{
				try
				{
					if (Directory_Exists(DirName))
						return true;
					Directory.CreateDirectory(DirName);
					return true;
				}
				catch (Exception) { }
			}
			return false;
		}

		public static bool Directory_Delete(string DirName)
		{
			if (!string.IsNullOrEmpty(DirName))
			{
				try
				{
					if (Directory_Exists(DirName))
					{
						Directory.Delete(DirName, true);
						return true;
					}
				}
				catch (Exception) { }
			}
			return false;
		}

		public static bool File_Delete(string FileName)
		{
			if (!string.IsNullOrEmpty(FileName))
			{
				try
				{
					File.Delete(FileName);
					return true;
				}
				catch (Exception) { }
			}
			return false;
		}

		public static bool File_Exists(string FileName)
		{
			if (string.IsNullOrEmpty(FileName))
				return false;

			return File.Exists(FileName);
		}

		public static bool File_Copy(string SourceFile, string DestFile, bool overwrite)
		{
			if (!string.IsNullOrEmpty(SourceFile) && !string.IsNullOrEmpty(DestFile))
			{
				try
				{
					File.Copy(SourceFile, DestFile, overwrite);
					return true;
				}
				catch (Exception) { }
			}
			return false;
		}

		public static bool File_Move(string SourceFile, string DestFile)
		{
			if (!string.IsNullOrEmpty(SourceFile) && !string.IsNullOrEmpty(DestFile))
			{
				try
				{
					File.Move(SourceFile, DestFile);
					return true;
				}
				catch (Exception) { }
			}
			return false;
		}

		public static bool File_Rename(string SourceFileName, string NewFileName)
		{
			if (!string.IsNullOrEmpty(SourceFileName) && !string.IsNullOrEmpty(NewFileName))
			{
				try
				{
					File.Move(SourceFileName, NewFileName);
					return true;
				}
				catch (Exception) { }
			}
			return false;
		}

		public static string GetFileContents(string FilePath)
		{
			try
			{
				StreamReader reader = new StreamReader(FilePath);
				string str = reader.ReadToEnd();
				reader.Close();
				return str;
			}
			catch (Exception) { }
			return "";
		}

		public static long GetFileSize(string FilePath)
		{
			FileInfo info = new FileInfo(FilePath);
			return info.Length;
		}
		
		public static bool SaveTextToFile(string Data, string FilePath)
		{
			try
			{
				StreamWriter writer = new StreamWriter(FilePath);
				writer.Write(Data);
				writer.Close();
				return true;
			}
			catch (Exception) { }
			return false;
		}

		public static void SaveResourceToDisk(string resourceName, string fileName)
		{
			if (!File_Exists(fileName))
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				foreach (string str in executingAssembly.GetManifestResourceNames())
				{
					if (str.ToLower().IndexOf(resourceName.ToLower()) != -1)
					{
						using (Stream stream = executingAssembly.GetManifestResourceStream(str))
						{
							if (stream != null)
							{
								using (BinaryReader reader = new BinaryReader(stream))
								{
									byte[] buffer = reader.ReadBytes((int)stream.Length);
									using (FileStream stream2 = new FileStream(fileName, FileMode.Create))
									{
										using (BinaryWriter writer = new BinaryWriter(stream2))
										{
											writer.Write(buffer);
										}
									}
								}
							}
						}
						break;
					}
				}
			}
		}

		[DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);
	}
}


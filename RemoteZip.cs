// RemoteZipFile.cs
// Copyright (C) 2003 Emanuele Ruffaldi
//
// ZipEntry parsing code taken from ZipFile.cs in SharpLibZip
// Copyright (C) 2001 Mike Krueger
//
// The original SharpLibZip code was translated from java, it was part of the GNU Classpath
// Copyright (C) 2001 Free Software Foundation, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
//
// Linking this library statically or dynamically with other modules is
// making a combined work based on this library.  Thus, the terms and
// conditions of the GNU General Public License cover the whole
// combination.
// 
// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module.  An independent module is a module which is not derived from
// or based on this library.  If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so.  If you do not wish to do so, delete this
// exception statement from your version.

using System;
using System.Net;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.BZip2;
using System.Collections;
using System.IO;
using System.Text;

namespace RemoteZip
{
	/// <summary>
	/// Summary description for ZipDownloader.
	/// </summary>
	public class RemoteZipFile : IEnumerable
	{
		ZipEntry [] entries;
		string baseUrl;
		int MaxFileOffset;

		public RemoteZipFile()
		{
		}

		/*
		end of central dir signature  	4 bytes (0x06054b50)
		number of this disk 	2 bytes
		number of the disk with the start of the central directory 	2 bytes
		total number of entries in the central directory on this disk 	2 bytes
		total number of entries in the central directory 	2 bytes
		size of the central directory 	4 bytes
		offset of start of central directory
		with respect to the starting disk number 	4 bytes
		.ZIP file comment length 	2 bytes
		.ZIP file comment 	(variable size)
		 */

		/// <summary>
		/// TODO: case when the whole file is smaller than 64K
		/// TODO: handle non HTTP case
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public bool Load(string url)
		{
			int CentralOffset, CentralSize;
			int TotalEntries;
			if(!FindCentralDirectory(url, out CentralOffset, out CentralSize, out TotalEntries))
				return false;

			MaxFileOffset = CentralOffset;

			// now retrieve the Central Directory
			baseUrl = url;
			entries = new ZipEntry[TotalEntries];

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.AddRange(CentralOffset, CentralOffset+CentralSize);
			HttpWebResponse res = (HttpWebResponse)req.GetResponse();
			Stream s = res.GetResponseStream();
			try 
			{
				// code taken from SharpZipLib with modification for not seekable stream
				// and adjustement for Central Directory entry
				for (int i = 0; i < TotalEntries; i++) 
				{
					if (ReadLeInt(s) != ZipConstants.CentralHeaderSignature)
					{
						throw new ZipException("Wrong Central Directory signature");
					}
					
					// skip 6 bytes: version made (W), version ext (W), flags (W)
					ReadLeInt(s);
					ReadLeShort(s);
					int method = ReadLeShort(s);
					int dostime = ReadLeInt(s);
					int crc = ReadLeInt(s);
					int csize = ReadLeInt(s);
					int size = ReadLeInt(s);
					int nameLen = ReadLeShort(s);
					int extraLen = ReadLeShort(s);
					int commentLen = ReadLeShort(s);				
					// skip 8 bytes: disk number start, internal file attribs, external file attribs (DW)
					ReadLeInt(s);
					ReadLeInt(s);
					int offset = ReadLeInt(s);
					
					byte[] buffer = new byte[Math.Max(nameLen, commentLen)];
					
					ReadAll(buffer, 0, nameLen, s);
					string name = ZipConstants.ConvertToString(buffer);
					
					ZipEntry entry = new ZipEntry(name);
					entry.CompressionMethod = (CompressionMethod)method;
					entry.Crc = crc & 0xffffffffL;
					entry.Size = size & 0xffffffffL;
					entry.CompressedSize = csize & 0xffffffffL;
					entry.DosTime = (uint)dostime;
					if (extraLen > 0) 
					{
						byte[] extra = new byte[extraLen];
						ReadAll(extra, 0, extraLen, s);
						entry.ExtraData = extra;
					}
					if (commentLen > 0) 
					{
						ReadAll(buffer, 0, commentLen, s);
						entry.Comment = ZipConstants.ConvertToString(buffer);
					}
					entry.ZipFileIndex = i;
					entry.Offset = offset;
					entries[i] = entry;
					OnProgress((i*100)/TotalEntries);
				}
			}
			finally
			{
				s.Close();
				res.Close();			
			}
			OnProgress(100);

			
			return true;
		}

		/// <summary>
		/// OnProgress during Central Header loading
		/// </summary>
		/// <param name="pct"></param>
		public virtual void OnProgress(int pct)
		{
		
		}

		/// <summary>
		/// Checks if there is a local header at the current position in the stream and skips it
		/// </summary>
		/// <param name="baseStream"></param>
		/// <param name="entry"></param>
		void SkipLocalHeader(Stream baseStream, ZipEntry entry)
		{
			lock(baseStream) 
			{
				if (ReadLeInt(baseStream) != ZipConstants.LocalHeaderSignature)
				{
					throw new ZipException("Wrong Local header signature");
				}
				
				Skip(baseStream, 10+12);
				int namelen = ReadLeShort(baseStream);
				int extralen = ReadLeShort(baseStream);
				Skip(baseStream, namelen+extralen);
			}
		}

		/// <summary>
		/// Finds the Central Header in the Zip file. We can minimize the number of requests and
		/// the bytes taken
		/// 
		/// Actually we do: 256, 1024, 65536
		/// </summary>
		/// <param name="baseurl"></param>
		/// <returns></returns>
		bool FindCentralDirectory(string url, out int  Offset, out int Size, out int Entries)
		{
			int currentLength = 256;
			Entries = 0;
			Size = 0;
			Offset = -1;

			while(true)
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
				req.AddRange(-(currentLength + 22));
				HttpWebResponse res = (HttpWebResponse)req.GetResponse();

				// copy the buffer because we need a back seekable buffer
				byte [] bb = new byte[res.ContentLength];
				int endSize = ReadAll(bb, 0, (int)res.ContentLength, res.GetResponseStream());
				res.Close();

				// scan for the central block. The position of the central block
				// is end-comment-22
				//<
				// 50 4B 05 06
				int pos = endSize-22;
//				int state = 0;
				while(pos >= 0)
				{
					if(bb[pos] == 0x50)
					{
						if(bb[pos+1] == 0x4b && bb[pos+2] == 0x05 && bb[pos+3] == 0x06)
							break; // found!!
						pos -= 4;
					}
					else
						pos --;
				}

				if(pos < 0)
				{
					if(currentLength == 65536)
						break;

					if(currentLength == 1024)
						currentLength = 65536;
					else if(currentLength == 256)
						currentLength = 1024;
					else
						break;
				}
				else
				{
					// found it!! so at offset pos+3*4 there is Size, and pos+4*4
					// BinaryReader is so elegant but now it's too much
					Size = MakeInt(bb, pos+12);
					Offset = MakeInt(bb, pos+16);
					Entries = MakeShort(bb, pos+10);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Get a Stream for reading the specified entry
		/// </summary>
		/// <param name="entry"></param>
		/// <returns></returns>
		public Stream GetInputStream(ZipEntry entry)
		{
			if(entry.Size == 0)
				return null;

			if (entries == null) 
			{
				throw new InvalidOperationException("ZipFile has closed");
			}

            int index = (int)entry.ZipFileIndex;
			if (index < 0 || index >= entries.Length || entries[index].Name != entry.Name) 
			{
				throw new IndexOutOfRangeException();
			}
			
			// WARNING
			// should parse the Local Header to get the data address
			// Maximum Size of the Local Header is ... 16+64K*2
			//
			// So the HTTP request should ask for the big local header, but actually the
			// additional data is not downloaded.
			// Optionally use an additional Request to be really precise
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(baseUrl);

			int limit = (int)(entry.Offset+entry.CompressedSize+16+65536*2);
			if (limit >= MaxFileOffset)
				limit = MaxFileOffset-1; 
			req.AddRange((int)entry.Offset, limit);
			HttpWebResponse res = (HttpWebResponse)req.GetResponse();
			Stream baseStream = res.GetResponseStream();

			// skips all the header
			SkipLocalHeader(baseStream, entries[index]);
			CompressionMethod method = entries[index].CompressionMethod;

			Stream istr = new PartialInputStream(baseStream, res, entries[index].CompressedSize);
			switch (method) 
			{
				case CompressionMethod.Stored:
					return istr;
				case CompressionMethod.Deflated:
					return new InflaterInputStream(istr, new Inflater(true));
				case (CompressionMethod)12:
					return new BZip2InputStream(istr);
				default:
					throw new ZipException("Unknown compression method " + method);
			}			
		}


		/// <summary>
		/// Read an unsigned short in little endian byte order.
		/// </summary>
		/// <exception name="System.IO.IOException">
		/// if a i/o error occured.
		/// </exception>
		/// <exception name="System.IO.EndOfStreamException">
		/// if the file ends prematurely
		/// </exception>
		int ReadLeShort(Stream s)
		{
			return s.ReadByte() | s.ReadByte() << 8;
		}
		
		/// <summary>
		/// Read an int in little endian byte order.
		/// </summary>
		/// <exception name="System.IO.IOException">
		/// if a i/o error occured.
		/// </exception>
		/// <exception name="System.IO.EndOfStreamException">
		/// if the file ends prematurely
		/// </exception>
		int ReadLeInt(Stream s)
		{
			return ReadLeShort(s) | ReadLeShort(s) << 16;
		}

		static void Skip(Stream s, int n)
		{
			for(int i = 0; i < n; i++)
				s.ReadByte();
		}

		static int ReadAll(byte [] bb, int p, int sst, Stream s)
		{
			int ss = 0;
			while(ss < sst)
			{
				int r = s.Read(bb, p, sst-ss);
				if(r <= 0)
					return ss;
				ss += r;
				p += r;
			}
			return ss;
		}

		public static int MakeInt(byte [] bb, int pos)
		{
			return bb[pos+0]|(bb[pos+1]<<8)|(bb[pos+2]<<16)|(bb[pos+3]<<24);
		}

		public static int MakeShort(byte [] bb, int pos)
		{
			return bb[pos+0]|(bb[pos+1]<<8);
		}

		public int Size 
		{
			get { return entries == null ? 0 : entries.Length; }
		}

		/// <summary>
		/// Returns an IEnumerator of all Zip entries in this Zip file.
		/// </summary>
		public IEnumerator GetEnumerator()
		{
			if (entries == null) 
			{
				throw new InvalidOperationException("ZipFile has closed");
			}
			
			return new ZipEntryEnumeration(entries);
		}

		public ZipEntry this[int index]
		{
			get { return entries[index]; }
		}


		class ZipEntryEnumeration : IEnumerator
		{
			ZipEntry[] array;
			int ptr = -1;
			
			public ZipEntryEnumeration(ZipEntry[] arr)
			{
				array = arr;
			}
			
			public object Current 
			{
				get 
				{
					return array[ptr];
				}
			}
			
			public void Reset()
			{
				ptr = -1;
			}
			
			public bool MoveNext() 
			{
				return (++ptr < array.Length);
			}
		}
		
		class PartialInputStream : InflaterInputStream
		{
			Stream baseStream;
			long filepos;
			long end;
			HttpWebResponse request;

			

			public PartialInputStream(Stream baseStream, HttpWebResponse request, long len) : base(baseStream)
			{
				this.baseStream = baseStream;
				filepos = 0;
				end = len;
				this.request = request;
			}
			
			public override int Available 
			{
				get 
				{
					long amount = end - filepos;
					if (amount > Int32.MaxValue) 
					{
						return Int32.MaxValue;
					}
					
					return (int) amount;
				}
			}
			
			public override int ReadByte()
			{
				if (filepos == end) 
				{
					return -1;
				}
				
				lock(baseStream) 
				{
					filepos++;
					return baseStream.ReadByte();
				}
			}
			
			public override int Read(byte[] b, int off, int len)
			{
				if (len > end - filepos) 
				{
					len = (int) (end - filepos);
					if (len == 0) 
					{
						return 0;
					}
				}
				lock(baseStream) 
				{
					int count = ReadAll(b, off, len, baseStream);
					if (count > 0) 
					{
						filepos += len;
					}
					return count;
				}
			}
			
			public long SkipBytes(long amount)
			{
				if (amount < 0) 
				{
					throw new ArgumentOutOfRangeException();
				}
				if (amount > end - filepos) 
				{
					amount = end - filepos;
				}
				filepos += amount;
				for(int i = 0; i < amount; i++)
					baseStream.ReadByte();
				return amount;
			}

		
			public override void Close()
			{
				request.Close();
				baseStream.Close();
			}
		}

	}


		/// <summary>
		/// This is a FilterOutputStream that writes the files into a zip
		/// archive one after another.  It has a special method to start a new
		/// zip entry.  The zip entries contains information about the file name
		/// size, compressed size, CRC, etc.
		/// 
		/// It includes support for STORED and DEFLATED and BZIP2 entries.
		/// This class is not thread safe.
		/// 
		/// author of the original java version : Jochen Hoenicke
		/// </summary>
		/// <example> This sample shows how to create a zip file
		/// <code>
		/// using System;
		/// using System.IO;
		/// 
		/// using NZlib.Zip;
		/// 
		/// class MainClass
		/// {
		/// 	public static void Main(string[] args)
		/// 	{
		/// 		string[] filenames = Directory.GetFiles(args[0]);
		/// 		
		/// 		ZipOutputStream s = new ZipOutputStream(File.Create(args[1]));
		/// 		
		/// 		s.SetLevel(5); // 0 - store only to 9 - means best compression
		/// 		
		/// 		foreach (string file in filenames) {
		/// 			FileStream fs = File.OpenRead(file);
		/// 			
		/// 			byte[] buffer = new byte[fs.Length];
		/// 			fs.Read(buffer, 0, buffer.Length);
		/// 			
		/// 			ZipEntry entry = new ZipEntry(file);
		/// 			
		/// 			s.PutNextEntry(entry);
		/// 			
		/// 			s.Write(buffer, 0, buffer.Length);
		/// 			
		/// 		}
		/// 		
		/// 		s.Finish();
		/// 		s.Close();
		/// 	}
		/// }	
		/// </code>
		/// </example>
		public class ZipOutputStream : DeflaterOutputStream
		{
			private ArrayList entries  = new ArrayList();
			private Crc32     crc      = new Crc32();
			private ZipEntry  curEntry = null;

			private long startPosition = 0;
			private Stream additionalStream = null;
		
			private CompressionMethod curMethod;
			private int size;
			private int offset = 0;
		
			private byte[] zipComment = new byte[0];
			private int defaultMethod = DEFLATED;
		
			/// <summary>
			/// Our Zip version is hard coded to 1.0 resp. 2.0
			/// </summary>
			private const int ZIP_STORED_VERSION   = 10;
			private const int ZIP_DEFLATED_VERSION = 20;
		
			/// <summary>
			/// Compression method.  This method doesn't compress at all.
			/// </summary>
			public const int STORED      =  0;
		
			/// <summary>
			/// Compression method.  This method uses the Deflater.
			/// </summary>
			public const int DEFLATED    =  8;

			public const int BZIP2 = 12;
		
			/// <summary>
			/// Creates a new Zip output stream, writing a zip archive.
			/// </summary>
			/// <param name="baseOutputStream">
			/// the output stream to which the zip archive is written.
			/// </param>
			public ZipOutputStream(Stream baseOutputStream) : base(baseOutputStream, new Deflater(Deflater.DEFAULT_COMPRESSION, true))
			{ 
			}
		
			/// <summary>
			/// Set the zip file comment.
			/// </summary>
			/// <param name="comment">
			/// the comment.
			/// </param>
			/// <exception name ="ArgumentException">
			/// if UTF8 encoding of comment is longer than 0xffff bytes.
			/// </exception>
			public void SetComment(string comment)
			{
				byte[] commentBytes = ZipConstants.ConvertToArray(comment);
				if (commentBytes.Length > 0xffff) 
				{
					throw new ArgumentException("Comment too long.");
				}
				zipComment = commentBytes;
			}
		
			/// <summary>
			/// Sets default compression method.  If the Zip entry specifies
			/// another method its method takes precedence.
			/// </summary>
			/// <param name = "method">
			/// the method.
			/// </param>
			/// <exception name = "ArgumentException">
			/// if method is not supported.
			/// </exception>
			public void SetMethod(int method)
			{
				if (method != STORED && method != DEFLATED && method != BZIP2) 
				{
					throw new ArgumentException("Method not supported.");
				}
				defaultMethod = method;
			}
		
			/// <summary>
			/// Sets default compression level.  The new level will be activated
			/// immediately.
			/// </summary>
			/// <exception cref="System.ArgumentOutOfRangeException">
			/// if level is not supported.
			/// </exception>
			/// <see cref="Deflater"/>
			public void SetLevel(int level)
			{
                deflater_.SetLevel(level);
			}
		
			/// <summary>
			/// Write an unsigned short in little endian byte order.
			/// </summary>
			private  void WriteLeShort(int value)
			{
				baseOutputStream_.WriteByte((byte)value);
				baseOutputStream_.WriteByte((byte)(value >> 8));
			}
		
			/// <summary>
			/// Write an int in little endian byte order.
			/// </summary>
			private void WriteLeInt(int value)
			{
				WriteLeShort(value);
				WriteLeShort(value >> 16);
			}
		
			/// <summary>
			/// Write an int in little endian byte order.
			/// </summary>
			private void WriteLeLong(long value)
			{
				WriteLeInt((int)value);
				WriteLeInt((int)(value >> 32));
			}
		
		
			bool shouldWriteBack = false;
			long seekPos         = -1;
			/// <summary>
			/// Starts a new Zip entry. It automatically closes the previous
			/// entry if present.  If the compression method is stored, the entry
			/// must have a valid size and crc, otherwise all elements (except
			/// name) are optional, but must be correct if present.  If the time
			/// is not set in the entry, the current time is used.
			/// </summary>
			/// <param name="entry">
			/// the entry.
			/// </param>
			/// <exception cref="System.IO.IOException">
			/// if an I/O error occured.
			/// </exception>
			/// <exception cref="System.InvalidOperationException">
			/// if stream was finished
			/// </exception>
			public void PutNextEntry(ZipEntry entry)
			{
				if (entries == null) 
				{
					throw new InvalidOperationException("ZipOutputStream was finished");
				}

				if (curEntry != null) 
				{
					CloseEntry();
				}

				CompressionMethod method = entry.CompressionMethod;
				int flags = 0;
			
				switch (method) 
				{
					case CompressionMethod.Stored:
						if (entry.CompressedSize >= 0) 
						{
							if (entry.Size < 0) 
							{
								entry.Size = entry.CompressedSize;
							} 
							else if (entry.Size != entry.CompressedSize) 
							{
								throw new ZipException("Method STORED, but compressed size != size");
							}
						} 
						else 
						{
							entry.CompressedSize = entry.Size;
						}
					
						if (entry.Size < 0) 
						{
							throw new ZipException("Method STORED, but size not set");
						} 
						else if (entry.Crc < 0) 
						{
							throw new ZipException("Method STORED, but crc not set");
						}
						break;
					case (CompressionMethod)12:
						startPosition = baseOutputStream_.Position;
						additionalStream = new BZip2OutputStream(new NoCloseSubStream(baseOutputStream_));
						if (entry.CompressedSize < 0 || entry.Size < 0 || entry.Crc < 0) 
						{
							flags |= 8;
						}
						break;
					case CompressionMethod.Deflated:
						if (entry.CompressedSize < 0 || entry.Size < 0 || entry.Crc < 0) 
						{
							flags |= 8;
						}
						break;
				}
			
			
				//			if (entry.DosTime < 0) {
				//				entry.Time = System.Environment.TickCount;
				//			}
			
				entry.Flags  = flags;
				entry.Offset = offset;
				entry.CompressionMethod = (CompressionMethod)method;
			
				curMethod    = method;
				// Write the local file header
				WriteLeInt(ZipConstants.LocalHeaderSignature);
			
				// write ZIP version
				WriteLeShort(method == CompressionMethod.Stored ? ZIP_STORED_VERSION : ZIP_DEFLATED_VERSION);
				if ((flags & 8) == 0) 
				{
					WriteLeShort(flags);
					WriteLeShort((byte)method);
					WriteLeInt((int)entry.DosTime);
					WriteLeInt((int)entry.Crc);
					WriteLeInt((int)entry.CompressedSize);
					WriteLeInt((int)entry.Size);
				} 
				else 
				{
					if (baseOutputStream_.CanSeek) 
					{
						shouldWriteBack = true;
						WriteLeShort((short)(flags & ~8));
					} 
					else 
					{
						shouldWriteBack = false;
						WriteLeShort(flags);
					}
					WriteLeShort((byte)method);
					WriteLeInt((int)entry.DosTime);
					seekPos = baseOutputStream_.Position;
					WriteLeInt(0);
					WriteLeInt(0);
					WriteLeInt(0);
				}
				byte[] name = ZipConstants.ConvertToArray(entry.Name);
			
				if (name.Length > 0xFFFF) 
				{
					throw new ZipException("Name too long.");
				}
				byte[] extra = entry.ExtraData;
				if (extra == null) 
				{
					extra = new byte[0];
				}
				if (extra.Length > 0xFFFF) 
				{
					throw new ZipException("Extra data too long.");
				}
			
				WriteLeShort(name.Length);
				WriteLeShort(extra.Length);
				baseOutputStream_.Write(name, 0, name.Length);
				baseOutputStream_.Write(extra, 0, extra.Length);
			
				offset += ZipConstants.LocalHeaderBaseSize + name.Length + extra.Length;
			
				/* Activate the entry. */
				curEntry = entry;
				crc.Reset();
				if (method == CompressionMethod.Deflated) 
				{
                    deflater_.Reset();
				}
				size = 0;
			}
		
			/// <summary>
			/// Closes the current entry.
			/// </summary>
			/// <exception cref="System.IO.IOException">
			/// if an I/O error occured.
			/// </exception>
			/// <exception cref="System.InvalidOperationException">
			/// if no entry is active.
			/// </exception>
			public void CloseEntry()
			{
				if (curEntry == null) 
				{
					throw new InvalidOperationException("No open entry");
				}
			
				/* First finish the deflater, if appropriate */
				int csize = 0;
				if (curMethod == CompressionMethod.Deflated) 
				{
					base.Finish();
                    csize = (int)deflater_.TotalOut;
				}
				else if(curMethod == (CompressionMethod)12)
				{
					// close the sub stream, no problem because the substream has a fake
					// close
					additionalStream.Close();
					additionalStream = null;
					csize = (int)(baseOutputStream_.Position-startPosition);
				}
				else
					csize = size;
						
				if (curEntry.Size < 0) 
				{
					curEntry.Size = size;
				} 
				else if (curEntry.Size != size) 
				{
					throw new ZipException("size was " + size + ", but I expected " + curEntry.Size);
				}
			
				if (curEntry.CompressedSize < 0) 
				{
					curEntry.CompressedSize = csize;
				} 
				else if (curEntry.CompressedSize != csize) 
				{
					throw new ZipException("compressed size was " + csize + ", but I expected " + curEntry.CompressedSize);
				}
			
				if (curEntry.Crc < 0) 
				{
					curEntry.Crc = crc.Value;
				} 
				else if (curEntry.Crc != crc.Value) 
				{
					throw new ZipException("crc was " + crc.Value +
						", but I expected " + 
						curEntry.Crc);
				}
			
				offset += csize;
			
				/* Now write the data descriptor entry if needed. */
				if (curMethod != CompressionMethod.Stored && (curEntry.Flags & 8) != 0) 
				{
					if (shouldWriteBack) 
					{
						curEntry.Flags &= ~8;
						long curPos = baseOutputStream_.Position;
						baseOutputStream_.Seek(seekPos, SeekOrigin.Begin);
						WriteLeInt((int)curEntry.Crc);
						WriteLeInt((int)curEntry.CompressedSize);
						WriteLeInt((int)curEntry.Size);
						baseOutputStream_.Seek(curPos, SeekOrigin.Begin);
						shouldWriteBack = false;
					} 
					else 
					{
						WriteLeInt(ZipConstants.DataDescriptorSignature);
						WriteLeInt((int)curEntry.Crc);
						WriteLeInt((int)curEntry.CompressedSize);
						WriteLeInt((int)curEntry.Size);
						offset += ZipConstants.DataDescriptorSize;
					}
				}
			
				entries.Add(curEntry);
				curEntry = null;
			}
		
			/// <summary>
			/// Writes the given buffer to the current entry.
			/// </summary>
			/// <exception cref="System.IO.IOException">
			/// if an I/O error occured.
			/// </exception>
			/// <exception cref="System.InvalidOperationException">
			/// if no entry is active.
			/// </exception>
			public override void Write(byte[] b, int off, int len)
			{
				if (curEntry == null) 
				{
					throw new InvalidOperationException("No open entry.");
				}
			
				switch (curMethod) 
				{
					case (CompressionMethod)12:
						additionalStream.Write(b, off, len);
						break;
					case CompressionMethod.Deflated:
						base.Write(b, off, len);
						break;
					case CompressionMethod.Stored:
						baseOutputStream_.Write(b, off, len);
						break;
				}
			
				crc.Update(b, off, len);
				size += len;
			}
		
			/// <summary>
			/// Finishes the stream.  This will write the central directory at the
			/// end of the zip file and flush the stream.
			/// </summary>
			/// <exception cref="System.IO.IOException">
			/// if an I/O error occured.
			/// </exception>
			public override void Finish()
			{
				if (entries == null)  
				{
					return;
				}
			
				if (curEntry != null) 
				{
					CloseEntry();
				}
			
				int numEntries = 0;
				int sizeEntries = 0;
			
				foreach (ZipEntry entry in entries) 
				{
					// TODO : check the appnote file for compilance with the central directory standard
					CompressionMethod method = entry.CompressionMethod;
					WriteLeInt(ZipConstants.CentralHeaderSignature); 
					WriteLeShort(method == CompressionMethod.Stored ? ZIP_STORED_VERSION : ZIP_DEFLATED_VERSION);
					WriteLeShort(method == CompressionMethod.Stored ? ZIP_STORED_VERSION : ZIP_DEFLATED_VERSION);
					if (entry.IsCrypted) 
					{
						entry.Flags |= 1;
					}
					WriteLeShort(entry.Flags);
					WriteLeShort((short)method);
					WriteLeInt((int)entry.DosTime);
					WriteLeInt((int)entry.Crc);
					WriteLeInt((int)entry.CompressedSize);
					WriteLeInt((int)entry.Size);
				
					byte[] name = ZipConstants.ConvertToArray(entry.Name);
				
					if (name.Length > 0xffff) 
					{
						throw new ZipException("Name too long.");
					}
				
					byte[] extra = entry.ExtraData;
					if (extra == null) 
					{
						extra = new byte[0];
					}
				
					string strComment = entry.Comment;
					byte[] comment = strComment != null ? ZipConstants.ConvertToArray(strComment) : new byte[0];
					if (comment.Length > 0xffff) 
					{
						throw new ZipException("Comment too long.");
					}
				
					WriteLeShort(name.Length);
					WriteLeShort(extra.Length);
					WriteLeShort(comment.Length);
					WriteLeShort(0); // disk number
					WriteLeShort(0); // internal file attr
					WriteLeInt(0);   // external file attr
					WriteLeInt((int)entry.Offset);
				
					baseOutputStream_.Write(name,    0, name.Length);
					baseOutputStream_.Write(extra,   0, extra.Length);
					baseOutputStream_.Write(comment, 0, comment.Length);
					++numEntries;
					sizeEntries += ZipConstants.CentralHeaderBaseSize + name.Length + extra.Length + comment.Length;
				}
			
				WriteLeInt(ZipConstants.EndOfCentralDirectorySignature);
				WriteLeShort(0); // disk number 
				WriteLeShort(0); // disk with start of central dir
				WriteLeShort(numEntries);
				WriteLeShort(numEntries);
				WriteLeInt(sizeEntries);
				WriteLeInt(offset);
				WriteLeShort(zipComment.Length);
				baseOutputStream_.Write(zipComment, 0, zipComment.Length);
				baseOutputStream_.Flush();
				entries = null;
			}
		}

	/// <summary>
	/// Stream without 
	/// </summary>
	public class NoCloseSubStream : Stream
	{
		Stream baseStream;

		public NoCloseSubStream(Stream b) 
		{
			baseStream = b;
		}
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override bool CanRead 
		{
			get 
			{
				return baseStream.CanRead;
			}
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override bool CanSeek 
		{
			get 
			{
				return baseStream.CanSeek;
			}
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override bool CanWrite 
		{
			get 
			{
				return baseStream.CanWrite;
			}
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override long Length 
		{
			get 
			{
				return baseStream.Length;
			}
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override long Position 
		{
			get 
			{
				return baseStream.Position;
			}
			set 
			{
				baseStream.Position = value;
			}
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override long Seek(long offset, SeekOrigin origin)
		{
			return baseStream.Seek(offset, origin);
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override void SetLength(long val)
		{
			baseStream.SetLength(val);
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override int ReadByte()
		{
			return baseStream.ReadByte();
		}
		
		/// <summary>
		/// I needed to implement the abstract member.
		/// </summary>
		public override int Read(byte[] b, int off, int len)
		{
			return baseStream.Read(b, off, len);
		}
		
		public override void Write(byte[] buf, int off, int len)
		{
			baseStream.Write(buf, off, len);
		}

		public override void WriteByte(byte bv)
		{
			baseStream.WriteByte(bv);
		}

		public override void Close()
		{
			baseStream = null;
		}

		public override void Flush()
		{
			baseStream.Flush();
		}
	}
}


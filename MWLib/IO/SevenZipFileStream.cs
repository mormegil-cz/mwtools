// MWLib
// Copyright (c) 2007  Petr Kadlec <mormegil@centrum.cz>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace MWLib.IO
{
    /// <summary>
    /// A stream allowing reading from a single file contained in a .7z file.
    /// </summary>
    public class SevenZipFileStream : Stream
    {
        /// <summary>
        /// Signature of a .7z file
        /// </summary>
        public const int SevenZipMagic = 0x7a37; // "7z", the first two bytes in a .7z file

        /// <summary>
        /// Input stream to be decompressed
        /// </summary>
        private Stream input;

        /// <summary>
        /// Size of data in the compressed file (after decompression)
        /// </summary>
        private long remainingData;

        /// <summary>
        /// Current position in the decompressed file
        /// </summary>
        private long position;

        /// <summary>
        /// Buffer used for decompression
        /// </summary>
        private MemoryBufferStream decompressionBuffer;

        /// <summary>
        /// Folders stored in the .7z file
        /// </summary>
        private FolderInfo[] folders;

        /// <summary>
        /// Thread doing the real decompression work
        /// </summary>
        private DecodingThread decodingThread;

        /// <summary>
        /// Exception signalled from decompression thread to the main thread
        /// </summary>
        private Exception decompressionError;

        /// <summary>
        /// Flushes the input stream
        /// </summary>
        /// <seealso cref="Stream.Flush"/>
        public override void Flush()
        {
            CheckDeferredException();
            input.Flush();
        }

        /// <summary>
        /// This stream does not support seeking, this method throws an exception
        /// </summary>
        /// <exception cref="NotSupportedException">As soon as this function is called</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException(LibraryResources.SeekingNotSupported);
        }

        /// <summary>
        /// This stream does not support setting length, this method throws an exception
        /// </summary>
        /// <exception cref="NotSupportedException">As soon as this function is called</exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException(LibraryResources.SettingLengthNotSupported);
        }

        /// <summary>
        /// This stream is read only, this method throws an exception
        /// </summary>
        /// <exception cref="NotSupportedException">As soon as this function is called</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(LibraryResources.StreamIsReadOnly);
        }

        /// <summary>
        /// Is this stream readable?
        /// </summary>
        /// <value><c>true</c>, this is a readable stream</value>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Is this stream seekable?
        /// </summary>
        /// <value><c>false</c>, this stream does not support seeking</value>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Is this stream writable?
        /// </summary>
        /// <value><c>false</c>, this is a read-only stream</value>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the total length of (decompressed) data in this stream
        /// </summary>
        public override long Length
        {
            get
            {
                CheckDeferredException();
                return remainingData;
            }
        }

        /// <summary>
        /// Gets the current position in the stream (number of bytes from the beginning of decompressed data)
        /// </summary>
        /// <exception cref="NotSupportedException">On attempt to write to this property</exception>
        public override long Position
        {
            get
            {
                CheckDeferredException();
                return position;
            }
            set { throw new NotSupportedException(LibraryResources.SeekingNotSupported); }
        }

        /// <summary>
        /// Read data from the stream
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDeferredException();
            int read = decompressionBuffer.Read(buffer, offset, count);
            position += read;
            return read;
        }

        /// <summary>
        /// Constructor, initializes the decompression
        /// </summary>
        /// <param name="inputStream">Input stream containing the compressed file</param>
        public SevenZipFileStream(Stream inputStream)
        {
            input = inputStream;

            var magic = new byte[6];
            ReadFully(magic);
            /* int verMaj = */ GetByte();
            /* int verMin = */ GetByte();
            /* int crc = */ ReadInt();
            long headerOfs = ReadUInt64();
            /* long headerLen = */ ReadUInt64();
            /* int headerCrc = */ ReadInt();

            // read headers
            input.Seek(headerOfs + 0x20, SeekOrigin.Begin);

            // break inside on kEnd
            while (true)
            {
                byte id = GetByte();
                if (id == 0)
                {
                    break;
                }
                else if (id == 1)
                {
                    // header mark
                }
                else if (id == 0x02)
                {
                    ReadArchiveProperties();
                }
                else if (id == 0x03)
                {
                    ReadStreamsInfo();
                }
                else if (id == 0x04)
                {
                    ReadStreamsInfo();
                }
                else if (id == 0x05)
                {
                    break;
                    //readFilesInfo();
                }
                else throw new IOException(LibraryResources.UnsupportedFormat);
            }

            // seek to start of data
            remainingData = folders[0].unpackSize;
            input.Seek(0x20, SeekOrigin.Begin);

            /*try {
            debugDump = new FileOutputStream("dump.bin");
            } catch(Exception e) {}*/

            // launch decompressor
            decompressionBuffer = new MemoryBufferStream(2097152);
            decodingThread = new DecodingThread(input, decompressionBuffer, remainingData, folders[0].properties, null, DecompressionError);
            decodingThread.Start();
        }

        /// <summary>
        /// Aborts the decompression
        /// </summary>
        [SecurityPermission(SecurityAction.Assert, ControlThread = true)]
        public void Abort()
        {
            if (decodingThread != null) decodingThread.Abort();
        }

        /// <summary>
        /// Handler for exceptions caught in the decompression thread
        /// </summary>
        /// <param name="ex">Exception caught in the decompression thread</param>
        /// <remarks>
        /// This method rethrows the exception in this thread as soon as any method is called.
        /// </remarks>
        private void DecompressionError(Exception ex)
        {
            decompressionError = ex;
        }

        /// <summary>
        /// Check if there is any deferred exception and if it is, throw it
        /// </summary>
        private void CheckDeferredException()
        {
            if (decompressionError != null) throw decompressionError;
        }

        /// <summary>
        /// Reads a single byte from the input stream
        /// </summary>
        /// <returns>The byte read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private byte GetByte()
        {
            int result = input.ReadByte();
            if (result < 0) throw new IOException(LibraryResources.EndOfFileReached);
            return (byte) result;
        }

        /// <summary>
        /// Reads a boolean value from the input stream
        /// </summary>
        /// <returns>The boolean value read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private bool ReadBool()
        {
            return GetByte() != 0;
        }

        /// <summary>
        /// Reads a bit vector from the input stream
        /// </summary>
        /// <param name="numberOfItems">Number of bits to be read</param>
        /// <returns>The data read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private bool[] ReadBitVector(int numberOfItems)
        {
            var result = new bool[numberOfItems];
            byte mask = 0x00;
            byte curr = 0x00;
            for (int i = 0; i < numberOfItems; ++i)
            {
                if (mask == 0x00)
                {
                    curr = GetByte();
                    mask = 0x80;
                }
                result[i] = (curr & mask) != 0;
                mask >>= 1;
            }
            return result;
        }

        /// <summary>
        /// Reads a 32-bit integer from the input stream
        /// </summary>
        /// <returns>The integer read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private int ReadInt()
        {
            byte a = GetByte();
            byte b = GetByte();
            byte c = GetByte();
            byte d = GetByte();
            return a | (b << 8) | (c << 16) | (d << 24);
        }

        /// <summary>
        /// Reads a 64-bit integer from the input stream
        /// </summary>
        /// <returns>The integer read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private long ReadUInt64()
        {
            byte a = GetByte();
            byte b = GetByte();
            byte c = GetByte();
            byte d = GetByte();
            byte e = GetByte();
            byte f = GetByte();
            byte g = GetByte();
            byte h = GetByte();
            return (       a) |
                   ((long) b << 8) |
                   ((long) c << 16) |
                   ((long) d << 24) |
                   ((long) e << 32) |
                   ((long) f << 40) |
                   ((long) g << 48) |
                   ((long) h << 56);
        }

        /// <summary>
        /// Reads a long integer value from the input stream
        /// </summary>
        /// <returns>The value read</returns>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private long ReadLong()
        {
            int lead = GetByte();
            int mask = 0x80;
            long result = 0;
            int len = 0;
            while ((lead & mask) != 0)
            {
                lead &= ~mask;
                result |= ((long) GetByte()) << len;
                mask >>= 1;
                len += 8;
            }
            result |= ((long) lead) << len;
            return result;
        }

        /// <summary>
        /// Reads a data buffer
        /// </summary>
        /// <param name="data">Buffer to be filled with read data</param>
        /// <exception cref="IOException">When the end of the stream has been reached</exception>
        private void ReadFully(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            int remaining = data.Length;
            int off = 0;
            while (remaining > 0)
            {
                int read = input.Read(data, off, remaining);
                if (read <= 0) throw new IOException(LibraryResources.EndOfFileReached);
                off += read;
                remaining -= read;
            }
        }

        /// <summary>
        /// Reads archive properties block
        /// </summary>
        /// <returns>Raw read data</returns>
        private byte[] ReadArchiveProperties()
        {
            var buff = new List<byte>();
            // break inside on type==0
            while (true)
            {
                byte type = GetByte();
                //buff.add(type);
                if (type == 0) break;
                long size = ReadLong();
                for (int i = 0; i < size; ++i)
                {
                    buff.Add(GetByte());
                }
                break;
            }
            return buff.ToArray();
        }

        /// <summary>
        /// Read stream digests
        /// </summary>
        /// <param name="numberOfStreams">Number of streams</param>
        /// <returns>Array of stream digests</returns>
        private int[] ReadDigests(int numberOfStreams)
        {
            int numDefined = 0;
            var isDefined = new bool[numberOfStreams];
            if (ReadBool())
            {
                numDefined = numberOfStreams;
                for (int i = 0; i < numberOfStreams; ++i) isDefined[i] = true;
            }
            else
            {
                isDefined = ReadBitVector(numberOfStreams);
                for (int i = 0; i < numberOfStreams; ++i)
                {
                    if (isDefined[i]) ++numDefined;
                }
            }
            var result = new int[numberOfStreams];
            for (int i = 0; i < numDefined; ++i)
            {
                result[i] = isDefined[i] ? ReadInt() : 0;
            }
            return result;
        }

        /// <summary>
        /// Read pack info block
        /// </summary>
        private void ReadPackInfo()
        {
            /* long packPos = */ ReadLong();
            int numPackStreams = (int) ReadLong();

            byte type = GetByte();
            if (type == 0x09)
            {
                for (int i = 0; i < numPackStreams; ++i)
                {
                    /* long packSize = */ ReadLong();
                }
                type = GetByte();
            }
            if (type == 0x0a)
            {
                ReadDigests(numPackStreams);
                type = GetByte();
            }
            if (type != 0x00)
            {
                throw new IOException(LibraryResources.UnsupportedFormat);
            }
        }

        /// <summary>
        /// Read folder info block for a single folder
        /// </summary>
        /// <returns>Read folder info</returns>
        private FolderInfo ReadFolder()
        {
            int numCoders = (int) ReadLong();
            if (numCoders != 1) throw new IOException(LibraryResources.UnsupportedFormat);

            int numInStreamsTotal = 0;
            int numOutStreamsTotal = 0;

            var result = new FolderInfo();
            byte paramz = GetByte();
            int idSize = paramz & 0x0F;
            var decompressionMethod = new byte[idSize];
            ReadFully(decompressionMethod);
            if (idSize != 3 || decompressionMethod[0] != 3 || decompressionMethod[1] != 1 || decompressionMethod[2] != 1)
            {
                throw new IOException(LibraryResources.UnsupportedFormat);
            }
            if ((paramz & 0x10) != 0)
            {
                result.numInStreams = (int) ReadLong();
                result.numOutStreams = (int) ReadLong();
            }
            else
            {
                result.numInStreams = 1;
                result.numOutStreams = 1;
            }
            numInStreamsTotal += result.numInStreams;
            numOutStreamsTotal += result.numOutStreams;
            if (decompressionMethod[0] != 0)
            {
                int propSize = (int) ReadLong();
                var properties = new byte[propSize];
                ReadFully(properties);
                result.properties = properties;
            }

            for (int i = 0; i < numOutStreamsTotal - 1; ++i)
            {
                /* long inIndex = */ ReadLong();
                /* long outIndex = */ ReadLong();
            }

            int numPackedStreams = numInStreamsTotal - numOutStreamsTotal + 1;
            if (numPackedStreams > 1)
                for (int i = 0; i < numPackedStreams; ++i)
                {
                    /* long index = */ ReadLong();
                }

            return result;
        }

        /// <summary>
        /// Read coders info block
        /// </summary>
        private void ReadCodersInfo()
        {
            byte type = GetByte();
            int numFolders = 0;
            if (type == 0x0b)
            {
                numFolders = (int) ReadLong();
                if (numFolders != 1) throw new IOException(LibraryResources.UnsupportedFormat);
                folders = new FolderInfo[numFolders];
                bool external = ReadBool();
                if (external)
                {
                    /* long dataStreamIndex = */ ReadLong();
                }
                else
                {
                    for (int i = 0; i < numFolders; ++i)
                    {
                        folders[i] = ReadFolder();
                    }
                }
                type = GetByte();
            }
            if (type == 0x0c)
            {
                if (folders != null)
                {
                    for (int i = 0; i < numFolders; ++i)
                    {
                        for (int j = 0; j < folders[i].numOutStreams; ++j)
                        {
                            folders[i].unpackSize += ReadLong();
                        }
                    }
                }

                type = GetByte();
            }
            if (type == 0x0a)
            {
                ReadDigests(numFolders);
                type = GetByte();
            }
            if (type != 0x00)
            {
                throw new IOException(LibraryResources.UnsupportedFormat);
            }
        }

        /// <summary>
        /// Read substream info block
        /// </summary>
        private void ReadSubstreamInfo()
        {
            byte type = GetByte();
            // NumUnPackStream unsupported yet
            // UnPackSizes unsupported yet
            if (type == 0x0a)
            {
                // another number of digests not supported yet
                ReadDigests(1);
                type = GetByte();
            }
            if (type != 0x00)
            {
                throw new IOException(LibraryResources.UnsupportedFormat);
            }
        }

        /// <summary>
        /// Read streams info block
        /// </summary>
        private void ReadStreamsInfo()
        {
            byte type = GetByte();

            if (type == 0x06)
            {
                ReadPackInfo();
                type = GetByte();
            }
            if (type == 0x07)
            {
                ReadCodersInfo();
                type = GetByte();
            }
            if (type == 0x08)
            {
                ReadSubstreamInfo();
                type = GetByte();
            }
            if (type != 0x00)
            {
                throw new IOException(LibraryResources.UnsupportedFormat);
            }
        }

        /// <summary>
        /// Folder info block
        /// </summary>
        private class FolderInfo
        {
            public int numInStreams;
            public int numOutStreams;
            public byte[] properties;
            public long unpackSize;
        }

        #region ------ Memory buffer --------------------------------------------------------------

        /// <summary>
        /// Memory buffer implementing a queue for producer-consumer communication offering stream-like interface.
        /// </summary>
        private class MemoryBufferStream : Stream
        {
            /// <summary>
            /// Buffer holding the queue with the transferred data
            /// </summary>
            private byte[] buffer;

            /// <summary>
            /// Position of the head of the queue in <see cref="buffer"/>
            /// </summary>
            private int head;

            /// <summary>
            /// Position of the tail of the queue in <see cref="buffer"/>
            /// </summary>
            private int tail;

            /// <summary>
            /// Free capacity in <see cref="buffer"/>
            /// </summary>
            private int free;

            /// <summary>
            /// Total capacity of <see cref="buffer"/>
            /// </summary>
            private int capacity;

            /// <summary>
            /// Has this stream been already closed?
            /// </summary>
            private bool isClosed;

#if CHECKED

    /// <summary>
    /// The buffer is blocking a read operation
    /// </summary>
            private bool readWaiting;

            /// <summary>
            /// The buffer is blocking a write operation
            /// </summary>
            private bool writeWaiting;

#endif

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="capacity">Capacity of the buffer (size of the queue), in bytes</param>
            public MemoryBufferStream(int capacity)
            {
                this.capacity = capacity;
                buffer = new byte[capacity];
                free = capacity;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException(LibraryResources.SeekingNotSupported);
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException(LibraryResources.SettingLengthNotSupported);
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return true; }
            }

            public override long Length
            {
                get { throw new NotSupportedException(LibraryResources.SeekingNotSupported); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(LibraryResources.SeekingNotSupported); }
                set { throw new NotSupportedException(LibraryResources.SeekingNotSupported); }
            }

            public override void Close()
            {
                lock (this)
                {
                    isClosed = true;
                    Monitor.PulseAll(this);
                }
                base.Close();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (this)
                {
                    //System.out.println("write(" + count + "), head = " + head + ", tail = " + tail + ", free = " + free);
                    while (count > 0)
                    {
                        if (isClosed) throw new InvalidOperationException(LibraryResources.StreamAlreadyClosed);
                        while (free == 0)
                        {
                            if (isClosed) throw new InvalidOperationException(LibraryResources.StreamAlreadyClosed);
                            try
                            {
#if CHECKED
                                if (readWaiting) throw new InvalidOperationException("Deadlock!");
                                writeWaiting = true;
#endif
                                Monitor.Wait(this);
#if CHECKED
                                writeWaiting = false;
#endif
                            }
                            catch (InvalidOperationException)
                            {
                            }
                        }
                        int l = count;
                        if (free < l) l = free;

                        if (free > 0.7*capacity)
                        {
                            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                        }

                        //if (free < l) throw new IllegalStateException("The buffer is full");
                        free -= l;
                        for (int i = 0; i < l; ++i)
                        {
                            this.buffer[head] = buffer[i + offset];
                            head = (head + 1)%capacity;
                        }

                        if (free < 0.3*capacity)
                        {
                            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                        }

                        count -= l;
                        offset += l;
                        //System.out.println("...> head = " + head + ", tail = " + tail + ", free = " + free);
                        Monitor.PulseAll(this);
                    }
                    //System.out.println("---> head = " + head + ", tail = " + tail + ", free = " + free);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                lock (this)
                {
                    //System.out.println("read(" + count + "), head = " + head + ", tail = " + tail + ", free = " + free);
                    while (capacity - free == 0)
                    {
                        if (isClosed) return 0;
                        try
                        {
#if CHECKED
                            if (writeWaiting) throw new InvalidOperationException("Deadlock!");
                            readWaiting = true;
#endif
                            Monitor.Wait(this);
#if CHECKED
                            readWaiting = false;
#endif
                        }
                        catch (ThreadInterruptedException)
                        {
                        }
                    }

                    if (capacity - free < count) count = capacity - free;

                    //if (capacity - free < count) throw new IllegalStateException("The buffer is empty");
                    free += count;
                    for (int i = 0; i < count; ++i)
                    {
                        buffer[i + offset] = this.buffer[tail];
                        tail = (tail + 1)%capacity;
                    }

                    /*
                    try
                    {
                        debugDump.write(buffer, offset, count);
                        debugDump.flush();
                    }
                    catch(IOException e)
                    {
                    }
                    */
                    //System.out.println("===> head = " + head + ", tail = " + tail + ", free = " + free + ", count = " + count);
                    Monitor.PulseAll(this);
                    return count;
                }
            }
        }

        #endregion

        #region ------ Decompression thread -------------------------------------------------------

        /// <summary>
        /// Thread for decompressing LZMA data
        /// </summary>
        private class DecodingThread
        {
            /// <summary>
            /// Handler for errors during decompression
            /// </summary>
            /// <param name="e">Exception reported from decompression</param>
            public delegate void ErrorHandler(Exception e);

            /// <summary>
            /// LZMA decoder implementation
            /// </summary>
            private Decoder decoder;

            /// <summary>
            /// Input stream containing LZMA compressed data
            /// </summary>
            private Stream input;

            /// <summary>
            /// Output stream into which the decompressed data are written
            /// </summary>
            private Stream output;

            /// <summary>
            /// Size of output data
            /// </summary>
            private long dataSize;

            /// <summary>
            /// An interface requesting progress notifications
            /// </summary>
            private ICodeProgress progress;

            /// <summary>
            /// The .NET thread object for the decompression thread
            /// </summary>
            private Thread thread;

            /// <summary>
            /// Handler for errors during decompression
            /// </summary>
            private ErrorHandler errorHandler;

            /// <summary>
            /// Constructor, initializes the thread
            /// </summary>
            /// <param name="input">Input stream containing the data to be decompressed</param>
            /// <param name="output">Output stream the decompressed data should be written to</param>
            /// <param name="dataSize">Size of output data</param>
            /// <param name="properties">Decoder properties (read from a .7z file), or <c>null</c> if none specified</param>
            /// <param name="progress">An interface for reporting progress notifications</param>
            /// <param name="errorHandler">Handler for errors during decompression</param>
            public DecodingThread(Stream input, Stream output, long dataSize, byte[] properties, ICodeProgress progress, ErrorHandler errorHandler)
            {
                if (input == null) throw new ArgumentNullException("input");
                if (output == null) throw new ArgumentNullException("output");
                if (!input.CanRead) throw new ArgumentException(LibraryResources.ReadableStreamRequired, "input");
                if (!output.CanWrite) throw new ArgumentException(LibraryResources.WritableStreamRequired, "output");
                this.input = input;
                this.output = output;
                this.dataSize = dataSize;
                decoder = new Decoder();
                if (properties != null)
                    decoder.SetDecoderProperties(properties);
                this.progress = progress;
                this.errorHandler = errorHandler;
            }

            private void Run()
            {
                try
                {
                    decoder.Code(input, output, 0, dataSize, progress);
                    input.Close();
                    output.Close();
                }
                catch (ThreadAbortException)
                {
                    // aborted...
                }
                catch (Exception e)
                {
                    // report the exception to the reader and bail out
                    if (errorHandler != null)
                    {
                        errorHandler(e);
                    }
                    else
                    {
                        // Oops! We do not know what to do with the exception! So let us die!
                        throw;
                    }
                }
            }

            public void Start()
            {
                thread = new Thread(Run) {Name = "Decompressor", IsBackground = true};
                thread.Start();
            }

            public void Abort()
            {
                thread.Abort();
            }
        }

        #endregion
    }
}
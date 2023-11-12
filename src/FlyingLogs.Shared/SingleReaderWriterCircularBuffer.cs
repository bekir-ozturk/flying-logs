using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FlyingLogs.Shared
{
    /// <summary>
    /// A single-reader, single-writer circular buffer implementation
    /// for storing contiguous memory sections.
    /// </summary>
    /// <remarks>
    /// Each memory section written into the buffer is prefixed with its size.
    /// Prefix with a value of zero (meaning that a buffer with zero size was pushed)
    /// means that the rest of the data in the buffer is invalid and the reader should
    /// start reading from index 0 instead. This can happen where memory continuity
    /// cannot be achieved near the end of the buffer, but there is enough space at
    /// the beginning.
    /// </remarks>
    public class SingleReaderWriterCircularBuffer
    {
        private readonly byte[] _buffer;
        private volatile int _writeHead;
        private volatile int _readHead;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleReaderWriterCircularBuffer"/> class.
        /// </summary>
        /// <param name="capacity">Capacity of the buffer. The actual size of the data
        /// that can be written to the buffer will be slightly smaller for two reasons:
        /// <list type="number">
        /// <item>
        /// Every write operation will use an additional 4 bytes as a prefix to store the data length.
        /// </item>
        /// <item>
        /// As a result of contiguous memory requirement, if the buffer doesn't have enough space
        /// at the end to store the given data, the insufficient space at the end will be skipped.
        /// Write operation will continue from index 0, resulting in wasted memory.</item>
        /// </list></param>
        public SingleReaderWriterCircularBuffer(int capacity)
        {
            // Allocate 4 more bytes to allow read/write ahead without bounds checking
            // in certain situations.
            _buffer = new byte[capacity + 4];
        }

        private int RingSize => _buffer.Length - 4;

        /// <summary>
        /// Returns a memory section to be written into.
        /// The data will not be available to the reader until <see cref="Push(int)"/> is called.
        /// Calling this method multiple times without calling <see cref="Push(int)"/> will repeatedly return
        /// the same memory section.
        /// </summary>
        /// <param name="size">The size of the memory section to be reserved for writing.</param>
        /// <returns>The memory section that is reserved for writing. If there is not enough contiguous space in the
        /// buffer for the specified size, an memory of zero size is returned.</returns>
        public Memory<byte> PeekWrite(int size)
        {
            if (size <= 0)
            {
                return Memory<byte>.Empty;
            }

            // Store in local variable to prevent aync updates.
            int readHead = _readHead;

            if (_writeHead >= readHead)
            {
                // Do we have enough space ahead for the data? We need +4 bytes for size prefix.
                if (RingSize - _writeHead < size + 4 ||
                    // Sometimes, we have just enough space, but _readHead is zero and writing would cause a leap.
                    (readHead == 0 && _writeHead + size + 4 == RingSize))
                {
                    // Not enough space at the end. Can we use the space at the beginning of the buffer?
                    // _witeHead cannot do full circle on _readHead. Make sure we have at least +1 byte in between.
                    if (readHead < size + 4 + 1)
                    {
                        // Not enough contiguous space at the moment
                        if (size + 4 + 1 < RingSize // Ring has enough space.
                            && _writeHead < size + 4 + 1 // _writeHead is blocking the way, if we start at index-0.
                            && _readHead != 0) // We shouldn't do a full circle.
                        {
                            // This buffer is big enough for this data when empty.
                            // But if _writeHead never moves out of the way, we will never be able to use the space as
                            // we are blocking the way. Skip the rest of the buffer and go to index-0. _readHead will
                            // eventually catch up and we will have enough space for this.
                            // "+1"s above are to prevent _writeHead doing a full circle on _readHead.
                            _buffer[_writeHead] = _buffer[_writeHead + 1] = 0;
                            _buffer[_writeHead + 2] = _buffer[_writeHead + 3] = 0;
                            _writeHead = 0;
                        }

                        return Memory<byte>.Empty;
                    }

                    // There isn't enough space at the end. But if we skip ahead, we can fit this at index-0.
                    _buffer[_writeHead] = _buffer[_writeHead + 1] = 0;
                    _buffer[_writeHead + 2] = _buffer[_writeHead + 3] = 0;
                    _writeHead = 0;
                }
            }
            else
            {
                // WriteHead cannot do full circle on ReadHead. Make sure we have at least +1 byte in between.
                if (_readHead - _writeHead - 1 < size + 4)
                {
                    // Not enough space.
                    return Memory<byte>.Empty;
                }
            }

            // Give away the memory. Skip the first 4 bytes: we will use it to store the size of the data.
            return _buffer.AsMemory(_writeHead + 4, size);
        }

        /// <summary>
        /// Commits the memory with the given size that was previously returned with <see cref="PeekWrite(int)"/>.
        /// After calling this method, the data is made available to the reader. Therefore, the memory should never be
        /// referenced after this call.
        /// The size can be lower than what was initially passed to <see cref="PeekWrite(int)"/>. But it should never
        /// be greater. If you need to push more memory, first call <see cref="PeekWrite(int)"/> with the required size.
        /// </summary>
        /// <param name="size">Length of the data to be made available to the reader. </param>
        /// <exception cref="ArgumentOutOfRangeException">The specified size is not available in the buffer.
        /// This can only happen if a size greater than what was reserved with <see cref="PeekWrite(int)"/> was passed
        /// to this method. </exception>
        public void Push(int size)
        {
            if (size <= 0)
            {
                return;
            }

            // Store in local variable to prevent aync updates.
            int readHead = _readHead;
            if (_writeHead >= readHead)
            {
                if (RingSize - _writeHead < size + 4 ||
                    (readHead == 0 && _writeHead + size + 4 == RingSize))
                {
                    // PeekWrite should have already considered skipping to index-0.
                    // If there isn't enough space already here, no need to check the head.
                    throw new ArgumentOutOfRangeException(nameof(size));
                }
            }
            else if (readHead - _writeHead - 1 < size + 4)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            MemoryMarshal.Write(_buffer.AsSpan().Slice(_writeHead), ref size);

            _writeHead += size + 4;
        }

        /// <summary>
        /// Attempts to read the next available section in the buffer.
        /// If no data is available, a zero-length memory will be returned.
        /// Processing of the returned data should be completed before calling <see cref="Pop"/>.
        /// After that, the memory region will be available for the writer, and therefore, should not be used.
        /// </summary>
        public ReadOnlyMemory<byte> PeekRead()
        {
            ReadOnlyMemory<byte> memory = PeekReadWithSizePrefix();
            if (memory.Length == 0)
            {
                return memory;
            }

            // Skip the prefix.
            return memory.Slice(4);
        }

        /// <summary>
        /// Attempts to read the next available section in the buffer, including the first 4 bytes that contain data
        /// length. If no data is available, a zero-length memory will be returned.
        /// Processing of the returned data should be completed before calling <see cref="Pop"/>.
        /// After that, the memory region will be available for the writer, and therefore, should not be used.
        /// </summary>
        public ReadOnlyMemory<byte> PeekReadWithSizePrefix()
        {
            if (_readHead == _writeHead)
            {
                // No data to read.
                return ReadOnlyMemory<byte>.Empty;
            }

            // Read ahead without checking for bounds. We have extra 4 bytes allocated at the end.
            // If we read too much ahead, we will read zero and go back to index-0.
            int dataLength = MemoryMarshal.Read<int>(_buffer.AsSpan().Slice(_readHead));
            if (dataLength == 0)
            {
                // Rest of the data in the buffer is invalid and should be skipped.
                _readHead = 0;
                if (_writeHead == 0)
                {
                    // We met _writeHead at index-0. No data to read.
                    return ReadOnlyMemory<byte>.Empty;
                }
                
                dataLength = MemoryMarshal.Read<int>(_buffer.AsSpan());
            }

            return _buffer.AsMemory(_readHead, dataLength + 4);
        }

        /// <summary>
        /// Attempts to read the next available section in the buffer, including the first 4 bytes that contain data
        /// length. If no data is available, a zero-length memory will be returned.
        /// Processing of the returned data should be completed before calling <see cref="Pop"/>.
        /// After that, the memory region will be available for the writer, and therefore, should not be used.
        /// </summary>
        public int PeekReadUpToBytes(int byteLimit, List<Memory<byte>> results)
        {
            int bytesFetched = 0;
            int readAheadHead = _readHead;
            while (bytesFetched < byteLimit)
            {
                if (readAheadHead == _writeHead)
                {
                    // No more data to read.
                    return bytesFetched;
                }

                // Read ahead without checking for bounds. We have extra 4 bytes allocated at the end.
                // If we read too much ahead, we will read zero and go back to index-0.
                int dataLength = MemoryMarshal.Read<int>(_buffer.AsSpan().Slice(readAheadHead));
                if (dataLength == 0)
                {
                    // Rest of the data in the buffer is invalid and should be skipped.
                    readAheadHead = 0;
                    if (_writeHead == 0)
                    {
                        // We met _writeHead at index-0. No data to read.
                        return bytesFetched;
                    }

                    dataLength = MemoryMarshal.Read<int>(_buffer.AsSpan());
                }

                results.Add(_buffer.AsMemory(readAheadHead + 4, dataLength));
                bytesFetched += dataLength;
                readAheadHead += 4 + dataLength;
            }
            return bytesFetched;
        }

        /// <summary>
        /// Releases the first piece of data in the buffer to the writer.
        /// The data received by <see cref="PeekRead"/> or <see cref="PeekReadWithSizePrefix"/> should not be
        /// referenced after this method was called.
        /// </summary>
        /// <param name="poppedByteCount">The size of the data that was pop out of the buffer. In bytes.</param>
        /// <exception cref="InvalidOperationException">There is no data in the buffer to pop.</exception>
        public void Pop(out int poppedByteCount)
        {
            ReadOnlyMemory<byte> data = PeekReadWithSizePrefix();
            if (data.Length == 0)
            {
                throw new InvalidOperationException();
            }

            // Caller wants to know about the removed data size. Don't include the 4 byte length prefix header.
            poppedByteCount = data.Length - 4;
            _readHead += data.Length;
        }
    }
}

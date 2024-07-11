using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Sinks.Seq;

namespace FlyingLogs.Sinks
{
    /// <summary>
    /// A simple sink that pushes log events to Seq. Note that this is a lightweight socket based implementation with
    /// no HTTPS support.
    /// </summary>
    public sealed class SeqHttpSink : Sink
    {
        /// <summary>
        /// The task that periodically checks for queued log events and pushes them to Seq.
        /// </summary>
        private readonly Task IngestionTask;

        /// <summary>
        /// Ring buffer that contains the log events that was queued, but not yet fully transmitted.
        /// Each thread has a separate buffer.
        /// </summary>
        private readonly ThreadLocal<SingleReaderWriterCircularBuffer> Buffer = new(
            // As of .NET 8, ThreadLocal.Values does not have an allocation-free alternative.
            // Until then, we will track queues ourselves. See _eventQueueRoot.
            trackAllValues: false);

        /// <summary>
        /// Non-zero if we should stop ingesting once all the queues are empty.
        /// </summary>
        private int _drainRequested = 0;

        private MultipleInsertSingleDeleteLinkedList<(
            // Buffer that we will read the events from.
            SingleReaderWriterCircularBuffer Buffer,
            // A reference to the owning thread. This is used to determine whether the thread has terminated.
            Thread OwningThread)> _ingestingThreads = new ();
        
        private static readonly byte[] _headerBytes = Encoding.ASCII.GetBytes(@"POST /api/events/raw?clef HTTP/1.1
Host: localhost
Connection: Keep-Alive
Content-Type: text/plain
Transfer-Encoding: chunked

");
        private static readonly byte[] _crlfBytes = { (byte)'\r', (byte)'\n' };
        private static readonly byte[] _chunkEndMarkerBytes = { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
        private static readonly byte[] _hexToChar = { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };
        private static readonly byte[] _http11Bytes = Encoding.ASCII.GetBytes("HTTP/1.1 ");

        private enum JsonValueQuotes
        {
            // We rely on default being 'not needed', dont change.
            NotNeeded = 0,
            Needed = 1,
            SpecialCaseFloatDouble = 2
        }

        private static readonly FrozenDictionary<Type, JsonValueQuotes> _quotedTypes = new Dictionary<Type, JsonValueQuotes>()
        {
            { typeof(sbyte), JsonValueQuotes.NotNeeded},
            { typeof(byte), JsonValueQuotes.NotNeeded},
            { typeof(short), JsonValueQuotes.NotNeeded},
            { typeof(ushort), JsonValueQuotes.NotNeeded},
            { typeof(int), JsonValueQuotes.NotNeeded},
            { typeof(uint), JsonValueQuotes.NotNeeded},
            { typeof(nint), JsonValueQuotes.NotNeeded},
            { typeof(nuint), JsonValueQuotes.NotNeeded},
            { typeof(long), JsonValueQuotes.NotNeeded},
            { typeof(ulong), JsonValueQuotes.NotNeeded},
            { typeof(float), JsonValueQuotes.SpecialCaseFloatDouble},
            { typeof(double), JsonValueQuotes.SpecialCaseFloatDouble},
            { typeof(decimal), JsonValueQuotes.NotNeeded},
        }.ToFrozenDictionary();

        public SeqHttpSink(string hostAddress, int port) : base(LogEncodings.Utf8Json)
        {
            HostAddress = hostAddress ?? throw new ArgumentNullException(nameof(hostAddress));
            Port = port;

            IngestionTask = Task.Run(SendToSeqPeriodically);
        }

        /// <summary>
        /// Gets the address of the Seq host that we will be sending the logs to.
        /// </summary>
        public string HostAddress { get; init; }

        /// <summary>
        /// Gets the port number of the Seq host that we will be sending the logs to.
        /// </summary>
        public int Port { get; init; }

        public override void Ingest(
            LogTemplate log,
            IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
            Memory<byte> temporaryBuffer)
        {
            Span<byte> writeHead = temporaryBuffer.Span;

            try
            {
                CopyToAndMoveDestination("{\""u8, ref writeHead);
                CopyToAndMoveDestination("@t\":\""u8, ref writeHead);
                // TODO process the error.
                _ = DateTime.UtcNow.TryFormat(writeHead, out int timestampBytes, "s", CultureInfo.InvariantCulture);
                writeHead = writeHead.Slice(timestampBytes);
                CopyToAndMoveDestination("\",\"@mt\":\""u8, ref writeHead);
                CopyToAndMoveDestination(log.TemplateString.Span, ref writeHead);
                CopyToAndMoveDestination("\",\"@i\":\""u8, ref writeHead);
                CopyToAndMoveDestination(log.EventId.Span, ref writeHead);
                CopyToAndMoveDestination("\",\"@l\":\""u8, ref writeHead);
                CopyToAndMoveDestination(Constants.LogLevelsUtf8Json.Span[(int)log.Level].Span, ref writeHead);
                CopyToAndMoveDestination("\""u8, ref writeHead);
                
                int propCount = log.PropertyNames.Length;
                int previousPropertyDepth = 0;
                for (int i = 0; i < propCount; i++)
                {
                    int depthDiff = log.PropertyDepths.Span[i] - previousPropertyDepth;
                    while (log.PropertyDepths.Span[i] > previousPropertyDepth++)
                        CopyToAndMoveDestination("{"u8, ref writeHead);
                    
                    while (log.PropertyDepths.Span[i] < previousPropertyDepth--)
                        CopyToAndMoveDestination("}"u8, ref writeHead);

                    // Don't start with a comma if we just went to a deeper level to avoid {,"name":"value"}.
                    CopyToAndMoveDestination(depthDiff > 0 ? "\""u8 : ",\""u8, ref writeHead);
                    CopyToAndMoveDestination(log.PropertyNames.Span[i].Span, ref writeHead);
                    CopyToAndMoveDestination("\":"u8, ref writeHead);

                    ReadOnlySpan<byte> value = propertyValues[i].Span;
                    if (value == PropertyValueHints.Complex.Span)
                    {
                        if (i + 1 == propCount || log.PropertyDepths.Span[i+1] <= previousPropertyDepth)
                        {
                            // The object is complex, but has no fields.
                            CopyToAndMoveDestination("{}"u8, ref writeHead);
                        }
                        continue;
                    }
                    else if (value == PropertyValueHints.Null.Span)
                    {
                        CopyToAndMoveDestination("null"u8, ref writeHead);

                        // If this is a complex object with null value, we should skip the deeper fields.
                        while (i + 1 < propCount && log.PropertyDepths.Span[i+1] > previousPropertyDepth)
                            i++;
                        continue;
                    }

                    bool addQuotes = false;
                    _quotedTypes.TryGetValue(log.PropertyTypes.Span[i], out JsonValueQuotes quoteNeed);
                    if (quoteNeed == JsonValueQuotes.Needed
                         // If value length is zero, fallback to quotes to avoid invalid JSON.
                         || value.Length == 0)
                    {
                        addQuotes = true;
                    }
                    else if (quoteNeed == JsonValueQuotes.SpecialCaseFloatDouble
                        // We always use InvariantCulture for numbers, so the comparisons below should work.
                        && (value[value.Length - 1] == "y"u8[0] // Catch Infinity and -Infinity
                        || value[value.Length - 1] == "N"u8[0])) // Catch NaN
                    {
                        addQuotes = true;
                    }

                    if (addQuotes)
                    {
                        CopyToAndMoveDestination("\""u8, ref writeHead);
                    }

                    CopyToAndMoveDestination(value, ref writeHead);

                    if (addQuotes)
                    {
                        CopyToAndMoveDestination("\""u8, ref writeHead);
                    }
                }

                // Close all open curly brackets. One extra for the main CLEF object.
                while (-1 < previousPropertyDepth--)
                    CopyToAndMoveDestination("}"u8, ref writeHead);
            }
            catch
            {
                // TODO emit error metrics?
                // Maybe we should throw our own exception and invoke caller's error callback above.
                // IndexOutOfRange exception can better be thrown as InsufficientBufferSpaceException.
                // But can we be sure that IOOR isn't thrown in any other case?
                // We don't want to falsely blame buffer size.
                return;
            }

            var ringBuffer = Buffer.Value ?? InitializeCurrentThread();
            int usedBytes = temporaryBuffer.Length - writeHead.Length;
            var buffer = ringBuffer.PeekWrite(usedBytes);
            if (buffer.IsEmpty)
            {
                // No more space left in the queue.
                // TODO emit overflow metric
                return;
            }

            ringBuffer.Push(usedBytes);
        }

        public Task DrainAsync()
        {
            Interlocked.Exchange(ref _drainRequested, 1);
            return IngestionTask;
        }

        private async Task SendToSeqPeriodically()
        {
            byte[] tmpBuffer = new byte[2048];
            List<Memory<byte>> tmpChunks = new(32);
            int failedConnectionCount = 0;
            bool anyLogsSinceRoot = false;
            
            // Did we have a request to start draining when we started over from the root node?
            // This is to give all the threads a chance to queue events after the drain flag is set.
            // Draining is still on a best effort basis; there is no guarantee we'll catch all the last minute logs
            // before we stop.
            bool drainRequestAtRoot = _drainRequested == 1;

            while (true)
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    NetworkStream networkStream;

                    try
                    {
                        await tcpClient.ConnectAsync(HostAddress, Port);
                        networkStream = tcpClient.GetStream();
                        failedConnectionCount = 0;
                    }
                    catch
                    {
                        // Connection failed. Avoid hot loops caused by exceptions
                        // TODO emit metric
                        await Task.Delay(failedConnectionCount * failedConnectionCount * 1000 + 500);
                        failedConnectionCount = failedConnectionCount < 2 ? failedConnectionCount + 1 : 2;
                        continue;
                    }

                    try
                    {
                        while (true)
                        {
                            await _ingestingThreads.DeleteAllAsync(
                                async (x) =>
                                {
                                    // We remove a thread from the queue if two things are true:
                                    // - Thread is no longer alive
                                    // - We processed all the awaiting events for the thread.
                                    // To determine whether to remove a thread, check the thread state first.
                                    // Otherwise, the thread can push new events just before it terminates and right after
                                    // we processed its previous batch of events. We'd be removing the thread despite there
                                    // being more events in its backlog.
                                    bool threadTerminated = x.OwningThread.IsAlive == false;
                                    int transferredEvents = await ProcessEventQueue(
                                        x.Buffer,
                                        networkStream,
                                        tmpBuffer,
                                        tmpChunks);

                                    if (transferredEvents > 0)
                                    {
                                        anyLogsSinceRoot = true;
                                    }
                                    else if (threadTerminated)
                                    {
                                        return MultipleInsertSingleDeleteLinkedList
                                            <(SingleReaderWriterCircularBuffer, Thread)>
                                            .DeletionOption.Delete;
                                    }

                                    return MultipleInsertSingleDeleteLinkedList
                                        <(SingleReaderWriterCircularBuffer, Thread)>
                                        .DeletionOption.Retain;
                                }
                            );

                            // We have reached the end of the queue. Do we need to quit or slow down?
                            if (anyLogsSinceRoot == false)
                            {
                                // We traversed the whole list but no threads queued any work for us.
                                if (drainRequestAtRoot)
                                {
                                    // At the beginning of the last round, we knew we'd stop once all queues are empty.
                                    return;
                                }

                                // Wait idle to avoid hot loops.
                                await Task.Delay(500);
                            }

                            anyLogsSinceRoot = false;
                            drainRequestAtRoot = _drainRequested == 1;
                        }
                    }
                    catch
                    {
                        // Socket must be dead. We need a new one.
                    }
                }
            }
        }

        private async ValueTask<int> ProcessEventQueue(
            SingleReaderWriterCircularBuffer buffer,
            NetworkStream networkStream,
            byte[] tmpBuffer,
            List<Memory<byte>> tmpChunks)
        {
            tmpChunks.Clear();
            int fetchedByteCount = buffer!.PeekReadUpToBytes(8 * 1024 * 1024, tmpChunks);

            if (fetchedByteCount == 0)
            {
                // No new logs to ingest.
                return 0;
            }

            await networkStream.WriteAsync(_headerBytes);

            // Write the chunk size
            {
                // Length of string + 2 bytes for the \r\n at the end of each line, excluding the last.
                int dataLen = fetchedByteCount + 2 * (tmpChunks.Count - 1);
                // Reuse tmpBuffer to store the chunk size
                tmpBuffer[0] = _hexToChar[dataLen >> 28 & 0xF];
                tmpBuffer[1] = _hexToChar[dataLen >> 24 & 0xF];
                tmpBuffer[2] = _hexToChar[dataLen >> 20 & 0xF];
                tmpBuffer[3] = _hexToChar[dataLen >> 16 & 0xF];
                tmpBuffer[4] = _hexToChar[dataLen >> 12 & 0xF];
                tmpBuffer[5] = _hexToChar[dataLen >> 8 & 0xF];
                tmpBuffer[6] = _hexToChar[dataLen >> 4 & 0xF];
                tmpBuffer[7] = _hexToChar[dataLen & 0xF];
                tmpBuffer[8] = (byte)'\r';
                tmpBuffer[9] = (byte)'\n';
                await networkStream.WriteAsync(tmpBuffer.AsMemory().Slice(0, 10));
            }

            for (int i = 0; i < tmpChunks.Count; i++)
            {
                await networkStream.WriteAsync(tmpChunks[i]);
                // All crlf here are line separators, except for the last.
                // The last one is marking the end of the chunk for http.
                await networkStream.WriteAsync(_crlfBytes);
            }
            await networkStream.WriteAsync(_chunkEndMarkerBytes);

            // Bytes from the beginning of the buffer that we no longer care about.
            int processedByteCount = 0;
            // Bytes from the beginning of the buffer that we searched for new line character
            int scannedByteCount = 0;
            // Total bytes in the buffer
            int receivedByteCount = 0;

            while (true)
            {
                bool ingestionVerified = false;
                for (; scannedByteCount < receivedByteCount; scannedByteCount++)
                {
                    if (tmpBuffer[scannedByteCount] == '\n')
                    {
                        var line = tmpBuffer.AsMemory().Slice(processedByteCount, scannedByteCount - processedByteCount);
                        if (line.Length > 0 && line.Span[line.Length - 1] == '\r')
                            line = line.Slice(0, line.Length - 1);
                        // We no longer care about this part of the buffer
                        processedByteCount = scannedByteCount + 1;

                        if (tmpBuffer.AsSpan(0, _http11Bytes.Length).SequenceEqual(_http11Bytes.AsSpan()))
                        {
                            // An http response was received. Parse status code.
                            int statusCode = 0;
                            for (int i = _http11Bytes.Length; i < line.Length; i++)
                            {
                                if (statusCode != 0 && (line.Span[i] < '0' || line.Span[i] > '9'))
                                    break;
                                statusCode *= 10;
                                statusCode += line.Span[i] - '0';
                            }

                            ingestionVerified = true;
                            break;
                        }
                    }
                }

                if (ingestionVerified)
                {
                    break;
                }

                // Remaining bytes don't form a full line. We need to read more.
                // Discard all the processed bytes first.
                tmpBuffer.AsMemory().Slice(processedByteCount).CopyTo(tmpBuffer);
                scannedByteCount -= processedByteCount;
                receivedByteCount -= processedByteCount;
                processedByteCount = 0;

                receivedByteCount += await networkStream.ReadAsync(tmpBuffer);
            }

            // Logs were successfully received. We can discard them.
            for (int i = 0; i < tmpChunks.Count; i++)
                buffer.Pop(out _);
            return tmpChunks.Count;
        }

        private SingleReaderWriterCircularBuffer InitializeCurrentThread()
        {
            var buffer = Buffer.Value = new SingleReaderWriterCircularBuffer(8 * 1024);
            var thread = Thread.CurrentThread;
            _ingestingThreads.Insert((buffer, thread));
            return buffer;
        }

        private static void CopyToAndMoveDestination(ReadOnlySpan<byte> source, ref Span<byte> destination)
        {
            source.CopyTo(destination);
            destination = destination.Slice(source.Length);
        }
    }
}

﻿using System.Diagnostics;
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

        public override void Ingest(LogTemplate log, IReadOnlyList<ReadOnlyMemory<byte>> propertyValues)
        {
            var ringBuffer = Buffer.Value ?? InitializeCurrentThread();

            // TODO rethink this. We can't calculate the length like this when complex objects are involved.
            // For int and doubles etc, we also don't use quotes, so we'll need less bytes.

            // TODO calculate timestamp string

            int dataLen =
                2 // Open and close curly brackets
                + 5 * (4 + log.PropertyNames.Length) // 2 quotes surrounding the names, 2 quotes surrounding the values and
                                                 // a semicolon in the middle for @t, @mt, @i, @l and each property.
                + (4 + log.PropertyNames.Length - 1) // A comma between @t, @mt, @i, @l and the other properties.
                + 9 // Names of built-in properties: @t, @mt, @i, @l
                + log.TemplateString.Length
                + log.EventId
                + Constants.LogLevelsUtf8Json[log.Level]
                + timestamp.length;

            // Add the length of property names and values
            for (int i = 0; i < log.Properties.Count; i++)
                dataLen += log.Properties[i].name.Length + log.Properties[i].value.Length;

            var buffer = ringBuffer.PeekWrite(dataLen);
            if (buffer.IsEmpty)
            {
                // No more space left in the queue.
                // TODO emit overflow metric
                return;
            }

            Span<byte> writeHead = buffer.Span;
            CopyToAndMoveDestination("{\""u8, ref writeHead);
            CopyToAndMoveDestination("@t\":\""u8, ref writeHead);
            CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.Timestamp].Span, ref writeHead);
            CopyToAndMoveDestination("\",\"@mt\":\""u8, ref writeHead);
            CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.Template].Span, ref writeHead);
            CopyToAndMoveDestination("\",\"@i\":\""u8, ref writeHead);
            CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.EventId].Span, ref writeHead);
            CopyToAndMoveDestination("\",\"@l\":\""u8, ref writeHead);
            CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.Level].Span, ref writeHead);

            for (int i = 0; i < log.Properties.Count; i++)
            {
                CopyToAndMoveDestination("\",\""u8, ref writeHead);
                CopyToAndMoveDestination(log.Properties[i].name.Span, ref writeHead);
                CopyToAndMoveDestination("\":\""u8, ref writeHead);
                CopyToAndMoveDestination(log.Properties[i].value.Span, ref writeHead);
            }

            CopyToAndMoveDestination("\"}"u8, ref writeHead);

            int usedBytes = buffer.Length - writeHead.Length;
            Debug.Assert(dataLen == usedBytes);
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

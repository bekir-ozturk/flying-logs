using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FlyingLogs.Shared
{
    public class LoggerThread
    {
        public readonly Task IngestionTask;

        public readonly SingleReaderWriterCircularBuffer Buffer = new SingleReaderWriterCircularBuffer(8 * 1024);

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

        public LoggerThread()
        {
            IngestionTask = Task.Run(Ingest);
        }

        public async Task Ingest()
        {
            byte[] tmpBuffer = new byte[2048];
            List<Memory<byte>> tmpChunks = new List<Memory<byte>>(32);

            while (true)
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    try
                    {
                        await tcpClient.ConnectAsync("localhost", 5341);
                    }
                    catch
                    {
                        // Connection failed. Avoid hot loops caused by exceptions
                        // TODO emit metric
                        await Task.Delay(5000);
                        continue;
                    }

                    NetworkStream networkStream = tcpClient.GetStream();

                    while (true)
                    {
                        tmpChunks.Clear();
                        int fetchedByteCount = Buffer.PeekReadUpToBytes(8*1024*1024, tmpChunks);

                        if (fetchedByteCount == 0)
                        {
                            // No new logs to ingest. Wait a while and retry
                            await Task.Delay(3000);
                            continue;
                        }

                        try
                        {
                            await networkStream.WriteAsync(_headerBytes);

                            // Write the chunk size
                            {
                                // Length of string + 2 bytes for the \r\n at the end.
                                int dataLen = fetchedByteCount + 2 * tmpChunks.Count;
                                // Reuse tmpBuffer to store the chunk size
                                tmpBuffer[0] = _hexToChar[(dataLen >> 24) & 0xFF];
                                tmpBuffer[1] = _hexToChar[(dataLen >> 16) & 0xFF];
                                tmpBuffer[2] = _hexToChar[(dataLen >> 8) & 0xFF];
                                tmpBuffer[3] = _hexToChar[dataLen & 0xFF];
                                tmpBuffer[4] = (byte)'\r';
                                tmpBuffer[5] = (byte)'\n';
                                await networkStream.WriteAsync(tmpBuffer.AsMemory().Slice(0, 6));
                            }

                            for(int i=0; i<tmpChunks.Count; i++)
                            {
                                await networkStream.WriteAsync(tmpChunks[i]);
                                await networkStream.WriteAsync(_crlfBytes);
                            }
                            await networkStream.WriteAsync(_chunkEndMarkerBytes);

                            bool ingestionVerified = false;
                            while (!ingestionVerified)
                            {
                                // Bytes from the beginning of the buffer that we no longer care about.
                                int processedByteCount = 0;
                                // Bytes from the beginning of the buffer that we searched for new line character
                                int scannedByteCount = 0;
                                // Total bytes in the buffer
                                int receivedByteCount = 0;

                                for (; scannedByteCount < receivedByteCount; scannedByteCount++)
                                {
                                    if (tmpBuffer[scannedByteCount] == '\n')
                                    {
                                        var line = tmpBuffer.AsMemory().Slice(processedByteCount, scannedByteCount - processedByteCount);
                                        // We no longer care about this part of the buffer
                                        processedByteCount = scannedByteCount + 1;

                                        if (tmpBuffer.AsMemory().Slice(_http11Bytes.Length).Equals(_http11Bytes))
                                        {
                                            // An http response was received. Parse status code.
                                            int statusCode = 0;
                                            for (int i=_http11Bytes.Length; i < line.Length; i++)
                                            {
                                                statusCode *= 10;
                                                statusCode += line.Span[i] - '0';
                                            }

                                            if (statusCode != 201)
                                                statusCode = statusCode;
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

                                await networkStream.ReadAsync(tmpBuffer);
                            }

                            if (ingestionVerified)
                            {
                                // Logs were successfully received. We can discard them.
                                Buffer.Pop(out _);
                            }
                        }
                        catch
                        {
                            // Socket must be dead. We need a new one.
                            break;
                        }
                    }
                }
            }
        }
    }
}

namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class Error
        {
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> LogThis_pieces = new System.ReadOnlyMemory<byte>[] {
                FlyingLogs.Constants._messsage__569401058
            };

            public static void LogThis(string template)
            {
                bool serialized = false;
                var log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var sinks = FlyingLogs.Configuration.ActiveSinks;
                var sinkCount = sinks.Length;

                for (int i=0; i < sinkCount; i++)
                {
                    if (sinks[i].IsLogLevelActive(FlyingLogs.Shared.LogLevel.Error) == false)
                        continue;

                    if (serialized == false)
                    {
                        log.Clear(4);
                
                        var b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                        int offset = 0;
                        var failed = false;

                        log.MessagePieces = LogThis_pieces;
                        log.PositionalPropertiesStartIndex = 4;
                        log.AdditionalPropertiesStartIndex = 4 + 0;

                        {
                
                            failed |= !DateTime.UtcNow.TryFormat(b.Span.Slice(offset), out int bytesWritten, "s", null);
                            if (offset + bytesWritten < b.Length)
                            {
                                offset += bytesWritten + 1;
                                b.Span[offset - 1] = (byte)'Z';
                                log.Properties[(int)FlyingLogs.Core.LogProperty.Timestamp] = (
                                    FlyingLogs.Constants.__t__1722084442,
                                    b.Slice(offset - bytesWritten - 1, bytesWritten + 1)
                                );
                            }
                            else
                                failed = true;
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Level] = (
                                FlyingLogs.Constants.__l__2078314802,
                                FlyingLogs.Constants._Error_22442200
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Template] = (
                                FlyingLogs.Constants.__mt__517915353,
                                FlyingLogs.Constants._messsage__569401058
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.EventId] = (
                                FlyingLogs.Constants.__i_334115681,
                                FlyingLogs.Constants.__666996142_867751520
                            );
                        }


                        // TODO serialization logic

                        if (failed)
                        {
                            // TODO emit serialization failure metric
                            return;
                        }
                        serialized = true;
                    }

                    sinks[i].Ingest(log);
                }
            }
        }
    }
}
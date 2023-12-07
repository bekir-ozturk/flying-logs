namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class Error
        {
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> LogThis_pieces = new System.ReadOnlyMemory<byte>[] {
                FlyingLogs.Constants.__396171664
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
                
                            failed |= !DateTime.UtcNow.TryFormat(b.Span.Slice(offset), out int bytesWritten, "o", null);
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Timestamp] = (
                                FlyingLogs.Constants.__t__1147000718,
                                b.Slice(offset, bytesWritten)
                            );
                            offset += bytesWritten;
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Level] = (
                                FlyingLogs.Constants.__l__1939665970,
                                FlyingLogs.Constants._Error_1551208805
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Template] = (
                                FlyingLogs.Constants.__mt__1938607663,
                                FlyingLogs.Constants.__396171664
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.EventId] = (
                                FlyingLogs.Constants.__i__1177218994,
                                FlyingLogs.Constants.__273139360__1571630843
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
namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class Information
        {
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> L1_pieces = new System.ReadOnlyMemory<byte>[] {
                FlyingLogs.Constants._whatever__1311278276, FlyingLogs.Constants.___372029310, FlyingLogs.Constants.__and_some__1460280919, FlyingLogs.Constants.__371857150
            };

            public static void L1(string template, float position, System.Numerics.Vector2 speed, float duration)
            {
                bool serialized = false;
                var log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var sinks = FlyingLogs.Configuration.ActiveSinks;
                var sinkCount = sinks.Length;

                for (int i=0; i < sinkCount; i++)
                {
                    if (sinks[i].IsLogLevelActive(FlyingLogs.Shared.LogLevel.Information) == false)
                        continue;

                    if (serialized == false)
                    {
                        log.Clear(4);
                
                        var b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                        int offset = 0;
                        var failed = false;

                        log.MessagePieces = L1_pieces;
                        log.PositionalPropertiesStartIndex = 4;
                        log.AdditionalPropertiesStartIndex = 4 + 3;

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
                                FlyingLogs.Constants._Information__46052920
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Template] = (
                                FlyingLogs.Constants.__mt__517915353,
                                FlyingLogs.Constants._whatever_position___speed__and_some__duration___1808598391
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.EventId] = (
                                FlyingLogs.Constants.__i_334115681,
                                FlyingLogs.Constants._44220389_2096637432
                            );
                        }
                        {
                            failed |= !position.TryFormat(b.Span.Slice(offset), out int bytesWritten, null, null);
                            log.Properties.Add((
                                FlyingLogs.Constants._position__240744113,
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
                        {
                            string ___value = speed.ToString(null);
                            failed |= !System.Text.Encoding.UTF8.TryGetBytes(___value, b.Span.Slice(offset), out int bytesWritten);
                            log.Properties.Add((
                                FlyingLogs.Constants._speed_338261383,
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
                        {
                            failed |= !duration.TryFormat(b.Span.Slice(offset), out int bytesWritten, null, null);
                            log.Properties.Add((
                                FlyingLogs.Constants._duration_1232249462,
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
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
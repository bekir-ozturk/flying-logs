namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class Information
        {
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> L2_pieces = new System.ReadOnlyMemory<byte>[] {
                FlyingLogs.Constants.__396171664, FlyingLogs.Constants.___1233957804, FlyingLogs.Constants.__and_some__302989204, FlyingLogs.Constants._____874867152
            };

            public static void L2(string template, float position, System.Numerics.Vector2 speed, float duration)
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

                        log.MessagePieces = L2_pieces;
                        log.PositionalPropertiesStartIndex = 4;
                        log.AdditionalPropertiesStartIndex = 4 + 3;

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
                                FlyingLogs.Constants._Information__975059597
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Template] = (
                                FlyingLogs.Constants.__mt__1938607663,
                                FlyingLogs.Constants.__position___speed__and_some__duration_F4____280765605
                            );
                        }
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.EventId] = (
                                FlyingLogs.Constants.__i__1177218994,
                                FlyingLogs.Constants._924280797__683073677
                            );
                        }
                        {
                            failed |= !position.TryFormat(b.Span.Slice(offset), out int bytesWritten, null, null);
                            log.Properties.Add((
                                FlyingLogs.Constants._position_1377324145,
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
                        {
                            string ___value = speed.ToString(null);
                            failed |= !System.Text.Encoding.UTF8.TryGetBytes(___value, b.Span.Slice(offset), out int bytesWritten);
                            log.Properties.Add((
                                FlyingLogs.Constants._speed__2018898324,
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
                        {
                            failed |= !duration.TryFormat(b.Span.Slice(offset), out int bytesWritten, "F4", null);
                            log.Properties.Add((
                                FlyingLogs.Constants._duration__1275604785,
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
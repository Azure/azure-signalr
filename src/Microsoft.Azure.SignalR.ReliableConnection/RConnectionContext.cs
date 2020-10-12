// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR;
using Newtonsoft.Json;
using static Microsoft.AspNetCore.SignalR.Protocol.ReliableProtocol;

namespace Microsoft.AspNetCore.SignalR
{
    public class RConnectionContext : ConnectionContext
    {
        internal class BufferConnection
        {
            internal ConnectionContext conn; // Inner wrap connectionContext
            internal bool receivedBarrier;  // Is this connection has already been active or not.
            internal int finish; // A flag represents finish state, 2 means both two sides have received barrier msg
            internal readonly Channel<ReadOnlyMemory<byte>> chan; // Used to store message received for this BufferConnection.
            internal CancellationTokenSource cts = new CancellationTokenSource(); // Use to cancel precessIncoming loop for this BufferConnection

            internal BufferConnection(ConnectionContext _conn, bool act = false)
            {
                conn = _conn;
                receivedBarrier = act;
                finish = 0;
                chan = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
            }
        }

        public override string ConnectionId { get => inner.conn.ConnectionId; set => inner.conn.ConnectionId = value; }

        public override IFeatureCollection Features => inner.conn.Features;

        public override IDictionary<object, object> Items { get => inner.conn.Items; set => inner.conn.Items = value; }
        public override IDuplexPipe Transport { get; set; }

        private BufferConnection inner { get; set; } // Current Connetion
        private Queue<BufferConnection> conns; // Backup connection list
        private RHttpConnectionFactory _connectionFactory; // Create a new one and replace its HttpConnectionOptions,  to provide our own accessToken.
        private readonly Pipe _transport;            //  Inner.Transport.Input(after process)    -> transport.Writer  
        private readonly Pipe _application;          //  application.Reader(after process) -> Inner.TransPort.Output
        private ReadOnlyMemory<byte> HandshakeBytes;  // Stored Handshake Bytes for virtual handshake for reloading.


        public RConnectionContext(ConnectionContext Inner, RHttpConnectionFactory connectionFactory)
        {
            inner = new BufferConnection(Inner, true);
            _connectionFactory = connectionFactory;
            conns = new Queue<BufferConnection>();
            _transport = new Pipe();
            _application = new Pipe();
            Transport = new DuplexPipe(_transport.Reader, _application.Writer);

            Task.Run(() => { _ = readFromChannel(inner); });

            Task.Run(() => { _ = ProcessIncoming(inner); });

            Task.Run(() => { _ = ProcessOutgoing(); });
        }

        private async Task readFromChannel(BufferConnection bc)
        {
            var chan = bc.chan;
            var token = bc.cts.Token;
            try
            {
                // print received messages if any
                while (await chan.Reader.WaitToReadAsync(token))
                {
                    while (!token.IsCancellationRequested && chan.Reader.TryRead(out ReadOnlyMemory<byte> item))
                    {
                        try
                        {
                            // There might be more than one segment.
                            _ = await _transport.Writer.WriteAsync(item);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
            finally
            {
                Console.WriteLine("[Transport Layer]\tReading from oldConnection("  + bc.conn.ConnectionId + ") Ended!");
            }

        }


        private async Task ProcessIncoming(BufferConnection bc)
        {
            Console.WriteLine("[Transport Layer]\t" + bc.conn.ConnectionId + " connected");
            // Process reciving message from this connection and send it to the channel.
            PipeReader input = bc.conn.Transport.Input;
            try
            {
                while (true)
                {
                    // Exceptions are handled above where the send and receive tasks are being run.
                    var result = await input.ReadAsync();
                    var buffer = result.Buffer;

                    var end = buffer.Start;
                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            if (TryParseMessage(ref buffer, out var rm))
                            {
                                end = buffer.Start;
                                switch (rm.MessageType)
                                {
                                    
                                    case RMType.Data:
                                        {
                                            // Received normal message, decode with base64 and deliver to application layer.
                                            var output = Convert.FromBase64String(rm.Payload);
                                            if (bc.receivedBarrier)
                                            {
                                                Console.WriteLine("[Transport Layer]\tConnection " + bc.conn.ConnectionId + " Received Data Message.");
                                                await bc.chan.Writer.WriteAsync(output);
                                            }
                                            break;
                                        }
                                    case RMType.Reload:
                                        {
                                            // Received Reload Message
                                            ReloadMessage rdm = JsonConvert.DeserializeObject<ReloadMessage>(rm.Payload);
                                            // Open a new connection.
                                            var backup = new BufferConnection(await _connectionFactory.ConnectAsync(new UriEndPoint(new Uri(rdm.url)), rdm.token));
                                            // Send HandShakeBytes to execute virtual handshake, application layer donesn't know.
                                            await backup.conn.Transport.Output.WriteAsync(HandshakeBytes);
                                            conns.Enqueue(backup);
                                            _ = ProcessIncoming(backup);


                                            // Send Barrier Message for both old and new connections.
                                            BarrierMessage bm = new BarrierMessage();
                                            bm.from = bc.conn.ConnectionId;
                                            bm.to = backup.conn.ConnectionId;
                                            RMessage nrm = new RMessage
                                            {
                                                MessageType = RMType.Barrier,
                                                Payload = JsonConvert.SerializeObject(bm)
                                            };
                                            var bmsg = EncodeMessage(nrm);

                                            await bc.conn.Transport.Output.WriteAsync(bmsg);
                                            await backup.conn.Transport.Output.WriteAsync(bmsg);
                                            break;
                                        }
                                    case RMType.Barrier:
                                        {
                                            Console.WriteLine("[Transport Layer]\tConnection " + bc.conn.ConnectionId + " Received Barrier Message.");
                                            BarrierMessage bm = JsonConvert.DeserializeObject<BarrierMessage>(rm.Payload);
                                            if (bm.from == bc.conn.ConnectionId)
                                            {
                                                // Receoved Barrier, finish one process.
                                                bc.receivedBarrier = false;
                                                bc.finish++;
                                            }
                                            else if (bm.to == bc.conn.ConnectionId)
                                            {
                                                bc.receivedBarrier = true;
                                            }
                                            break;
                                        }
                                    case RMType.ACK:
                                        {  
                                            // Receoved ACK, finish one process.
                                            ReloadAckMessage ram = JsonConvert.DeserializeObject<ReloadAckMessage>(rm.Payload);
                                            if (ram.oldConnID == bc.conn.ConnectionId)
                                            {
                                                Console.WriteLine("[Transport Layer]\tConnection " + bc.conn.ConnectionId + " Received ReloadACK Message.");
                                                bc.finish++;
                                            }
                                            break;
                                        }
                                    case RMType.Dummy:
                                        break;
                                }
                            }
                           
                        }
                        else if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        input.AdvanceTo(end);
                    }

                    // Finish from both sending and receiving.
                    if (bc.finish == 2)
                    {
                        if (conns.Count == 0) return;
                        inner.cts.Cancel();
                        // Switch to new connection.
                        inner = conns.Dequeue();
                        _ = readFromChannel(inner);
                        // close current connection.
                        bc.conn.Abort();
                        Console.WriteLine("[Transport Layer]\t" + bc.conn.ConnectionId + " disconnected");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                input.Complete(ex);
            }
            finally
            {
                input.Complete();
            }
        }

        private async Task ProcessOutgoing()
        {
            bool handshakeProcessed = false;
            try
            {
                while (true)
                {
                    // Exceptions are handled above where the send and receive tasks are being run.
                    var result = await _application.Reader.ReadAsync();
                    var buffer = result.Buffer;

                    var end = buffer.End;
                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }
                        else if (!buffer.IsEmpty)
                        {
                            if (!handshakeProcessed)
                            {
                                if (HandshakeProtocol.TryParseRequestMessage(ref buffer, out var handshakeRequest))
                                {
                                    // For the first handshake bytes, we need to send without any changes, because runtime couldn't recognize our wrapper.
                                    end = buffer.Start;
                                    handshakeProcessed = true;
                                    var memoryBufferWriter = MemoryBufferWriter.Get();
                                    try
                                    {
                                        HandshakeProtocol.WriteRequestMessage(handshakeRequest, memoryBufferWriter);
                                        await inner.conn.Transport.Output.WriteAsync(memoryBufferWriter.ToArray());
                                        HandshakeBytes = memoryBufferWriter.ToArray();
                                    }
                                    finally
                                    {
                                        MemoryBufferWriter.Return(memoryBufferWriter);
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                // Wrap with our RMessage.
                                var rm = new RMessage();
                                rm.MessageType = RMType.Data;
                                var array = buffer.ToArray();
                                rm.Payload = Convert.ToBase64String(array);
                                var appMessage = EncodeMessage(rm);
                                
                                // Send messages for both old and new connections and let receiver to handle receiving logic.
                                await inner.conn.Transport.Output.WriteAsync(appMessage);
                                foreach (var back_bc in conns) await back_bc.conn.Transport.Output.WriteAsync(appMessage);
                            }
                        }
                        else if (result.IsCompleted) 
                        {
                            if (!buffer.IsEmpty)
                            {
                                throw new InvalidDataException("Connection terminated while reading a message.");
                            }
                            break;
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex);
                        break;
                    }
                    finally
                    {
                        _application.Reader.AdvanceTo(end);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _application.Reader.Complete(ex);
            }
            finally
            {
                _application.Reader.Complete();
            }
        }
    }
}

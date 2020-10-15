// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class TCPSignaler : Signaler
{
    /// <summary>
    /// Client pipe for sending data. This is connected to the remote signaler's server pipe.
    /// </summary>
    private TcpClient _client = null;

    /// <summary>
    /// Server pipe for receiving data. This is connected to the remote signaler's client pipe.
    /// </summary>
    private TcpListener _server = null;

    private NetworkStream _stream = null;
    private StreamReader _streamReader = null;
    private StreamWriter _streamWriter = null;
    private int _serverPort;

    private readonly BlockingCollection<JObject> _outgoingMessages = new BlockingCollection<JObject>(new ConcurrentQueue<JObject>());

    public TCPSignaler(int port)
    {
        _serverPort = port;
        ClientDisconnected += OnConnectionClosed;
    }

    public void WaitForTCPConnection()
    {
        Debug.Log("Waiting for the remote peer to connect...");
        _client = _server.AcceptTcpClient();
        _stream = _client.GetStream();
        _streamReader = new StreamReader(_stream);
        _streamWriter = new StreamWriter(_stream);
        _streamWriter.AutoFlush = true;
        Debug.Log("Remote peer connected.");
        Console.WriteLine("Signaler connection established.");
        ClientConnected?.Invoke();
        _ = Task.Factory.StartNew(ProcessIncomingMessagesQueue, TaskCreationOptions.LongRunning);
        _ = Task.Factory.StartNew(WriteOutgoingMessages, TaskCreationOptions.LongRunning);
    }

    public void OnConnectionClosed()
    {
        _streamReader?.Dispose();
        _streamWriter?.Dispose();
        _stream?.Dispose();
        _client?.Close();
        _ = Task.Factory.StartNew(WaitForTCPConnection, TaskCreationOptions.LongRunning);
    }

    public override void Start()
    {
        _server = new TcpListener(IPAddress.Any, _serverPort);
        _server.Start();
        Debug.Log("Created tcp server;");
        _ = Task.Factory.StartNew(WaitForTCPConnection, TaskCreationOptions.LongRunning);
    }

    public override void Stop()
    {
        _outgoingMessages.CompleteAdding();
        _outgoingMessages.Dispose();
        _server.Stop();
    }

    private void ProcessIncomingMessagesQueue()
    {
        // ReadLine() will block while waiting for a new line
        JsonSerializer serializer = new JsonSerializer();
        using (StreamReader sr = new StreamReader(_stream))
        using (JsonReader reader = new JsonTextReader(sr))
        {
            reader.SupportMultipleContent = true;
            while (reader.Read())
            {
                var json = serializer.Deserialize<JObject>(reader);
                //Console.WriteLine($"[<-] {json}");
                ProcessIncomingMessage(json);
            }
        }
        Console.WriteLine("Finished processing messages");
    }

    private void WriteOutgoingMessages()
    {
        // GetConsumingEnumerable() will block when no message is available,
        // until CompleteAdding() is called from Stop().
        foreach (var msg in _outgoingMessages.GetConsumingEnumerable())
        {
            _streamWriter.WriteLine(JsonConvert.SerializeObject(msg, Formatting.None));
        }
    }

    protected override void SendMessage(JObject json)
    {
        try
        {
            // Enqueue the message and immediately return, to avoid blocking the
            // WebRTC signaler thread which is typically invoking this method through
            // the PeerConnection signaling callbacks.
            Console.WriteLine($"[->] {json}");
            _outgoingMessages.Add(json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: {e.Message}");
            Environment.Exit(-1);
        }
    }
}

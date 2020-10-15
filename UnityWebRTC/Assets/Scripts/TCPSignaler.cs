// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class TCPSignaler
{
    public PeerConnection PeerConnection { get; }
    /// <summary>
    /// Event invoked when an ICE candidate message has been received from the remote peer's signaler.
    /// </summary>
    public PeerConnection.IceCandidateReadytoSendDelegate IceCandidateReceived;

    /// <summary>
    /// Event invoked when an SDP offer or answer message has been received from the remote peer's signaler.
    /// </summary>
    public PeerConnection.LocalSdpReadyToSendDelegate SdpMessageReceived;

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

    private Action _callback = null;

    private string _serverName;
    private int _serverPort;

    private readonly BlockingCollection<JObject> _outgoingMessages = new BlockingCollection<JObject>(new ConcurrentQueue<JObject>());

    public TCPSignaler(PeerConnection peerConnection, string host, int port)
    {
        PeerConnection = peerConnection;
        _serverName = host;
        _serverPort = port;
        _server = new TcpListener(IPAddress.Parse(_serverName), _serverPort);
        _server.Start();
        Debug.Log("Created tcp server;");
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
        PeerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
        PeerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;
        _ = Task.Factory.StartNew(ProcessIncomingMessages, TaskCreationOptions.LongRunning);
        _ = Task.Factory.StartNew(WriteOutgoingMessages, TaskCreationOptions.LongRunning);
        _callback.Invoke();
    }

    public void Start(Action callback)
    {
        _callback = callback;
        _ = Task.Factory.StartNew(WaitForTCPConnection, TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        _outgoingMessages.CompleteAdding();
        _outgoingMessages.Dispose();
        PeerConnection.LocalSdpReadytoSend -= PeerConnection_LocalSdpReadytoSend;
        PeerConnection.IceCandidateReadytoSend -= PeerConnection_IceCandidateReadytoSend;
        _streamReader.Dispose();
        _streamWriter.Dispose();
        _stream.Dispose();
        _server.Stop();
        _client.Close();
    }

    private void ProcessIncomingMessages()
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


                if ((string)json["type"] == "ice")
                {
                    string sdpMid = json["sdpMid"].Value<string>();
                    int sdpMlineindex = json["sdpMLineindex"].Value<int>();
                    string candidate = json["candidate"].Value<string>();
                    Console.WriteLine($"[<-] ICE candidate: {sdpMid} {sdpMlineindex} {candidate}");
                    var iceCandidate = new IceCandidate
                    {
                        SdpMid = sdpMid,
                        SdpMlineIndex = sdpMlineindex,
                        Content = candidate
                    };
                    IceCandidateReceived?.Invoke(iceCandidate);
                }
                if ((string)json["type"] == "sdp")
                {
                    string sdp;
                    string type;
                    if (json.ContainsKey("offer"))
                    {
                        sdp = json["offer"].Value<string>();
                        type = "offer";
                    }
                    else
                    {
                        type = "answer";
                        sdp = (string)json["answer"];
                    }
                    Console.WriteLine($"[<-] SDP message: {type} {sdp}");
                    var message = new SdpMessage { Type = SdpMessage.StringToType(type), Content = sdp };
                    SdpMessageReceived?.Invoke(message);
                }
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

    private void SendMessage(JObject json)
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

    private void PeerConnection_IceCandidateReadytoSend(IceCandidate candidate)
    {
        // See ProcessIncomingMessages() for the message format

        JObject iceCandidate = new JObject {
                        { "type", "ice" },
                        { "candidate", candidate.Content },
                        { "sdpMLineindex", candidate.SdpMlineIndex },
                        { "sdpMid", candidate.SdpMid }
            };

        SendMessage(iceCandidate);
    }

    private void PeerConnection_LocalSdpReadytoSend(SdpMessage message)
    {
        // See ProcessIncomingMessages() for the message format
        string typeStr = SdpMessage.TypeToString(message.Type);

        JObject sdpMessage = new JObject {
                { "type", "sdp"},
                {  typeStr, message.Content }
            };
        SendMessage(sdpMessage);
    }
}

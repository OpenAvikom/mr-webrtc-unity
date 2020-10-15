
using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

using UnityEngine;
public abstract class Signaler
{
    private PeerConnection _peerConnection;
    public PeerConnection PeerConnection { get => _peerConnection; }
    /// <summary>
    /// Event invoked when an ICE candidate message has been received from the remote peer's signaler.
    /// </summary>
    public PeerConnection.IceCandidateReadytoSendDelegate IceCandidateReceived;

    /// <summary>
    /// Event invoked when an SDP offer or answer message has been received from the remote peer's signaler.
    /// </summary>
    public PeerConnection.LocalSdpReadyToSendDelegate SdpMessageReceived;

    public System.Action ClientConnected;

    protected abstract void SendMessage(JObject json);

    public Signaler()
    {
        SdpMessageReceived += ProcessSdpMessage;
    }

    public async virtual Task Start()
    {
        _peerConnection = new PeerConnection();
        // Initialize the connection with a STUN server to allow remote access
        var config = new PeerConnectionConfiguration
        {
            IceServers = new List<IceServer> {
                            new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
                        }
        };
        await _peerConnection.InitializeAsync(config);
        PeerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
        PeerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;
    }

    public virtual void Stop()
    {
        PeerConnection.LocalSdpReadytoSend -= PeerConnection_LocalSdpReadytoSend;
        PeerConnection.IceCandidateReadytoSend -= PeerConnection_IceCandidateReadytoSend;
        _peerConnection?.Dispose();
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

        // Debug.Log($"Ice Candidate {iceCandidate}");
        SendMessage(iceCandidate);
    }

    private void PeerConnection_LocalSdpReadytoSend(SdpMessage message)
    {
        // See ProcessIncomingMessages() for the message format
        string typeStr = SdpMessage.TypeToString(message.Type);

        // https://github.com/microsoft/MixedReality-WebRTC/issues/501#issuecomment-674469381
        // sdp message headeer is deprecated and needs fixing
        JObject sdpMessage = new JObject {
                { "type", "sdp"},
                {  typeStr, message.Content.Replace("msid-semantic: WMS\r", "msid-semantic: WMS local_av_stream\r").Replace("msid:-", "msid:-local_av_stream") }
        };
        SendMessage(sdpMessage);
    }


    protected void ProcessIncomingMessage(JObject json)
    {
        if ((string)json["type"] == "ice")
        {
            string sdpMid = json["sdpMid"].Value<string>();
            int sdpMlineindex = json["sdpMLineindex"].Value<int>();
            string candidate = json["candidate"].Value<string>();
            // Debug.Log($"[<-] ICE candidate: {sdpMid} {sdpMlineindex} {candidate}");
            var iceCandidate = new IceCandidate
            {
                SdpMid = sdpMid,
                SdpMlineIndex = sdpMlineindex,
                Content = candidate
            };
            PeerConnection.AddIceCandidate(iceCandidate);
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
            // Debug.Log($"[<-] SDP message: {type} {sdp}");
            var message = new SdpMessage { Type = SdpMessage.StringToType(type), Content = sdp };
            SdpMessageReceived?.Invoke(message);
        }
    }

    private async void ProcessSdpMessage(SdpMessage message)
    {
        await PeerConnection.SetRemoteDescriptionAsync(message);
        if (message.Type == SdpMessageType.Offer)
        {
            PeerConnection.CreateAnswer();
        }
    }
}
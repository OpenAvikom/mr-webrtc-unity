using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.WebRTC;

public class RTCServer : MonoBehaviour
{
    TCPSignaler signaler;
    Transceiver audioTransceiver = null;
    Transceiver videoTransceiver = null;
    AudioTrackSource audioTrackSource = null;
    VideoTrackSource videoTrackSource = null;
    LocalAudioTrack localAudioTrack = null;
    LocalVideoTrack localVideoTrack = null;

    PeerConnection pc = null;
    // Start is called before the first frame update

    public bool NeedVideo = true;
    public bool NeedAudio = false;

    async void Start()
    {
        var deviceList = await DeviceVideoTrackSource.GetCaptureDevicesAsync();

        // For example, print them to the standard output
        foreach (var device in deviceList)
        {
            Debug.Log($"Found webcam {device.name} (id: {device.id})");
        }

        pc = new PeerConnection();

        // Initialize the connection with a STUN server to allow remote access
        var config = new PeerConnectionConfiguration
        {
            IceServers = new List<IceServer> {
                            new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
                        }
        };
        await pc.InitializeAsync(config);
        Debug.Log("Peer connection initialized.");

        // Setup signaling
        Debug.Log("Starting signaling...");
        signaler = new TCPSignaler(pc, "0.0.0.0", 9999);
        signaler.SdpMessageReceived += async (SdpMessage message) =>
        {
            await pc.SetRemoteDescriptionAsync(message);
            if (message.Type == SdpMessageType.Offer)
            {
                pc.CreateAnswer();
            }
        };
        signaler.IceCandidateReceived += (IceCandidate candidate) =>
        {
            pc.AddIceCandidate(candidate);
        };
        signaler.Start(OnClientConnected);
    }

    async void OnClientConnected()
    {
        // Record video from local webcam, and send to remote peer
        if (NeedVideo)
        {
            Debug.Log("Opening local webcam...");
            var deviceSettings = new LocalVideoDeviceInitConfig
            {
                width = 1280,
                height = 720
            };
            videoTrackSource = await DeviceVideoTrackSource.CreateAsync(deviceSettings);

            Debug.Log("Create local video track...");
            var trackSettings = new LocalVideoTrackInitConfig
            {
                trackName = "webcam_track"
            };
            localVideoTrack = LocalVideoTrack.CreateFromSource(videoTrackSource, trackSettings);

            Debug.Log("Create video transceiver and add webcam track...");
            videoTransceiver = pc.AddTransceiver(MediaKind.Video);
            videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
            videoTransceiver.LocalVideoTrack = localVideoTrack;
        }

        // Record audio from local microphone, and send to remote peer
        if (NeedAudio)
        {
            Debug.Log("Opening local microphone...");
            audioTrackSource = await DeviceAudioTrackSource.CreateAsync();

            Debug.Log("Create local audio track...");
            var trackSettings = new LocalAudioTrackInitConfig { trackName = "mic_track" };
            localAudioTrack = LocalAudioTrack.CreateFromSource(audioTrackSource, trackSettings);

            Debug.Log("Create audio transceiver and add mic track...");
            audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
            audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
            audioTransceiver.LocalAudioTrack = localAudioTrack;
        }

        // Start peer connection
        pc.Connected += () => { Debug.Log("PeerConnection: connected."); };
        pc.IceStateChanged += (IceConnectionState newState) => { Debug.Log($"ICE state: {newState}"); };
        int numFrames = 0;
        pc.VideoTrackAdded += (RemoteVideoTrack track) =>
        {
            Debug.Log($"Attach Frame Listener...");
            track.I420AVideoFrameReady += (I420AVideoFrame frame) =>
            {
                ++numFrames;
                if (numFrames % 60 == 0)
                {
                    Debug.Log($"Received video frames: {numFrames}");
                }
            };
        };
        pc.CreateOffer();
        Debug.Log("Waiting for offer from remote peer...");
    }

    // Update is called once per frame

    void OnDisable()
    {
        localAudioTrack?.Dispose();
        localVideoTrack?.Dispose();
        audioTrackSource?.Dispose();
        videoTrackSource?.Dispose();
        signaler?.Stop();
        pc?.Dispose();
        Debug.Log("Program terminated.");
    }
}

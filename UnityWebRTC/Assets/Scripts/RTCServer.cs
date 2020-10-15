using UnityEngine;
using Microsoft.MixedReality.WebRTC;
using System.Threading.Tasks;

public class RTCServer : MonoBehaviour
{
    Signaler signaler;
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

    public uint VideoWidth = 640;
    public uint VideoHeight = 400;
    public uint VideoFps = 30;

    public bool UseRemoteStun = false;

    async void Start()
    {
        var deviceList = await DeviceVideoTrackSource.GetCaptureDevicesAsync();

        // For example, print them to the standard output
        foreach (var device in deviceList)
        {
            Debug.Log($"Found webcam {device.name} (id: {device.id})");
        }

        // Setup signaling
        Debug.Log("Starting signaling...");
        signaler = new TCPSignaler(9998);
        signaler.ClientConnected += OnClientConnected;
        signaler.ClientDisconnected += OnClientDisconnected;
        if (UseRemoteStun)
        {
            signaler.IceServers.Add(new IceServer { Urls = { "stun:stun.l.google.com:19302" } });
        }

        signaler.Start();
    }

    async void OnClientConnected()
    {
        var pc = signaler.PeerConnection;
        // Record video from local webcam, and send to remote peer
        if (NeedVideo)
        {
            Debug.Log("Opening local webcam...");
            var deviceSettings = new LocalVideoDeviceInitConfig
            {
                width = VideoWidth,
                height = VideoHeight,
                framerate = VideoFps
            };

            videoTrackSource = await DeviceVideoTrackSource.CreateAsync(deviceSettings);

            Debug.Log($"Create local video track... {videoTrackSource}");
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
        // we need a short delay here for the video stream to settle...
        // I assume my Logitech webcam is sending some garbage frames in the beginning.
        await Task.Delay(200);
        pc.CreateOffer();
        Debug.Log("Send offer to remote peer");
    }

    public void OnClientDisconnected()
    {
        localAudioTrack?.Dispose();
        localVideoTrack?.Dispose();
        audioTrackSource?.Dispose();
        videoTrackSource?.Dispose();
    }

    void OnDisable()
    {
        OnClientDisconnected();
        signaler?.Stop();
        Debug.Log("Program terminated.");
    }
}

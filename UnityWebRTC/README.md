# UnityWebRTC

This should not be confused with the official [WebRTC for Unity](https://github.com/Unity-Technologies/com.unity.webrtc). The project uses [Microsoft Mixed Reality Toolkit (MRTK)](https://github.com/microsoft/MixedRealityToolkit-Unity) and [Microsoft Mixed Reality WebRTC](https://github.com/microsoft/MixedReality-WebRTC) which are configured as UPM packages. Additionally, the project includes a `WebSocketSharp` DLL in the `Assets/Plugin` folder for the `WebSocketSignaler`

## Requirements for WebSocketSignaler

Before you run the Unity project, you need to create an SSL certificate. Modern browsers do not allow video and/or audio streaming over insecure connections. The whole process is described [here](https://github.com/microsoft/MixedReality-WebRTC/tree/master/examples/TestReceiveAV) but for this project can be summarized as:

* switch into the project folder `<git_repo>/UnityWebRTC/Assets/StreamingAssets/Certs`
* the file `req.conf` is used to configure the certificate. You need to adapt `CN` and `subjectAltName` if you want to access the stream from somewhere else than `localhost`.
* **in this folder** execute
  - `openssl req -config req.conf -x509 -newkey rsa:4096 -keyout localhost_key.pem -out localhost.pem -nodes -days 3650`
  - `openssl pkcs12 -export -in localhost.pem -inkey localhost_key.pem -out localhost.pfx -nodes`
* If you have chosen the default configuration, you can make Chrome/Chromium accept self-signed certificates for localhost by typing `chrome://flags/#allow-insecure-localhost` into the address bar and enabling that option.
* Alternatively, you can access the `WebSocketSignaler` via `https` instead of `wss` (eg. type `https:\\localhost:9999` into your browser while the project is running) and manually accept the certificate.

## Limitations HoloLens 2 

As of 2020-10-16, `Mixed Reality WebRTC` does not support HoloLens 2 builds targeting `ARM64` ([ref](https://github.com/microsoft/MixedReality-WebRTC/issues/414)). `ARM` (32-bit) seems to be [supported](https://github.com/microsoft/MixedReality-WebRTC/issues/235) though.

## Usage

Open `Assets/Scenes/SignalerExample` in Unity and check the `Launcher` GameObject. The `RTCServer` features some settings which can be adjusted:

* `NeedVideo` -- whether the video stream should be sent
* `NeedAudio` -- whether an audio track should be send
* `VideoWidth`, `VideoHeight` and `VideoFps` should be set to values your WebCam/HoloLens camera is actually supporting. Otherwise the stream might not be initialized successfully
* `ConnectionType` -- Should be set to `TCP` or `WebSocket`, depending on the client (TCP: Python, WebSocket: browser) you plan to use
* `UseRemoteStun` -- when enabled, the server `stun.l.google.com:19302` will be promoted to the `PeerConnection`. This might help in cases where server and client cannot reach each other directly.


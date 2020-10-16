# Mixed Reality video streaming via WebRTC

This project uses the [Microsoft Mixed Reality Toolkit (MRTK)](https://github.com/microsoft/MixedRealityToolkit-Unity) and [Microsoft Mixed Reality WebRTC](https://github.com/microsoft/MixedReality-WebRTC) to stream video captures from the HoloLens (1/2) to a [web browser](./web) or [Python client](./python). The Unity project [UnityWebRTC](./UnityWebRTC) implements a `TCPSignaler` and a `WebSocketSignaler`. Thus, no additional component/server is needed for streaming. Stun/turn servers can be optionally activated.

## Important remark

This is an example and test project to illustrate how WebRTC can be used to stream video captures from a webcam/HoloLens to a browser or Python client. Its sole purpose is illustration and by no means claim functional completeness. Future updates in MR-WebRTC or MRTK might break it! **Constant maintenance is not provided!** Feel free to open Pull Requests to improve robustness, comprehensibility, versatility and flexibility of the shown code samples and documentation. 

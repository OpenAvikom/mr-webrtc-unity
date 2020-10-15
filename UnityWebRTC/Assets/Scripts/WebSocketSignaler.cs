using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
public class WebRtcSession : WebSocketBehavior
{

    public event System.Action<string> MessageReceived;
    public event System.Action SocketOpen;

    protected override void OnMessage(MessageEventArgs e)
    {
        MessageReceived?.Invoke(e.Data);
    }

    protected override void OnOpen()
    {
        SocketOpen?.Invoke();
    }
}

public class WebSocketSignaler : Signaler
{

    // pfx file
    private int _serverPort;
    private WebSocketServer _server;
    private WebRtcSession _session;

    public WebSocketSignaler(int port)
    {
        _serverPort = port;
    }

    public async override Task Start()
    {
        await base.Start();
        _server = new WebSocketServer(IPAddress.Any, _serverPort, true);
        var path = Application.streamingAssetsPath + "/Certs/localhost.pfx";
        _server.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(path);
        _server.SslConfiguration.CheckCertificateRevocation = false;
        _session = new WebRtcSession();
        _session.MessageReceived += MessageReceived;
        _session.SocketOpen += OnSocketOpen;
        _server.AddWebSocketService<WebRtcSession>("/", () => _session);
        _server.Start();
        Debug.Log($"Waiting for browser web socket connection to {_server.Address}:{_server.Port}...");
    }

    protected override void SendMessage(JObject json)
    {
        _session.Context.WebSocket.Send(JsonConvert.SerializeObject(json, Formatting.None));
    }

    private void OnSocketOpen()
    {
        Debug.Log("Socket open!");
        ClientConnected?.Invoke();
    }
    private void MessageReceived(string msg)
    {
        JObject json = JObject.Parse(msg);
        // Debug.Log($"Received: {json}");
        ProcessIncomingMessage(json);
    }

    public override void Stop()
    {
        _server.Stop();
        base.Stop();
    }
}

// if ((string)jsonMsg["type"] == "ice")
//         {
//             while (PeerConnection.Initialized)
//             {
//                 // This delay is needed due to an initialise bug in the Microsoft.MixedReality.WebRTC
//                 // nuget packages up to version 0.2.3. On master awaiting pc.InitializeAsync does end 
//                 // up with the pc object being ready.
//                 await Task.Delay(1000);
//             }

//             session.pc.AddIceCandidate((string)jsonMsg["sdpMLineindex"], (int)jsonMsg["sdpMid"], (string)jsonMsg["candidate"]);
//         }
//         else if ((string)jsonMsg["type"] == "sdp")
//         {
//             Console.WriteLine("Received remote peer SDP offer.");

//             var config = new PeerConnectionConfiguration();

//             session.pc.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid) =>
//             {
//                 Console.WriteLine($"Sending ice candidate: {candidate}");
//                 JObject iceCandidate = new JObject {
//                         { "type", "ice" },
//                         { "candidate", candidate },
//                         { "sdpMLineindex", sdpMlineindex },
//                         { "sdpMid", sdpMid}
//                 };
//                 session.Context.WebSocket.Send(iceCandidate.ToString());
//             };

//             session.pc.IceStateChanged += (newState) =>
//             {
//                 Console.WriteLine($"ice connection state changed to {newState}.");
//             };

//             session.pc.LocalSdpReadytoSend += (string type, string sdp) =>
//             {
//                 Console.WriteLine($"SDP answer ready, sending to remote peer.");

//                 // Send our SDP answer to the remote peer.
//                 JObject sdpAnswer = new JObject {
//                         { "type", "sdp" },
//                         { "answer", sdp }
//                 };
//                 session.Context.WebSocket.Send(sdpAnswer.ToString());
//             };

//             await session.pc.InitializeAsync(config).ContinueWith((t) =>
//             {
//                 session.pc.SetRemoteDescription("offer", (string)jsonMsg["offer"]);

//                 if (!session.pc.CreateAnswer())
//                 {
//                     Console.WriteLine("Failed to create peer connection answer, closing peer connection.");
//                     session.pc.Close();
//                     session.Context.WebSocket.Close();
//                 }
//             });

//             // Create a new form to display the video feed from the WebRTC peer.
//             var form = new Form();
//             form.AutoSize = true;
//             form.BackgroundImageLayout = ImageLayout.Center;
//             PictureBox picBox = null;

//             form.HandleDestroyed += (object sender, EventArgs e) =>
//             {
//                 Console.WriteLine("Form closed, closing peer connection.");
//                 session.pc.Close();
//                 session.Context.WebSocket.Close();
//             };

//             session.pc.ARGBRemoteVideoFrameReady += (frame) =>
//             {
//                 var width = frame.width;
//                 var height = frame.height;
//                 var stride = frame.stride;
//                 var data = frame.data;

//                 if (picBox == null)
//                 {
//                     picBox = new PictureBox
//                     {
//                         Size = new Size((int)width, (int)height),
//                         Location = new Point(0, 0),
//                         Visible = true
//                     };
//                     form.BeginInvoke(new Action(() => { form.Controls.Add(picBox); }));
//                 }

//                 form.BeginInvoke(new Action(() =>
//                 {
//                     System.Drawing.Bitmap bmpImage = new System.Drawing.Bitmap((int)width, (int)height, (int)stride, System.Drawing.Imaging.PixelFormat.Format32bppArgb, data);
//                     picBox.Image = bmpImage;
//                 }));
//             };

//             Application.EnableVisualStyles();
//             Application.Run(form);
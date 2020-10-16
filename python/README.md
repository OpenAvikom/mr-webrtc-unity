# Receiving MR WebRTC video stream via TCP.

This Python sample makes use of `aiortc` to send and receive WebRTC messaged and `OpenCV` to show an image. The message structure of `Mixed Reality WebRTC` varies a bit from the structure the default `TCPSignaler` expects. `UnityTcpSignaling` takes care of this. Note that this connection is not encrypted and should be used with caution.

Requirements can be install with `pip`: 

```
pip install -r requirements.txt
```

Note that depending on your platform, installing OpenCV might be more difficult. I would recommend using [conda](https://docs.conda.io/en/latest/miniconda.html) to setup and maintain python environments in this case. Installing OpenCV can be done with `conda install opencv` on most platforms.

The program can be launched from the command line: 

```python cli.py --host localhost --port 9095``` 

After a couple of seconds, an `OpenCV` window should pop up, showing the stream. The program can be closed with `Ctrl+C` in the command line.
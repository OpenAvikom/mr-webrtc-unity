from aiortc.contrib.signaling import TcpSocketSignaling, candidate_to_sdp, candidate_from_sdp
from aiortc import RTCIceCandidate, RTCSessionDescription
import json
import asyncio

BYE = object()


def unity_object_to_string(obj):
    if isinstance(obj, RTCSessionDescription):
        message = {"type": "sdp", obj.type: obj.sdp}
    elif isinstance(obj, RTCIceCandidate):
        message = {
            "candidate": "candidate:" + candidate_to_sdp(obj),
            "sdpMid": obj.sdpMid,
            "sdpMLineindex": obj.sdpMLineIndex,
            "type": "ice",
        }
    else:
        assert obj is BYE
        message = {"type": "bye"}
    return json.dumps(message, sort_keys=True)


def unity_object_from_string(message_str):
    message = json.loads(message_str)
    if message["type"] == "sdp":
        if "answer" in message:
            return RTCSessionDescription(type="answer", sdp=message["answer"])
        else:
            return RTCSessionDescription(type="offer", sdp=message["offer"])
    elif message["type"] == "ice" and message["candidate"]:
        candidate = candidate_from_sdp(message["candidate"].split(":", 1)[1])
        candidate.sdpMid = message["sdpMid"]
        candidate.sdpMLineIndex = message["sdpMLineindex"]
        return candidate
    elif message["type"] == "bye":
        return BYE


class UnityTcpSignaling(TcpSocketSignaling):
    async def receive(self):
        await self._connect(False)
        try:
            data = await self._reader.readuntil()
        except asyncio.IncompleteReadError:
            return
        return unity_object_from_string(data.decode("utf8"))

    async def send(self, descr):
        await self._connect(True)
        data = unity_object_to_string(descr).encode("utf8")
        self._writer.write(data + b"\n")

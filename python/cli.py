import argparse
import asyncio
import logging
import math
from collections import deque

import cv2
import numpy
import logging

from aiortc import (
    RTCIceCandidate,
    RTCPeerConnection,
    RTCSessionDescription,
    VideoStreamTrack,
)
from aiortc.contrib.signaling import BYE, add_signaling_arguments, create_signaling

from receiver import OpenCVReceiver
from signaler import UnityTcpSignaling

_LOGGER = logging.getLogger("mr.webrtc.python")
_LOGGER.addHandler(logging.NullHandler())


async def run(pc, player, receiver, signaling, role, queue):
    def add_tracks():
        if player and player.audio:
            pc.addTrack(player.audio)

        if player and player.video:
            pc.addTrack(player.video)
        else:
            pc.addTrack(FlagVideoStreamTrack())

    @pc.on("track")
    def on_track(track):
        _LOGGER.info("Receiving %s" % track.kind)
        receiver.addTrack(track)

    # connect signaling
    _LOGGER.info("Waiting for signaler connection ...")
    await signaling.connect()

    # consume signaling
    while True:
        obj = await signaling.receive()
        # if obj is not None:
        #     print(obj)

        if isinstance(obj, RTCSessionDescription):
            await pc.setRemoteDescription(obj)
            await receiver.start()

            async def check_queue():
                while True:
                    if len(queue):
                        img = queue.pop()
                        queue.clear()
                        try:
                            cv2.imshow("hello", img)
                            cv2.waitKey(1)
                        except Exception as e:
                            print(e)
                    await asyncio.sleep(0.05)

            asyncio.create_task(check_queue())

            if obj.type == "offer":
                # send answer
                # add_tracks()
                await pc.setLocalDescription(await pc.createAnswer())
                await signaling.send(pc.localDescription)
        elif isinstance(obj, RTCIceCandidate):
            await pc.addIceCandidate(obj)
        elif obj is BYE:
            print("Exiting")
            break


if __name__ == "__main__":
    import time

    parser = argparse.ArgumentParser(description="Video stream from the command line")
    parser.add_argument("--verbose", "-v", action="count")
    parser.add_argument("--host", "-ip", help="ip address of signaler/sender instance")
    parser.add_argument("--port", "-p", help="port of signaler/sender instance")
    add_signaling_arguments(parser)
    args = parser.parse_args()

    if args.verbose:
        logging.basicConfig(level=logging.DEBUG)
    else:
        logging.basicConfig(level=logging.WARN)
        _LOGGER.setLevel(level=logging.INFO)

    host = args.host or "localhost"
    port = args.port or 9095

    # create signaling and peer connection
    signaling = UnityTcpSignaling(host=host, port=port)
    pc = RTCPeerConnection()

    player = None
    frame_queue = deque()
    receiver = OpenCVReceiver(queue=frame_queue)
    # run event loop
    loop = asyncio.get_event_loop()
    try:
        loop.run_until_complete(
            run(
                pc=pc,
                player=player,
                receiver=receiver,
                signaling=signaling,
                role="answer",
                queue=frame_queue,
            )
        )
    except KeyboardInterrupt:
        pass
    finally:
        # cleanup
        _LOGGER.info("Shutting down receiver and peer connection.")
        loop.run_until_complete(receiver.stop())
        loop.run_until_complete(signaling.close())
        loop.run_until_complete(pc.close())

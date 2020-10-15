import argparse
import asyncio
import logging
import math
from collections import deque

import cv2
import numpy

from aiortc import (
    RTCIceCandidate,
    RTCPeerConnection,
    RTCSessionDescription,
    VideoStreamTrack,
)
from aiortc.contrib.signaling import BYE, add_signaling_arguments, create_signaling

from receiver import OpenCVReceiver
from signaler import UnityTcpSignaling


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
        print("Receiving %s" % track.kind)
        receiver.addTrack(track)

    # connect signaling
    await signaling.connect()

    # if role == "offer":
    #     # send offer
    #     add_tracks()
    #     await pc.setLocalDescription(await pc.createOffer())
    #     await signaling.send(pc.localDescription)

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
            pc.addIceCandidate(obj)
        elif obj is BYE:
            print("Exiting")
            break


if __name__ == "__main__":
    import time

    parser = argparse.ArgumentParser(description="Video stream from the command line")
    parser.add_argument("--verbose", "-v", action="count")
    add_signaling_arguments(parser)
    args = parser.parse_args()

    if args.verbose:
        logging.basicConfig(level=logging.DEBUG)

    # create signaling and peer connection
    signaling = UnityTcpSignaling(host="129.70.145.122", port=9999)
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
        loop.run_until_complete(receiver.stop())
        loop.run_until_complete(signaling.close())
        loop.run_until_complete(pc.close())

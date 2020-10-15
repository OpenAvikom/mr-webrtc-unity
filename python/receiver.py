import cv2
from aiortc.contrib.media import MediaRecorderContext
import asyncio


class OpenCVReceiver:
    def __init__(self, queue):
        self.__tracks = []
        self.__tasks = []
        self.queue = queue

    def addTrack(self, track):
        self.__tracks.append(track)

    async def start(self):
        for track in self.__tracks:
            self.__tasks.append(asyncio.ensure_future(self.__run_track(track)))

    async def stop(self):
        for task in self.__tasks:
            task.cancel()

    async def __run_track(self, track):
        while True:
            try:
                frame = await track.recv()
                self.queue.append(frame.to_ndarray(format="bgr24"))
            except MediaStreamError:
                pass

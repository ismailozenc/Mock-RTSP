# MockRtsp

A Windows Forms app that acts as a mock RTSP camera for testing. It runs [MediaMTX](https://github.com/bluenviron/mediamtx) as the RTSP server, and FFmpeg loops a source video into it as a named camera path. Your RTSP viewer and ALPR pipeline can then run without real hardware.

## Requirements

- .NET 8 SDK (Windows)
- `mediamtx.exe` and `ffmpeg.exe`, placed in folders next to `MockRtsp.exe`:

```
MockRtsp.exe
mediamtx\mediamtx.exe
ffmpeg\ffmpeg.exe        (or ffmpeg\bin\ffmpeg.exe)
```

At startup the app searches those folders and logs what it found. Nothing needs to be configured, and nothing is read from PATH.

For development, put the `mediamtx` and `ffmpeg` folders in the project folder. The build copies them next to the exe. They are git-ignored, so the binaries are not committed.

## Using the app

1. Click **Start server**.
2. Enter a stream path such as `Streaming/Channels/101` and choose a video file to loop, then click **Add camera**.
3. The camera starts as soon as it is added. Use **Stop** and **Start** to take it off and back on line. Point your viewer at the URL shown in the log, for example `rtsp://127.0.0.1:8554/Streaming/Channels/101`.
4. Select a camera and click **Open in VLC** to watch it. The button is enabled while the camera is running. VLC is found in a `vlc` folder next to `MockRtsp.exe`, or in its normal install folder.
5. Use the player row and the timeline under the camera list to control the selected camera: **Pause**/**Play**, **« 10 s** and **10 s »**, and a **Speed** from 0.25x to 4x. Hover over the timeline to see a preview frame of that moment. Click or drag on it to jump there, scroll with the mouse wheel or the scrollbar, and zoom with Ctrl + wheel or the **-**, **+** and **Whole** buttons. While playing, it follows the playhead. Viewers stay connected through all of these and simply see the picture freeze, jump or speed up.
6. Click **Settings...** to change resolution, frame rate, bitrate and the other video settings. Saving applies them to every camera: running cameras restart with the new settings (keeping position, pause and speed), and VLC windows opened from the app reopen on the new stream. Width and height must be even, for example 1280 x 720.
7. Use **Simulate outage** to drop the stream for a set number of seconds and test reconnect behaviour. Like a real camera, it comes back showing "now": the video has moved on by the outage time.

### Skipping parts of a video

Mark the parts the camera should jump over; they show as red blocks on the timeline and viewers see a clean cut.

- **Skip start** at the playhead, then move to where it should end and click **Skip end**. Or hold **Shift** and drag across the timeline. Or right-click and choose **Add a 5 s skip**.
- **Drag the white edge** of a red block to move where it starts or ends. The preview shows the exact frame at the edge. Dragging an edge into a neighbouring block joins the two.
- **Right-click a block** to delete it, or **Clear all skips**.

Skips are saved next to the video as `<video file>.skips.json` (for example `parking.mp4.skips.json`) every time you change them, and load automatically whenever that video is added as a camera, on any computer the two files are copied to. Cameras playing the same file share them. If the video's folder is not writable the skips still work, but the log says they could not be saved.

### How a camera works

Each camera runs two FFmpeg processes with the app in between:

- a **decoder** reads the video file from the current position and hands raw frames to the app (seeking restarts only this one),
- the app sends exactly one frame per frame interval; paused, it sends the last frame again, and at 2x it takes two source frames per frame,
- an **encoder** turns the frames into H.264 and publishes them to MediaMTX. It runs for as long as the camera is on, which is why viewers stay connected through pause and seek.

Preview frames for the timeline are made in the background by one more FFmpeg pass when a camera is added, about one per second (at most 600 for long videos). Cameras playing the same file share them.

The stream is always H.264, at the source's size and frame rate unless you set others. A keyframe is sent every second (unless you set an interval), so a new viewer shows a picture within about a second.

Changing settings disconnects anyone watching, just as a real camera does when its resolution changes. Your own viewer has to reconnect; VLC windows opened from the app are reopened for you.

Settings are saved in `%APPDATA%\MockRtsp\options.json` (including any passwords, as plain text) and loaded at startup. Server settings (transports, timeouts, logins, log level) apply after you stop and start the server. Logins can only use letters, digits and `! $ ( ) * + . ; < = > [ ] ^ _ - @ #`.

Video files you add as cameras are remembered in the dropdown next to **Video file**, up to 10 files, in `%APPDATA%\MockRtsp\recent-files.txt`.

When the server starts, any leftover `mediamtx.exe` from an earlier run of the same folder is stopped first.

Errors are shown in a dialog and in the log. Unexpected errors are also written to `%LOCALAPPDATA%\MockRtsp\errors.log`.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- `-stream_loop` can cause timestamp jumps at each loop point. Re-encode the source once with `ffmpeg -i in.mp4 -c:v libx264 -g 25 -an out.mp4` if needed.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

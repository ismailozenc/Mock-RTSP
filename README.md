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
2. Choose your **Video folder** (Browse, or pick a recent one). The list shows the playable videos in it with their length, picture size, skips and which cameras use them; other files are left out.
3. **Double-click a video** (or select it and click **Add camera**) to add it as a camera. Select several videos (Ctrl or Shift + click) to add one camera that plays them in turn. The stream path is filled in for you (Streaming/Channels/101, 201, 301 and so on) and can be changed first. The same video can be used by more than one camera.
4. Every camera gets its own panel on the right, with everything for that camera, so nothing needs selecting first:
   - **Start/Stop**, **Open in VLC**, **Playlist...**, **Video settings...**, **Scan motion**, **Outage** with **Simulate**, and **Remove**.
   - **Rewind** (back to the start of the video; pressed again at the start, to the video before), **« 10 s**, **Pause/Play**, **10 s »**, **Speed** (0.25x to 4x), **Skip start**/**Skip end**, and **-**, **+**, **Fit** to zoom its timeline. A camera with several videos also has a list to pick the video and **Next**.
   - Its own **timeline** of the video playing now: hover for a preview frame, click or drag to jump, drag the edges of the red skip blocks, Shift + drag to add one, right-click to delete. Ctrl + wheel zooms. Under the green movement graph a strip shows what plays (green) and what is skipped (red).
   Viewers stay connected through pause, seek and speed changes.

Point your viewer at the URL in the log, for example `rtsp://127.0.0.1:8554/Streaming/Channels/101`.

### Playlists

**Playlist...** on a camera picks the videos it plays, in order: add from the folder or any other file, remove, move up and down. After the last video the first starts again, unless looping is off in the settings, in which case the camera holds the last picture until you press Play. Viewers stay connected from one video to the next.

The camera's picture size, frame rate and quality come from the first video's settings. Videos of another size are scaled to it, with black bars when their shape differs. Each video keeps its own skips and motion data. Changing the first video restarts the camera only if that changes what it sends.

### Settings: defaults, and per video

- **Settings (defaults and server)...** holds the defaults used by every video without settings of its own, plus the server settings.
- **Video settings...** on a camera gives the video it is playing its own settings: resolution, frame rate, bitrate, keyframes, looping and the motion scan settings. **Use defaults** in that dialog goes back to the defaults. Cameras that play the video first restart with the new picture settings, keeping position, pause and speed.

### Saved next to each video

Everything about a video is kept in a file next to it, `<video file>.mockrtsp.json` (for example `parking.mp4.mockrtsp.json`), saved whenever it changes and loaded whenever the video is used again, on any computer the files are copied to:

- its own settings, if it has any,
- its skip ranges,
- its motion data: how much of the picture moves, 5 times a second, from the motion scan.

The preview pictures for the timeline are kept next to it too, in `<video file>.mockrtsp.previews`: small JPEG pictures (a few KB each) with a table of where each one is, so they load at once and any moment can be shown straight away. They are made again if the video file changes. Deleting the file is safe; it is made again the next time.

Because the motion data is kept, changing the motion settings (for the video or the defaults) picks the skips again straight away, without scanning again. If you edited the skips by hand you are asked first. Files from the previous version (`<video file>.skips.json`) are read and replaced by the new file on the first change.

### Skips and motion scan

- The first time a video is used it is scanned for movement in the background (it plays in full until then). Still parts become skip ranges, so by default only the parts with movement are streamed. The timeline shows the movement as a green graph: bright where it is over the threshold.
- **Scan motion** on a camera scans its video again, for example after replacing the file; you are asked before existing skips are replaced. An automatic scan never replaces skips you made while it ran.
- Motion settings: how much of the picture must change (default 0.3 %), how long a still part must last to be skipped (default 3 s), and how much to keep around movement (default 1 s). Small changes such as camera noise are ignored; a video where nothing moves enough gets no skips and plays in full.
- Add skips with **Skip start**/**Skip end** at the playhead, Shift + drag on the timeline, or right-click > **Add a 5 s skip**. Drag a red block's white edge to move it; dragging into a neighbouring block joins them. Viewers see a clean cut where a skip is.

### How a camera works

Each camera runs two FFmpeg processes with the app in between:

- a **decoder** reads the video file from the current position and hands raw frames to the app (seeking, and going on to the next video of a playlist, restart only this one),
- the app sends exactly one frame per frame interval; paused, it sends the last frame again, and at 2x it takes two source frames per frame,
- an **encoder** turns the frames into H.264 and publishes them to MediaMTX. It runs for as long as the camera is on, which is why viewers stay connected through pause and seek.

Preview frames for the timeline are made in the background by one more FFmpeg pass the first time a video is used, about one per second (at most 600 for long videos), and saved next to the video. Cameras playing the same file share them.

The stream is always H.264, at the source's size and frame rate unless you set others. A keyframe is sent every second (unless you set an interval), so a new viewer shows a picture within about a second.

Changing settings disconnects anyone watching, just as a real camera does when its resolution changes. Your own viewer has to reconnect; VLC windows opened from the app are reopened for you.

Settings are saved in `%APPDATA%\MockRtsp\options.json` (including any passwords, as plain text) and loaded at startup. Server settings (transports, timeouts, logins, log level) apply after you stop and start the server. Logins can only use letters, digits and `! $ ( ) * + . ; < = > [ ] ^ _ - @ #`.

The last 10 video folders are remembered in the dropdown next to **Video folder**, together with the other settings.

When the server starts, any leftover `mediamtx.exe` from an earlier run of the same folder is stopped first.

Errors are shown in a dialog and in the log. Unexpected errors are also written to `%LOCALAPPDATA%\MockRtsp\errors.log`.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- `-stream_loop` can cause timestamp jumps at each loop point. Re-encode the source once with `ffmpeg -i in.mp4 -c:v libx264 -g 25 -an out.mp4` if needed.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

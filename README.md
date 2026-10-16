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
5. Click **Quality...** to change a camera's bitrate, size, frame rate and other FFmpeg settings. Saving restarts that camera. These settings need re-encoding, so changing one turns **Re-encode** on. Width and height must be even, for example 1280 x 720.
6. Use **Simulate outage** to drop the stream for a set number of seconds and test reconnect behaviour.

**Stream options...** are saved in `%APPDATA%\MockRtsp\options.json` (including any passwords, as plain text) and loaded at startup. Saving applies the FFmpeg settings to every existing camera and restarts running ones. MediaMTX settings (transports, timeouts, logins, log level) need the server to be stopped and started again.

Video files you add as cameras are remembered in the dropdown next to **Video file**, up to 10 files, in `%APPDATA%\MockRtsp\recent-files.txt`.

When the server starts, any leftover `mediamtx.exe` from an earlier run of the same folder is stopped first.

Errors are shown in a dialog and in the log. Unexpected errors are also written to `%LOCALAPPDATA%\MockRtsp\errors.log`.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- `-stream_loop` can cause timestamp jumps at each loop point. Re-encode the source once with `ffmpeg -i in.mp4 -c:v libx264 -g 25 -an out.mp4` if needed.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

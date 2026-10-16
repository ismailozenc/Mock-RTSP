# MockRtsp

A Windows Forms app that acts as a mock RTSP camera for testing. It runs [MediaMTX](https://github.com/bluenviron/mediamtx) as the RTSP server, and FFmpeg loops a source video into it as a named camera path. Your RTSP viewer and ALPR pipeline can then run without real hardware.

## Requirements

- .NET 8 SDK (Windows)
- `ffmpeg` on PATH
- `mediamtx.exe` from the [MediaMTX releases](https://github.com/bluenviron/mediamtx/releases). Either choose it in the app, or place it at `Binaries\mediamtx.exe` before building to embed it.

## Build and run

Open `MockRtsp.sln` in Visual Studio, or run:

```
dotnet build
dotnet run --project MockRtsp.csproj
```

## Using the app

1. Choose `mediamtx.exe` (or leave it blank if embedded) and click **Start server**.
2. Enter a stream path such as `Streaming/Channels/101` and choose a video file to loop, then click **Add camera**.
3. Select the camera and click **Start**. Point your viewer at the URL shown in the log, for example `rtsp://127.0.0.1:8554/Streaming/Channels/101`.
4. Use **Simulate outage** to drop the stream for a set number of seconds and test reconnect behaviour.

Errors are shown in a dialog and in the log. Unexpected errors are also written to `%LOCALAPPDATA%\MockRtsp\errors.log`.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- `-stream_loop` can cause timestamp jumps at each loop point. Re-encode the source once with `ffmpeg -i in.mp4 -c:v libx264 -g 25 -an out.mp4` if needed.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

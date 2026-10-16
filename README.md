# MockRtsp

C# wrapper around [MediaMTX](https://github.com/bluenviron/mediamtx) that acts as a mock RTSP camera for testing. FFmpeg loops a source video into MediaMTX as a named camera path, so your RTSP viewer and ALPR pipeline can run without real hardware.

## Requirements

- .NET 8 SDK
- `ffmpeg` on PATH
- `mediamtx.exe` (Windows) from the [MediaMTX releases](https://github.com/bluenviron/mediamtx/releases). Either pass its path to `StartAsync`, or place it at `Binaries\mediamtx.exe` before building to embed it.

## Usage

```csharp
using MockRtsp;

using var server = await MediaMtxServer.StartAsync(mediaMtxPath: @"C:\tools\mediamtx.exe");

using var cam1 = new MockCamera(server, "Streaming/Channels/101", @"C:\samples\plates_1080p.mp4");
cam1.Start();

Console.WriteLine(cam1.Url); // rtsp://127.0.0.1:8554/Streaming/Channels/101

await cam1.SimulateOutageAsync(TimeSpan.FromSeconds(5));
```

Dispose cameras before the server so publishers stop before MediaMTX shuts down.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- `-stream_loop` can cause timestamp jumps at each loop point. Re-encode the source once with `ffmpeg -i in.mp4 -c:v libx264 -g 25 -an out.mp4` if needed.
- Each server needs its own port. RTP and RTCP ports are derived from it, so use ports at least 2000 apart.

# MockRtsp

A Windows Forms app that acts as a set of mock RTSP cameras for testing. It runs [MediaMTX](https://github.com/bluenviron/mediamtx) as the RTSP server and uses FFmpeg to stream video files into it as named camera paths (Hikvision-style `Streaming/Channels/101` by default), so an RTSP viewer or an ALPR pipeline can be developed and tested without real cameras.

Beyond looping a file, each camera behaves like a controllable camera:
- pause, rewind, seek, speed and simulated outages
- playlists of several videos
- skip ranges, picked automatically from a motion scan, so only the parts with movement are streamed
- a substream (`.../102` next to `.../101`), like a real camera

For ALPR testing, each video can carry a ground-truth list of the plates in it. The app logs the moment each plate is actually streamed and scores your ALPR results against that log (hit rate, misses, latency). Each camera shows live stats (frame rate, bitrate, viewers). The whole setup can be saved as a project and reopened.

Contents: [Requirements](#requirements) · [Using the app](#using-the-app) · [Ground truth for ALPR testing](#ground-truth-for-alpr-testing) · [For developers: how the code works](#for-developers-how-the-code-works) · [File reference](#file-reference) · [Changes timeline](#changes-timeline) · [Where we left off](#where-we-left-off)

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
   - **Start/Stop**, **Open in VLC**, **Playlist...**, **Video settings...**, **Scan motion**, **Plates...**, **Outage** with **Simulate**, and **Remove**.
   - Live stats at the end of the player row: frames per second actually sent, bitrate and viewers, for the main stream and the substream.
   - **Rewind** (back to the start of the video; pressed again at the start, to the video before), **« 10 s**, **Pause/Play**, **10 s »**, **Speed** (0.25x to 4x), **Skip start**/**Skip end**, and **-**, **+**, **Fit** to zoom its timeline. A camera with several videos also has a list to pick the video and **Next**.
   - Its own **timeline** of the video playing now: hover for a preview frame, click or drag to jump, drag the white edges of a skipped part (dark red), Shift + drag to add one, right-click to delete. Ctrl + wheel zooms. The graph shows how much of the picture moves: faint below the threshold (dashed line), solid green above it, and as a red hatched grid in skipped parts, so you can see the movement that is cut. Under it a strip shows what plays (green) and what is skipped (red).
   Viewers stay connected through pause, seek and speed changes.

Point your viewer at the URL in the log, for example `rtsp://127.0.0.1:8554/Streaming/Channels/101`, or the substream `rtsp://127.0.0.1:8554/Streaming/Channels/102`.

### Substreams

Like a real camera, every camera also sends a smaller second stream: `Streaming/Channels/102` next to `.../101` (a path that does not end in 01 gets `_sub` added). Real ALPR setups often detect vehicles on the substream and read plates on the main stream. Both come from the same FFmpeg process, so they always show the same moment.

The substream is set in **Settings > Defaults for videos > Substream**. It is on by default, 640 wide (the height keeps the main stream's shape; it is never larger than the main stream), at the main frame rate, with the encoder's default bitrate. Turn it off to send the main stream only. Changing these settings restarts every camera. A new camera's path is refused if it would land on another camera's substream, or the other way round.

### Live stats

Each camera panel shows, about once a second:
- **fps**: the frames per second actually sent, measured by the camera itself
- **Mbit/s or kbit/s**: what the camera publishes, from MediaMTX's byte count
- **viewers**: how many are watching, separately for the main stream and the substream

The numbers come from MediaMTX's control API, which the app turns on for this computer only, on a free port.

## Ground truth for ALPR testing

**Plates...** on a camera lists the licence plates visible in the video it is playing, and when:
- **time**: seconds (83.4) or minutes and seconds (1:23.4)
- **plate**: the plate text
- **region** (optional): where the plate is, as x, y, width and height fractions (0 to 1) of the video's picture

**Add at playhead** puts the camera's current moment into a new row. **Import CSV...** and **Export CSV...** use a simple `time,plate,x,y,width,height` file. The plates are saved with the video in its `.mockrtsp.json`, and shown on the timeline as yellow flags with the plate text. A plate inside a skipped part is shown grey: it is never streamed, so it is never expected.

**The ground-truth log.** When a camera hands the frame at a plate's time to the encoder, the app writes a line to the log and to the window's log. The log is one CSV file per day, `ground-truth-YYYY-MM-DD.csv`, in `Documents\MockRtsp\ground-truth` (change the folder in **Settings > Server**). Each line holds:
- the UTC and local time it was streamed
- the camera path and its substream path
- the video and the time in the video
- the plate
- its region as fractions of the video's picture, and in pixels of the main stream (after scaling and black bars)

A plate is logged every time it is streamed: on every loop, and again after seeking back over it. Plates passed over by a seek, and plates in skipped parts, are not logged.

**Scoring.** **Score ALPR results...** compares the log with what your ALPR system read. Give it a CSV of your system's results with a header line containing:
- a time column: `time`, `timestamp`, `utc`, `utc_time`, `datetime` or `date`. ISO 8601 with Z or an offset (without one it is local time), or Unix time in seconds or milliseconds.
- a plate column: `plate`, `plate_text`, `text`, `number` or `license_plate`
- optionally a camera column: `camera`, `camera_path`, `path`, `channel`, `stream` or `url`. It matches when it ends with the camera's path or its substream's, so `101`, `Streaming/Channels/102` and a full URL all work.

A streamed plate counts as **read** when the results have the same plate (letters and digits only, any case) from that camera, from 2 s before to 10 s after it was streamed (both adjustable). The window ends early if the same plate is streamed again. Further reads in the window are repeats, not errors.

The report shows:
- plates streamed, read correctly (hit rate), read **one character off**, and missed
- reads of plates that were not streamed
- latency from streamed to first correct read: average, median, 95th percentile, fastest and slowest
- per-camera results when there are several cameras
- a list of the plates not read correctly

**Save report (CSV)...** writes one line per streamed plate with its outcome, what it was read as, and the latency in ms.

### Projects: keep the whole setup

**Save project** keeps everything needed to get back to where you are: the port, the settings (defaults and server), the video folder, and every camera with its stream path, playlist, speed, position, pause and whether it is on. **Open project...** puts it all back: the server starts and the cameras are added. With **Reopen the last project at startup** ticked, the app does that by itself when it starts.

Projects are `.mockrtsp-project` files. Videos inside the project's folder are saved relative to it, so the folder can be moved or copied to another computer. Each video's own settings, skips and motion data are in the files next to the video, so they come along too. A video that is missing when the project opens is left out and logged. Closing the app, or opening another project, asks first if the setup has changed since it was saved.

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
- its motion data: how much of the picture moves, 5 times a second, from the motion scan,
- its ground-truth plates.

The preview pictures for the timeline are kept next to it too, in `<video file>.mockrtsp.previews`: small JPEG pictures (a few KB each) with a table of where each one is, so they load at once and any moment can be shown straight away. They are made again if the video file changes. Deleting the file is safe; it is made again the next time.

Because the motion data is kept, changing the motion settings (for the video or the defaults) picks the skips again straight away, without scanning again. If you edited the skips by hand you are asked first. Files from the previous version (`<video file>.skips.json`) are read and replaced by the new file on the first change.

### Skips and motion scan

- The first time a video is used it is scanned for movement in the background (it plays in full until then). Still parts become skip ranges, so by default only the parts with movement are streamed. The timeline shows the movement as a graph (see above).
- **Scan motion** on a camera scans its video again, for example after replacing the file; you are asked before existing skips are replaced. An automatic scan never replaces skips you made while it ran.
- Motion settings: how much of the picture must change (default 0.3 %), how long a still part must last to be skipped (default 3 s), and how much to keep around movement (default 1 s). Small changes such as camera noise are ignored; a video where nothing moves enough gets no skips and plays in full.
- Add skips with **Skip start**/**Skip end** at the playhead, Shift + drag on the timeline, or right-click > **Add a 5 s skip**. Drag a skipped part's white edge to move it; dragging into a neighbouring one joins them. Viewers see a clean cut where a skip is.

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
- The stream is always re-encoded, so any source FFmpeg can read works; there are no timestamp jumps at loop points or between playlist videos, because the encoder's clock never restarts.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

## For developers: how the code works

### Big picture

```
MainForm  (window, owns everything below; implements ICameraHost for the panels)
 ├─ MediaMtxServer          one mediamtx.exe process, config written to a temp folder
 ├─ VideoFile (per file)    shared by every camera that plays the file
 │    ├─ VideoSidecar        <video>.mockrtsp.json: own settings, skips, motion data
 │    ├─ MotionScan          background FFmpeg pass -> MotionProfile (5 samples/s)
 │    └─ ThumbnailStrip      background FFmpeg pass -> JPEG previews, cached in <video>.mockrtsp.previews
 ├─ CameraEntry (per camera) stream path + playlist of VideoFiles + MockCamera + VLC process
 │    ├─ MockCamera          decoder FFmpeg -> pump thread -> encoder FFmpeg -> MediaMTX
 │    └─ CameraPanel         the camera's controls and its Timeline
 ├─ GroundTruthLog          ground-truth-YYYY-MM-DD.csv: one line per plate streamed (from MockCamera.PlateStreamed)
 ├─ LiveStats               bitrates and viewers from MediaMtxServer.GetPathStatsAsync, polled every second
 ├─ OptionsStore            %APPDATA%\MockRtsp\options.json (defaults, server, remembered folders)
 └─ ProjectFile             *.mockrtsp-project: the whole setup
PlatesDialog (ground truth per video) · ScoreDialog + PlateScorer (ALPR results vs the log)
```

### The camera pipeline (MockCamera)

- **Decoder**: `ffmpeg -ss <pos> -i <file> -vf scale+pad+fps -pix_fmt yuv420p -f rawvideo pipe:1`. Outputs raw frames already scaled (letterboxed if the shape differs) to the camera's format and resampled to its frame rate. It is killed and restarted for every seek, and for the next video of the playlist.
- **Pump thread**: writes exactly one frame per frame interval to the encoder, using a Stopwatch schedule and `timeBeginPeriod(1)` on Windows. Paused, it writes the last frame again. Speed works with an accumulator: at 2x it reads two source frames per output frame. Skip ranges are checked on every frame *before* it is shown (`Show`), so a skipped frame is never sent and viewers see a clean cut.
- **Encoder**: `ffmpeg -f rawvideo -i pipe:0 -c:v libx264 -preset veryfast -tune zerolatency -force_key_frames expr:gte(t,n_forced*1) -f rtsp`. It runs for as long as the camera is on, so viewers stay connected through pause, seek, speed changes and playlist changes. A keyframe every second lets a new viewer start within about a second.
  - With the substream on, the same process has two outputs: `-filter_complex [0:v]split=2[main][sub];[sub]scale=W:H,fps=R[subv]` maps `[main]` to `.../101` and `[subv]` to `.../102`. The substream keyframe interval is scaled to its frame rate. If either publish fails, FFmpeg exits and the camera reports it as usual.
- **Ground truth**: when `Show` accepts a frame, `CollectPlates` notes the plates between the previous frame shown and this one. Playing on means a gap under 2 s in the same video; after a jump, only a plate on this very frame counts. Once the frame has been written to the encoder, `RaisePlates` raises `PlateStreamed` with the UTC time, on the pump thread. MainForm writes it to the GroundTruthLog.
- **Measured frame rate**: the pump counts frames written each second (`MeasuredFrameRate`).
- **Restarts**: settings that change the stream (size, frame rate, bitrate, keyframes, B-frames, publish transport) need `ApplyOptions`. That stops and starts the camera, which disconnects viewers like a real camera would, keeping position, pause and speed. VLC windows opened by the app are reopened.
- **Format**: the camera's size and rate come from `StreamOptions.OutputFormat(first video's VideoInfo)`. `CameraOptions(entry)` in MainForm = defaults, then the first video's own settings over them, then the logins the running server was started with.
- **Locks**: `_gate` guards processes and `_options`; `_control` guards player state (playlist, item, position, seek target, pause, speed). The only allowed nesting is `_gate` then `_control` (in `Start` via `Format`). Never take `_gate` while holding `_control`, which is why `_loop` is a volatile copy of `_options.Loop`.

### Motion and skips

- `MotionScan` decodes the file at 5 fps, 160x90 grey (area scaling smooths noise). For each sample it counts the share of pixels that changed by more than 18 levels, and stores it as a byte (percent x 10). The result is a `MotionProfile`.
- `MotionProfile.StillParts(threshold, minStill, padding)` turns the profile into skip ranges: still runs longer than `minStill`, shrunk by `padding` on each side. Because the profile is saved, new motion settings re-derive the skips instantly (`VideoFile.ApplyMotionSettings`), with no rescan. Skips edited by hand are detected (`SkipsMatchScan`) and only replaced if the user agrees.
- `SkipRanges.Normalize` sorts, clamps and merges ranges. `SkipRanges.Resolve` decides, for a position, whether to play, jump past a range, or report that the rest of the video is skipped (then the camera moves to the next playlist video, or wraps, or holds the last frame when looping is off). If the skips would leave less than 0.5 s to play, they are ignored.

### Files the app writes

| File | Where | Written by | Contents |
|---|---|---|---|
| `options.json` | `%APPDATA%\MockRtsp\` | OptionsStore | Defaults for videos, server settings (logins in plain text), last folder, recent folders, last project, reopen-at-startup flag |
| `errors.log` | `%LOCALAPPDATA%\MockRtsp\` | ErrorReporter | Unexpected exceptions with context |
| `<video>.mockrtsp.json` | next to each video | VideoSidecar | Video's own settings (or none), skip ranges, motion data (base64), ground-truth plates (time, plate, optional region). Atomic save. Deleted when there is nothing to keep. Replaces the older `<video>.skips.json`, which is read and deleted on the first save |
| `<video>.mockrtsp.previews` | next to each video | ThumbnailStrip | Binary: `"MRTPREV1"`, video size and last-write ticks, width, height, interval, count, one int32 length per picture, then the JPEGs. Rebuilt if the video changed or the file is damaged |
| `*.mockrtsp-project` | where the user saves it | ProjectFile | JSON: port, StartServer, Settings (StreamOptions without the remembered fields), VideoFolder, Cameras[] (StreamPath, Videos, Running, Paused, Speed, Video, PositionSeconds). Paths are relative to the project folder when inside it |
| `ground-truth-YYYY-MM-DD.csv` | `Documents\MockRtsp\ground-truth\` or the folder in Settings > Server | GroundTruthLog | One line per plate streamed. Columns: utc_time, local_time, camera, substream, video, video_time, plate, x, y, width, height, stream_x, stream_y, stream_width, stream_height |
| MediaMTX config | temp folder | MediaMtxServer | `mediamtx.yml` with RTSP only, the port, RTP/RTCP at port+1000/+1001, the control API on `127.0.0.1:<free port>` (for live stats, open to this computer only), and `authInternalUsers` for logins. Deleted on stop |

### Conventions (keep these when changing the code)

- **Designer-compatible forms**: every form and the UserControl has a `.Designer.cs` whose `InitializeComponent` uses no loops, no local variables, no string concatenation and no interpolation. Only `new`, property sets, `Controls.Add/AddRange` and event wiring are used, so the Visual Studio designer can open it. Each file starts with `#nullable enable`. Use `AutoScaleDimensions = new SizeF(7F, 15F)` with Segoe UI 9.
- **Layout**: add the Fill-docked control before edge-docked ones. Avoid an AutoSize GroupBox around a Fill-docked AutoSize grid (it grows without limit). Set SplitContainer `Panel1MinSize`/`Panel2MinSize` in code after the form has its size (`MainForm.SetSplitters`): Windows throws if they don't fit the default 150x100 while the designer code runs.
- **NumericUpDown**: typed text is committed explicitly before reading values (`CommitTyped` in OptionsDialog), or a value typed without leaving the box is lost.
- **UI thread**: all UI and `VideoFile` work runs on the UI thread. Background work (FFmpeg passes, the pump) never touches controls; the 250 ms `_timer` in MainForm polls (`VideoFile.Poll`, `CameraPanel.UpdateView`). `MainForm.Log` marshals with `BeginInvoke`.
- **Errors**: UI actions run through `Safe`/`SafeAsync` (`ICameraHost.Run/RunAsync` for panels). These log, write `errors.log` and show a message box. Background code reports with `ErrorReporter.Report` and never throws out of threads.
- **Wording**: user-facing text is plain and short. Log lines for a camera start with `'<stream path>': `.
- **Git**: work on `main` unless a version branch is asked for (v2 and v3 were). Commit messages describe the change for a user.

### Building and testing

- Build on Windows with the .NET 8 SDK: open `MockRtsp.sln`, or run `dotnet build`. Put `mediamtx\` and `ffmpeg\` in the project folder; the build copies them next to the exe.
- There is no test project in the repo. Changes so far were checked like this:
  - The engine (MockCamera, MediaMtxServer, VideoFile, MotionScan, ThumbnailStrip, ProjectFile) was run on Linux .NET 8 against real FFmpeg 6.1 and MediaMTX 1.15.1. A viewer recorded the stream to confirm one continuous connection across seeks and playlist changes, and frames were grabbed to check letterboxing.
  - The forms were type-checked and driven under Mono WinForms (net48) with stub cameras, using screenshots from Xvfb.
  - Mono does not enforce some Windows rules (the SplitContainer crash above was missed that way), so a run on Windows is the final check.

## File reference

Every source file, what it is for and what it does. All code is in namespace `MockRtsp`, one project (`MockRtsp.csproj`, net8.0-windows, WinForms, nullable on, implicit usings).

### Entry and infrastructure

- **`Program.cs`**: entry point. Installs last-resort exception handlers (UI thread, AppDomain, unobserved tasks) that report through ErrorReporter, then runs `MainForm`.
- **`ErrorReporter.cs`**: the one place errors are recorded. `Report(ex, context, showDialog)` appends to `%LOCALAPPDATA%\MockRtsp\errors.log` and optionally shows a dialog. Never throws, because it is called from catch blocks.
- **`Toolchain.cs`**: finds bundled tools. `Find(folder, exe)` looks in `<exe dir>\<folder>\` and then any subfolder of it (so `ffmpeg\bin\ffmpeg.exe` is found). Constants `MediaMtxFolder = "mediamtx"`, `FfmpegFolder = "ffmpeg"`. Nothing is read from PATH.
- **`MediaMtxServer.cs`**: starts and owns one `mediamtx.exe`.
  - `StartAsync(port, exe, ..., log, options)` does the following:
    - checks the port range (RTP/RTCP need port+1000/+1001) and that the TCP/UDP ports are free
    - stops leftover mediamtx processes started from the same exe path
    - writes a minimal YAML config to a temp folder (`BuildConfig`: RTSP only, transports, timeouts, `authInternalUsers` for publisher and viewer logins, or `any` with no password)
    - starts it, and waits until the port accepts connections
  - `IsPathReadyAsync` sends a real RTSP DESCRIBE and expects 200 OK: "a viewer could play this now".
  - `GetPathStatsAsync` reads `/v3/paths/list` from MediaMTX's API (on `ApiPort`, a free local port chosen at start) and returns `PathStats(Ready, BytesReceived, Viewers)` per path, or null. Never throws.
  - `BuildUrl` puts logins in the URL. `Dispose` kills the process tree and deletes the temp folder.
- **`OptionsStore.cs`**: loads and saves `StreamOptions` as JSON in `%APPDATA%\MockRtsp\options.json`. Never throws; invalid files fall back to defaults.
- **`VlcLauncher.cs`**: finds `vlc.exe` (a bundled `vlc\` folder, then Program Files), opens a stream in its own window with low network caching and RTSP over TCP, and closes it again. MainForm reopens VLC after a camera restart because VLC does not reconnect by itself.

### Settings and data

- **`StreamOptions.cs`**: every setting.
  - Video: loop, bitrate, keyframe interval, size, frame rate, B-frames, publish transport.
  - Motion: auto scan, threshold %, min still s, padding s.
  - Substream: on/off, width, frame rate, bitrate. `SubstreamPathFor(path)` (101 → 102), `SubstreamFormat(main)`, `SameSubstream`.
  - Ground-truth log folder.
  - FFmpeg and MediaMTX log levels; server transports, timeouts and logins.
  - Remembered between runs: video folder, recent folders, last project, reopen flag.
  - Also: `Validate()` (all rules, including the characters MediaMTX accepts in logins), `ServerSettingsEqual`, `OutputFormat(VideoInfo)`, `DecoderArguments`, `ScaleFilter` (scale to fit, pad, setsar, fps) and `EncoderArguments(format, url, substreamUrl)` (one output, or two through a split filter).
  - `VideoFormat` record struct: width, height, rate and `FrameBytes` for yuv420p.
- **`VideoSettings.cs`**: the per-video subset of StreamOptions (loop, size, rate, bitrate, keyframes, B-frames, motion settings).
  - `From(StreamOptions)` and `ApplyTo(StreamOptions)`.
  - `SameStream` (does a camera need a restart?) and `SameMotion` (would the skips change?).
  - `StillParts(MotionProfile, duration)`.
- **`VideoInfo.cs`**: `record VideoInfo(Width, Height, FrameRate, DurationSeconds)`. `Probe` runs `ffmpeg -i` and parses its stderr (`Parse`); an unplayable file throws InvalidOperationException with FFmpeg's last lines.
- **`SkipRanges.cs`**: `record SkipRange(Start, End)` with an exclusive end, and the static rules: `Normalize`, `LeavesSomethingToPlay`, `Resolve` (Play / JumpTo / EndReached). Used by the camera, the timeline and the sidecar.
- **`MotionProfile.cs`**: `record MotionProfile(IntervalSeconds, byte[] Levels)` with levels as percent x 10. `PercentAt(i)` and `StillParts(threshold, minStill, padding, duration)`.
- **`MotionScan.cs`**: background FFmpeg pass that measures movement (see Motion and skips above). Exposes `Progress`, `IsComplete`, `IsCancelled`, `Result` and `Error`; `Dispose` cancels it.
- **`VideoSidecar.cs`**: reads and writes `<video>.mockrtsp.json`.
  - `Load` returns `SavedVideo(Skips, Motion, Settings, FileExists, FromOldFile, Plates)` and throws InvalidDataException on a damaged file.
  - `Save` writes atomically, deletes the file when there is nothing to keep, and removes the old `.skips.json`.
- **`ThumbnailStrip.cs`**: preview pictures for the hover popup.
  - Pictures are taken about once per second (at most 600; keyframes only for videos over 5 minutes), at 108 px high, as JPEG via `-c:v mjpeg -f image2pipe`.
  - `ReadJpeg` splits the stream by parsing JPEG markers, not by searching for FFD9.
  - `Start` first tries `TryLoad` from `<video>.mockrtsp.previews`. The file is checked against the video's size and time and the expected dimensions and interval; otherwise the pictures are made again and saved with `TrySave` (temp file, then move; quietly skipped on a read-only folder).
  - `GetJpeg(i)` gives random access to any picture.
- **`VideoFile.cs`**: one open video, shared by all cameras that play it: path, `Info`, `Item` (as a PlaylistItem), `Skips`, `Motion`, `Previews`, own settings, scan state.
  - `Load(defaults)` reads the sidecar, starts an automatic scan the first time (when `AutoScanMotion` is on), and starts the previews.
  - `SetSkips`, `SetPlates`, `SetOwnSettings`, `ApplyMotionSettings` and `SkipsMatchScan` (the setters save straight away).
  - `StartScan(automatic)`; `Poll(defaults)` applies a finished scan (an automatic scan keeps skips the user made meanwhile).
  - Events: `Changed` (MainForm pushes skips to cameras) and `Message` (log lines).
- **`PlateMarks.cs`**: ground-truth plates.
  - `record PlateMark(Time, Plate, X?, Y?, Width?, Height?)` with the region as fractions of the video's picture; `Normalize(plate text)` keeps letters and digits in upper case for comparing.
  - `PlateMarks`: `Normalize(list)`, `Check` (time inside the video, all-or-none region inside the picture), `FormatTime`/`TryParseTime` (83.4 or 1:23.4), and `ToCsv`/`FromCsv` (`time,plate,x,y,width,height`; errors name the line).
  - `Csv`: escaping and a reader that handles quoted fields.
- **`GroundTruthLog.cs`**: `record StreamedPlate` (one log line) and `GroundTruthLog`.
  - `Write` appends thread-safely to the day's file and adds the header to a new file.
  - `From(camera, substream, PlateStreamedEventArgs)` builds a line; `StreamRegion` maps a region into main-stream pixels, the same way `ScaleFilter` letterboxes.
  - `Read(path)` parses a log; `DefaultFolder`/`FolderFor(options)` give the folder.
- **`PlateScorer.cs`**: scoring ALPR results against the log.
  - `ReadResults(path)` finds the time, plate and camera columns by name; times are ISO 8601 or Unix s/ms.
  - `Score(truth, reads, before, after)`: exact match = hit, one character off = close, otherwise missed. The window ends early at the next pass of the same plate; repeats are used up.
  - `ScoreResult` gives the counts and hit rate; `Summary` is the report text and `ToCsv` the per-plate report.
- **`LiveStats.cs`**: `record CameraStats` and `LiveStats`. `Update(paths, time)` turns byte counts into kbit/s per path; `For(camera, path)` combines them with the camera's measured fps and viewer counts; `Describe` gives the panel text.
- **`ProjectFile.cs`**: the saved setup (`ProjectFile`, `ProjectCamera`).
  - `Load` validates and turns relative paths into full ones.
  - `Save` makes paths relative where possible, strips the remembered fields and writes atomically.
  - `Fingerprint` serializes the setup without position, current video or pause, so MainForm can tell whether there are unsaved changes.

### Camera engine

- **`MockCamera.cs`**: one camera; see the pipeline section above.
  - `PlaylistItem(File, Info)` record.
  - State: `Playlist`, `ItemIndex`, `Current`, `Position`, `Duration`, `IsPaused`, `Speed`, `Format`, `Options`, `IsOnline`, `Url`/`ViewerUrl`, and `LastFfmpegOutput` (the last 8 lines, for errors).
  - Controls: `Pause`/`Resume` (Resume at the end of a non-looping playlist starts over), `SeekTo`/`SeekBy` (within the current video), `PlayItem(i)`, `Rewind()` (to the start, or to the previous video within the first 2 s), `Next()`.
  - Playlist and skips: `SetPlaylist` (keeps the playing video and its skips if it stays), `SetSkipRanges(file, ranges)` and `SkipRangesOf`.
  - Running: `ApplyOptions`, `Start`/`Stop`, `SimulateOutageAsync` (comes back at "now", moved on through the playlist), and `WaitUntilLiveAsync` (polls DESCRIBE).
  - Substream: `SubstreamPath`, `SubstreamUrl`, `SubstreamViewerUrl`, `SubstreamFormat`. `MeasuredFrameRate` is the frames actually sent per second.
  - Ground truth: `SetPlates(file, plates)`; `PlateStreamed` (`PlateStreamedEventArgs(Utc, Video, Plate, Format)`) is raised on the pump thread after the plate's frame is written.
  - `PublisherExited` is raised on a background thread when FFmpeg dies by itself.
  - Internals: `Pump`; `ReadFrame` (moves to the next video at end of file, and skips a video that gives no frames); `Show` (skip check); `Resolve`; `MoveOnBy`; the `Decoder` class; `HighResolutionTimer`.

### User interface

- **`MainForm.cs` / `MainForm.Designer.cs`**: the window.
  - Layout:
    - top row: port, Start/Stop server, Settings, Open/Save/Save as project, "Reopen the last project at startup", Score ALPR results
    - folder row: the folder combo with recent folders, Browse, Refresh
    - left: the video list (ListView: name, length, picture, skips, cameras; multi-select), the stream path box and Add camera
    - right: `_cameras` FlowLayoutPanel with one CameraPanel per camera
    - bottom: the log; then a status bar
  - Folder: `LoadFolder` lists video extensions, then `ProbeFolderAsync` probes two at a time, hides unplayable files and fills in the rows (`_playableFiles`).
  - Cameras:
    - `AddCamera` adds the selected videos as one camera (a playlist when several are selected) through `CreateCamera`.
    - `OpenVideo` / `ReleaseVideoIfUnused` share VideoFiles by path.
    - `OnVideoChanged` pushes a video's skips to every camera that plays it.
    - `CamerasOf(video)` returns cameras with the video anywhere in their playlist; `StreamCamerasOf(video)` returns those that play it first.
    - `NextStreamPath` returns the next free `Streaming/Channels/{n}01`.
  - Settings:
    - `EditSettingsAsync`: defaults and server. Restarts cameras whose first video uses the defaults and whose stream changed, or every camera if the publish settings changed. Re-derives skips from motion data.
    - `EditVideoSettingsAsync(entry, video)`: one video's own settings; choosing the same as the defaults clears them.
    - `ReapplyMotion` asks before replacing hand-edited skips.
    - `RestartAsync`/`ApplySettingsToCameraAsync` restart, wait for the stream to be live, and reopen VLC.
  - Playlists: `EditPlaylistAsync` opens the PlaylistDialog, opens and releases videos, and restarts only if the first video changes what is sent.
  - Ground truth: `OnPlateStreamed` writes each `PlateStreamed` to `_groundTruth` (a GroundTruthLog for the folder in the settings) and the log. `EditPlatesAsync` opens the PlatesDialog; `OnScoreClick` opens the ScoreDialog on today's (or the latest) log.
  - Live stats: `PollStats`/`PollStatsAsync` run from the timer about once a second; `StatsFor(entry)` serves the panels. `CheckStreamPath` and `NextStreamPath` keep substream paths free of clashes.
  - Projects: `CaptureProject`, `SaveProject(path or null = ask)`, `OpenProjectAsync` (stops the server, applies settings, port and folder, starts the server, `RestoreCamera` for each), `ConfirmDiscardChanges` (Yes/No/Cancel when the fingerprint differs), `OnShown` (reopens the last project), `UpdateTitle`.
  - Also: `OnTimerTick` (every 250 ms: server health, scan polling, panel refresh, layout), `LayoutPanels`, `SetSplitters`/`FitSplitter`, `Safe`/`SafeAsync`/`HandleError`, `Log`.
- **`CameraEntry.cs`**: one camera's state for the UI.
  - `StreamPath`, `Playlist` (VideoFiles), `Video` (the one playing now), `First` (sets the format), `Camera`, `Vlc`.
  - `PendingSkipStart`/`PendingSkipVideo`: a skip begun with Skip start, tied to its video.
  - `Plays(video)`; `SetPlaylist` updates the camera and its skips.
- **`CameraPanel.cs` / `CameraPanel.Designer.cs`**: the UserControl for one camera, plus `ICameraHost`, the interface MainForm implements for it.
  - Row 1: state badge (LIVE/PAUSED/OFF), stream path, details (videos, format, substream format, settings, skips, plates), Start/Stop, Open in VLC, Playlist..., Video settings..., Scan motion, Plates..., outage seconds with Simulate, Remove.
  - Row 2: video picker and Next (only with several videos), Rewind, « 10 s, Pause/Play, 10 s », time, Speed, Skip start/end, zoom -, +, Fit, and the live stats.
  - Row 3: the Timeline.
  - `UpdateView()` (from the timer) refreshes everything, and switches the timeline to the video now playing. `_shown` records which video the timeline shows, so skip edits always go to that video even if the playlist moved on. `HeightFor(width)` gives the height with wrapped rows.
- **`Timeline.cs`**: custom control for one video.
  - Ruler; a bar with skipped parts as dark red areas with draggable white edges.
  - The motion graph: faint below the threshold, solid green above, a red hatched grid where skipped. It is scaled to the video's busiest moment, with a dashed threshold line.
  - Plate flags (`SetPlates`): yellow with the plate text where it fits, grey inside a skipped part; the hover caption lists plates near the mouse.
  - A green/red play strip; the playhead, the hover line, and an orange pending-skip line.
  - A horizontal scrollbar with zoom (Ctrl + wheel, ZoomIn/Out/ToFit) and auto-follow of the playhead.
  - Hover shows `PreviewPopup` with the JPEG decoded on demand (at most 150 kept).
  - Events `SeekRequested` and `SkipRangesChanged`. Mouse: click/drag to seek, drag edges, Shift + drag to create, right-click menu.
- **`OptionsDialog.cs` / `OptionsDialog.Designer.cs`**: Settings dialog with the tabs "Defaults for videos" (looping, motion scan, encoding, publishing, substream) and "Server" (MediaMTX transports, timeouts, log level, logins, ground-truth log folder).
  - The same dialog edits one video (`new OptionsDialog(current, videoName, defaults)`): the server tab and server-only rows are hidden, and Reset becomes "Use defaults".
  - `Result` holds the edited copy; `CommitTyped` fixes NumericUpDown typing.
- **`PlaylistDialog.cs` / `PlaylistDialog.Designer.cs`**: two lists (videos in the folder, and the playlist) with Add, Remove, Move up/down and Other file.... `Files` returns the chosen order.
- **`PlatesDialog.cs` / `PlatesDialog.Designer.cs`**: a DataGridView of time, plate, x, y, width and height for one video, with Add at playhead, Remove, Import CSV... and Export CSV.... `Plates` returns the checked, sorted list; errors name the row.
- **`ScoreDialog.cs` / `ScoreDialog.Designer.cs`**: picks the ground-truth log and the results file, sets the before/after window, shows `PlateScorer.Summary`, and saves the CSV report.
- **`MainForm.resx`, `OptionsDialog.resx`**: resource files added by the Visual Studio designer. Nothing in them is used by the code.

### Other files

- **`MockRtsp.csproj`**: net8.0-windows WinExe with WinForms and PerMonitorV2 DPI. It copies `mediamtx\` and `ffmpeg\` to the output when they exist.
- **`MockRtsp.sln`**: the solution.
- **`.gitignore`**: bin, obj, .vs, user files, and the `mediamtx/` and `ffmpeg/` binaries.

## Changes timeline

Newest last. Commit hashes are on `main`.

**2026-10-16: first version and stabilisation**
- `85f3c3c` MediaMTX-based mock camera; `a4538a5` solution file; `5e0b55b` WinForms app with exception handling.
- `871d891` RTP/RTCP port checks; `8e72c6c` stop leftover MediaMTX; `01621c5` bundled `mediamtx\` and `ffmpeg\` folders instead of settings.ini.
- `3fbf6a7`, `839c33a`, `f019685` Stream options dialog, made designer-compatible (no loops or locals in InitializeComponent).
- `3daac53` Recent files, Open in VLC, per-camera quality. `736e81f` Size and frame rate changes always re-encode. `e7f5f36` Dialog fits, FFmpeg errors shown, odd sizes rejected.
- `01552c2` Options saved and applied to running cameras. `9178468` Stuck status and scaling fixes. `0122178` One Settings dialog; MediaMTX `authInternalUsers` logins. `5cba928` Missing OK/Cancel, runaway layout and lost typed values fixed.
- `6606a65` A keyframe every second, so playback starts in about a second instead of about 12.

**2026-10-18: v2, a controllable camera**
- `65184c2` Decoder → pump → encoder pipeline: pause, rewind, forward, seek and speed without disconnecting viewers.
- `8190d60` Scrollable timeline with thumbnails; `8be5342` replaced by a hover preview. Skip ranges with draggable edges, saved next to the video.
- `c6fc3b8` Motion scan: still parts are skipped automatically; Scan motion button.

**2026-10-23: v3, folders, per-video data, panels, playlists, projects** (merged in `8df5185`)
- `a1aa1bb` Video folder with a list of playable videos. Per-video settings, skips and motion data in `<video>.mockrtsp.json` (motion settings re-derive skips without a rescan). One panel per camera with its own controls and timeline. Recent files replaced by recent folders.
- `8662bbe` Fix: startup crash from SplitContainer minimum sizes during InitializeComponent.
- `3a0f29a` Playlists per camera (letterboxed to the first video's format, viewers stay connected); Rewind and Next; JPEG previews cached in `<video>.mockrtsp.previews`; green/red play strip.
- `748e4b9` Projects (`.mockrtsp-project`): save, open, reopen at startup, unsaved-change prompt. The motion graph is drawn everywhere, with a red hatched grid where skipped.
- Visual Studio added `MainForm.resx` and `OptionsDialog.resx` on main.
- `0335caf` This README: developer guide, file reference, changes timeline.

**2026-10-29: ALPR test bench**
- Substream per camera (`.../102`, 640 wide by default) from the same encoder as the main stream; settings in Defaults for videos; clash checks on stream paths.
- Ground-truth plates per video (Plates... dialog, CSV import and export, saved in `.mockrtsp.json`, flags on the timeline). The camera raises `PlateStreamed` when a plate's frame is sent, and the app writes the daily ground-truth log.
- Score ALPR results: hit rate, one-character-off, misses, unmatched reads, latency statistics, per camera, CSV report.
- Live stats per camera: measured fps, bitrate and viewers for the main stream and substream, from MediaMTX's API (now on, on a free local port, for this computer only).

## Where we left off

- **Not yet confirmed on Windows**: the latest UI (playlist controls, Playlist dialog, project buttons, the timeline drawing, the Plates and Score dialogs, the stats text) was checked under Mono only. Things to look at on Windows:
  - whether the player row wraps on narrower windows
  - the project open/save file dialogs
  - the startup reopen
- **Known limits**:
  - A camera's picture settings come from its first video; other videos' own picture settings only matter for cameras that play them first.
  - Cameras are only saved in projects while the server runs (stopping the server removes them).
  - Logins are stored in plain text in options.json and in project files.
  - "Rewind" goes back to the start; it does not play backwards.
  - Substream settings apply to every camera; there is no per-camera substream setting.
  - The ground-truth time is when the frame goes into the encoder. What the ALPR system measures also includes encoding, MediaMTX and the network, so latency figures include that pipeline too.
- **Ideas not done**:
  - per-camera loop setting (looping is per video/defaults now)
  - showing the whole playlist on one timeline
  - a test project in the repo
  - drawing plate regions on the preview instead of typing fractions
  - more ideas discussed: Hikvision ISAPI endpoints and snapshots, ONVIF discovery, fault injection (frozen picture, packet loss), a control API with headless mode, hardware encoding
  - the video list's Camera column is cut off at the default window width

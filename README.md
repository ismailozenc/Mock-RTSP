# MockRtsp

Two Windows Forms apps for testing camera software without real cameras:

- **Mock RTSP** (`MockRtsp.exe`) acts as a set of mock RTSP cameras. It runs [MediaMTX](https://github.com/bluenviron/mediamtx) as the RTSP server and uses FFmpeg to stream video files into it as named camera paths (Hikvision-style `Streaming/Channels/101` by default), so an RTSP viewer or an ALPR pipeline can be developed and tested against them.
- **Mock RTSP Scanner** (`MockRtspScanner.exe`) prepares the videos: it finds their **gaps** (still parts, which the cameras skip), reads the **plates** of the cars in the remaining playing parts with Birel Plaka's detector and OCR, works out their **directions** (coming or going), and lets you check and correct all of it as ground truth. It scans a queue of videos one after another; results appear while they are found and can be edited straight away.

The scanner saves everything in a file next to each video. The RTSP app only shows it (on each camera's timeline: gaps, motion, plates and labels) and picks up every change the scanner saves within a second, so both can run side by side.

Beyond looping a file, each camera behaves like a controllable camera:
- pause, rewind, seek, speed and simulated outages
- playlists of several videos
- the gaps found by the scanner are skipped, so only the parts with movement are streamed
- a substream (`.../102` next to `.../101`), like a real camera

For ALPR testing, each video can carry a ground-truth list of the plates in it. The RTSP app logs the moment each plate is actually streamed and scores your ALPR results against that log (hit rate, misses, latency). Each camera shows live stats (frame rate, bitrate, viewers). The whole setup can be saved as a project and reopened.

Contents: [Requirements](#requirements) · [Using the RTSP app](#using-the-rtsp-app) · [Mock RTSP Scanner](#mock-rtsp-scanner) · [Ground truth for ALPR testing](#ground-truth-for-alpr-testing) · [For developers: how the code works](#for-developers-how-the-code-works) · [File reference](#file-reference) · [Changes timeline](#changes-timeline) · [Where we left off](#where-we-left-off)

## Requirements

- .NET 8 SDK (Windows)
- `mediamtx.exe` and `ffmpeg.exe`, placed in folders next to the exes:

```
MockRtsp.exe
MockRtspScanner.exe
mediamtx\mediamtx.exe    (the RTSP app only)
ffmpeg\ffmpeg.exe        (both; or ffmpeg\bin\ffmpeg.exe)
models\                  (the scanner: Birel Plaka's plate models, copied by the build)
```

At startup each app searches those folders and logs what it found. Nothing needs to be configured, and nothing is read from PATH.

For development, put the `mediamtx` and `ffmpeg` folders in the solution folder (next to `MockRtsp.sln`). The build copies them next to each exe. They are git-ignored, so the binaries are not committed.

## Using the RTSP app

1. Click **Start server**.
2. Choose your **Video folder** (Browse, or pick a recent one). The list shows the playable videos in it with their length, picture size, gaps and labels (from the scanner) and which cameras use them; other files are left out.
3. **Double-click a video** (or select it and click **Add camera**) to add it as a camera. Select several videos (Ctrl or Shift + click) to add one camera that plays them in turn. The stream path is filled in for you (Streaming/Channels/101, 201, 301 and so on) and can be changed first. The same video can be used by more than one camera.
4. Every camera gets its own panel on the right, with everything for that camera, so nothing needs selecting first:
   - **Start/Stop**, **Open in VLC**, **Playlist...**, **Video settings...**, **Outage** with **Simulate**, and **Remove**.
   - Live stats at the end of the player row: frames per second actually sent, bitrate and viewers, for the main stream and the substream.
   - **Rewind** (back to the start of the video; pressed again at the start, to the video before), **« 10 s**, **Pause/Play**, **10 s »**, **Speed** (0.25x to 4x), and **-**, **+**, **Fit** to zoom its timeline. A camera with several videos also has a list to pick the video and **Next**.
   - Its own **timeline** of the video playing now: hover for a preview frame, click or drag to jump, Ctrl + wheel zooms. It shows what the scanner found, and cannot change it: the gaps (dark red, skipped), the motion graph (faint below the threshold, the dashed line, solid green above it, and as a red hatched grid in gaps, so you can see the movement that is cut), a strip of what plays (green) and what is skipped (red), the ground-truth plates as yellow flags, and the labels strip: one block per playing part with its vehicles (green coming, orange going; an orange hatch with "?" for a part still to label). The top right shows how many parts there are and how many are labelled.
   Viewers stay connected through pause, seek and speed changes.

A video the scanner has not seen yet plays in full. When the scanner saves new gaps or labels, the cameras playing that video follow them at once and the log says so ("updated by the scanner").

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

## Mock RTSP Scanner

Run `MockRtspScanner.exe` and choose the **Video folder** (Browse, a recent folder, or type it and press Enter). Only one scanner runs at a time.

**The videos** of the folder are listed on the left with their length, **Gaps** (how many, "none", or "not scanned"), **Plates read** (cars found), **Labelled** (labelled parts of all playing parts, and how many are still to check) and **Scan** (waiting, finding gaps 40%, reading plates 55%: part 2 of 5, done, stopped or failed). Hover a row for the whole text.

**The queue.** **Scan selected** queues the selected videos, **Scan all not scanned** queues every video whose gaps or plates are missing. They are scanned one after another:
1. **Gaps**: the motion is measured through the whole video (5 times a second). The graph on the timeline grows while it runs; then the still parts become gaps, picked with the video's gap settings (or those for new videos, set under the list: movement needed 0.3 % of the picture, gaps of at least 3 s, 1 s kept around movement).
2. **Plates and directions**: each playing part is read with Birel Plaka's models (below). As soon as a part is read, its cars are listed and, when the part has no labels yet, it is prefilled with them.
- A video with gaps already found skips step 1, and one with plates read skips step 2, unless **Find gaps again** or **Read plates again** is ticked. Measuring the motion again keeps gaps you changed by hand.
- **Stop** stops the video being scanned (what was found so far is kept) and empties the queue; **Take out of the queue** removes waiting videos.
- Closing the scanner while it scans asks first.

**The editor** on the right shows the selected video, and stays editable while it is being scanned (scanned or not). Every change is saved at once, next to the video, where the RTSP app picks it up:
- **The timeline**: the gaps (dark red), the motion graph, the ground-truth plates and the labels strip, as in the RTSP app, but here the gaps can be changed: drag a gap's white edges, Shift + drag to draw a new one, right-click to delete one, add a 5 s gap or clear them all. Click a moment to show it in the player; a double-click on the labels strip picks that part.
- **Gap settings**: movement needed, minimum gap and time kept around movement for this video, and **Pick gaps again**, which picks them from the saved motion data at once (no rescan; gaps changed by hand are only replaced if you agree).
- **The player**: the selected playing part plays straight from the video file, in a loop, so you can see each vehicle, which way it goes and its plate. **Pause**/**Play**, **< Frame** and **Frame >**, **|< Start**, speed (0.25x to 2x) and **Loop**. Click or drag the green bar under the picture to jump within the part. The **mouse wheel zooms** around the mouse (up to 8x) to read a plate; drag to move the zoomed picture, double-click for the whole picture again. With the picture clicked, Space pauses and Left/Right step one frame.
- **Where plates are read** (under the player), see below.
- **The list of playing parts**, with their start, end, length, vehicles and how many cars were read in them. Parts still to label are orange, prefilled parts still to check blue. The line above counts them: "12 playing parts, 7 labelled (9 vehicles: 5 coming, 4 going), 5 to label; 3 prefilled from the plates, to check".
- **The selected part's vehicles**, as many as it has: **time**, **direction** (coming towards the camera, or going away) and **plate** (optional; leave it empty when it cannot be read). **Add vehicle at the player's time** puts one at the moment the player shows, so pause on the vehicle first. Double-click a vehicle's time or plate to see that moment in the player. A part with movement but no vehicle (a pedestrian, a door) gets **No vehicles in this part**, so it counts as labelled too. A row that cannot be read yet (a time outside the part, no direction) is saved once it is fixed; the line at the bottom says what is wrong.
- **The cars read in the selected part**: a picture of the plate, the plate, which way the car moved, the time, how many reads it had and the result: "decided (60 reads agree)", or why no plate was decided (for example "Reads disagree on character 5"), with the best guess and a "?". Hover a car for the whole text. Selecting a car shows its moment in the player.
- **Readings of the selected car**: every different plate text the OCR gave for it, with how often and its best OCR confidence, so a wrong decision can be corrected.
- **Add as vehicle** (or a double-click) adds the selected car, with the selected reading as its plate. **Put in the selected vehicle** writes the reading into the vehicle selected above. **Add every car of this part** adds all of them. Cars already in the part's vehicles are greyed out. A car is added with its most likely direction; when nothing tells, the direction is left empty for you to choose.
- **Next to label** and **Previous to label** go through the parts without labels and the prefilled ones still to check. **Delete labels in gaps** removes labels that ended up in gaps (never streamed). **Export CSV...** writes one line per vehicle: `part,part_start,part_end,time,direction,plate` (a part with no vehicles gets one line with direction `none`). **Ground-truth plates...** edits the video's ground-truth plates for ALPR scoring (see below). **Prefill unlabelled parts** prefills from plates read before.

Each vehicle is kept with its time, so its labels stay with it when the gaps change: they belong to whichever playing part contains that time.

### Prefilled with the most likely result

When a part has been read and has no labels yet (no vehicles, not checked as "no vehicles"), it is filled with the cars read in it: each at the moment of its best read, with its plate (the voted one, or the best guess when the reads did not agree) and its most likely direction. Labels you made are never changed.
- Cars seen in fewer than 3 pictures (Birel Plaka's [Travel] MinReads) are left out as glimpses; they are still in the cars list. A part with a car whose direction nothing tells is left to label.
- Prefilled parts are marked **to check** (blue in the list, "prefilled from the plates read: check it" over the vehicles, counted in the line at the top and in the video list) until you have looked at them: moving on to another part, another video, or closing the scanner counts as checked. Which parts are still to check is kept in the plates file, so they are still marked the next time.
- The direction: Birel Plaka's own estimate (coming or going) when it gives one. It only says so when the plate grows or shrinks by at least 10 % (or moves at least 6 % along the drawn road); otherwise it says unknown, stopped or unclear, and the most likely direction is taken from the same measurements: a smaller change in size or road position still points one way (at least 3 % in size or 2 % along the road; the clearer of the two). When size and road clearly disagree ("unclear"), the size wins, since Birel Plaka then asks to check the road perspective. The cars list shows such a guess as "going? (unclear)".

### Reading plates with Birel Plaka

**Where plates are read** (the buttons under the player): the same two settings Birel Plaka has for each camera.
- **Detection area (R)**: only this part of the picture goes to the plate detector, as Birel Plaka's detection area (ROI) does. Drag over the picture to draw it; then drag its edges or corners, or drag inside it to move it. Outside it the picture is darker while you edit. Right-click or **Whole picture** goes back to reading the whole picture. A plate touching the left or right edge of the area may be cut off, so such reads count as cut off, as in Birel Plaka.
- **Road perspective (P)**: four corners of a rectangle lying on the road (far edge first), so the direction of a car uses where its plate is on the road as well as its size. Drag the corners onto the lane edges; the grid shows whether it lies flat on the road. Right-click resets it; **No perspective** removes it.
- Esc undoes the changes since editing started; press the button (or R / P) again when done, which saves it. Zooming with the wheel still works while editing.
- **From Birel Plaka...** takes both from one of Birel Plaka's cameras: choose its `camera-settings.ini` (next to `Birel Plaka.exe`, or in `profiles\<name>\`), then the camera. Birel Plaka keeps them as fractions of the picture, so they fit as long as the video shows the same view. The file is only read.
- Both are saved with the video, in Birel Plaka's own format (`Roi=` / `Perspective=` values), so they can be copied back. The plates read remember the area they were read with; when it has changed since, the editor says so: scan the video again with **Read plates again** ticked.

How it reads: each part is decoded by FFmpeg at 10 pictures a second (at most 1920 wide). In every picture the detection area (or the whole picture) is stretched to the detector's input; each plate box is cropped and read by the OCR; the plates are followed from picture to picture (with the road perspective when there is one) and the reads are grouped into cars and decided the way Birel Plaka does it, with its factory settings (detector threshold 0.50, at least 5 reads, 72 % agreement, reads touching the edge of the detection area counted as cut off). On 4 cores, reading takes about half as long as the playing parts last. The models come with the scanner (`models\` next to the exe). The results are saved in `<video file>.mockrtsp.plates.json`.

## Ground truth for ALPR testing

**Ground-truth plates...** in the scanner's editor lists the licence plates visible in the video, and when:
- **time**: seconds (83.4) or minutes and seconds (1:23.4)
- **plate**: the plate text
- **region** (optional): where the plate is, as x, y, width and height fractions (0 to 1) of the video's picture

**Add at playhead** puts the player's current moment into a new row. **Import CSV...** and **Export CSV...** use a simple `time,plate,x,y,width,height` file. The plates are saved with the video in its `.mockrtsp.json`, and shown on both apps' timelines as yellow flags with the plate text. A plate inside a gap is shown grey: it is never streamed, so it is never expected.

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

Projects are `.mockrtsp-project` files. Videos inside the project's folder are saved relative to it, so the folder can be moved or copied to another computer. Each video's own settings, gaps, labels and motion data are in the files next to the video, so they come along too. A video that is missing when the project opens is left out and logged. Closing the app, or opening another project, asks first if the setup has changed since it was saved.

### Playlists

**Playlist...** on a camera picks the videos it plays, in order: add from the folder or any other file, remove, move up and down. After the last video the first starts again, unless looping is off in the settings, in which case the camera holds the last picture until you press Play. Viewers stay connected from one video to the next.

The camera's picture size, frame rate and quality come from the first video's settings. Videos of another size are scaled to it, with black bars when their shape differs. Each video keeps its own gaps. Changing the first video restarts the camera only if that changes what it sends.

### Settings: defaults, and per video

- **Settings (defaults and server)...** holds the defaults used by every video without settings of its own, plus the server settings. (How gaps are found is set in the scanner: per video in its editor, and for new videos under its list.)
- **Video settings...** on a camera gives the video it is playing its own settings: resolution, frame rate, bitrate, keyframes and looping. **Use defaults** in that dialog goes back to the defaults. Cameras that play the video first restart with the new picture settings, keeping position, pause and speed.

### Saved next to each video

Everything about a video is kept in a file next to it, `<video file>.mockrtsp.json` (for example `parking.mp4.mockrtsp.json`), saved whenever it changes and loaded whenever the video is used again, on any computer the files are copied to:

- its own stream settings, if it has any (written by the RTSP app),
- and everything else, written by the scanner:
- its gaps (skip ranges),
- its motion data: how much of the picture moves, 5 times a second, and the gap settings the gaps were picked with,
- its ground-truth plates,
- its vehicle labels: the vehicles in its playing parts (time, coming or going, plate), and the parts checked as having none,
- where its plates are read: the detection area and road perspective (see [Reading plates with Birel Plaka](#reading-plates-with-birel-plaka)).

The plates read with Birel Plaka are kept in their own file, `<video file>.mockrtsp.plates.json`, since they hold pictures. Deleting it only loses the reading; the labels stay.

The preview pictures for the timeline are kept next to it too, in `<video file>.mockrtsp.previews`: small JPEG pictures (a few KB each) with a table of where each one is, so they load at once and any moment can be shown straight away. They are made again if the video file changes. Deleting the file is safe; it is made again the next time.

Both apps can run at once: each writes only its own part of the file, under a lock the two share, and the RTSP app reads the file again when the scanner has changed it. A damaged file is reported and never overwritten. Because the motion data is kept, the scanner picks gaps again for new settings straight away, without scanning again. Files from older versions (`<video file>.skips.json`, motion settings kept inside the stream settings) are read and converted on the first save.

Gaps are cut cleanly: viewers see a jump, never a skipped frame. A video where nothing moves enough has no gaps and plays in full; one with movement throughout too.

### How a camera works

Each camera runs two FFmpeg processes with the app in between:

- a **decoder** reads the video file from the current position and hands raw frames to the app (seeking, and going on to the next video of a playlist, restart only this one),
- the app sends exactly one frame per frame interval; paused, it sends the last frame again, and at 2x it takes two source frames per frame,
- an **encoder** turns the frames into H.264 and publishes them to MediaMTX. It runs for as long as the camera is on, which is why viewers stay connected through pause and seek.

Preview frames for the timeline are made in the background by one more FFmpeg pass the first time a video is used (by either app), about one per second (at most 600 for long videos), and saved next to the video. Cameras playing the same file share them.

The stream is always H.264, at the source's size and frame rate unless you set others. A keyframe is sent every second (unless you set an interval), so a new viewer shows a picture within about a second.

Changing settings disconnects anyone watching, just as a real camera does when its resolution changes. Your own viewer has to reconnect; VLC windows opened from the app are reopened for you.

Settings are saved in `%APPDATA%\MockRtsp\options.json` (including any passwords, as plain text) and loaded at startup. Server settings (transports, timeouts, logins, log level) apply after you stop and start the server. Logins can only use letters, digits and `! $ ( ) * + . ; < = > [ ] ^ _ - @ #`.

The last 10 video folders are remembered in the dropdown next to **Video folder**, together with the other settings.

MediaMTX ends with the app, however the app ends (closed, crashed, or stopped from the debugger). When the server starts, any `mediamtx.exe` still left over from an earlier run of Mock RTSP (from this program folder or any other Mock RTSP build) is stopped first; other MediaMTX installations are left alone. If the port is still taken, the message names the MediaMTX processes running.

The scanner keeps its folder, recent folders, the gap settings for new videos and the two "again" ticks in `%APPDATA%\MockRtsp\scanner.json`.

Errors are shown in a dialog and in the log. Unexpected errors (from both apps) are also written to `%LOCALAPPDATA%\MockRtsp\errors.log`.

## Notes

- Check the stream in VLC (Media > Open Network Stream) before testing your app.
- The stream is always re-encoded, so any source FFmpeg can read works; there are no timestamp jumps at loop points or between playlist videos, because the encoder's clock never restarts.
- RTP and RTCP ports are derived from the server port (+1000 and +1001), so the port range is 1025 to 64534.

## For developers: how the code works

### Big picture

One solution (`MockRtsp.sln`), three projects, all in namespace `MockRtsp`:

```
Core\     MockRtsp.Core.dll: what both apps share
            VideoSidecar (<video>.mockrtsp.json, each app writes only its keys, under a shared lock),
            SkipRanges, MotionProfile, MotionSettings, VideoSettings, PlateMarks, VehicleLabels, ReadArea,
            VideoInfo, ThumbnailStrip, Timeline, Toolchain, ErrorReporter, and BirelPlaka\ (copied files)
MockRtsp\ MockRtsp.exe: the cameras; shows what the scanner found
Scanner\  MockRtspScanner.exe: finds gaps, plates and directions; edits labels
```

The RTSP app:

```
MainForm  (window, owns everything below; implements ICameraHost for the panels)
 ├─ MediaMtxServer          one mediamtx.exe process, config written to a temp folder
 ├─ VideoFile (per file)    shared by every camera that plays the file; reads <video>.mockrtsp.json and
 │    │                     reads it again when the scanner saved it (Poll, about once a second)
 │    └─ ThumbnailStrip      background FFmpeg pass -> JPEG previews, cached in <video>.mockrtsp.previews
 ├─ CameraEntry (per camera) stream path + playlist of VideoFiles + MockCamera + VLC process
 │    ├─ MockCamera          decoder FFmpeg -> pump thread -> encoder FFmpeg -> MediaMTX
 │    └─ CameraPanel         the camera's controls and its Timeline (EditSkips off: show only)
 ├─ GroundTruthLog          ground-truth-YYYY-MM-DD.csv: one line per plate streamed (from MockCamera.PlateStreamed)
 ├─ LiveStats               bitrates and viewers from MediaMtxServer.GetPathStatsAsync, polled every second
 ├─ OptionsStore            %APPDATA%\MockRtsp\options.json (defaults, server, remembered folders)
 └─ ProjectFile             *.mockrtsp-project: the whole setup
ScoreDialog + PlateScorer (ALPR results vs the log)
```

The scanner:

```
ScannerForm  (window: folder, video list, queue buttons, log)
 ├─ ScanVideo (per opened video)  everything the scanner keeps about it; every change saved at once
 │    │                           (VideoSidecar.SaveScan, PlateScanFile); raises Changed(VideoChange)
 │    └─ ThumbnailStrip
 ├─ ScanQueue                     one video after another: MotionScan (live graph via SoFar), then
 │    └─ PlateScanner              PlateReader (ONNX) + Birel Plaka's TravelEstimator and ReadGroups, part by
 │                                part; each part's cars go to ScanVideo.AddReadPart on the UI thread (prefill)
 ├─ VideoEditor (UserControl)     the selected video: Timeline (EditSkips on), gap settings, PartPlayer,
 │                                read area, parts, vehicles, cars, readings; edits go into the ScanVideo
 └─ ScannerOptions                %APPDATA%\MockRtsp\scanner.json
PlatesDialog (ground-truth plates of a video)
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

- `MotionScan` (scanner) decodes the file at 5 fps, 160x90 grey (area scaling smooths noise). For each sample it counts the share of pixels that changed by more than 18 levels, and stores it as a byte (percent x 10). The result is a `MotionProfile`; `SoFar` gives what is measured so far, for the live graph.
- `MotionProfile.StillParts(threshold, minStill, padding)` (through `MotionSettings.StillParts`) turns the profile into gaps: still runs longer than `minStill`, shrunk by `padding` on each side. The profile and the settings used are saved, so the scanner re-derives the gaps instantly for new settings (`ScanVideo.PickGaps`), with no rescan; gaps edited by hand are detected (`ScanVideo.GapsFromMotion`) and only replaced if the user agrees. A new motion scan keeps hand-edited gaps (`ScanVideo.SetMotion`).
- `SkipRanges.Normalize` sorts, clamps and merges ranges. `SkipRanges.Resolve` decides, for a position, whether to play, jump past a range, or report that the rest of the video is skipped (then the camera moves to the next playlist video, or wraps, or holds the last frame when looping is off). If the skips would leave less than 0.5 s to play, they are ignored.

### Files the apps write

| File | Where | Written by | Contents |
|---|---|---|---|
| `options.json` | `%APPDATA%\MockRtsp\` | OptionsStore (RTSP app) | Defaults for videos, server settings (logins in plain text), last folder, recent folders, last project, reopen-at-startup flag |
| `scanner.json` | `%APPDATA%\MockRtsp\` | ScannerOptions (scanner) | Folder, recent folders, gap settings for new videos (gapThresholdPercent, minGapSeconds, keepAroundSeconds), GapsAgain, PlatesAgain |
| `errors.log` | `%LOCALAPPDATA%\MockRtsp\` | ErrorReporter | Unexpected exceptions with context |
| `<video>.mockrtsp.json` | next to each video | VideoSidecar (both apps, each its own keys) | `settings` (the RTSP app: the video's own stream settings, or none). The scanner: `skips` (the gaps), `motion` (interval and levels, base64), `motionSettings` (thresholdPercent, minStillSeconds, paddingSeconds: what picked the gaps), `plates` (ground truth: time, plate, optional region), `vehicles` (time, `coming`/`going`, optional plate), `emptyParts` (moments in parts checked as having no vehicles), `detectionArea` (`"x,y,width,height"`) and `roadPerspective` (`"x,y;x,y;x,y;x,y"`, far-left, far-right, near-right, near-left), fractions of the picture in Birel Plaka's `Roi=` / `Perspective=` format, left out for the whole picture / no perspective. Every save reads the file, replaces the writer's keys and writes it back atomically, under a named mutex per video (`MockRtsp.Sidecar.<hash of the path>`), so the two apps never lose each other's changes. A damaged file is never overwritten (InvalidDataException). Deleted when there is nothing to keep. Older files: `<video>.skips.json` and motion settings inside `settings` are read and converted on the first save |
| `<video>.mockrtsp.plates.json` | next to each video | PlateScanFile | The last plate reading (Read plates in every part): when, the model files, the interval, the detection area and road perspective it used (same format as above), the parts read (start, end, pictures) and the cars: start, end, time, decided, plate, reason, movement (Birel Plaka's estimate), likelyMovement (the guess when that is neither coming nor going, or none), reads, votes, agreement, readings (text, count, best OCR, best detection) and the plate and car pictures as base64 JPEGs; `toCheck`: moments in parts prefilled from it and not looked at yet. Written only by the scanner: when a reading starts, after each part, and when the parts to check change. Atomic save. A damaged file is reported in the log and replaced by the next reading |
| `<video>.mockrtsp.previews` | next to each video | ThumbnailStrip | Binary: `"MRTPREV1"`, video size and last-write ticks, width, height, interval, count, one int32 length per picture, then the JPEGs. Rebuilt if the video changed or the file is damaged |
| `*.mockrtsp-project` | where the user saves it | ProjectFile | JSON: port, StartServer, Settings (StreamOptions without the remembered fields), VideoFolder, Cameras[] (StreamPath, Videos, Running, Paused, Speed, Video, PositionSeconds). Paths are relative to the project folder when inside it |
| `ground-truth-YYYY-MM-DD.csv` | `Documents\MockRtsp\ground-truth\` or the folder in Settings > Server | GroundTruthLog | One line per plate streamed. Columns: utc_time, local_time, camera, substream, video, video_time, plate, x, y, width, height, stream_x, stream_y, stream_width, stream_height |
| MediaMTX config | temp folder | MediaMtxServer | `mediamtx.yml` with RTSP only, the port, RTP/RTCP at port+1000/+1001, the control API on `127.0.0.1:<free port>` (for live stats, open to this computer only), and `authInternalUsers` for logins. Deleted on stop |

### Conventions (keep these when changing the code)

- **Designer-compatible forms**: every form and the UserControl has a `.Designer.cs` whose `InitializeComponent` uses no loops, no local variables, no string concatenation and no interpolation. Only `new`, property sets, `Controls.Add/AddRange` and event wiring are used, so the Visual Studio designer can open it. Each file starts with `#nullable enable`. Use `AutoScaleDimensions = new SizeF(7F, 15F)` with Segoe UI 9.
- **Layout**: add the Fill-docked control before edge-docked ones. Avoid an AutoSize GroupBox around a Fill-docked AutoSize grid (it grows without limit). Set SplitContainer `Panel1MinSize`/`Panel2MinSize` in code after the form has its size (`MainForm.SetSplitters`): Windows throws if they don't fit the default 150x100 while the designer code runs.
- **NumericUpDown**: typed text is committed explicitly before reading values (`CommitTyped` in OptionsDialog), or a value typed without leaving the box is lost.
- **UI thread**: all UI, `VideoFile` and `ScanVideo` work runs on the UI thread. Background work (FFmpeg passes, the pump, the plate reading) never touches controls or a ScanVideo: the RTSP app's 250 ms `_timer` polls (`VideoFile.Poll`, `CameraPanel.UpdateView`); the scanner's ScanQueue posts results with `BeginInvoke`. `MainForm.Log` / `ScannerForm.Log` marshal with `BeginInvoke`.
- **Who writes what**: the RTSP app writes only a video's stream settings; everything found or labelled is the scanner's (`VideoSidecar.SaveStreamSettings` / `SaveScan`). Keep it that way when adding data: a key belongs to one app.
- **Errors**: UI actions run through `Safe`/`SafeAsync` (`ICameraHost.Run/RunAsync` for panels). These log, write `errors.log` and show a message box. Background code reports with `ErrorReporter.Report` and never throws out of threads.
- **Wording**: user-facing text is plain and short. Log lines for a camera start with `'<stream path>': `.
- **Git**: work on `main` unless a version branch is asked for (v2 and v3 were). Commit messages describe the change for a user.

### Building and testing

- Build on Windows with the .NET 8 SDK: open `MockRtsp.sln` (set either exe as the startup project, or both as multiple startup projects), or run `dotnet build`. Put `mediamtx\` and `ffmpeg\` in the solution folder; the build copies them next to each exe (`models\` comes with the scanner project).
- There is no test project in the repo. Changes so far were checked like this:
  - The engine (MockCamera, MediaMtxServer, VideoFile, MotionScan, ThumbnailStrip, ProjectFile) was run on Linux .NET 8 against real FFmpeg 6.1 and MediaMTX 1.15.1. A viewer recorded the stream to confirm one continuous connection across seeks and playlist changes, and frames were grabbed to check letterboxing.
  - The plate reading (PlateReader, PlateScanner with Birel Plaka's copied logic) was run on Linux .NET 8 with the real models on test videos made from plate photos.
  - Console tests of the labels logic, the read area, Birel Plaka's camera-settings.ini, and the file next to each video: both apps' saves, from two threads at once, older files, damaged files. The RTSP app's reload (VideoFile.Poll) was run under Mono.
  - Every project is compiled against the .NET 8 Windows Forms reference assemblies on Linux (0 errors, 0 warnings).
  - The forms were driven under Mono WinForms (net48, with small test-only patches for newer APIs) with screenshots from Xvfb: the scanner's whole flow with a stand-in plate reader (live graph, gaps, parts read one by one and prefilled, an edit while scanning, closing).
  - Mono does not enforce some Windows rules (the SplitContainer crash above was missed that way), so a run on Windows is the final check.

## File reference

Every source file, what it is for and what it does. All code is in namespace `MockRtsp` (except the copies in `Core\BirelPlaka\`, which keep Birel Plaka's `Birel_Plaka`); every project is net8.0-windows, WinForms, nullable on, implicit usings.

### Core\ (MockRtsp.Core.dll, used by both apps)

- **`ErrorReporter.cs`**: the one place errors are recorded. `Report(ex, context, showDialog)` appends to `%LOCALAPPDATA%\MockRtsp\errors.log` (both apps) and optionally shows a dialog. Never throws, because it is called from catch blocks.
- **`Toolchain.cs`**: finds bundled tools. `Find(folder, exe)` looks in `<exe dir>\<folder>\` and then any subfolder of it (so `ffmpeg\bin\ffmpeg.exe` is found). Constants `MediaMtxFolder = "mediamtx"`, `FfmpegFolder = "ffmpeg"`. Nothing is read from PATH.
- **`VideoInfo.cs`**: `record VideoInfo(Width, Height, FrameRate, DurationSeconds)`. `Probe` runs `ffmpeg -i` and parses its stderr (`Parse`); an unplayable file throws InvalidOperationException with FFmpeg's last lines. `IsVideoFile(path)` and `VideoFileTypes`: the video extensions both apps list.
- **`SkipRanges.cs`**: `record SkipRange(Start, End)` with an exclusive end, and the static rules: `Normalize`, `LeavesSomethingToPlay`, `Resolve` (Play / JumpTo / EndReached). Used by the camera, the timeline and the sidecar.
- **`MotionProfile.cs`**: `record MotionProfile(IntervalSeconds, byte[] Levels)` with levels as percent x 10. `PercentAt(i)` and `StillParts(threshold, minStill, padding, duration)`.
- **`MotionSettings.cs`**: `record MotionSettings(ThresholdPercent, MinStillSeconds, PaddingSeconds)`: how gaps are picked from motion data. `StillParts(motion, duration)`, `Validate()` (0.1 to 25 %, 1 to 3600 s, 0 to 60 s), `Describe()`, and `Legacy` (0.3 %, 3 s, 1 s: the old defaults, for older files that have motion data but no settings).
- **`VideoSettings.cs`**: a video's own stream settings in the RTSP app (loop, size, rate, bitrate, keyframes, B-frames). `Clone`, `SameStream` (does a camera need a restart?). The link to StreamOptions is in `MockRtsp\VideoSettingsOptions.cs`.
- **`VideoSidecar.cs`**: reads and writes `<video>.mockrtsp.json` for both apps (see Files the apps write).
  - `Load(video, duration)` returns `SavedVideo(Skips, Motion, MotionSettings, Settings, FileExists, FromOldFile, Plates, Vehicles, EmptyParts, ReadArea)`; InvalidDataException on a damaged file (bad JSON, a direction, a read area or motion settings out of range).
  - `SaveStreamSettings(video, settings)` (the RTSP app) and `SaveScan(video, ScanData)` (the scanner) go through `Update`: a named mutex per video (`LockName`, from a hash of the full path) held while the file is read, the caller's keys replaced, older motion settings moved to `motionSettings`, and the file written beside and swapped in (or deleted when empty); the old `.skips.json` is removed.
- **`ThumbnailStrip.cs`**: preview pictures for the hover popup.
  - Pictures are taken about once per second (at most 600; keyframes only for videos over 5 minutes), at 108 px high, as JPEG via `-c:v mjpeg -f image2pipe`.
  - `ReadJpeg` splits the stream by parsing JPEG markers, not by searching for FFD9.
  - `Start` first tries `TryLoad` from `<video>.mockrtsp.previews`. The file is checked against the video's size and time and the expected dimensions and interval; otherwise the pictures are made again and saved with `TrySave` (temp file, then move; quietly skipped on a read-only folder).
  - `GetJpeg(i)` gives random access to any picture.
- **`PlateMarks.cs`**: ground-truth plates.
  - `record PlateMark(Time, Plate, X?, Y?, Width?, Height?)` with the region as fractions of the video's picture; `Normalize(plate text)` keeps letters and digits in upper case for comparing.
  - `PlateMarks`: `Normalize(list)`, `Check` (time inside the video, all-or-none region inside the picture), `FormatTime`/`TryParseTime` (83.4 or 1:23.4), and `ToCsv`/`FromCsv` (`time,plate,x,y,width,height`; errors name the line).
  - `Csv`: escaping and a reader that handles quoted fields.
- **`VehicleLabels.cs`**: vehicle labels per playing part.
  - `enum VehicleDirection` (Coming, Going); `record VehicleMark(Time, Direction, Plate)`; `record PlayingPart(Number, Start, End)` with `Contains` (end exclusive, except the end of the video); `record PartLabels(Part, Vehicles, MarkedEmpty)` with `IsLabelled`; `record LabelSummary(Parts, Labelled, Vehicles, Coming, Going, Outside)` with `ToLabel` and `Describe`.
  - `VehicleLabels`: `PlayingParts(skips, duration)` (the video minus its skips; the whole video when there are none, or when they would leave nothing to play), `Group`, `CountOutside`, `Summarize`, `Describe(part)`, `Normalize`, `NormalizeEmpty`, `TryParseDirection` and `ToCsv`.
- **`ReadArea.cs`**: where plates are read in a video. `ReadArea(Detection, Perspective)`: the detection area (a RectangleF of fractions, null = whole picture) and road perspective (four corners, null = none), checked when made (inside the picture; a convex shape by `RoadPerspective.IsValidShape`). Text in Birel Plaka's RoiSettings format (`FormatDetection`, `ParseDetection`, `FormatPerspective`, `ParsePerspective`, `Parse`); `DetectionPixels` and `PerspectivePixels` turn them into frame pixels like Birel Plaka's StreamTile (`GetFrameRoi`, `GetFramePerspective`); `SameAs` compares with saved texts; `Describe` for the dialog. `BirelCameraSettings.Read` reads the `[Camera_N]` sections (Name, Url, Roi, Perspective) of Birel Plaka's `camera-settings.ini` (read only); `BirelCamera.ToReadArea` clips a Roi reaching outside the picture as Birel Plaka does.
- **`Timeline.cs`**: custom control for one video, in both apps.
  - Ruler; a bar with skipped parts as dark red areas with draggable white edges.
  - The motion graph: faint below the threshold, solid green above, a red hatched grid where skipped. It is scaled to the video's busiest moment, with a dashed threshold line.
  - Plate flags (`SetPlates`): yellow with the plate text where it fits, grey inside a skipped part; the hover caption lists plates near the mouse.
  - The labels strip under the play strip (`SetLabels`): a block per playing part with a chip per vehicle (green coming, orange going), counts or dots when they do not fit, "none", or an orange hatch with "?" to label. The ruler's top right shows the parts and how many are labelled when no scan or preview progress is shown. `LabelPartRequested` is raised by a double-click on the strip and by the right-click menu's "Label part N...".
  - A green/red play strip; the playhead, the hover line, and an orange pending-skip line.
  - A horizontal scrollbar with zoom (Ctrl + wheel, ZoomIn/Out/ToFit) and auto-follow of the playhead.
  - Hover shows `PreviewPopup` with the JPEG decoded on demand (at most 150 kept).
  - Events `SeekRequested`, `SkipRangesChanged` and `LabelPartRequested`. Mouse: click/drag to seek, drag edges, Shift + drag to create, right-click menu.
  - `EditSkips` (default on): off in the RTSP app, where the gaps are only shown (no edges drawn or dragged, no Shift + drag, no gap items in the menu; the label item only when someone listens to `LabelPartRequested`).
- **`BirelPlaka\`**: files copied unchanged from Birel Plaka (commit a4e0815): `OcrModelConfig.cs`, `OcrResult.cs`, `PlateRead.cs`, `PlateConsensus.cs`, `ReadGroups.cs`, `TravelEstimator.cs`, `RoadPerspective.cs`. `AppConfigShim.cs` supplies the `AppConfig.Travel` settings TravelEstimator reads. `SOURCE.txt` says where they came from. Do not edit them; copy them again to follow Birel Plaka. They are internal; `InternalsVisibleTo` lets the scanner use them.
- **`MockRtsp.Core.csproj`**: the class library; `InternalsVisibleTo` MockRtspScanner.

### MockRtsp\ (MockRtsp.exe, the RTSP app)

- **`Program.cs`**: entry point. Installs last-resort exception handlers (UI thread, AppDomain, unobserved tasks) that report through ErrorReporter, then runs `MainForm`.
- **`ChildProcesses.cs`**: `EndWithThisApp(process)` puts a process in a Windows job object with "kill on close", so it ends with the app however the app ends. MediaMtxServer uses it for MediaMTX. Does nothing off Windows; returns why when it cannot (then logged).
- **`MediaMtxServer.cs`**: starts and owns one `mediamtx.exe`.
  - `StartAsync(port, exe, ..., log, options)` does the following:
    - checks the port range (RTP/RTCP need port+1000/+1001) and that the TCP/UDP ports are free
    - stops leftover mediamtx processes of Mock RTSP (`StopStaleInstances`: the same exe path, or `IsMockRtspCopy`: a `mediamtx` folder next to a MockRtsp program, so leftovers of another build folder are found too)
    - writes a minimal YAML config to a temp folder (`BuildConfig`: RTSP only, transports, timeouts, `authInternalUsers` for publisher and viewer logins, or `any` with no password)
    - starts it, ties it to the app (`ChildProcesses.EndWithThisApp`), and waits until the port accepts connections
  - `IsPathReadyAsync` sends a real RTSP DESCRIBE and expects 200 OK: "a viewer could play this now".
  - `GetPathStatsAsync` reads `/v3/paths/list` from MediaMTX's API (on `ApiPort`, a free local port chosen at start) and returns `PathStats(Ready, BytesReceived, Viewers)` per path, or null. Never throws.
  - `BuildUrl` puts logins in the URL. `Dispose` kills the process tree and deletes the temp folder.
- **`OptionsStore.cs`**: loads and saves `StreamOptions` as JSON in `%APPDATA%\MockRtsp\options.json`. Never throws; invalid files fall back to defaults.
- **`VlcLauncher.cs`**: finds `vlc.exe` (a bundled `vlc\` folder, then Program Files), opens a stream in its own window with low network caching and RTSP over TCP, and closes it again. MainForm reopens VLC after a camera restart because VLC does not reconnect by itself.
- **`StreamOptions.cs`**: every setting.
  - Video: loop, bitrate, keyframe interval, size, frame rate, B-frames, publish transport.
  - Substream: on/off, width, frame rate, bitrate. `SubstreamPathFor(path)` (101 → 102), `SubstreamFormat(main)`, `SameSubstream`.
  - Ground-truth log folder.
  - FFmpeg and MediaMTX log levels; server transports, timeouts and logins.
  - Remembered between runs: video folder, recent folders, last project, reopen flag.
  - Also: `Validate()` (all rules, including the characters MediaMTX accepts in logins), `ServerSettingsEqual`, `OutputFormat(VideoInfo)`, `DecoderArguments`, `ScaleFilter` (scale to fit, pad, setsar, fps) and `EncoderArguments(format, url, substreamUrl)` (one output, or two through a split filter).
  - `VideoFormat` record struct: width, height, rate and `FrameBytes` for yuv420p.
- **`VideoSettingsOptions.cs`**: `From(StreamOptions)` (a video's settings out of the defaults), and the extensions `ApplyTo(StreamOptions)` and `Validate()` for VideoSettings.
- **`VideoFile.cs`**: one open video, shared by all cameras that play it: path, `Info`, `Item` (as a PlaylistItem), and what it read from the file next to it: `Skips` (the gaps), `Motion`, `MotionSettings` (for the threshold line), `Plates`, `Vehicles`, `EmptyParts`, `LabelSummary`, own stream settings; `Previews`.
  - `Load()` reads the file and starts the previews. `Poll()` (from the timer) checks the file's time about once a second and reads it again when the scanner changed it; a file being replaced is tried again, a damaged one keeps what was read before (said once).
  - `SetOwnSettings` is the only write (`VideoSidecar.SaveStreamSettings`); its own save is not taken for a change.
  - Events: `Changed` (MainForm pushes gaps and plates to cameras) and `Message` (log lines).
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
- **`MainForm.cs` / `MainForm.Designer.cs`**: the window.
  - Layout:
    - top row: port, Start/Stop server, Settings, Open/Save/Save as project, "Reopen the last project at startup", Score ALPR results
    - folder row: the folder combo with recent folders, Browse, Refresh
    - left: the video list (ListView: name, length, picture, gaps and labels, cameras; multi-select), the stream path box and Add camera
    - right: `_cameras` FlowLayoutPanel with one CameraPanel per camera
    - bottom: the log; then a status bar
  - Folder: `LoadFolder` lists video extensions, then `ProbeFolderAsync` probes two at a time, hides unplayable files and fills in the rows (`_playableFiles`).
  - Cameras:
    - `AddCamera` adds the selected videos as one camera (a playlist when several are selected) through `CreateCamera`.
    - `OpenVideo` / `ReleaseVideoIfUnused` share VideoFiles by path.
    - `OnVideoChanged` pushes a video's gaps and plates to every camera that plays it (after the scanner saved them).
    - `CamerasOf(video)` returns cameras with the video anywhere in their playlist; `StreamCamerasOf(video)` returns those that play it first.
    - `NextStreamPath` returns the next free `Streaming/Channels/{n}01`.
  - Settings:
    - `EditSettingsAsync`: defaults and server. Restarts cameras whose first video uses the defaults and whose stream changed, or every camera if the publish settings changed.
    - `EditVideoSettingsAsync(entry, video)`: one video's own settings; choosing the same as the defaults clears them.
    - `RestartAsync`/`ApplySettingsToCameraAsync` restart, wait for the stream to be live, and reopen VLC.
  - Playlists: `EditPlaylistAsync` opens the PlaylistDialog, opens and releases videos, and restarts only if the first video changes what is sent.
  - Ground truth: `OnPlateStreamed` writes each `PlateStreamed` to `_groundTruth` (a GroundTruthLog for the folder in the settings) and the log. `OnScoreClick` opens the ScoreDialog on today's (or the latest) log.
  - Live stats: `PollStats`/`PollStatsAsync` run from the timer about once a second; `StatsFor(entry)` serves the panels. `CheckStreamPath` and `NextStreamPath` keep substream paths free of clashes.
  - Projects: `CaptureProject`, `SaveProject(path or null = ask)`, `OpenProjectAsync` (stops the server, applies settings, port and folder, starts the server, `RestoreCamera` for each), `ConfirmDiscardChanges` (Yes/No/Cancel when the fingerprint differs), `OnShown` (reopens the last project), `UpdateTitle`.
  - Also: `OnTimerTick` (every 250 ms: server health, `VideoFile.Poll` for the scanner's changes, panel refresh, layout), `LayoutPanels`, `SetSplitters`/`FitSplitter`, `Safe`/`SafeAsync`/`HandleError`, `Log`.
- **`CameraEntry.cs`**: one camera's state for the UI.
  - `StreamPath`, `Playlist` (VideoFiles), `Video` (the one playing now), `First` (sets the format), `Camera`, `Vlc`.
  - `Plays(video)`; `SetPlaylist` updates the camera and its gaps.
- **`CameraPanel.cs` / `CameraPanel.Designer.cs`**: the UserControl for one camera, plus `ICameraHost`, the interface MainForm implements for it.
  - Row 1: state badge (LIVE/PAUSED/OFF), stream path, details (videos, format, substream format, settings, skips, plates), Start/Stop, Open in VLC, Playlist..., Video settings..., outage seconds with Simulate, Remove.
  - Row 2: video picker and Next (only with several videos), Rewind, « 10 s, Pause/Play, 10 s », time, Speed, zoom -, +, Fit, and the live stats.
  - Row 3: the Timeline, with `EditSkips` off: it shows the scanner's gaps, motion, plates and labels.
  - `UpdateView()` (from the timer) refreshes everything, and switches the timeline to the video now playing. `_shown` records which video the timeline shows (Video settings go to it, even if the playlist moved on). `HeightFor(width)` gives the height with wrapped rows.
- **`OptionsDialog.cs` / `OptionsDialog.Designer.cs`**: Settings dialog with the tabs "Defaults for videos" (looping, encoding, publishing, substream) and "Server" (MediaMTX transports, timeouts, log level, logins, ground-truth log folder).
  - The same dialog edits one video (`new OptionsDialog(current, videoName, defaults)`): the server tab and server-only rows are hidden, and Reset becomes "Use defaults".
  - `Result` holds the edited copy; `CommitTyped` fixes NumericUpDown typing.
- **`PlaylistDialog.cs` / `PlaylistDialog.Designer.cs`**: two lists (videos in the folder, and the playlist) with Add, Remove, Move up/down and Other file.... `Files` returns the chosen order.
- **`ScoreDialog.cs` / `ScoreDialog.Designer.cs`**: picks the ground-truth log and the results file, sets the before/after window, shows `PlateScorer.Summary`, and saves the CSV report.
- **`MainForm.resx`, `OptionsDialog.resx`**: resource files added by the Visual Studio designer. Nothing in them is used by the code.
- **`MockRtsp.csproj`**: net8.0-windows WinExe (MockRtsp.exe) with WinForms and PerMonitorV2 DPI, referencing Core. It copies `..\mediamtx\` and `..\ffmpeg\` (the solution folder) to the output when they exist.

### Scanner\ (MockRtspScanner.exe)

- **`Program.cs`**: entry point. The same last-resort exception handlers as the RTSP app, then a named mutex (`MockRtspScanner.SingleInstance`) so only one scanner runs (two would both save the same videos), then `ScannerForm`.
- **`ScannerForm.cs` / `ScannerForm.Designer.cs`**: the window. Folder row (combo with recent folders, Browse, Refresh); left: the video list (name, length, gaps, plates read, labelled, scan state), Scan selected, Scan all not scanned, Stop, Take out of the queue, the "again" ticks and the gap settings for new videos; right: the VideoEditor; bottom: the log.
  - `LoadFolder` lists the video files, `ProbeFolderAsync` reads each one's details and files in the background (`FileState`: info, saved data, cars read). `OpenVideo` opens a ScanVideo on demand and keeps it while the app runs; `UpdateRow` fills a row from the open video or from its files, with its queue status.
  - `Queue` adds videos to the ScanQueue; `OnQueueChanged` refreshes the rows and the editor's title. `OnOptionChanged` saves the ticks and gap settings. Closing asks while a scan runs, then closes the editor (keeping its edits), stops the queue and saves the options.
- **`ScannerOptions.cs`**: `%APPDATA%\MockRtsp\scanner.json`: folder, recent folders (`Remember`), gap settings for new videos (`GapDefaults`), GapsAgain, PlatesAgain. Never throws; out-of-range values fall back to the defaults (reported).
- **`ScanVideo.cs`**: one opened video and everything the scanner keeps about it; every change is saved at once (`VideoSidecar.SaveScan`, `PlateScanFile.Save`) and raised as `Changed(VideoChange)` (Gaps, Motion, Labels, Plates, Area, Reading).
  - `Open(ffmpeg, path)` probes, reads both files (`LoadError` when the main file is damaged: then nothing is saved or scanned), starts the previews.
  - Gaps: `SetSkips`, `ShowLiveMotion`/`ClearLiveMotion` (a running scan's graph), `SetMotion` (a finished scan: replaces the gaps only when they were not changed by hand, `GapsFromMotion`), `PickGaps(settings)`.
  - Labels: `SetPartLabels(part, vehicles, markedEmpty)`, `DeleteLabelsInGaps`, `SetPlates`, `SetReadArea`; parts to check (`IsToCheck`, `ToCheckCount`, `MarkChecked`; kept as moments in the plates file).
  - Reading: `BeginReading`, `AddReadPart(part, cars)` (adds the cars and prefills the part through `PrefillPart`: cars with at least [Travel] MinReads reads, all with a most likely direction, into a part without labels, which is then to check), `Prefill()` (every part, for an earlier reading), `EndReading`.
- **`ScanQueue.cs`**: `ScanJob` (video, GapsAgain, PlatesAgain, Step, Progress, Status) and `ScanQueue`: `Add`, `Remove`, `Stop`, `JobOf`, `IsBusy`; `RunAll` takes the jobs in turn on the UI thread. `RunAsync`: a MotionScan polled every 250 ms (`ShowLiveMotion`), then `SetMotion` with the video's gap settings (or the defaults); then `BeginReading` and `PlateScanner.Scan` on a background task, whose progress and finished parts are posted to the UI thread (`AddReadPart`). The PlateScanner (models loaded once) is kept for the next videos. Errors fail only that video; `Stop` cancels (what was found is kept).
- **`VideoEditor.cs` / `VideoEditor.Designer.cs`**: the UserControl for one ScanVideo (`SetVideo(video, ffmpeg)`), which it shows and edits; the video saves. Rows: the title (name, size, length, scan state from `ShowTitle`), the Timeline (gaps editable: `OnTimelineSkipsChanged` → `SetSkips`; a click selects the part and shows the moment), the gap settings with Pick gaps again, the summary line, then the player with the read-area buttons and the parts list on the left, and on the right the part's vehicles, the cars read and the readings, then the bottom buttons and the status line.
  - Follows the video (`OnVideoChanged`): new gaps rebuild the parts and keep the part at the same moment; motion redraws the graph without touching the gaps (one may be dragged); labels and readings refresh the list and the cars, and reload the part shown when its vehicles changed and it is not being edited (`ReloadCurrentIfChanged`).
  - Edits are committed as they happen (`CommitCurrent(quiet)` after a cell edit, a direction chosen, a row removed, "No vehicles", a car added): rows are read by `TryReadTable` (a time inside the part, a direction) and given to `SetPartLabels`; a row that cannot be read is reported in the status line and saved once fixed. Moving to another part checks it with a message and marks the part left as looked at (`LeavePart`).
  - From the old labels dialog: the cars of the part (`_cars`, `_plateImages`, `_used` by reference, cars at a vehicle's time count as added), the readings, Add as vehicle / Put in the selected vehicle / Add every car (with `MostLikelyDirection`), Prefill unlabelled parts, Previous/Next to label (also stop at parts to check), Delete labels in gaps, Export CSV, Ground-truth plates (PlatesDialog at the player's moment), and the read area (Detection area / Road perspective buttons driving `PartPlayer.SetEditing`, Whole picture, No perspective, From Birel Plaka; saved when editing stops).
  - `Close()` (the app closing): commits, marks the part looked at and lets go of the video, so the grid emptying its rows while the window is taken down is not taken for an edit.
- **`PartPlayer.cs`**: a Control that plays one part of a video file, for labelling. `SetVideo(ffmpeg, file, info)`, `PlayPart(start, end)`, `ShowMoment(t)` (paused), `Position`, `Stop()`. A `Decoder` runs `ffmpeg -ss <start> -i <file> -t <length> -vf scale -pix_fmt bgr24 -f rawvideo pipe:1` at the picture's size (at most 1600 wide, width a multiple of 4 so bitmap rows need no padding) and reads pictures on a background thread into a small BlockingCollection, so FFmpeg waits while paused. A 5 ms WinForms timer shows them at the video's frame rate times the speed; at the end of the part it loops or pauses. Frame back decodes again from one frame earlier. Zoom: `_zoom` and `_focus` pick the source rectangle drawn into the letterboxed picture area. Toolbar buttons are child controls created in code, like the Timeline's scroll bar. Plate reading area: `Area` (a ReadArea, in fractions of the picture) is drawn over the picture (detection area green, road perspective a dashed gold outline). `SetEditing(AreaEdit.Detection / Perspective / None)` (or R / P) lets the mouse edit it like Birel Plaka's RoiPictureBox: grips for corners, edges and inside (`GripAt`), a new area when dragging outside it, the perspective's corners or whole shape (`CornerAt`, only valid shapes accepted), right-click to clear or reset, Esc to undo (Escape is an input key only while editing, so it does not close the dialog). Positions are converted with `ToScreen` / `ToPicture` at the current zoom. `AreaChanged` after each change, `EditingChanged` when editing starts or stops.
- **`PlatesDialog.cs` / `PlatesDialog.Designer.cs`**: a DataGridView of time, plate, x, y, width and height for one video, with Add at playhead (the editor's player), Remove, Import CSV... and Export CSV.... `Plates` returns the checked, sorted list; errors name the row.
- **`PlateReader.cs`**: Birel Plaka's Detector and PlateOCR without OpenCV. Loads the two ONNX models (ONNX Runtime) and checks the OCR model against its plate config like PlateOCR. `Read(rgb, width, height, area)` stretches the detection area of the frame to the detector input (planar RGB 0..1), keeps boxes at or above the threshold (rows of 7: batch, x1, y1, x2, y2, class, score, clamped to the area like Birel Plaka's ROI image, then moved into frame pixels), crops each box and stretches it to the OCR input (NHWC bytes), decodes with `OcrResult.Decode` and drops empty reads. `Resize` is a bilinear resize sampled like OpenCV's INTER_LINEAR, so the models see the same pixels as in Birel Plaka.
- **`PlateScan.cs`**: the plate reading of a video. `PlateScanSettings.BirelPlakaFactory` holds Birel Plaka's factory settings (detection, [Travel], [Consensus] and grouping). `PlateScanner.Scan(video, info, parts, area, progress, partDone, cancel)` decodes each part with FFmpeg (`-ss -t`, `fps=10`, `rgb24` raw frames), runs `PlateReader` on the area's detection area, marks reads touching its left or right edge (`EdgeMarginPixels`), feeds Birel Plaka's `TravelEstimator` (with the area's road perspective in pixels) and `ReadGroups` per part as Birel Plaka does per camera, and turns each group into a `PlateCar` (decision, reason in plain English, movement, the likely movement from `DirectionGuess.Likely`, readings, plate and car JPEG crops). `PlateCar.MostLikelyDirection` is the estimate's direction or else the guess; `DirectionText` shows a guess as "going? (unclear)". `DirectionGuess.Likely(estimate, reads, travel)`: Approaching/Leaving as they are; Unclear by the size change; otherwise the clearer of the size and road changes (each against its [Travel] threshold) when above `MinSizeChange` (3 %) / `MinRoadChange` (0.02), from the estimate when it has enough reads, else from the first and last reads' plate sizes. `PlateScanFile` loads and saves the result as `<video>.mockrtsp.plates.json`. `partDone` gets each part as soon as it is read, with its cars. `PlateScan.CarsIn(part)` gives a part's cars; `PlateScan.ToCheck` the moments of prefilled parts not looked at yet.
- **`MotionScan.cs`**: background FFmpeg pass that measures movement (see Motion and skips above). Exposes `Progress`, `SoFar` (the levels so far, for the live graph), `IsComplete`, `IsCancelled`, `Result` and `Error`; `Dispose` cancels it.
- **`models\`**: Birel Plaka's factory models, copied to the output: `yolo-v9-t-384-license-plates-end2end.onnx` (plate detector), `cct_xs_v2_global.onnx` (OCR, fast-plate-ocr) and `cct_xs_v2_global_plate_config.yaml`.
- **`MockRtspScanner.csproj`**: net8.0-windows WinExe (MockRtspScanner.exe), referencing Core, with `Microsoft.ML.OnnxRuntime`; copies `models\` and `..\ffmpeg\` to the output.

### Other files

- **`MockRtsp.sln`**: the solution with the three projects.
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

**2026-11-21: vehicle labels per playing part**
- Labels... on each camera and the timeline's labels strip: per playing part, the vehicles in it (time, coming or going, optional plate) or "no vehicles"; counts of parts and labelled parts on the timeline, the camera panel and the video list; Next to label, Export CSV. Saved in `.mockrtsp.json`.
- A player in the labels dialog plays the selected part from the file (FFmpeg raw frames), looping, with pause, frame steps, speed and mouse-wheel zoom to read plates; Add vehicle uses its time. Label parts... under the video list labels a video without a camera.
- Checked by compiling against the .NET 8 Windows Forms reference assemblies on Linux (0 errors, 0 warnings), a console test of the labels logic and the sidecar round trip, and the Timeline and LabelsDialog (with the player decoding a real test video through FFmpeg) driven under Mono WinForms (net48) with screenshots from Xvfb. Not yet run on Windows.

**2026-11-21: read plates with Birel Plaka**
- Read plates in every part in the labels dialog: Birel Plaka's detector, OCR and plate consensus over every playing part, the cars read per part with plate pictures, direction and all readings; add them as vehicles or put a reading in a vehicle. Saved in `<video>.mockrtsp.plates.json`. Birel Plaka's logic files and models are copied into `BirelPlaka\` and `models\`; Birel Plaka itself is not changed.
- Checked on Linux: the real models read a test video made from plate photos (12 s of playing parts read in 6.7 s; both cars decided as the right plate from 60 reads, direction right), the results file saved and loaded again, and the dialog driven under Mono WinForms with screenshots. Compiled against the .NET 8 Windows Forms reference assemblies (0 errors). Not yet run on Windows.

**2026-11-21: where plates are read, from Birel Plaka**
- Detection area and road perspective per video, as Birel Plaka has them per camera: drawn on the labels dialog's player (R / P, like Birel Plaka's video box) or taken from a camera in Birel Plaka's `camera-settings.ini`, saved in `.mockrtsp.json` in Birel Plaka's format. Reading plates uses them: only the detection area goes to the detector, reads at its edges count as cut off, and the perspective is used for the direction. The plates file records the area it was read with.
- Checked on Linux: the real models with a test video (whole picture: both cars decided; an area cutting the plate: "may be cut off"; an area beside it: nothing read; with a perspective: used for the direction), a console test of the area texts, checks, pixels, the sidecar round trip and reading Birel Plaka's own factory `camera-settings.ini`, and the dialog under Mono WinForms with screenshots (drawing, editing, From Birel Plaka, reading again). Compiled against the .NET 8 Windows Forms reference assemblies (0 errors). Not yet run on Windows.

**2026-11-21: prefilled with the most likely result**
- After reading plates, every part without labels is prefilled with its cars: the voted plate (or the best guess) and the most likely direction. Birel Plaka's estimate when it gives one; otherwise a guess from the same plate-size and road measurements ("going? (unclear)"). Prefill unlabelled parts does it for an earlier reading. Prefilled parts are marked to check (blue) until looked at, Next to label stops at them, and the marks are kept in the plates file.
- Checked on Linux: the direction guess with the real models (a car Birel Plaka calls unclear is guessed right from its shrinking plate) and on its own (approaching, small changes, stopped, size against road, two reads, one read), compiled against the .NET 8 Windows Forms reference assemblies (0 errors, 0 warnings), and the dialog under Mono WinForms with screenshots (prefill, OK with a part left to check, reopened: still marked, Next to label goes there, OK clears it). Not yet run on Windows.

**2026-11-21: two apps: the RTSP app and the scanner**
- The solution now has three projects: `Core` (shared), `MockRtsp` (the RTSP app) and `Scanner` (new: Mock RTSP Scanner). The RTSP app only shows gaps, plates and labels; it no longer scans or edits them (Scan motion, Plates..., Labels..., Label parts..., Skip start/end, skip dragging and the motion settings are gone from it), and it reads a video's file again within a second when the scanner saves it.
- The scanner lists a folder's videos with what each has, scans a queue of them (gaps, then plates and directions part by part) and shows the results while they are found: the motion graph grows, the gaps appear, each part's cars arrive and prefill it. The editor (the old labels dialog with the timeline and gap settings added) stays editable during a scan and saves every change at once.
- The file next to each video is shared safely: each app writes only its own keys, under a lock both share; a damaged file is never overwritten. Motion settings moved from the stream settings to `motionSettings` (the settings the gaps were picked with); older files are converted on the first save. The scanner's own settings are in `scanner.json`.
- Checked on Linux: all three projects compile against the .NET 8 Windows Forms reference assemblies (0 errors, 0 warnings); the file tests (both apps saving, from two threads at once, older and damaged files); the RTSP app's reload under Mono; the real models reporting parts one by one; and the scanner driven under Mono WinForms with screenshots (live graph, gaps, parts prefilled one by one, an edit while scanning, closing). That run found and fixed two bugs: a crash when the editor was created, and labels of the part on screen being lost when the window closed. Not yet run on Windows.

**2026-11-21: fix: the RTSP server did not start after the split**
- The split moved MockRtsp.exe to another build folder (`MockRtsp\bin\...`). A MediaMTX left running by the old build (stopping the debugger ends MockRtsp.exe but not its MediaMTX) kept the RTSP port, and the new build did not count it as its own, so the server could not start ("port already in use"). Leftovers of any Mock RTSP build are now stopped first, MediaMTX is tied to the app with a Windows job object so it can no longer be left behind, and the port-in-use message names the MediaMTX processes running.
- Checked on Linux with MediaMTX 1.15.1: a MediaMTX left running from another build folder reproduced the failure with the code before the fix; with the fix it is stopped and the server starts; a camera streams the main stream and substream while scanner-style saves change the video's file 30 times (the camera follows the gaps, the server keeps running). The job object is Windows only and was compile-checked.

## Where we left off

- **The split** has not run on Windows yet. Check: both exes start from the solution (set multiple startup projects), the tools are copied from the solution folder, the scanner's layout at 100 % and higher scaling (the editor is dense; the right-hand lists are short at 940 px high), and the RTSP app's timeline following the scanner live while both run.
- **Prefill**: try it on a long real video. The 3 % size / 2 % road minimum for a guess and leaving out cars with fewer than 3 reads are first choices; tune them if many prefilled labels need fixing. The to-check marks live in the plates file.

- **Where plates are read**: check on Windows how drawing feels (handle sizes at higher scaling), and that areas taken from Birel Plaka line up with the camera's view when the stream is letterboxed (a playlist with another picture shape): the area is in fractions of the video, Birel Plaka's in fractions of the stream.
- **Reading plates** has not run on Windows yet. Check that ONNX Runtime loads (the NuGet package brings `onnxruntime.dll`), how long a real video takes, and the cars list at higher scaling. The reading decodes with FFmpeg, not libvlc as Birel Plaka does for cameras, so pictures can differ slightly from what Birel Plaka sees live.

- **Vehicle labels** (the scanner's editor, the labels strip) are not yet run on Windows; check the strip's text at 100 % and higher scaling. Next step: score a vehicle detector's results (time, direction, plate) against the labels, like Score ALPR results does for plates.
- **Not yet confirmed on Windows**: the latest UI (playlist controls, Playlist dialog, project buttons, the timeline drawing, the ground-truth Plates and Score dialogs, the stats text) was checked under Mono only. Things to look at on Windows:
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

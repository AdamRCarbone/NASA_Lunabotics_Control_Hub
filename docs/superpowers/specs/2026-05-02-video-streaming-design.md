# Video Streaming Design

**Date:** 2026-05-02  
**Branch:** octane/video_stream

## Overview

Add live video streaming to the Control Hub. The rover streams JPEG frames over UDP port 5002. Stream requests are sent over the existing TCP connection on port 5000 using a new 'V' message type. The UI adds RGB/Depth toggle, Quality and FPS sliders, and a Stop All button in the viewport header bar. A Mosaic button (all 6 cameras tiled) is added to the Near Cameras panel.

## Protocol

**Stream request (TCP, 8 bytes):**
```
[0x4F][0x56][0x04][source_id][variant][quality][fps][CRC8]
```
- `source_id`: 0=orbbec, 1=left_side, 2=left_front, 3=right_side, 4=right_front, 5=back_rear, 6=mosaic, 255=stop all
- `variant`: 0x52=RGB, 0x44=depth heatmap
- `quality`: 1–100
- `fps`: 1–30
- CRC8 polynomial 0x07 over first 7 bytes (same as existing protocol)

Rover ACKs with standard `[0x4F][0x41][0x01][0x31][CRC]` frame.

**UDP video frame (port 5002):**
```
[0x4F][0x56][source_id][variant][seq_hi][seq_lo][chunk_idx][chunk_total][jpeg_bytes...]
```
Reassemble by seq, concatenate chunks in chunk_idx order, decode as JPEG. Discard incomplete frame on new seq arrival.

## Architecture

### New Files
- `Components/VideoStreamClient.cs` — UDP receiver + frame reassembly
- `Controls/Video/VideoPanel.axaml` + `.axaml.cs` — video display + stream controls

### Modified Files
- `Components/NetworkProtocol.cs` — add `EncodeVideoRequest(sourceId, variant, quality, fps)`
- `Components/NetworkModeClient.cs` — add `SendVideoRequestAsync(sourceId, variant, quality, fps)`
- `ViewModels/MainViewModel.cs` — add `ActiveStreamSourceId`, `CurrentFrame`, `VideoStreamRequested` event, `RequestVideoStream()`, `StopStream()`
- `Controls/Cameras/CameraStatusCard.axaml` + `.cs` — add `SourceId` byte property; on click call `vm.RequestVideoStream(SourceId)`
- `Views/MainView.axaml` — replace center viewport placeholder with `VideoPanel`; add Mosaic card (source_id=6) spanning both columns at bottom of Near Cameras grid
- `Views/MainView.axaml.cs` — inject `NetworkModeClient` into `VideoPanel` post-connect; wire disconnect → clear stream

## Component Design

### VideoStreamClient
- Binds `UdpClient` to `0.0.0.0:5002`
- Reassembly: `Dictionary<ushort, FrameBuffer>` where `FrameBuffer` holds `byte[][] Chunks`, `byte Total`, `int Received`
- New seq while one in-flight → drop old, start fresh
- Complete frame (received == total) → concatenate in chunk_idx order → fire `event Action<byte[]> FrameDecoded` on background thread
- `Start()` / `Stop()` with `CancellationTokenSource`
- UDP errors: log + continue loop

### VideoPanel
- `NetworkClient` property (set by `MainView.axaml.cs` after connection)
- Owns `VideoStreamClient`; subscribes to `FrameDecoded` → `Dispatcher.UIThread.Post` → decode JPEG to `Bitmap` → update `vm.CurrentFrame`
- Subscribes to `vm.VideoStreamRequested` event to send TCP stream request
- Header bar (Option C — inline with viewport name): segmented RGB/Depth toggle, Quality slider (1–100, default 70), FPS slider (1–30, default 10), Stop button
- Debounce: single `DispatcherTimer` 300ms one-shot; slider/toggle changes reset timer; on tick, resend request only if stream is active
- Stop button: sends source_id=255, clears `CurrentFrame`, sets `ActiveStreamSourceId = null`
- On disconnect: clear stream state, stop UDP listener

### MainViewModel additions
- `byte? ActiveStreamSourceId` — null = no stream
- `Bitmap? CurrentFrame` — bound to `Image` in VideoPanel
- `event Action<byte> VideoStreamRequested` — fired by `RequestVideoStream()`
- `void RequestVideoStream(byte sourceId)` — sets `ActiveStreamSourceId`, fires event
- `void StopStream()` — sets `ActiveStreamSourceId = null`, clears `CurrentFrame`

### CameraStatusCard
- Add `SourceId` StyledProperty of type `byte`
- On click: existing `vm.OnViewportSelected(ViewportId)` + new `vm.RequestVideoStream(SourceId)`

### ViewportId → SourceId mapping (set in MainView.axaml)
| ViewportId | SourceId |
|---|---|
| depth_cam | 0 |
| left_side | 1 |
| left_front | 2 |
| right_side | 3 |
| right_front | 4 |
| back_rear | 5 |
| mosaic | 6 |

## Threading
- `FrameDecoded` fires on UDP background thread → `Dispatcher.UIThread.Post` in VideoPanel handler
- JPEG decode failure: log + skip frame
- TCP send failure: existing NetworkModeClient reconnect cycle handles it

## Startup Behavior
- No stream on launch — `ActiveStreamSourceId` is null
- Sliders/toggle are configurable before any stream starts
- First stream starts only when user explicitly clicks a camera card

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Etch.Compose;
using Etch.Gpu;
using Etch.Gpu.Descriptors;
using Etch.Gpu.Native;
using Etch.Gpu.SwapChains;
using Etch.Gpu.Validation;
using Etch.Gpu.Diagnostics;
using Etch.Scene;
using GpuBuffer = Etch.Gpu.Buffer;
using Cascade.UI.Diagnostics;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Presents frames to a window's swapchain through wgpu-native. Each frame's draws are scheduled
/// into an Etch <see cref="DrawList"/> by a <see cref="DrawListBuilder"/> and executed by an Etch
/// <see cref="GpuComposer"/>; this class owns the device, the swapchain and screenshot capture.
/// </summary>
#pragma warning disable CA1812
internal sealed unsafe class EtchGpuPresenter : IDisposable
#pragma warning restore CA1812
{
    private readonly Instance _instance;
    private readonly Adapter _adapter;
    private readonly Device _device;
    private readonly Surface _surface;
    private readonly SwapChain _swapChain;
    private readonly GpuComposer _composer;
    private readonly DeviceLossWatch _lossWatch;
    private bool _acquireLost;
    private readonly DrawList _drawList = new();
    private readonly DrawListBuilder _builder = new();
    private readonly List<PlacedGlyph> _placedGlyphs = new();
    private uint _currentWidth;
    private uint _currentHeight;
    private bool _disposed;
    private int _lastLoggedBatchCount = -1;

    // Performance instrumentation
    private readonly Stopwatch _phaseTimer = new();
    private int _frameCount;

    // Screenshot capture. _captureBuffer is written row-by-row on the present
    // thread (PerformCapture) and read on the MCP/CLI thread (CaptureFrame);
    // _captureGate serializes the two so a screenshot can never observe a buffer
    // half-overwritten by the next frame (the WP-3519 torn-top-band golden flake).
    private readonly Lock _captureGate = new();
    private byte[]? _captureBuffer;
    private int _capturedWidth;
    private int _capturedHeight;
    private GpuBuffer _stagingBuffer;
    private ulong _stagingBufferSize;
    private int _captureRequested;

    // ── Animated font-size churn (WP-3509 decision) ─────────────────
    // A size animation emits a distinct raster font size per frame, each a new atlas
    // entry. Animated sizes are NOT quantized: the atlas generation reset already makes
    // churn self-healing and memory-bounded, and a size sweep spans a range no sub-pixel
    // quantization can collapse without visibly stair-stepping the motion.

    /// <summary>
    /// WP-3519: default glyph text-weight gamma. Pure-linear blending (0) renders
    /// black-on-white text too light (~33% mean ink darkness at 9px → washed out);
    /// 1.5 restores a perceptually correct, macOS-like weight (~61%) without
    /// adding aliasing (coverage is remapped monotonically, not contrast-crushed).
    /// </summary>
    internal const float DefaultTextGamma = 1.5f;

    /// <summary>
    /// Glyph text-weight gamma uploaded to the glyph shader. Defaults to
    /// <see cref="DefaultTextGamma"/>; 0 selects the legacy pure-linear blend.
    /// Set live via cascade_set_render_param "textgamma".
    /// </summary>
    internal float TextGamma { get; set; } = DefaultTextGamma;

    /// <summary>
    /// WP-3537: light-on-dark weight factor. The WP-3525 symmetric model weighted
    /// both polarities equally, but light text on a dark background blooms and reads
    /// too heavy at that weight (visual review + the WP-3527 heaviness vs DirectWrite/
    /// Skia). This scales the light-on-dark side of the curve toward the un-weighted
    /// the actual fg-vs-background contrast (WP-3537): dark-on-light keeps the full
    /// perceptual weight; light-on-dark is scaled toward linear coverage as contrast
    /// grows, because light text blooms. This value is the adaptive STRENGTH —
    /// 1 = full adaptive (default), 0 = the legacy symmetric weight — not a fixed
    /// magnitude, so there is no tuned per-condition constant. Set live via
    /// cascade_set_render_param "textlightweight".
    /// </summary>
    internal const float DefaultLightWeight = 1.0f;

    /// <summary>Adaptive light-weight strength uploaded to the glyph shader. See <see cref="DefaultLightWeight"/>.</summary>
    internal float LightWeight { get; set; } = DefaultLightWeight;

    // Auto prefers the GPU driving the window's monitor. Otherwise hardware first unless Software was
    // asked for; the software adapter (WARP) is the fallback when no hardware adapter can present,
    // which keeps rendering on the GPU pipeline instead of dropping to the reduced-resolution CPU
    // rasterizer.
    private static RequestAdapterResult RequestAdapter(Instance instance, Surface surface, nint hwnd, GpuPreference preference)
    {
        if (preference == GpuPreference.Auto
            && Win32DisplayAdapter.TryGetForWindow(hwnd, out uint vendorId, out uint deviceId))
        {
            Adapter display = FindHardwareAdapter(instance, surface, vendorId, deviceId);
            if (!display.IsInvalid)
            {
                return new RequestAdapterResult(RequestAdapterStatus.Success, display, null);
            }
        }

        if (preference != GpuPreference.Software)
        {
            PowerPreference power = preference switch
            {
                GpuPreference.LowPower => PowerPreference.LowPower,
                GpuPreference.HighPerformance => PowerPreference.HighPerformance,
                _ => PowerPreference.Undefined,
            };
            var hardware = AsyncRequest.RequestAdapterSync(instance, surface, power);
            if (hardware.Status == RequestAdapterStatus.Success && !hardware.Adapter.IsInvalid)
            {
                return hardware;
            }
        }

        return AsyncRequest.RequestAdapterSync(instance, surface, forceFallbackAdapter: true);
    }

    // The first hardware adapter with this PCI id that can present to the surface; every other
    // enumerated adapter is released.
    private static Adapter FindHardwareAdapter(Instance instance, Surface surface, uint vendorId, uint deviceId)
    {
        Adapter chosen = default;
        foreach (Adapter candidate in instance.EnumerateAdapters())
        {
            if (chosen.IsInvalid)
            {
                AdapterDescription description = candidate.GetDescription();
                if (!description.IsSoftware
                    && description.VendorId == vendorId
                    && description.DeviceId == deviceId
                    && candidate.CanPresentTo(surface))
                {
                    chosen = candidate;
                    continue;
                }
            }
            candidate.Dispose();
        }
        return chosen;
    }


    public EtchGpuPresenter(nint hwnd, uint width, uint height, GpuPreference preference = GpuPreference.Auto)
    {
        // Etch's default instance enables only the platform's primary backend (D3D12 on Windows),
        // so no other driver stack is loaded into the process.
        _instance = Instance.Create();
        if (_instance.IsInvalid)
        {
            throw new InvalidOperationException("Failed to create wgpu instance");
        }

        try
        {
            // The surface comes first so the adapter request can insist on one that presents to it.
            nint hinstance = Win32.GetModuleHandleW(null);
            _surface = SurfaceFactory.CreateFromWin32(_instance, hwnd, hinstance, "CascadeUI");
            if (!_surface.IsValid)
            {
                throw new InvalidOperationException("Failed to create surface from HWND");
            }

            var adapterResult = RequestAdapter(_instance, _surface, hwnd, preference);
            if (adapterResult.Status != RequestAdapterStatus.Success || adapterResult.Adapter.IsInvalid)
            {
                throw new InvalidOperationException($"No GPU adapter available: {adapterResult.Message ?? adapterResult.Status.ToString()}");
            }
            _adapter = adapterResult.Adapter;
            if (DebugLog.IsEnabled(DebugLogCategory.Present))
            {
                DebugLog.Write(DebugLogCategory.Present, $"[{DateTime.Now:O}] GPU adapter ({preference}): {_adapter.GetDescription()}");
            }

            // Etch's device defaults: allocator blocks sized for UI content and a descriptor heap sized for
            // Cascade's bind-group usage, instead of wgpu's game-sized defaults (~200 MB committed up front).
            // The loss watch reports a driver reset or removal, so frames fall back to the CPU.
            DeviceDescriptor deviceDesc = default;
            ValidationBridge.ConfigureDeviceDescriptor(&deviceDesc);
            _lossWatch = DeviceLossWatch.Attach(&deviceDesc);
            var deviceResult = AsyncRequest.RequestDeviceSync(_instance, _adapter, &deviceDesc);
            if (deviceResult.Status != RequestDeviceStatus.Success || deviceResult.Device.IsInvalid)
            {
                throw new InvalidOperationException($"Failed to create wgpu device: {deviceResult.Message ?? deviceResult.Status.ToString()}");
            }
            _device = deviceResult.Device;

            _currentWidth = width;
            _currentHeight = height;
            var swapChainConfig = new SwapChainConfig
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Width = width,
                Height = height,
                // Mailbox, not Fifo: on this stack Fifo's AcquireFrame blocked ~93 ms
                // per present (measured), capping the whole app to ~11 fps whenever it
                // presents continuously (a blinking caret, any animation) and adding up
                // to ~90 ms of input→repaint latency. Mailbox acquires immediately and
                // still syncs to vblank (no tearing), so presents are ~0.5 ms and the
                // frame loop paces on the frame timer instead of stalling in the driver.
                PresentMode = PresentMode.Mailbox,
                AlphaMode = CompositeAlphaMode.Auto,
                Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
                ColorSpace = ColorSpace.Srgb,
            };
            // With the loss watch, acquiring from a lost device reports DeviceLost.
            _swapChain = SwapChain.Configure(_device, _surface, swapChainConfig, _lossWatch);

            _composer = new GpuComposer(_device, width, height)
            {
                SkipGlyphs = RenderDebugSwitches.SkipGlyphs,
            };

            // Force GPU initialization by submitting an empty command buffer.
            // Some drivers defer initialization until first use, causing
            // multi-second stalls on the first real frame.
            using var warmupEncoder = _device.CreateCommandEncoder();
            using var warmupCb = warmupEncoder.Finish();
            Span<CommandBuffer> warmupCmds = stackalloc CommandBuffer[1];
            warmupCmds[0] = warmupCb;
            _device.Queue.Submit(warmupCmds);
            _device.Poll(true);
        }
        catch
        {
            // Whatever was created before the failure (any step can throw: the surface, the
            // device, swapchain configuration, pipeline creation) is released, newest first.
            ReleaseNatives();
            throw;
        }
    }

    // Releases the native objects in reverse creation order; each is skipped when it was never
    // created (a failed constructor) and nothing is released twice.
    private void ReleaseNatives()
    {
        _composer?.Dispose();
        if (!_stagingBuffer.IsInvalid)
        {
            _stagingBuffer.Dispose();
        }
        _swapChain.Dispose();
        _lossWatch?.Dispose();
        _device.Dispose();
        _adapter.Dispose();
        if (_surface.IsValid)
        {
            _surface.Dispose();
        }
        _instance.Dispose();
    }

    /// <summary>
    /// True once the device is lost (a driver reset or removal): nothing more can be presented
    /// with it, and the provider falls back to CPU frames.
    /// </summary>
    internal bool DeviceLost => _lossWatch.IsLost || _acquireLost;

    /// <summary>Destroys the device as a driver reset would (tests of the CPU fallback).</summary>
    internal void SimulateDeviceLoss() => _device.Destroy();

    public void Resize(uint width, uint height)
    {
        _currentWidth = width;
        _currentHeight = height;
        _swapChain.Resize(width, height);
        _composer.Resize(width, height);
    }

    /// <summary>Frees the texture CPU frames are presented through (the window is hidden).</summary>
    internal void ReleaseCpuFrameTexture() => _composer.ReleaseFramebufferTexture();

    /// <summary>Frees the GPU texture of a destroyed image (no-op if it was never drawn).</summary>
    internal void ReleaseImage(ulong handle) => _composer.ReleaseImage((int)handle);

    /// <summary>
    /// The window is hidden: frees image textures, mask pages and the CPU-frame texture (all
    /// re-created on demand by the next frame). The glyph atlases stay.
    /// </summary>
    internal void Trim() => _composer.Trim();

    /// <summary>GPU objects the composer holds (tests and diagnostics).</summary>
    internal global::Etch.Compose.GpuResourceUsage ResourceUsage() => _composer.ResourceUsage();

    /// <summary>Glyph atlas dimension in texels (page 0, monochrome atlas).</summary>
    internal int AtlasDimension => _composer.MonoAtlas.Dimension;

    /// <summary>
    /// GPU readback of a monochrome glyph-atlas rect as RGBA pixels (coverage
    /// expanded to gray, alpha 255). Serves the <c>cascade_atlas</c> tool.
    /// Must run on the UI thread — it submits GPU queue work and maps a
    /// staging buffer synchronously. Width/height of 0 mean "to the atlas edge".
    /// Returns null when the readback fails (map timeout, device loss).
    /// </summary>
    internal unsafe AtlasRegionCapture? CaptureAtlasRegion(int u, int v, int width, int height)
    {
        int dim = _composer.MonoAtlas.Dimension;
        int rx = Math.Clamp(u, 0, dim - 1);
        int ry = Math.Clamp(v, 0, dim - 1);
        int rw = width <= 0 ? dim - rx : Math.Clamp(width, 1, dim - rx);
        int rh = height <= 0 ? dim - ry : Math.Clamp(height, 1, dim - ry);

        ulong size = (ulong)(dim * dim);
        var staging = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.MapRead | BufferUsage.CopyDst),
            Size = size,
        });

        try
        {
            using (var encoder = _device.CreateCommandEncoder())
            {
                var srcTextureInfo = new WGPUTexelCopyTextureInfo
                {
                    Aspect = (uint)TextureAspect.All,
                    MipLevel = 0,
                    Origin = new WGPUOrigin3D { X = 0, Y = 0, Z = 0 },
                    Texture = _composer.MonoAtlas.GetPage(0).Texture.Handle,
                };
                var dstBufferInfo = new WGPUTexelCopyBufferInfo
                {
                    Layout = new WGPUTexelCopyBufferLayout
                    {
                        Offset = 0,
                        BytesPerRow = (uint)dim,
                        RowsPerImage = (uint)dim,
                    },
                    Buffer = staging.Handle,
                };
                var copySize = new Extent3D { Width = (uint)dim, Height = (uint)dim, DepthOrArrayLayers = 1 };
                WebGPU.CommandEncoderCopyTextureToBuffer(encoder.Handle, (nint)(&srcTextureInfo), (nint)(&dstBufferInfo), (nint)(&copySize));
                using var cb = encoder.Finish();
                Span<CommandBuffer> cmds = stackalloc CommandBuffer[1];
                cmds[0] = cb;
                _device.Queue.Submit(cmds);
            }

            if (!staging.MapSync(_device, MapMode.Read, 0, size, timeoutMilliseconds: 2000))
            {
                return null;
            }

            nint ptr = WebGPU.BufferGetConstMappedRange(staging.Handle, 0, size);
            if (ptr == nint.Zero)
            {
                staging.Unmap();
                return null;
            }

            // Glyph bitmaps are stored bottom-up in the atlas (rasterizer row
            // convention; the GPU compensates in UVs). Flip the region so a
            // captured glyph reads upright — an agent shown "∩" for "U" will
            // draw wrong conclusions.
            var pixels = new byte[rw * rh * 4];
            for (int row = 0; row < rh; row++)
            {
                int sourceRow = ry + (rh - 1 - row);
                for (int col = 0; col < rw; col++)
                {
                    byte coverage = ((byte*)ptr)[sourceRow * dim + (rx + col)];
                    int offset = (row * rw + col) * 4;
                    pixels[offset] = coverage;
                    pixels[offset + 1] = coverage;
                    pixels[offset + 2] = coverage;
                    pixels[offset + 3] = 255;
                }
            }
            staging.Unmap();

            var image = new ImageData
            {
                Pixels = pixels,
                Width = rw,
                Height = rh,
                Stride = rw * 4,
            };
            return new AtlasRegionCapture(image, dim, rx, ry, rw, rh);
        }
        catch (Exception ex)
        {
            DebugLog.Write(DebugLogCategory.Glyph,
                $"ATLAS CAPTURE FAILED: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            staging.Dispose();
        }
    }

    private long _lastValidationCount;

    private void PollValidationErrors(string context)
    {
        // The snapshot/decode below exists solely to feed the validation log —
        // skip all of it (including the ring snapshot allocation) when off.
        if (!DebugLog.IsEnabled(DebugLogCategory.Validation))
        {
            return;
        }

        long current = ValidationBridge.TotalDelivered;
        long newErrors = current - _lastValidationCount;
        if (newErrors > 0)
        {
            _lastValidationCount = current;
            byte[] blob = ValidationBridge.Ring.Snapshot();
            if (ValidationLogRing.TryDecode(blob, out var snapshot))
            {
                for (int i = snapshot.Count - (int)newErrors; i < snapshot.Count; i++)
                {
                    if (i < 0)
                    {
                        continue;
                    }
                    var entry = snapshot[i];
                    DebugLog.Write(DebugLogCategory.Validation,
                        $"[{DateTime.Now:HH:mm:ss.fff}] [VALIDATION] {context}: [{entry.ErrorType}] {entry.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Renders a recorded frame to the swapchain: the recording is replayed into the frame's
    /// <see cref="DrawList"/> (paint order, clips, masks, glyph runs) and executed by the
    /// <see cref="GpuComposer"/>. When <paramref name="glyphRecords"/> is given, every placed glyph
    /// is reported there (draw provenance); <paramref name="layerCompositeIndex"/> maps a retained
    /// layer's handle to the main-stream command that composites it, for their paint order.
    /// </summary>
    /// <returns>False when nothing was presented (see <see cref="DeviceLost"/>).</returns>
    public bool PresentRecording(DrawRecording main, ComposeParameters parameters,
        List<GlyphDrawRecord>? glyphRecords = null, IReadOnlyDictionary<ulong, int>? layerCompositeIndex = null)
    {
        if (_disposed)
        {
            return false;
        }

        // Phase timestamps, not Stopwatch instances: a frame allocates nothing for its timing.
        long frameStart = Stopwatch.GetTimestamp();
        // Etch's wgpu-native fails (rather than aborts on) an acquire, submit or present on a lost
        // device and reports the loss to _lossWatch; checking first just skips a doomed frame.
        if (DeviceLost)
        {
            return false;
        }
        long acquireStart = Stopwatch.GetTimestamp();
        var status = _swapChain.AcquireFrame(out SurfaceTexture frame);
        long acquireEnd = Stopwatch.GetTimestamp();
        if (status != SurfaceTextureResult.Ok || !frame.IsValid)
        {
            HandleAcquireFailure(status);
            frame.Dispose();
            return false;
        }
        using var encoder = _device.CreateCommandEncoder();

        // WP-3509: if churn filled an atlas last frame (or a forced reset is requested), clear it
        // now — between frames, before anything of this frame is looked up or inserted.
        _composer.ResetAtlasesIfExhausted(RenderDebugSwitches.ForceAtlasReset);

        _phaseTimer.Restart();
        _placedGlyphs.Clear();
        _builder.Begin(_drawList, _currentWidth, _currentHeight, _composer.Masks, _composer.MonoAtlas, _composer.ColorAtlas,
            glyphRecords is null ? null : _placedGlyphs);
        _builder.Replay(main);
        _builder.End();
        _drawList.Parameters = parameters;
        if (glyphRecords is not null)
        {
            GlyphProvenance.Report(_placedGlyphs, _drawList, glyphRecords, layerCompositeIndex);
        }
        _phaseTimer.Stop();
        double buildMs = _phaseTimer.Elapsed.TotalMilliseconds;

        _phaseTimer.Restart();
        _composer.Encode(encoder, new Texture(frame.Texture), new TextureView(frame.View), _drawList);
        _phaseTimer.Stop();
        double renderMs = _phaseTimer.Elapsed.TotalMilliseconds;

        if (DebugLog.IsEnabled(DebugLogCategory.Clip) && _builder.CulledGlyphs > 0)
        {
            DebugLog.Write(DebugLogCategory.Clip,
                $"[{DateTime.Now:O}] culled={_builder.CulledGlyphs} glyphs outside their clip");
        }
        if (DebugLog.IsEnabled(DebugLogCategory.Frame) && _drawList.BatchCount != _lastLoggedBatchCount)
        {
            _lastLoggedBatchCount = _drawList.BatchCount;
            var batchList = new StringBuilder();
            foreach (var b in _drawList.Batches)
            {
                batchList.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $" {b.Kind}×{b.Count}@({b.MinX:F0},{b.MinY:F0})-({b.MaxX:F0},{b.MaxY:F0})");
            }
            DebugLog.Write(DebugLogCategory.Frame,
                $"[{DateTime.Now:O}] paint order: {_drawList.BatchCount} batches, {_composer.LastCopyCount} framebuffer copies, shapes={_drawList.Shapes.Count} glyphs={_drawList.Glyphs.Count} colorGlyphs={_drawList.ColorGlyphs.Count} images={_drawList.ImageInstances.Count} blurs={_drawList.Blurs.Count} masks dropped={_builder.DroppedMasks}:{batchList}");
        }

        using var cb = encoder.Finish();
        Span<CommandBuffer> cmdsSpan = stackalloc CommandBuffer[1];
        cmdsSpan[0] = cb;

        long submitStart = Stopwatch.GetTimestamp();
        _device.Queue.Submit(cmdsSpan);
        PollValidationErrors("PresentRecording");
        long submitEnd = Stopwatch.GetTimestamp();

        // Capture the framebuffer to CPU ONLY when a screenshot was requested: the synchronous
        // readback stalls the present pipeline, so doing it every frame capped continuously
        // presenting apps to ~11 fps. Screenshot callers RequestCapture() + force a present.
        if (System.Threading.Interlocked.Exchange(ref _captureRequested, 0) == 1)
        {
            EnsureStagingBuffer();
            PerformCapture(frame);
            PresentMonitor.MarkCapture();
        }

        long presentStart = Stopwatch.GetTimestamp();
        bool presented = _swapChain.Present(frame);
        long presentEnd = Stopwatch.GetTimestamp();
        // A device lost during the submit or the present: the frame never reached the screen.
        if (!presented || DeviceLost)
        {
            return false;
        }
        PresentMonitor.CpuRenderActive = false;
        PresentMonitor.NotifyPresented();

        if (_frameCount == 0 && DebugLog.IsEnabled(DebugLogCategory.Frame))
        {
            DebugLog.Write(DebugLogCategory.Frame,
                $"[{DateTime.Now:O}] Frame 0: total={Ms(frameStart, presentEnd):F2}ms poll={Ms(frameStart, acquireStart):F2}ms acquire={Ms(acquireStart, acquireEnd):F2}ms build={buildMs:F2}ms encode={renderMs:F2}ms submit={Ms(submitStart, submitEnd):F2}ms present={Ms(presentStart, presentEnd):F2}ms shapes={_drawList.Shapes.Count} batches={_drawList.BatchCount}");
        }
        _frameCount++;
        return true;
    }

    private void HandleAcquireFailure(SurfaceTextureResult status)
    {
        if (status == SurfaceTextureResult.DeviceLost)
        {
            _acquireLost = true;
            return;
        }
        if (status == SurfaceTextureResult.Outdated || status == SurfaceTextureResult.Lost)
        {
            _swapChain.Resize(_currentWidth, _currentHeight);
        }
    }

    private static double Ms(long start, long end) => (end - start) * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Presents a CPU-rendered frame: uploads <paramref name="dirty"/> of <paramref name="frame"/>
    /// into the composer's persistent frame texture and blits it to the swapchain. False when nothing
    /// was presented (the caller must then present the whole frame next time).
    /// </summary>
    public bool PresentCpuFrame(global::Etch.Compose.Cpu.CpuFramebuffer frame, ReadOnlySpan<global::Etch.Compose.Cpu.CpuDirtyRect> dirty)
    {
        if (_disposed)
        {
            return false;
        }

        // A lost device cannot present (see PresentRecording).
        if (DeviceLost)
        {
            return false;
        }

        var status = _swapChain.AcquireFrame(out SurfaceTexture surface);
        if (status != SurfaceTextureResult.Ok || !surface.IsValid)
        {
            HandleAcquireFailure(status);
            surface.Dispose();
            return false;
        }
        using var encoder = _device.CreateCommandEncoder();

        _composer.EncodeFramebufferUpload(encoder, new TextureView(surface.View), frame.Pixels, (uint)frame.Width, (uint)frame.Height, dirty);

        using var cb = encoder.Finish();
        Span<CommandBuffer> cmdsSpan = stackalloc CommandBuffer[1];
        cmdsSpan[0] = cb;
        _device.Queue.Submit(cmdsSpan);
        PollValidationErrors("PresentCpuFrame");

        // Capture to CPU only on request (see PresentRecording) — the readback stalls
        // the present, so it must not run every frame.
        if (System.Threading.Interlocked.Exchange(ref _captureRequested, 0) == 1)
        {
            EnsureStagingBuffer();
            PerformCapture(surface);
            PresentMonitor.MarkCapture();
        }

        if (!_swapChain.Present(surface) || DeviceLost)
        {
            return false;
        }
        PresentMonitor.CpuRenderActive = true;
        PresentMonitor.NotifyPresented();
        return true;
    }

    /// <summary>
    /// Returns the most recently captured frame as RGBA pixel data.
    /// Capture happens during the normal presentation cycle on the UI thread.
    /// </summary>
    public ImageData? CaptureFrame()
    {
        // Defensive copy under _captureGate: PerformCapture overwrites
        // _captureBuffer in place each frame, so handing out the live array (or
        // reading it while it is being rewritten) can tear across two frames.
        // Use the dimensions recorded with the buffer, not _currentWidth — a
        // resize can change those before the next capture lands.
        lock (_captureGate)
        {
            if (_captureBuffer == null || _capturedWidth == 0 || _capturedHeight == 0)
            {
                return null;
            }

            int byteCount = _capturedWidth * _capturedHeight * 4;
            if (_captureBuffer.Length < byteCount)
            {
                return null;
            }

            var copy = new byte[byteCount];
            Array.Copy(_captureBuffer, copy, byteCount);
            return new ImageData
            {
                Pixels = copy,
                Width = _capturedWidth,
                Height = _capturedHeight,
                Stride = _capturedWidth * 4,
            };
        }
    }

    /// <summary>
    /// Returns a snapshot of GPU resource counts for diagnostics.
    /// </summary>
    internal NativeMemorySnapshot GetNativeMemorySnapshot()
    {
        // Counted from the composer's live resources (a trimmed, hidden window reports less).
        var usage = _composer.ResourceUsage();
        return new NativeMemorySnapshot
        {
            Version = 1,
            CountersFeatureEnabled = true,

            DeviceCount = 1,
            SurfaceCount = 1,
            SceneFrameCount = (ulong)_frameCount,

            SurfaceIntermediateBytes = (ulong)(_currentWidth * _currentHeight * 4),
            SurfaceSwapchainBytesEst = (ulong)(_currentWidth * _currentHeight * 4 * 2),

            ImageCount = (ulong)_composer.ImageTextureCount,
            WgpuShaderModules = (ulong)usage.ShaderModules,
            WgpuRenderPipelines = (ulong)usage.RenderPipelines,
            WgpuBuffers = (ulong)usage.Buffers,
            WgpuBufferMemoryBytes = (ulong)usage.BufferBytes,
            WgpuTextures = (ulong)usage.Textures,
            WgpuTextureViews = (ulong)usage.TextureViews,
            WgpuTextureMemoryBytes = (ulong)usage.TextureBytes,
            WgpuBindGroups = (ulong)usage.BindGroups,
        };
    }
    /// <summary>
    /// Requests that the next presented frame be captured to CPU memory.
    /// Capture happens during the next PresentRecording/PresentCpuFrame call.
    /// </summary>
    public void RequestCapture()
    {
        System.Threading.Interlocked.Exchange(ref _captureRequested, 1);
    }

    private void EnsureStagingBuffer()
    {
        // Texture→buffer copies require BytesPerRow aligned to 256, so the
        // staging buffer holds padded rows. Sizing it for tight rows crashed
        // the present thread (wgpu panic) for any window width not divisible
        // by 64 — found 2026-06-12 by the WP-3506 scale-1.25 specimen page
        // (800 px wide).
        int paddedBytesPerRow = ((int)_currentWidth * 4 + 255) & ~255;
        ulong size = (ulong)(paddedBytesPerRow * (int)_currentHeight);

        if (_stagingBuffer.IsInvalid || _stagingBufferSize < size)
        {
            if (!_stagingBuffer.IsInvalid)
            {
                _stagingBuffer.Dispose();
            }

            _stagingBuffer = _device.CreateBuffer(new BufferDescriptor
            {
                Usage = (ulong)(BufferUsage.MapRead | BufferUsage.CopyDst),
                Size = size,
            });
            _stagingBufferSize = size;
        }
    }

    private unsafe void PerformCapture(SurfaceTexture frame)
    {
        if (_stagingBuffer.IsInvalid)
        {
            if (DebugLog.IsEnabled(DebugLogCategory.Capture))
            {
                DebugLog.Write(DebugLogCategory.Capture,
                    $"[{DateTime.Now:O}] PerformCapture: staging buffer invalid");
            }
            return;
        }

        int w = (int)_currentWidth;
        int h = (int)_currentHeight;
        int pixelCount = w * h;

        // wgpu requires texture→buffer copies to use a BytesPerRow that is a
        // multiple of 256; an unpadded value panics the present thread for
        // any window width not divisible by 64. Rows are de-strided into the
        // tight _captureBuffer after mapping.
        int tightBytesPerRow = w * 4;
        int paddedBytesPerRow = (tightBytesPerRow + 255) & ~255;
        ulong paddedSize = (ulong)(paddedBytesPerRow * h);

        if (_captureBuffer == null || _captureBuffer.Length < pixelCount * 4)
        {
            _captureBuffer = new byte[pixelCount * 4];
        }

        // Copy from swapchain texture to staging buffer
        using (var encoder = _device.CreateCommandEncoder())
        {
            var srcOrigin = new WGPUOrigin3D { X = 0, Y = 0, Z = 0 };
            var copySize = new Extent3D { Width = (uint)w, Height = (uint)h, DepthOrArrayLayers = 1 };

            var srcTextureInfo = new WGPUTexelCopyTextureInfo
            {
                Aspect = (uint)TextureAspect.All,
                MipLevel = 0,
                Origin = srcOrigin,
                Texture = frame.Texture,
            };
            var dstBufferInfo = new WGPUTexelCopyBufferInfo
            {
                Layout = new WGPUTexelCopyBufferLayout
                {
                    Offset = 0,
                    BytesPerRow = (uint)paddedBytesPerRow,
                    RowsPerImage = (uint)h,
                },
                Buffer = _stagingBuffer.Handle,
            };

            WebGPU.CommandEncoderCopyTextureToBuffer(
                encoder.Handle,
                (nint)(&srcTextureInfo),
                (nint)(&dstBufferInfo),
                (nint)(&copySize));

        using var cb = encoder.Finish();
            Span<CommandBuffer> cmds = stackalloc CommandBuffer[1];
            cmds[0] = cb;
            _device.Queue.Submit(cmds);
        }

        // Map and read back (synchronous, polls device until complete)
        bool mapSuccess = _stagingBuffer.MapSync(_device, MapMode.Read, 0, paddedSize, timeoutMilliseconds: 1000);
        if (!mapSuccess)
        {
            if (DebugLog.IsEnabled(DebugLogCategory.Capture))
            {
                DebugLog.Write(DebugLogCategory.Capture,
                    $"[{DateTime.Now:O}] PerformCapture: MapSync failed");
            }
            return;
        }

        nint ptr = WebGPU.BufferGetConstMappedRange(_stagingBuffer.Handle, 0, paddedSize);
        if (ptr != nint.Zero)
        {
            // Hold _captureGate across the whole de-stride so a concurrent
            // CaptureFrame() sees either the previous complete frame or this one,
            // never a row-by-row mix of the two.
            lock (_captureGate)
            {
                if (paddedBytesPerRow == tightBytesPerRow)
                {
                    Marshal.Copy(ptr, _captureBuffer, 0, pixelCount * 4);
                }
                else
                {
                    for (int row = 0; row < h; row++)
                    {
                        Marshal.Copy(ptr + row * paddedBytesPerRow, _captureBuffer,
                            row * tightBytesPerRow, tightBytesPerRow);
                    }
                }
                _capturedWidth = w;
                _capturedHeight = h;
            }
            if (DebugLog.IsEnabled(DebugLogCategory.Capture))
            {
                int nonZero = 0;
                for (int i = 0; i < Math.Min(1000, pixelCount * 4); i++)
                {
                    if (_captureBuffer[i] != 0)
                    {
                        nonZero++;
                    }
                }
                DebugLog.Write(DebugLogCategory.Capture,
                    $"[{DateTime.Now:O}] PerformCapture: copied {pixelCount * 4} bytes, nonZero={nonZero}");
            }
        }
        else if (DebugLog.IsEnabled(DebugLogCategory.Capture))
        {
            DebugLog.Write(DebugLogCategory.Capture,
                $"[{DateTime.Now:O}] PerformCapture: GetConstMappedRange returned null");
        }

        _stagingBuffer.Unmap();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ReleaseNatives();
    }
}

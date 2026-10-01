using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Etch.Geometry;
using Etch.Gpu;
using Etch.Gpu.Descriptors;
using Etch.Gpu.Native;
using Etch.Gpu.Compositor;
using Etch.Gpu.SwapChains;
using Etch.Gpu.Validation;
using Etch.Gpu.Diagnostics;
using Etch.Scene;
using Etch.Text.Atlas;
using Etch.Text.Outline;
using Etch.Text.Rasterize;
using Etch.Text.Shape;
using GpuBuffer = Etch.Gpu.Buffer;
using EGeometry = Etch.Geometry;
using Cascade.UI.Diagnostics;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Native GPU presenter that renders an Etch SceneBuffer directly to the
/// swapchain using wgpu-native. Uses instanced rendering with a storage buffer
/// for shape data. Supports solid-fill rects, circles, and 2-stop linear
/// gradients. Text is rendered through the GPU glyph atlas; glyph bitmaps are
/// rasterized on CPU and cached in the atlas.
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
    private uint _currentWidth;
    private uint _currentHeight;
    private bool _disposed;

    // Geometry pipeline
    private readonly ShaderModule _geomShader;
    private readonly RenderPipeline _geomPipeline;
    private readonly PipelineLayout _geomPipelineLayout;
    private readonly BindGroupLayout _geomBgLayout;
    private readonly GpuBuffer _geomUniformBuffer;
    private GpuBuffer _geomStorageBuffer;
    private int _geomStorageCapacity;
    private BindGroup _geomBindGroup;

    // Textured-quad pipeline — shared by the CPU-fallback blit and image rendering
    private readonly ShaderModule _textShader;
    private readonly RenderPipeline _textPipeline;
    private readonly PipelineLayout _textPipelineLayout;
    private readonly BindGroupLayout _textBgLayout;
    private readonly Sampler _textSampler;
    private readonly GpuBuffer _textVertexBuffer;

    // Fallback texture blit (for CPU-rendered scenes)
    private Texture _fallbackTexture;
    private TextureView _fallbackTextureView;
    private BindGroup _fallbackBindGroup;

    // Image rendering
    private readonly Dictionary<int, Texture> _imageTextures = new();
    private readonly Dictionary<int, TextureView> _imageTextureViews = new();
    private readonly Dictionary<int, BindGroup> _imageBindGroups = new();
    // Every image quad of a frame, quad i at vertices [6i, 6i+6) (see UploadImageQuads); grown on demand.
    private GpuBuffer _imageVertexBuffer;
    private const int InitialImageQuadCapacity = 4096;
    private int _imageQuadCapacity = InitialImageQuadCapacity;

    // Performance instrumentation
    private readonly Stopwatch _phaseTimer = new();
    private int _frameCount;
    private double _totalFrameMs;
    private double _totalBuildMs;
    private double _totalUploadMs;
    private double _totalRenderMs;

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

    // Layer instance cache — avoid rebuilding instances for unchanged layers.
    // Stores the scene reference alongside instances so that layer recapture
    // (which produces a new SceneBuffer) invalidates the cache correctly.
    // InstanceAt maps the layer's captured command index to its shape-instance index (paint order).
    private readonly Dictionary<ulong, (SceneBuffer Scene, List<ShapeInstance> Instances, List<int> InstanceAt)> _layerInstanceCache = new();

    // Strip-coverage pipeline (Etch GPU-007)
    private readonly GpuCompositor _compositor;
    private int _stripCoverageHash;
    private SceneBuffer? _stripCoverageScene;
    private bool _enableStripCoverage = false;

    // Layer strip-coverage cache — keyed by layer handle, stores (hash, scene)
    private readonly Dictionary<ulong, (int Hash, SceneBuffer Scene)> _layerStripCoverageCache = new();

    // Glyph atlas text rendering (GPU-native)
    private readonly GlyphAtlas _glyphAtlas;
    private readonly TextureView _glyphAtlasView;
    private readonly ShaderModule _glyphShader;
    private readonly RenderPipeline _glyphPipeline;
    private readonly PipelineLayout _glyphPipelineLayout;
    private readonly BindGroupLayout _glyphAtlasLayout;
    private readonly BindGroupLayout _glyphInstanceLayout;
    private readonly GpuBuffer _glyphUniformBuffer;
    private GpuBuffer _glyphInstanceBuffer;
    private int _glyphInstanceCapacity;
    private readonly Sampler _glyphSampler;
    private BindGroup _glyphAtlasBindGroup;
    private BindGroup _glyphInstanceBindGroup;
    // WP-3537: a per-frame copy of the painted framebuffer, sampled by the glyph
    // shader to read the local background behind each glyph so the text weight can
    // adapt to the actual fg-vs-bg contrast (destination sampling).
    private Texture _bgCopyTexture;
    private TextureView _bgCopyView;
    private uint _bgCopyWidth, _bgCopyHeight;

    // Frosted-glass backdrop blur — a batch (after a framebuffer copy) that fills
    // rounded rects with a Gaussian blur of _bgCopyTexture, tinted. Costs nothing
    // when there are no DrawBackdropBlur ops.
    private readonly ShaderModule _blurShader;
    private readonly RenderPipeline _blurPipeline;
    private readonly PipelineLayout _blurPipelineLayout;
    private readonly BindGroupLayout _blurBind0Layout;
    private readonly BindGroupLayout _blurInstanceLayout;
    private readonly Sampler _blurSampler;
    private GpuBuffer _blurInstanceBuffer;
    private int _blurInstanceCapacity;

    // Color glyph atlas for COLR/CPAL emoji (RGBA8Unorm)
    private readonly GlyphAtlas _colorGlyphAtlas;

    private const int GlyphAtlasSize = 2048;
    private const int ColorGlyphAtlasSize = 1024;
    private readonly TextureView _colorGlyphAtlasView;
    private readonly ShaderModule _colorGlyphShader;
    private readonly RenderPipeline _colorGlyphPipeline;
    private readonly PipelineLayout _colorGlyphPipelineLayout;
    private readonly Sampler _colorGlyphSampler;
    private BindGroup _colorGlyphAtlasBindGroup;
    private GpuBuffer _colorGlyphInstanceBuffer;
    private int _colorGlyphInstanceCapacity;
    private BindGroup _colorGlyphInstanceBindGroup;

    // Shape instance cache
    private SceneBuffer? _lastScene;
    private readonly List<ShapeInstance> _cachedInstances = new();

    private const string GeometryWgsl = """
        struct SurfaceSize {
            width: f32,
            height: f32,
            offset_x: f32,
            offset_y: f32,
            text_gamma: f32,
            light_weight: f32,
            dissolve: f32,
            pad2: f32,
        };

        struct ShapeInstance {
            bounds_min: vec2<f32>,
            bounds_max: vec2<f32>,
            color0: vec4<f32>,
            color1: vec4<f32>,
            p0: vec2<f32>,
            p1: vec2<f32>,
            shape_type: u32,
            _pad0: u32,
            center: vec2<f32>,
            radius: f32,
            stroke_width: f32,
            expand: f32,
            _pad2: f32,
        };

        @group(0) @binding(0)
        var<uniform> surface: SurfaceSize;

        @group(0) @binding(1)
        var<storage, read> instances: array<ShapeInstance>;

        struct VertexOutput {
            @builtin(position) position: vec4<f32>,
            @location(0) @interpolate(flat) instance_idx: u32,
        };

        const QUAD_VERTICES = array<vec2<f32>, 4>(
            vec2<f32>(0.0, 0.0),
            vec2<f32>(1.0, 0.0),
            vec2<f32>(0.0, 1.0),
            vec2<f32>(1.0, 1.0)
        );

        @vertex
        fn vs(@builtin(vertex_index) vertex_idx: u32, @builtin(instance_index) instance_idx: u32) -> VertexOutput {
            var quad = QUAD_VERTICES[vertex_idx];
            var inst = instances[instance_idx];
            var e = vec2<f32>(inst.expand, inst.expand);
            var pixel_pos = mix(inst.bounds_min - e, inst.bounds_max + e, quad) + vec2<f32>(surface.offset_x, surface.offset_y);
            var clip = vec4<f32>(
                pixel_pos.x / surface.width * 2.0 - 1.0,
                1.0 - pixel_pos.y / surface.height * 2.0,
                0.0, 1.0
            );
            return VertexOutput(clip, instance_idx);
        }

        // Compute adaptive antialias width from screen-space derivative of signed distance.
        // This adapts fade width to the local slope: shallow angles get wider fades,
        // steep angles get sharper fades — eliminating jaggies without blurring everything.
        fn adaptive_aa(dist: f32) -> f32 {
            // Use the Euclidean gradient magnitude, not fwidth (= |dFdx| + |dFdy|,
            // the Manhattan sum). For a true distance field |grad| = 1 everywhere,
            // but the Manhattan sum overestimates by up to sqrt(2) where the
            // gradient runs diagonally — i.e. at rounded-rect corners — widening
            // the antialiased band there and making stroked corners look chunkier
            // than the straight edges. The Euclidean length keeps the band uniform.
            let g = vec2<f32>(dpdx(dist), dpdy(dist));
            let fw = length(g);
            // Clamp to avoid excessive blur at glancing angles and ensure a minimum fade
            return clamp(fw, 0.35, 1.5);
        }

        // Drop shadow: coverage of a rounded rect blurred by a Gaussian (σ). Mirrors Etch's
        // ShadowShape.Coverage (the CPU fallback) — exact across x via erf, four Gaussian-weighted
        // samples over y (Evan Wallace, "Fast Rounded Rectangle Shadows").
        fn shadow_erf(x: vec2<f32>) -> vec2<f32> {
            let s = sign(x);
            let a = abs(x);
            var r = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
            r = r * r;
            return s - s / (r * r);
        }

        fn shadow_x(x: f32, y: f32, sigma: f32, corner: f32, half_size: vec2<f32>) -> f32 {
            let delta = min(half_size.y - corner - abs(y), 0.0);
            let curved = half_size.x - corner + sqrt(max(0.0, corner * corner - delta * delta));
            let integral = 0.5 + 0.5 * shadow_erf((vec2<f32>(x, x) + vec2<f32>(-curved, curved)) * (0.70710678 / sigma));
            return integral.y - integral.x;
        }

        fn rounded_box_shadow(lower: vec2<f32>, upper: vec2<f32>, point: vec2<f32>, sigma: f32, corner_in: f32) -> f32 {
            let center = (lower + upper) * 0.5;
            let half_size = (upper - lower) * 0.5;
            let corner = min(corner_in, min(half_size.x, half_size.y));
            let p = point - center;
            let low = p.y - half_size.y;
            let high = p.y + half_size.y;
            let start = clamp(-3.0 * sigma, low, high);
            let end = clamp(3.0 * sigma, low, high);
            let step = (end - start) / 4.0;
            var y = start + step * 0.5;
            var value = 0.0;
            for (var i = 0; i < 4; i = i + 1) {
                let g = exp(-(y * y) / (2.0 * sigma * sigma)) / (2.50662827 * sigma);
                value = value + shadow_x(p.x, p.y - y, sigma, corner, half_size) * g * step;
                y = y + step;
            }
            return clamp(value, 0.0, 1.0);
        }

        @fragment
        fn fs(in: VertexOutput) -> @location(0) vec4<f32> {
            if (surface.dissolve > 0.0) {
                let dn = fract(sin(dot(floor(in.position.xy), vec2<f32>(12.9898, 78.233))) * 43758.5453);
                if (dn < surface.dissolve) { discard; }
            }
            var inst = instances[in.instance_idx];
            if (inst.shape_type == 1u) {
                // Circle fill with adaptive antialiasing
                let dx = in.position.x - inst.center.x;
                let dy = in.position.y - inst.center.y;
                let dist = sqrt(dx * dx + dy * dy) - inst.radius;
                let aa = adaptive_aa(dist);
                if (dist > aa) {
                    discard;
                }
                let coverage = 1.0 - smoothstep(0.0, aa, dist);
                return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
            }
            if (inst.shape_type == 2u) {
                let pos = in.position.xy;
                let v = inst.p1 - inst.p0;
                let len_sq = dot(v, v);
                let t = select(0.0, dot(pos - inst.p0, v) / len_sq, len_sq > 0.0);
                let clamped_t = clamp(t, 0.0, 1.0);
                return mix(inst.color0, inst.color1, clamped_t);
            }
            if (inst.shape_type == 3u) {
                // Ring (stroked circle) with adaptive antialiasing
                let dx = in.position.x - inst.center.x;
                let dy = in.position.y - inst.center.y;
                let dist = sqrt(dx * dx + dy * dy);
                let outer_dist = dist - inst.radius;
                let inner_dist = (inst.radius - inst.stroke_width) - dist;
                let outer_aa = adaptive_aa(outer_dist);
                let inner_aa = adaptive_aa(inner_dist);
                if (outer_dist > outer_aa || inner_dist > inner_aa) {
                    discard;
                }
                let outer_coverage = 1.0 - smoothstep(0.0, outer_aa, outer_dist);
                let inner_coverage = 1.0 - smoothstep(0.0, inner_aa, inner_dist);
                let coverage = min(outer_coverage, inner_coverage);
                return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
            }
            if (inst.shape_type == 4u) {
                // Line segment stroke with adaptive antialiasing
                let p = in.position.xy;
                let a = inst.p0;
                let b = inst.p1;
                let pa = p - a;
                let ba = b - a;
                let h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
                let dist = length(pa - ba * h);
                let half_sw = inst.stroke_width * 0.5;
                let aa = adaptive_aa(dist - half_sw);
                if (dist > half_sw + aa) {
                    discard;
                }
                let coverage = 1.0 - smoothstep(half_sw, half_sw + aa, dist);
                return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
            }
            if (inst.shape_type == 5u || inst.shape_type == 6u || inst.shape_type == 7u) {
                // Rounded rect (5=fill, 6=stroke, 7=gradient) with adaptive antialiasing
                let center = (inst.bounds_min + inst.bounds_max) * 0.5;
                let half_size = (inst.bounds_max - inst.bounds_min) * 0.5;
                let d = abs(in.position.xy - center) - half_size + vec2<f32>(inst.radius, inst.radius);
                let dist = length(max(d, vec2<f32>(0.0))) + min(max(d.x, d.y), 0.0) - inst.radius;
                let aa = adaptive_aa(dist);
                if (inst.shape_type == 5u || inst.shape_type == 7u) {
                    // Fill
                    if (dist > aa) {
                        discard;
                    }
                    let coverage = 1.0 - smoothstep(0.0, aa, dist);
                    if (inst.shape_type == 7u) {
                        let pos = in.position.xy;
                        let v = inst.p1 - inst.p0;
                        let len_sq = dot(v, v);
                        let t = select(0.0, dot(pos - inst.p0, v) / len_sq, len_sq > 0.0);
                        let clamped_t = clamp(t, 0.0, 1.0);
                        let grad = mix(inst.color0, inst.color1, clamped_t);
                        return vec4<f32>(grad.rgb, grad.a * coverage);
                    }
                    return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
                } else {
                    // Stroke
                    let abs_dist = abs(dist);
                    let half_sw = inst.stroke_width * 0.5;
                    let stroke_aa = adaptive_aa(abs_dist - half_sw);
                    if (abs_dist > half_sw + stroke_aa) {
                        discard;
                    }
                    let coverage = 1.0 - smoothstep(half_sw, half_sw + stroke_aa, abs_dist);
                    return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
                }
            }
            if (inst.shape_type == 9u) {
                // Drop shadow: p0..p1 = shadow rect, radius = corner, stroke_width = σ. The quad
                // (bounds) is the rect grown by 3σ and already clipped.
                let coverage = rounded_box_shadow(inst.p0, inst.p1, in.position.xy, inst.stroke_width, inst.radius);
                if (coverage <= 0.0) {
                    discard;
                }
                return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
            }
            if (inst.shape_type == 8u) {
                // Annular sector (pie/donut slice) with radial and angular antialiasing
                let dx = in.position.x - inst.center.x;
                let dy = in.position.y - inst.center.y;
                let dist = sqrt(dx * dx + dy * dy);
                let outer_r = inst.radius;
                let inner_r = inst.stroke_width;
                let outer_dist = dist - outer_r;
                let inner_dist = inner_r - dist;
                let outer_aa = adaptive_aa(outer_dist);
                let inner_aa = adaptive_aa(inner_dist);
                if (outer_dist > outer_aa || inner_dist > inner_aa) {
                    discard;
                }
                let outer_coverage = 1.0 - smoothstep(0.0, outer_aa, outer_dist);
                let inner_coverage = 1.0 - smoothstep(0.0, inner_aa, inner_dist);
                var coverage = min(outer_coverage, inner_coverage);
                // Angular test: p0.x = startAngle, p0.y = sweepAngle (both in radians)
                let angle = atan2(dy, dx);
                let start_angle = inst.p0.x;
                let sweep_angle = inst.p0.y;
                let end_angle = start_angle + sweep_angle;
                // Normalize the test angle into [start_angle, start_angle + 2π] range
                var test_angle = angle;
                if (sweep_angle > 0.0) {
                    while (test_angle < start_angle) {
                        test_angle = test_angle + 6.28318530718;
                    }
                } else {
                    while (test_angle > start_angle) {
                        test_angle = test_angle - 6.28318530718;
                    }
                }
                // Determine if inside the angular sweep and compute signed distance
                // to the nearest angular edge (positive = outside, negative = inside)
                var signed_angular_dist = 0.0;
                var in_sweep = false;
                if (sweep_angle > 0.0) {
                    in_sweep = test_angle >= start_angle && test_angle <= end_angle;
                } else {
                    in_sweep = test_angle <= start_angle && test_angle >= end_angle;
                }
                if (in_sweep) {
                    let dist_to_start = abs(test_angle - start_angle);
                    let dist_to_end = abs(test_angle - end_angle);
                    signed_angular_dist = -min(dist_to_start, dist_to_end);
                } else {
                    if (sweep_angle > 0.0) {
                        signed_angular_dist = max(start_angle - test_angle, test_angle - end_angle);
                    } else {
                        signed_angular_dist = max(test_angle - start_angle, end_angle - test_angle);
                    }
                }
                let signed_angular_dist_px = signed_angular_dist * dist;
                let angular_aa = adaptive_aa(signed_angular_dist_px);
                let angular_coverage = 1.0 - smoothstep(0.0, angular_aa, signed_angular_dist_px);
                if (angular_coverage <= 0.0) {
                    discard;
                }
                coverage = min(coverage, angular_coverage);
                return vec4<f32>(inst.color0.rgb, inst.color0.a * coverage);
            }
            return inst.color0;
        }
        """;

    private const string TextWgsl = """
        @group(0) @binding(0) var tex: texture_2d<f32>;
        @group(0) @binding(1) var samp: sampler;

        struct VertexOutput {
            @builtin(position) position: vec4<f32>,
            @location(0) uv: vec2<f32>,
        };

        @vertex
        fn vs(@location(0) pos: vec2<f32>, @location(1) uv: vec2<f32>) -> VertexOutput {
            return VertexOutput(vec4<f32>(pos, 0.0, 1.0), uv);
        }

        @fragment
        fn fs(in: VertexOutput) -> @location(0) vec4<f32> {
            return textureSample(tex, samp, in.uv);
        }
        """;

    private const string GlyphAtlasWgsl = """
        struct SurfaceSize {
            width: f32,
            height: f32,
            offset_x: f32,
            offset_y: f32,
            text_gamma: f32,
            light_weight: f32,
            dissolve: f32,
            pad2: f32,
        };

        struct GlyphInstance {
            pos: vec2<f32>,
            size: vec2<f32>,
            atlas_uv0: vec2<f32>,
            atlas_uv1: vec2<f32>,
            color: vec4<f32>,
            clip_min: vec2<f32>,
            clip_max: vec2<f32>,
        };

        @group(0) @binding(0) var<uniform> surface: SurfaceSize;
        @group(0) @binding(1) var atlas: texture_2d<f32>;
        @group(0) @binding(2) var atlas_sampler: sampler;
        @group(0) @binding(3) var bg_tex: texture_2d<f32>;
        @group(1) @binding(0) var<storage, read> instances: array<GlyphInstance>;

        fn lin_to_srgb(c: vec3<f32>) -> vec3<f32> {
            let lo = c * 12.92;
            let hi = 1.055 * pow(max(c, vec3<f32>(0.0)), vec3<f32>(1.0 / 2.4)) - 0.055;
            return select(lo, hi, c > vec3<f32>(0.0031308));
        }

        struct VsOut {
            @builtin(position) position: vec4<f32>,
            @location(0) uv: vec2<f32>,
            @location(1) @interpolate(flat) color: vec4<f32>,
            @location(2) @interpolate(flat) clip_min: vec2<f32>,
            @location(3) @interpolate(flat) clip_max: vec2<f32>,
        };

        const QUAD = array<vec2<f32>, 4>(
            vec2<f32>(0.0, 0.0),
            vec2<f32>(1.0, 0.0),
            vec2<f32>(0.0, 1.0),
            vec2<f32>(1.0, 1.0)
        );

        @vertex
        fn vs(@builtin(vertex_index) vi: u32, @builtin(instance_index) ii: u32) -> VsOut {
            let quad = QUAD[vi];
            let inst = instances[ii];
            let screen_pos = inst.pos + quad * inst.size;
            let ndc = vec2<f32>(
                screen_pos.x / surface.width * 2.0 - 1.0,
                1.0 - screen_pos.y / surface.height * 2.0
            );
            let uv = mix(inst.atlas_uv0, inst.atlas_uv1, quad);
            return VsOut(
                vec4<f32>(ndc, 0.0, 1.0),
                uv,
                inst.color,
                inst.clip_min,
                inst.clip_max
            );
        }

        @fragment
        fn fs(in: VsOut) -> @location(0) vec4<f32> {
            if (surface.dissolve > 0.0) {
                let dn = fract(sin(dot(floor(in.position.xy), vec2<f32>(12.9898, 78.233))) * 43758.5453);
                if (dn < surface.dissolve) { discard; }
            }
            if (in.clip_max.x > in.clip_min.x || in.clip_max.y > in.clip_min.y) {
                let screen_pos = in.position.xy;
                if (screen_pos.x < in.clip_min.x || screen_pos.x >= in.clip_max.x ||
                    screen_pos.y < in.clip_min.y || screen_pos.y >= in.clip_max.y) {
                    discard;
                }
            }
            // The swapchain is sRGB, so the hardware blends in linear light and
            // coverage IS the linear-correct blend factor (text_gamma == 0).
            // But linear-correct blending renders black-on-white text too LIGHT
            // (coverage 0.5 -> sRGB ~0.74), which reads washed-out at small
            // sizes. text_gamma > 0 selects a perceptual weight: we pre-distort
            // the coverage so the *displayed* black-on-white luminance becomes
            // (1-coverage)^text_gamma (1.0 ≈ sRGB weight, 1.5 ≈ macOS smoothing).
            // Unlike a contrast preblend this is monotonic, so no partial pixel
            // is pushed to fully on/off — it adds weight without adding aliasing.
            let cov = textureSample(atlas, atlas_sampler, in.uv).r;
            var weighted = cov;
            if (surface.text_gamma > 0.0) {
                // Perceptual ink fraction: darken the antialiased coverage so text
                // reads with correct weight (WP-3519).
                let pc = 1.0 - pow(max(1.0 - cov, 0.0), surface.text_gamma);

                // WP-3537: adapt the weight to the ACTUAL contrast between the glyph
                // and the local background (read from the framebuffer copy), so text
                // is correctly weighted on any background with no tuned constant. The
                // luminance comparison is in sRGB; bg_tex is sRGB-format so textureLoad
                // returns linear — convert it back.
                let bg_srgb = lin_to_srgb(textureLoad(bg_tex, vec2<i32>(i32(in.position.x), i32(in.position.y)), 0).rgb);
                let fg_lum = dot(in.color.rgb, vec3<f32>(0.2126, 0.7152, 0.0722));
                let bg_lum = dot(bg_srgb, vec3<f32>(0.2126, 0.7152, 0.0722));

                // Dark-on-light (fg darker than bg) keeps the full perceptual weight
                // (light-theme output unchanged). Light-on-dark blooms, so scale its
                // displayed weight toward the un-weighted linear coverage as contrast
                // grows. surface.light_weight is the adaptive strength (1 = full
                // adaptive, 0 = legacy symmetric weight).
                let rel = clamp(fg_lum - bg_lum, 0.0, 1.0);
                let w_light = mix(cov, pc, 1.0 - surface.light_weight * rel);

                // Map the target displayed weight to the HW linear-blend alpha per
                // polarity, then blend by the true (bg-aware) polarity.
                let dDark = 1.0 - pc;
                let aDark = 1.0 - select(dDark / 12.92, pow((dDark + 0.055) / 1.055, 2.4), dDark > 0.04045);
                let aLight = select(w_light / 12.92, pow((w_light + 0.055) / 1.055, 2.4), w_light > 0.04045);
                let polarity = smoothstep(-0.1, 0.1, fg_lum - bg_lum);
                weighted = mix(aDark, aLight, polarity);
            }
            let finalAlpha = in.color.a * weighted;
            return vec4<f32>(in.color.rgb * finalAlpha, finalAlpha);
        }
        """;

    private const string ColorGlyphAtlasWgsl = """
        struct SurfaceSize {
            width: f32,
            height: f32,
            offset_x: f32,
            offset_y: f32,
            text_gamma: f32,
            light_weight: f32,
            dissolve: f32,
            pad2: f32,
        };

        struct GlyphInstance {
            pos: vec2<f32>,
            size: vec2<f32>,
            atlas_uv0: vec2<f32>,
            atlas_uv1: vec2<f32>,
            color: vec4<f32>,
            clip_min: vec2<f32>,
            clip_max: vec2<f32>,
        };

        @group(0) @binding(0) var<uniform> surface: SurfaceSize;
        @group(0) @binding(1) var atlas: texture_2d<f32>;
        @group(0) @binding(2) var atlas_sampler: sampler;
        @group(1) @binding(0) var<storage, read> instances: array<GlyphInstance>;

        struct VsOut {
            @builtin(position) position: vec4<f32>,
            @location(0) uv: vec2<f32>,
            @location(1) @interpolate(flat) clip_min: vec2<f32>,
            @location(2) @interpolate(flat) clip_max: vec2<f32>,
        };

        const QUAD = array<vec2<f32>, 4>(
            vec2<f32>(0.0, 0.0),
            vec2<f32>(1.0, 0.0),
            vec2<f32>(0.0, 1.0),
            vec2<f32>(1.0, 1.0)
        );

        @vertex
        fn vs(@builtin(vertex_index) vi: u32, @builtin(instance_index) ii: u32) -> VsOut {
            let quad = QUAD[vi];
            let inst = instances[ii];
            let screen_pos = inst.pos + quad * inst.size;
            let ndc = vec2<f32>(
                screen_pos.x / surface.width * 2.0 - 1.0,
                1.0 - screen_pos.y / surface.height * 2.0
            );
            let uv = mix(inst.atlas_uv0, inst.atlas_uv1, quad);
            return VsOut(
                vec4<f32>(ndc, 0.0, 1.0),
                uv,
                inst.clip_min,
                inst.clip_max
            );
        }

        @fragment
        fn fs(in: VsOut) -> @location(0) vec4<f32> {
            if (surface.dissolve > 0.0) {
                let dn = fract(sin(dot(floor(in.position.xy), vec2<f32>(12.9898, 78.233))) * 43758.5453);
                if (dn < surface.dissolve) { discard; }
            }
            if (in.clip_max.x > in.clip_min.x || in.clip_max.y > in.clip_min.y) {
                let screen_pos = in.position.xy;
                if (screen_pos.x < in.clip_min.x || screen_pos.x >= in.clip_max.x ||
                    screen_pos.y < in.clip_min.y || screen_pos.y >= in.clip_max.y) {
                    discard;
                }
            }
            let texColor = textureSample(atlas, atlas_sampler, in.uv);
            // Atlas stores straight RGBA; pipeline blend is premultiplied (One, OneMinusSrcAlpha)
            return vec4<f32>(texColor.rgb * texColor.a, texColor.a);
        }
        """;

    // Frosted-glass backdrop blur: for each rounded-rect panel, Gaussian-blur the
    // framebuffer copy (bg_tex) behind it and tint the result. bg_tex is sRGB so
    // sampling returns linear; we blur in linear (correct) and output linear (the
    // sRGB target re-encodes on write, reproducing the backdrop). Premultiplied.
    private const string BackdropBlurWgsl = """
        struct SurfaceSize {
            width: f32,
            height: f32,
            offset_x: f32,
            offset_y: f32,
            text_gamma: f32,
            light_weight: f32,
            dissolve: f32,
            pad2: f32,
        };
        struct BlurInstance {
            bounds_min: vec2<f32>,
            bounds_max: vec2<f32>,
            tint: vec4<f32>,
            radius: f32,
            sigma: f32,
            pad0: f32,
            pad1: f32,
        };

        @group(0) @binding(0) var<uniform> surface: SurfaceSize;
        @group(0) @binding(1) var bg_tex: texture_2d<f32>;
        @group(0) @binding(2) var bg_sampler: sampler;
        @group(1) @binding(0) var<storage, read> instances: array<BlurInstance>;

        var<private> QUAD: array<vec2<f32>, 4> = array<vec2<f32>, 4>(
            vec2<f32>(0.0, 0.0), vec2<f32>(1.0, 0.0), vec2<f32>(0.0, 1.0), vec2<f32>(1.0, 1.0)
        );

        struct VsOut {
            @builtin(position) position: vec4<f32>,
            @location(0) @interpolate(flat) idx: u32,
        };

        fn srgb_to_lin(c: vec3<f32>) -> vec3<f32> {
            let lo = c / 12.92;
            let hi = pow((c + vec3<f32>(0.055)) / 1.055, vec3<f32>(2.4));
            return select(lo, hi, c > vec3<f32>(0.04045));
        }

        @vertex
        fn vs(@builtin(vertex_index) vi: u32, @builtin(instance_index) ii: u32) -> VsOut {
            let inst = instances[ii];
            let p = mix(inst.bounds_min, inst.bounds_max, QUAD[vi]);
            let ndc = vec2<f32>(p.x / surface.width * 2.0 - 1.0, 1.0 - p.y / surface.height * 2.0);
            return VsOut(vec4<f32>(ndc, 0.0, 1.0), ii);
        }

        @fragment
        fn fs(in: VsOut) -> @location(0) vec4<f32> {
            let inst = instances[in.idx];
            let pos = in.position.xy;

            // Rounded-rect coverage (SDF), like shape_type 5 in the geometry shader.
            let center = (inst.bounds_min + inst.bounds_max) * 0.5;
            let half_size = (inst.bounds_max - inst.bounds_min) * 0.5;
            let q = abs(pos - center) - half_size + vec2<f32>(inst.radius, inst.radius);
            let dist = length(max(q, vec2<f32>(0.0))) + min(max(q.x, q.y), 0.0) - inst.radius;
            if (dist > 1.0) {
                discard;
            }
            let coverage = 1.0 - smoothstep(-1.0, 1.0, dist);

            // 7x7 Gaussian tap of the framebuffer copy behind the panel.
            let sigma = max(inst.sigma, 0.5);
            let texel = vec2<f32>(1.0 / surface.width, 1.0 / surface.height);
            let step = sigma * 0.5;
            var acc = vec3<f32>(0.0);
            var wsum = 0.0;
            for (var j: i32 = -3; j <= 3; j = j + 1) {
                for (var i: i32 = -3; i <= 3; i = i + 1) {
                    let off = vec2<f32>(f32(i), f32(j)) * step;
                    let w = exp(-(off.x * off.x + off.y * off.y) / (2.0 * sigma * sigma));
                    let uv = (pos + off) * texel;
                    acc = acc + textureSampleLevel(bg_tex, bg_sampler, uv, 0.0).rgb * w;
                    wsum = wsum + w;
                }
            }
            let blurred = acc / max(wsum, 0.0001);

            // Tint over the blur (convert the sRGB tint to linear to match).
            let outc = mix(blurred, srgb_to_lin(inst.tint.rgb), inst.tint.a);
            return vec4<f32>(outc * coverage, coverage);
        }
        """;

    // Fullscreen quad: pos (x,y), uv (u,v)
    private static ReadOnlySpan<float> TextQuadVertices =>
    [
        -1.0f, -1.0f, 0.0f, 1.0f,
         1.0f, -1.0f, 1.0f, 1.0f,
        -1.0f,  1.0f, 0.0f, 0.0f,
         1.0f, -1.0f, 1.0f, 1.0f,
         1.0f,  1.0f, 1.0f, 0.0f,
        -1.0f,  1.0f, 0.0f, 0.0f,
    ];

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    private struct ShapeInstance
    {
        public float MinX, MinY;       // offset 0
        public float MaxX, MaxY;       // offset 8
        public float R0, G0, B0, A0;   // offset 16
        public float R1, G1, B1, A1;   // offset 32
        public float P0X, P0Y, P1X, P1Y; // offset 48
        public uint ShapeType;         // offset 64
        private uint _pad0;            // offset 68 (padding for vec2 alignment)
        public float CenterX, CenterY; // offset 72
        public float Radius;           // offset 80
        public float StrokeWidth;      // offset 84
        public float Expand;           // offset 88
        private float _pad3;           // offset 92

        /// <summary>
        /// Returns a copy shifted by (dx, dy) — every positional field (quad bounds and
        /// the SDF/gradient params P0/P1/Center) moves; colours, ShapeType, Radius,
        /// StrokeWidth, Expand and padding carry through automatically via <c>with</c>.
        /// RENDER-001: this is why layer compositing offsets instances through here
        /// rather than a hand-written field copy — adding a field can no longer drop it
        /// in the layer path (the original <see cref="Expand"/> blocky-corner bug).
        /// </summary>
        public readonly ShapeInstance Translated(float dx, float dy) => this with
        {
            MinX = MinX + dx, MinY = MinY + dy,
            MaxX = MaxX + dx, MaxY = MaxY + dy,
            P0X = P0X + dx, P0Y = P0Y + dy,
            P1X = P1X + dx, P1Y = P1Y + dy,
            CenterX = CenterX + dx, CenterY = CenterY + dy,
        };
    }

    /// <summary>
    /// The scroll delta to add to a retained layer's baked (absolute) instances when
    /// compositing: <c>offset − clipOrigin</c>. The cached instances already carry the
    /// ScrollView's on-screen origin (baked through the layer's InitialTransform), and so
    /// does the composite offset, so adding the whole offset would double-count it — the
    /// residual shift is just the scroll amount. Shared by the shape, glyph, and image
    /// composite paths so they can never disagree (RENDER-001).
    /// </summary>
    private static (float X, float Y) LayerScrollDelta(in LayerRenderInfo layer)
    {
        float dx = layer.OffsetX;
        float dy = layer.OffsetY;
        if (layer.ViewportClip is Cascade.UI.Rect clip)
        {
            dx -= clip.X;
            dy -= clip.Y;
        }
        return (dx, dy);
    }

    // Must stay in sync with the `aa` constant in the WGSL fragment shader.
    private const float SdfAntialiasBand = 1.5f;

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    private struct GlyphInstance
    {
        public float PosX, PosY;
        public float SizeX, SizeY;
        public float AtlasU0, AtlasV0;
        public float AtlasU1, AtlasV1;
        public float R, G, B, A;
        public float ClipMinX, ClipMinY;
        public float ClipMaxX, ClipMaxY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SurfaceSizeData
    {
        public float Width, Height;
        public float OffsetX, OffsetY;
        // WP-3519: glyph text-weight gamma. 0 = legacy pure-linear blend
        // (coverage IS the blend factor). >0 selects a perceptual weight where
        // the displayed black-on-white luminance is (1-coverage)^TextGamma —
        // 1.0 ≈ sRGB-space weight, 1.5 ≈ macOS "font smoothing". Padding keeps
        // the uniform struct 16-byte aligned.
        public float TextGamma;
        // WP-3537: light-on-dark weight factor. 0 = linear (no perceptual weight on
        // light text, which blooms), 1 = the full WP-3525 symmetric weight. Only the
        // light-on-dark side is scaled; dark-on-light keeps the full TextGamma weight.
        public float LightWeight;
        // Per-frame pixel-dissolve threshold [0,1]; fragments with screen-space hash
        // noise below this are discarded (see the WGSL fragment shaders). 0 = off.
        public float Dissolve;
        public float Pad2;
    }

    // Instance for the backdrop-blur pass. Layout matches the WGSL BlurInstance
    // (std430): vec2 min@0, vec2 max@8, vec4 tint@16, radius@32, sigma@36. 48 bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct BlurInstance
    {
        public float MinX, MinY;
        public float MaxX, MaxY;
        public float TintR, TintG, TintB, TintA;
        public float Radius, Sigma, Pad0, Pad1;
    }

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
    /// Per-frame pixel-dissolve threshold [0,1], copied from the backend each frame
    /// in <see cref="PresentScene"/> and uploaded into the surface uniform. Drives
    /// the screen-space dissolve discard in the geometry/glyph fragment shaders.
    /// </summary>
    internal float FrameDissolve { get; set; }

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

    /// <summary>
    /// WP-3526/3537: the single source of the contrast-adaptive perceptual text-weight
    /// curve, shared by both render paths. Maps a glyph's linear
    /// <paramref name="coverage"/> (0..1), its foreground luminance
    /// <paramref name="fgLum"/> and the local background luminance
    /// <paramref name="bgLum"/> (both sRGB 0..1) to the displayed ink fraction.
    /// Dark-on-light (fg darker than bg) gets the full perceptual weight 1-(1-c)^gamma;
    /// light-on-dark is scaled toward linear coverage as the contrast grows (light text
    /// blooms), by the adaptive <paramref name="strength"/> (1 = full adaptive). Because
    /// the weight is derived from the real contrast, there is no tuned constant.
    ///
    /// The CPU blitter blends in naïve sRGB and uses this value directly as the blend
    /// alpha (it reads the destination pixel as the background); the GPU shader reaches
    /// the same displayed result through a linear-space transform after destination
    /// sampling. Keep this in sync with the WGSL fragment shader so the paths never drift.
    /// </summary>
    internal static float AdaptiveInkCoverage(float coverage, float gamma, float fgLum, float bgLum, float strength)
    {
        if (gamma <= 0f)
        {
            return coverage;
        }
        float c = Math.Clamp(coverage, 0f, 1f);
        float pc = 1f - MathF.Pow(1f - c, gamma);                       // dark-on-light: full weight
        float rel = Math.Clamp(fgLum - bgLum, 0f, 1f);                  // light-on-dark contrast
        float wLight = c + (pc - c) * (1f - Math.Clamp(strength, 0f, 1f) * rel);
        float polarity = SmoothStep01(-0.1f, 0.1f, fgLum - bgLum);      // 0 dark-on-light → 1 light-on-dark
        return pc + (wLight - pc) * polarity;
    }

    private static float SmoothStep01(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

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

        // The surface comes first so the adapter request can insist on one that presents to it.
        nint hinstance = Win32.GetModuleHandleW(null);
        _surface = SurfaceFactory.CreateFromWin32(_instance, hwnd, hinstance, "CascadeUI");
        if (!_surface.IsValid)
        {
            _instance.Dispose();
            throw new InvalidOperationException("Failed to create surface from HWND");
        }

        var adapterResult = RequestAdapter(_instance, _surface, hwnd, preference);
        if (adapterResult.Status != RequestAdapterStatus.Success || adapterResult.Adapter.IsInvalid)
        {
            _surface.Dispose();
            _instance.Dispose();
            throw new InvalidOperationException($"No GPU adapter available: {adapterResult.Message ?? adapterResult.Status.ToString()}");
        }
        _adapter = adapterResult.Adapter;
        if (DebugLog.IsEnabled(DebugLogCategory.Present))
        {
            DebugLog.Write(DebugLogCategory.Present, $"[{DateTime.Now:O}] GPU adapter ({preference}): {_adapter.GetDescription()}");
        }

        // Etch's device defaults: allocator blocks sized for UI content and a descriptor heap sized for
        // Cascade's bind-group usage, instead of wgpu's game-sized defaults (~200 MB committed up front).
        DeviceDescriptor deviceDesc = default;
        ValidationBridge.ConfigureDeviceDescriptor(&deviceDesc);
        var deviceResult = AsyncRequest.RequestDeviceSync(_instance, _adapter, &deviceDesc);
        if (deviceResult.Status != RequestDeviceStatus.Success || deviceResult.Device.IsInvalid)
        {
            _adapter.Dispose();
            _surface.Dispose();
            _instance.Dispose();
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
        _swapChain = SwapChain.Configure(_device, _surface, swapChainConfig);

        // Geometry pipeline setup
        _geomShader = _device.CreateShaderModuleWgsl(GeometryWgsl, "Geometry");
        _geomUniformBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Uniform | BufferUsage.CopyDst),
            Size = (ulong)sizeof(SurfaceSizeData),
        });
        _geomStorageBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
            Size = 16,
        });
        _geomStorageCapacity = 0;
        _geomPipeline = BuildGeometryPipeline(out _geomPipelineLayout, out _geomBgLayout);
        UpdateGeometryBindGroup(0);

        // Text overlay pipeline setup
        _textShader = _device.CreateShaderModuleWgsl(TextWgsl, "Text");
        _textSampler = _device.CreateSampler(new SamplerDescriptor
        {
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Linear,
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            LodMinClamp = 0,
            LodMaxClamp = 32,
            MaxAnisotropy = 1,
        });
        _textVertexBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Vertex | BufferUsage.CopyDst),
            Size = (ulong)(TextQuadVertices.Length * sizeof(float)),
        });
        _device.Queue.WriteBuffer(_textVertexBuffer, 0, MemoryMarshal.AsBytes(TextQuadVertices));
        _textPipeline = BuildTextPipeline(out _textPipelineLayout, out _textBgLayout);

        // Glyph atlas text pipeline setup. Atlas sizes are a resident-memory budget (they exist for
        // the life of the window, hidden or not): 2048² R8 is 4 MB and holds thousands of UI glyphs;
        // when it fills it is reset and refilled (WasExhausted). 4096² cost 16 MB.
        _glyphAtlas = new GlyphAtlas(_device, GlyphAtlasSize, TextureFormat.R8Unorm, 128, maxPages: 1);
        _glyphAtlasView = _glyphAtlas.GetPage(0).Texture.CreateView();
        _glyphShader = _device.CreateShaderModuleWgsl(GlyphAtlasWgsl, "GlyphAtlas");
        _glyphSampler = _device.CreateSampler(new SamplerDescriptor
        {
            MagFilter = FilterMode.Nearest,
            MinFilter = FilterMode.Nearest,
            MipmapFilter = MipmapFilterMode.Nearest,
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            LodMinClamp = 0,
            LodMaxClamp = 32,
            MaxAnisotropy = 1,
        });
        _glyphUniformBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Uniform | BufferUsage.CopyDst),
            Size = (ulong)sizeof(SurfaceSizeData),
        });
        _glyphInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
            Size = 16,
        });
        _glyphInstanceCapacity = 0;
        _glyphPipeline = BuildGlyphPipeline(out _glyphPipelineLayout, out _glyphAtlasLayout, out _glyphInstanceLayout);

        EnsureBgCopyTexture(width, height);
        _glyphAtlasBindGroup = default;
        _glyphInstanceBindGroup = default;
        UpdateGlyphAtlasBindGroup();

        // Color glyph atlas for emoji: 1024² RGBA is 4 MB (4096² was 64 MB, for apps that may never
        // show an emoji). Resets when full like the text atlas.
        _colorGlyphAtlas = new GlyphAtlas(_device, ColorGlyphAtlasSize, TextureFormat.Rgba8UnormSrgb, 128, maxPages: 1);
        _colorGlyphAtlasView = _colorGlyphAtlas.GetPage(0).Texture.CreateView();
        _colorGlyphShader = _device.CreateShaderModuleWgsl(ColorGlyphAtlasWgsl, "ColorGlyphAtlas");
        _colorGlyphSampler = _device.CreateSampler(new SamplerDescriptor
        {
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Nearest,
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            LodMinClamp = 0,
            LodMaxClamp = 32,
            MaxAnisotropy = 1,
        });
        _colorGlyphAtlasBindGroup = default;
        _colorGlyphInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
            Size = 16,
        });
        _colorGlyphInstanceCapacity = 0;
        _colorGlyphPipeline = BuildColorGlyphPipeline(out _colorGlyphPipelineLayout);
        UpdateColorGlyphAtlasBindGroup();

        // Frosted-glass backdrop blur pipeline (samples the framebuffer copy).
        _blurShader = _device.CreateShaderModuleWgsl(BackdropBlurWgsl, "BackdropBlur");
        _blurSampler = _device.CreateSampler(new SamplerDescriptor
        {
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Nearest,
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            LodMinClamp = 0,
            LodMaxClamp = 0,
            MaxAnisotropy = 1,
        });
        _blurInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
            Size = 16,
        });
        _blurInstanceCapacity = 0;
        _blurPipeline = BuildBlurPipeline(out _blurPipelineLayout, out _blurBind0Layout, out _blurInstanceLayout);

        // Holds one quad (6 verts × 4 floats) per DrawImage op in the frame, each at its
        // own offset. A single shared slot would collapse every image onto the last one,
        // because Queue.WriteBuffer writes are all applied before the encoder's passes run
        // (last write wins) — fatal once a frame draws many images (e.g. cached icons).
        _imageVertexBuffer = _device.CreateBuffer(new BufferDescriptor
        {
            Usage = (ulong)(BufferUsage.Vertex | BufferUsage.CopyDst),
            Size = (ulong)(_imageQuadCapacity * ImageQuadFloats * sizeof(float)),
        });

        _compositor = new GpuCompositor(_device);

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

    public void Resize(uint width, uint height)
    {
        _currentWidth = width;
        _currentHeight = height;
        _swapChain.Resize(width, height);
        // WP-3537: the bg-copy texture tracks the swapchain size; rebuild it and the
        // glyph bind groups that reference its view.
        if (EnsureBgCopyTexture(width, height))
        {
            UpdateGlyphAtlasBindGroup();
            UpdateColorGlyphAtlasBindGroup();
        }
    }

    /// <summary>
    /// WP-3537: (re)creates the framebuffer-copy texture used for destination
    /// sampling, sized to the swapchain. Returns true if it was (re)created.
    /// </summary>
    private bool EnsureBgCopyTexture(uint width, uint height)
    {
        if (width == 0 || height == 0)
        {
            return false;
        }
        if (!_bgCopyTexture.IsInvalid && _bgCopyWidth == width && _bgCopyHeight == height)
        {
            return false;
        }
        if (!_bgCopyView.IsInvalid)
        {
            _bgCopyView.Dispose();
        }
        if (!_bgCopyTexture.IsInvalid)
        {
            _bgCopyTexture.Dispose();
        }
        _bgCopyTexture = _device.CreateTexture(new TextureDescriptor
        {
            Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
            Format = TextureFormat.Rgba8UnormSrgb, // matches the swapchain
            Usage = (ulong)(TextureUsage.TextureBinding | TextureUsage.CopyDst),
            Dimension = TextureDimension.D2,
            MipLevelCount = 1,
            SampleCount = 1,
        });
        _bgCopyView = _bgCopyTexture.CreateView();
        _bgCopyWidth = width;
        _bgCopyHeight = height;
        return true;
    }

    private static readonly bool GlyphDumpEnabled =
        DebugLog.IsEnabled(DebugLogCategory.Glyph);

    private static readonly bool SkipGlyphPass =
        Environment.GetEnvironmentVariable("CASCADE_SKIP_GLYPHS") == "1";

    // WP-3509 test hook: reset both glyph atlases at the start of every frame.
    // A reset re-rasterizes the whole frame, so output must be byte-identical
    // to a non-reset frame — the golden suite asserts exactly that, proving a
    // mid-session atlas reset yields a visually complete frame.
    private static readonly bool ForceAtlasReset =
        Environment.GetEnvironmentVariable("CASCADE_FORCE_ATLAS_RESET") == "1";

    // ── Animated font-size churn (WP-3509 decision) ─────────────────
    // A size animation (e.g. donut-gauge labels sweeping 9.9 → 19.8 px) emits
    // a distinct rasterFontSize per frame, each a new atlas entry. We do NOT
    // quantize animated sizes to collapse those entries, a decision made in
    // WP-3509: there is no animation-state signal in the presenter, so the only
    // options were (a) always rounding the rasterized size — which alters
    // static text at fractional DPI for a churn benefit the reset already
    // provides — or (b) per-run frame-to-frame size tracking, speculative state
    // for a case the generation reset already makes self-healing and
    // memory-bounded. A size *sweep* spans a range that no sub-pixel-grid
    // quantization can collapse without visibly stair-stepping the motion
    // anyway. So the atlas reset above is the whole policy: churn is bounded,
    // never fatal, and settled text always rasterizes at its exact size.

    /// <summary>Glyph atlas dimension in texels (page 0, monochrome atlas).</summary>
    internal int AtlasDimension => _glyphAtlas.Dimension;

    /// <summary>
    /// GPU readback of a monochrome glyph-atlas rect as RGBA pixels (coverage
    /// expanded to gray, alpha 255). Serves the <c>cascade_atlas</c> tool.
    /// Must run on the UI thread — it submits GPU queue work and maps a
    /// staging buffer synchronously. Width/height of 0 mean "to the atlas edge".
    /// Returns null when the readback fails (map timeout, device loss).
    /// </summary>
    internal unsafe AtlasRegionCapture? CaptureAtlasRegion(int u, int v, int width, int height)
    {
        int dim = _glyphAtlas.Dimension;
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
                    Texture = _glyphAtlas.GetPage(0).Texture.Handle,
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

    private static void LogGlyphRegion(int faceId, ushort glyphId, float fontSize, byte subpixelQuant, int pageIndex, global::Etch.Text.Atlas.AtlasRegion region)
    {
        DebugLog.Write(DebugLogCategory.Glyph,
            $"face={faceId} glyph={glyphId} size={fontSize} sub={subpixelQuant} page={pageIndex} u={region.U} v={region.V} w={region.W} h={region.H}");
    }

    private static void DumpGlyphBitmap(ReadOnlySpan<byte> bitmap, int w, int h, int faceId, ushort glyphId, float fontSize, byte subpixelQuant, int measuredW, int measuredH)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== face={faceId} glyph={glyphId} size={fontSize} sub={subpixelQuant} rasterized={w}x{h} measured={measuredW}x{measuredH} thread={Environment.CurrentManagedThreadId} ===");
        // Bitmap rows are stored bottom-up; print top-down.
        for (int row = h - 1; row >= 0; row--)
        {
            for (int col = 0; col < w; col++)
            {
                byte v = bitmap[row * w + col];
                sb.Append(v switch
                {
                    0 => '.',
                    < 64 => ':',
                    < 128 => '+',
                    < 192 => '#',
                    _ => '@',
                });
            }
            sb.AppendLine();
        }
        DebugLog.Write(DebugLogCategory.Glyph, sb.ToString().TrimEnd());
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

    private void EnsureFallbackTexture(uint width, uint height)
    {
        if (!_fallbackTextureView.IsInvalid)
        {
            _fallbackTextureView.Dispose();
        }
        if (!_fallbackTexture.IsInvalid)
        {
            _fallbackTexture.Dispose();
        }
        if (!_fallbackBindGroup.IsInvalid)
        {
            _fallbackBindGroup.Dispose();
        }

        // SceneCpuRenderer output is sRGB-encoded: an sRGB view decodes it on sample, so the sRGB
        // target re-encodes it once (a unorm view encoded it twice, washing colours out).
        _fallbackTexture = _device.CreateTexture(new TextureDescriptor
        {
            Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
            Format = TextureFormat.Rgba8UnormSrgb,
            Usage = (ulong)(TextureUsage.TextureBinding | TextureUsage.CopyDst),
            Dimension = TextureDimension.D2,
            MipLevelCount = 1,
            SampleCount = 1,
        });
        _fallbackTextureView = _fallbackTexture.CreateView();

        var bgEntries = stackalloc BindGroupEntry[2];
        bgEntries[0] = new BindGroupEntry { Binding = 0, TextureView = _fallbackTextureView.Handle };
        bgEntries[1] = new BindGroupEntry { Binding = 1, Sampler = _textSampler.Handle };
        _fallbackBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _textBgLayout.Handle,
            EntryCount = (UIntPtr)2,
            Entries = (nint)bgEntries,
        });
    }

    private void UpdateGeometryBindGroup(int instanceCount)
    {
        if (!_geomBindGroup.IsInvalid)
        {
            _geomBindGroup.Dispose();
        }

        var entries = stackalloc BindGroupEntry[2];
        entries[0] = new BindGroupEntry
        {
            Binding = 0,
            Buffer = (nint)_geomUniformBuffer.Handle,
            Size = (ulong)sizeof(SurfaceSizeData),
        };
        entries[1] = new BindGroupEntry
        {
            Binding = 1,
            Buffer = (nint)_geomStorageBuffer.Handle,
            Size = ulong.MaxValue,
        };
        _geomBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _geomBgLayout.Handle,
            EntryCount = (UIntPtr)2,
            Entries = (nint)entries,
        });
    }

    /// <summary>
    /// Returns true if the scene can be rendered by this presenter.
    /// Supports solid fills, strokes, 2-stop linear gradients, and clips.
    /// Silently skips unsupported ops rather than rejecting the entire scene.
    /// </summary>
    public static bool CanRenderGpu(SceneBuffer scene)
    {
        bool hasRenderableContent = false;
        foreach (ref readonly var cmd in scene.Commands)
        {
            switch (cmd.Op)
            {
                case SceneOpcode.FillRect:
                case SceneOpcode.FillPath:
                case SceneOpcode.StrokePath:
                case SceneOpcode.FillSector:
                case SceneOpcode.DrawShadow:
                case SceneOpcode.DrawImage:
                    hasRenderableContent = true;
                    continue;
                case SceneOpcode.SetTransform:
                case SceneOpcode.PushClip:
                case SceneOpcode.PopClip:
                case SceneOpcode.BeginFrame:
                case SceneOpcode.EndFrame:
                    continue;
                default:
                    return false;
            }
        }
        return hasRenderableContent;
    }

    /// <summary>
    /// Renders the frame to the swapchain in paint order. Shapes come from the scene buffer,
    /// glyph runs and images from the backend's command stream, and retained ScrollView layers are
    /// spliced in where their <c>DrawLayerTexture</c> op was painted (with their scroll offset baked
    /// into positions). <paramref name="sceneMarks"/> (see <see cref="EtchBackendProvider.SceneMarks"/>)
    /// is what lets text and images sit between the shapes painted before and after them; see
    /// <see cref="PaintOrderBatcher"/> for how the frame is batched so that costs little.
    /// </summary>
    public void PresentScene(SceneBuffer scene, EtchBackend backend, IReadOnlyList<int>? sceneMarks,
        List<LayerRenderInfo>? layers = null, List<GlyphDrawRecord>? glyphRecords = null)
    {
        if (_disposed)
        {
            return;
        }

        // Per-frame dissolve threshold set by the painter (dissolve transition).
        FrameDissolve = backend.FrameDissolve;

        // Drop cached layer instances for layers no longer present.
        _activeLayerHandles.Clear();
        if (layers != null)
        {
            foreach (var layer in layers)
            {
                _activeLayerHandles.Add(layer.LayerHandle);
            }
        }
        PruneLayerCache(_layerInstanceCache);
        PruneLayerCache(_layerStripCoverageCache);

        var frameSw = System.Diagnostics.Stopwatch.StartNew();

        var pollSw = System.Diagnostics.Stopwatch.StartNew();
        _device.Poll(false);
        pollSw.Stop();

        var acquireSw = System.Diagnostics.Stopwatch.StartNew();
        var status = _swapChain.AcquireFrame(out SurfaceTexture frame);
        acquireSw.Stop();
        if (status != SurfaceTextureResult.Ok || !frame.IsValid)
        {
            if (status == SurfaceTextureResult.Outdated || status == SurfaceTextureResult.Lost)
            {
                _swapChain.Resize(_currentWidth, _currentHeight);
            }
            frame.Dispose();
            return;
        }

        _phaseTimer.Restart();
        if (!ReferenceEquals(scene, _lastScene))
        {
            _lastScene = scene;
            BuildInstances(scene, _cachedInstances, sceneMarks, _mainInstanceAt, skipArbitraryPaths: _enableStripCoverage);
        }

        if (layers != null)
        {
            foreach (var layer in layers)
            {
                bool needsRebuild = !_layerInstanceCache.TryGetValue(layer.LayerHandle, out var cached)
                    || !ReferenceEquals(cached.Scene, layer.Scene);
                if (!needsRebuild)
                {
                    continue;
                }

                // Layers don't have strip-coverage — always use AABB fallback for arbitrary paths
                var rebuiltInstances = cached.Instances ?? new List<ShapeInstance>();
                var rebuiltInstanceAt = cached.InstanceAt ?? new List<int>();
                BuildInstances(layer.Scene, rebuiltInstances, layer.SceneMarks, rebuiltInstanceAt, skipArbitraryPaths: false);
                _layerInstanceCache[layer.LayerHandle] = (layer.Scene, rebuiltInstances, rebuiltInstanceAt);
                LogLayerInstances(layer, rebuiltInstances);
            }
        }
        _phaseTimer.Stop();
        double buildMs = _phaseTimer.Elapsed.TotalMilliseconds;

        _phaseTimer.Restart();
        SceneBuffer? stripScene = null;
        // Strip-coverage is the primary bottleneck during scroll.
        // Disable it until the architecture supports transform-independent caching.
        if (_enableStripCoverage)
        {
            int stripHash = ComputeStripCoverageHash(scene);
            if (stripHash != _stripCoverageHash || _stripCoverageScene is null || _stripCoverageScene.Commands.Length == 0)
            {
                _stripCoverageHash = stripHash;
                _stripCoverageScene = BuildStripCoverageScene(scene);
            }
            stripScene = _stripCoverageScene;
        }

        // WP-3509: if churn filled either glyph atlas last frame (or a forced
        // reset is requested), clear it now — between frames, before any glyph
        // is looked up or inserted this frame. Every glyph re-rasterizes from
        // the draw commands below, so the reset costs one frame of re-raster
        // and never leaves a stale UV. Resetting mid-build instead would
        // corrupt glyphs already placed earlier in this frame.
        if (ForceAtlasReset || _glyphAtlas.WasExhausted)
        {
            _glyphAtlas.Reset();
        }
        if (ForceAtlasReset || _colorGlyphAtlas.WasExhausted)
        {
            _colorGlyphAtlas.Reset();
        }

        // Glyph instances are rebuilt from the frame's glyph ops every frame (not cached), so
        // their atlas UVs are always fresh after LRU evictions caused by layer recapture.
        ScheduleFrame(backend, layers, glyphRecords, stripScene);
        UploadSchedule();
        UploadSurfaceSize(_currentWidth, _currentHeight, 0, 0);
        _phaseTimer.Stop();
        double uploadMs = _phaseTimer.Elapsed.TotalMilliseconds;

        using var encoder = _device.CreateCommandEncoder();

        _phaseTimer.Restart();
        int copies = EncodeSchedule(encoder, frame, stripScene);
        _phaseTimer.Stop();
        double renderMs = _phaseTimer.Elapsed.TotalMilliseconds;

        if (DebugLog.IsEnabled(DebugLogCategory.Clip) && _culledGlyphs > 0)
        {
            DebugLog.Write(DebugLogCategory.Clip,
                $"[{DateTime.Now:O}] culled={_culledGlyphs} glyphs outside their clip");
        }
        if (DebugLog.IsEnabled(DebugLogCategory.Frame) && _batcher.Count != _lastLoggedBatchCount)
        {
            _lastLoggedBatchCount = _batcher.Count;
            var batchList = new StringBuilder();
            foreach (var b in _batcher.Batches)
            {
                batchList.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $" {b.Kind}×{b.Count}@({b.MinX:F0},{b.MinY:F0})-({b.MaxX:F0},{b.MaxY:F0})");
            }
            DebugLog.Write(DebugLogCategory.Frame,
                $"[{DateTime.Now:O}] paint order: {_batcher.Count} batches, {copies} framebuffer copies, shapes={_frameShapes.Count} glyphs={_frameGlyphs.Count} colorGlyphs={_frameColorGlyphs.Count} images={_frameImages.Count} blurs={_frameBlurs.Count}:{batchList}");
        }

        using var cb = encoder.Finish();
        Span<CommandBuffer> cmdsSpan = stackalloc CommandBuffer[1];
        cmdsSpan[0] = cb;

        var submitSw = System.Diagnostics.Stopwatch.StartNew();
        _device.Queue.Submit(cmdsSpan);
        PollValidationErrors("PresentScene");
        submitSw.Stop();

        // Capture the framebuffer to CPU ONLY when a screenshot was requested.
        // PerformCapture does a synchronous full-framebuffer GPU→CPU readback
        // (MapSync polls the device to completion), which stalls the present
        // pipeline ~90 ms every frame — doing it unconditionally capped the whole
        // app to ~11 fps whenever anything presents continuously (a blinking caret,
        // a transition). On-demand keeps steady-state present at display rate.
        // Screenshot callers RequestCapture() + force a present + WaitForCapture().
        if (System.Threading.Interlocked.Exchange(ref _captureRequested, 0) == 1)
        {
            EnsureStagingBuffer();
            PerformCapture(frame);
            Cascade.UI.Diagnostics.PresentMonitor.MarkCapture();
        }

        var presentSw = System.Diagnostics.Stopwatch.StartNew();
        _swapChain.Present(frame);
        presentSw.Stop();
        Cascade.UI.Diagnostics.PresentMonitor.CpuRenderActive = false;
        Cascade.UI.Diagnostics.PresentMonitor.NotifyPresented();

        frameSw.Stop();
        double totalMs = frameSw.Elapsed.TotalMilliseconds;

        // Log first frame timing to diagnose startup delay
        if (_frameCount == 0 && DebugLog.IsEnabled(DebugLogCategory.Frame))
        {
            DebugLog.Write(DebugLogCategory.Frame,
                $"[{DateTime.Now:O}] Frame 0: total={totalMs:F2}ms poll={pollSw.ElapsedMilliseconds}ms acquire={acquireSw.ElapsedMilliseconds}ms build={buildMs:F2}ms schedule+upload={uploadMs:F2}ms encode={renderMs:F2}ms submit={submitSw.ElapsedMilliseconds}ms present={presentSw.ElapsedMilliseconds}ms shapes={_frameShapes.Count} batches={_batcher.Count}");
        }

        _totalFrameMs += totalMs;
        _totalBuildMs += buildMs;
        _totalUploadMs += uploadMs;
        _totalRenderMs += renderMs;
        _frameCount++;
    }

    private void PruneLayerCache<TValue>(Dictionary<ulong, TValue> cache)
    {
        if (cache.Count == 0)
        {
            return;
        }

        _staleLayerHandles.Clear();
        foreach (var key in cache.Keys)
        {
            if (!_activeLayerHandles.Contains(key))
            {
                _staleLayerHandles.Add(key);
            }
        }
        foreach (var key in _staleLayerHandles)
        {
            cache.Remove(key);
        }
    }

    private static void LogLayerInstances(in LayerRenderInfo layer, List<ShapeInstance> layerInstances)
    {
        if (!DebugLog.IsEnabled(DebugLogCategory.Instance) || layerInstances.Count == 0)
        {
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var inst in layerInstances)
        {
            if (inst.MinX < minX) { minX = inst.MinX; }
            if (inst.MinY < minY) { minY = inst.MinY; }
            if (inst.MaxX > maxX) { maxX = inst.MaxX; }
            if (inst.MaxY > maxY) { maxY = inst.MaxY; }
        }
        DebugLog.Write(DebugLogCategory.Instance,
            $"[{DateTime.Now:O}] Layer {layer.LayerHandle}: {layerInstances.Count} instances, bounds ({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}), offset ({layer.OffsetX:F1},{layer.OffsetY:F1})");
    }

    // ── Paint-order scheduling (see PaintOrderBatcher) ─────────────────────────

    /// <summary>One image quad: device rect plus the UV sub-rect that survives clipping.</summary>
    private struct ImageQuad
    {
        public int Handle;
        public float L, T, R, B;
        public float U0, V0, U1, V1;
    }

    private readonly PaintOrderBatcher _batcher = new();
    private int _lastLoggedBatchCount = -1;
    private int _culledGlyphs;

    // Paint-order lists, the batch-ordered copies made only when batching reordered them, and the
    // ranges that map one onto the other. All reused across frames.
    private readonly List<ShapeInstance> _frameShapes = new();
    private readonly List<ShapeInstance> _orderedShapes = new();
    private readonly List<DrawItem> _shapeItems = new();
    private readonly List<GlyphInstance> _frameGlyphs = new();
    private readonly List<GlyphInstance> _orderedGlyphs = new();
    private readonly List<DrawItem> _glyphItems = new();
    private readonly List<GlyphInstance> _frameColorGlyphs = new();
    private readonly List<GlyphInstance> _orderedColorGlyphs = new();
    private readonly List<DrawItem> _colorGlyphItems = new();
    private readonly List<ImageQuad> _frameImages = new();
    private readonly List<ImageQuad> _orderedImages = new();
    private readonly List<DrawItem> _imageItems = new();
    private readonly List<BlurInstance> _frameBlurs = new();
    private readonly List<BlurInstance> _orderedBlurs = new();
    private readonly List<DrawItem> _blurItems = new();

    // The image quads as uploaded (the paint-order list when no reordering was needed, else the
    // ordered copy) — the encoder binds each quad's texture from it.
    private List<ImageQuad> _uploadImages = null!;

    // Main-stream image replay state (transform and device-space clip stacks).
    private readonly Stack<Matrix3x2> _imageTransformStack = new();
    private readonly Stack<(float L, float T, float R, float B, bool Constrains)> _imageClipStack = new();

    // Reused per-frame bookkeeping for the layer caches.
    private readonly HashSet<ulong> _activeLayerHandles = new();
    private readonly List<ulong> _staleLayerHandles = new();

    // Shape-instance index before each backend command of the main stream (see BuildInstances).
    private readonly List<int> _mainInstanceAt = new();

    private float[] _imageVertexScratch = new float[64 * ImageQuadFloats];
    private const int ImageQuadFloats = 24; // 6 vertices × (x, y, u, v)

    /// <summary>
    /// Walks the frame in paint order — the main command stream with each retained layer spliced in
    /// at its <c>DrawLayerTexture</c> op — and places every shape instance, glyph run, image and
    /// backdrop blur into a batch (see <see cref="PaintOrderBatcher"/>).
    /// </summary>
    private void ScheduleFrame(EtchBackend backend, List<LayerRenderInfo>? layers,
        List<GlyphDrawRecord>? glyphRecords, SceneBuffer? stripScene)
    {
        _batcher.Reset();
        _frameShapes.Clear();
        _shapeItems.Clear();
        _frameGlyphs.Clear();
        _glyphItems.Clear();
        _frameColorGlyphs.Clear();
        _colorGlyphItems.Clear();
        _frameImages.Clear();
        _imageItems.Clear();
        _frameBlurs.Clear();
        _blurItems.Clear();
        _culledGlyphs = 0;
        _imageTransformStack.Clear();
        _imageClipStack.Clear();

        var commands = backend.Commands;
        var glyphs = backend.GlyphCommands;
        int shapeCursor = 0;
        int glyphCursor = 0;
        var currentTransform = Matrix3x2.Identity;

        for (int k = 0; k <= commands.Count; k++)
        {
            // Glyph runs painted before command k (after command k − 1).
            while (glyphCursor < glyphs.Count && glyphs[glyphCursor].CommandIndex <= k)
            {
                var op = glyphs[glyphCursor++];
                shapeCursor = EmitShapes(_cachedInstances, shapeCursor, InstanceAt(_mainInstanceAt, op.CommandIndex, _cachedInstances.Count));
                EmitGlyphRun(op, backend, 0, 0, null, glyphRecords, "main", DrawPaintOrder.MainGlyphRun(op.CommandIndex));
            }

            if (k == commands.Count)
            {
                break;
            }

            var cmd = commands[k];
            switch (cmd.Kind)
            {
                case EtchBackend.OpKind.PushTransform:
                    _imageTransformStack.Push(currentTransform);
                    currentTransform = cmd.Matrix * currentTransform;
                    break;

                case EtchBackend.OpKind.PopTransform:
                    if (_imageTransformStack.Count > 0)
                    {
                        currentTransform = _imageTransformStack.Pop();
                    }
                    break;

                // Active clip rects in DEVICE space. Images must be clamped to these (the image
                // shader has no scissor), otherwise an image straddling a clip edge — e.g. a list
                // row's icon at the viewport boundary — bleeds past it. Rounded clips use their
                // AABB (square corners), which still beats no clip at all. Path clips push a
                // non-constraining entry so PushClip/PopClip stay balanced.
                case EtchBackend.OpKind.PushClip:
                case EtchBackend.OpKind.PushClipRoundedRect:
                    {
                        var clipLocal = new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
                        var clipDev = clipLocal.Transform(EtchBackend.ToAffine(currentTransform));
                        _imageClipStack.Push((
                            (float)clipDev.MinX, (float)clipDev.MinY,
                            (float)clipDev.MaxX, (float)clipDev.MaxY, true));
                        break;
                    }

                case EtchBackend.OpKind.PushClipPath:
                    _imageClipStack.Push((0f, 0f, 0f, 0f, false)); // shape unknown: don't constrain
                    break;

                case EtchBackend.OpKind.PopClip:
                    if (_imageClipStack.Count > 0)
                    {
                        _imageClipStack.Pop();
                    }
                    break;

                case EtchBackend.OpKind.DrawImage:
                    shapeCursor = EmitShapes(_cachedInstances, shapeCursor, InstanceAt(_mainInstanceAt, k, _cachedInstances.Count));
                    EmitMainImage(backend, cmd, currentTransform);
                    break;

                case EtchBackend.OpKind.DrawBackdropBlur:
                    shapeCursor = EmitShapes(_cachedInstances, shapeCursor, InstanceAt(_mainInstanceAt, k, _cachedInstances.Count));
                    EmitBlur(cmd);
                    break;

                case EtchBackend.OpKind.DrawLayerTexture:
                    if (layers != null && TryFindLayer(layers, (ulong)cmd.W, out var layer))
                    {
                        shapeCursor = EmitShapes(_cachedInstances, shapeCursor, InstanceAt(_mainInstanceAt, k, _cachedInstances.Count));
                        EmitLayer(backend, layer, k, glyphRecords);
                    }
                    break;
            }
        }

        EmitShapes(_cachedInstances, shapeCursor, _cachedInstances.Count);

        // Strip coverage (arbitrary paths; disabled — see _enableStripCoverage) renders the whole
        // scene's complex paths in one compositor pass, so it cannot be interleaved; it is placed
        // as one full-frame draw at the end of the frame's paint order.
        if (stripScene is not null && stripScene.Commands.Length > 0)
        {
            int batch = _batcher.Place(DrawKind.Strip, 0, 0, _currentWidth, _currentHeight);
            _batcher.AddCount(batch, 1);
        }
    }

    private static bool TryFindLayer(List<LayerRenderInfo> layers, ulong handle, out LayerRenderInfo layer)
    {
        foreach (var candidate in layers)
        {
            if (candidate.LayerHandle == handle)
            {
                layer = candidate;
                return true;
            }
        }
        layer = default;
        return false;
    }

    /// <summary>
    /// Instances emitted before backend command <paramref name="commandIndex"/>. Without marks the
    /// table is empty and every glyph/image falls after all shapes — the old fixed-pass order.
    /// </summary>
    private static int InstanceAt(List<int> instanceAt, int commandIndex, int total)
        => commandIndex < instanceAt.Count ? instanceAt[commandIndex] : total;

    /// <summary>
    /// Composites one retained layer at its paint position: its shapes (scroll delta baked into
    /// the cached absolute positions, clipped to the viewport), glyph runs and images, interleaved
    /// in the layer's own paint order.
    /// </summary>
    private void EmitLayer(EtchBackend backend, in LayerRenderInfo layer, int mainIndex, List<GlyphDrawRecord>? glyphRecords)
    {
        if (!_layerInstanceCache.TryGetValue(layer.LayerHandle, out var cached))
        {
            return;
        }

        // The cached instances are baked at ABSOLUTE positions: BuildInstances replays the layer
        // scene through its InitialTransform, which already includes the ScrollView's on-screen
        // origin. The residual shift to apply is just the scroll delta (see LayerScrollDelta).
        var (dx, dy) = LayerScrollDelta(layer);
        var instances = cached.Instances;
        var instanceAt = cached.InstanceAt;
        var glyphs = layer.GlyphCommands;
        var images = layer.ImageCommands;
        int shapeCursor = 0;
        int glyphCursor = 0;
        int imageCursor = 0;

        while (glyphCursor < glyphs.Count || imageCursor < images.Count)
        {
            // A glyph run with CommandIndex c is painted before command c, so on a tie with the
            // image that IS command c, the run goes first.
            bool glyphNext = imageCursor >= images.Count
                || (glyphCursor < glyphs.Count && glyphs[glyphCursor].CommandIndex <= images[imageCursor].CommandIndex);
            if (glyphNext)
            {
                var op = glyphs[glyphCursor++];
                shapeCursor = EmitLayerShapes(instances, shapeCursor, InstanceAt(instanceAt, op.CommandIndex, instances.Count), dx, dy, layer.ViewportClip);
                EmitGlyphRun(op, backend, dx, dy, layer.ViewportClip, glyphRecords, "layer", DrawPaintOrder.LayerGlyphRun(mainIndex, op.CommandIndex));
            }
            else
            {
                var image = images[imageCursor++];
                shapeCursor = EmitLayerShapes(instances, shapeCursor, InstanceAt(instanceAt, image.CommandIndex, instances.Count), dx, dy, layer.ViewportClip);
                EmitLayerImage(backend, image, dx, dy, layer.ViewportClip);
            }
        }

        EmitLayerShapes(instances, shapeCursor, instances.Count, dx, dy, layer.ViewportClip);
    }

    /// <summary>Places main-stream shape instances [from, to) and returns <paramref name="to"/>.</summary>
    private int EmitShapes(List<ShapeInstance> source, int from, int to)
    {
        if (to <= from)
        {
            return Math.Max(from, to);
        }

        var span = CollectionsMarshal.AsSpan(source).Slice(from, to - from);

        // Nothing but shapes placed so far (e.g. a canvas frame, or the backgrounds at the start of
        // any frame): they all join the one shape batch, so skip per-instance placement.
        if (_batcher.OnlyShapes)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (ref readonly var inst in span)
            {
                minX = Math.Min(minX, inst.MinX - inst.Expand);
                minY = Math.Min(minY, inst.MinY - inst.Expand);
                maxX = Math.Max(maxX, inst.MaxX + inst.Expand);
                maxY = Math.Max(maxY, inst.MaxY + inst.Expand);
            }
            int batch = _batcher.Place(DrawKind.Shape, minX, minY, maxX, maxY);
            _batcher.Record(DrawKind.Shape, _shapeItems, batch, _frameShapes.Count, span.Length);
            _frameShapes.AddRange(span);
            return to;
        }

        foreach (ref readonly var inst in span)
        {
            EmitShape(inst);
        }
        return to;
    }

    /// <summary>
    /// Places retained-layer shape instances [from, to), shifted by the scroll delta and clipped
    /// to the layer viewport, and returns <paramref name="to"/>.
    /// </summary>
    private int EmitLayerShapes(List<ShapeInstance> source, int from, int to, float dx, float dy, Cascade.UI.Rect? viewportClip)
    {
        for (int i = from; i < to; i++)
        {
            // Shift every positional field by the scroll delta. Using Translated (not a
            // hand-written field copy) guarantees colours, ShapeType, Radius, StrokeWidth, Expand —
            // and any field added later — carry through (RENDER-001).
            var moved = source[i].Translated(dx, dy);

            // WP-3517: clip only the rasterization extent (quad bounds) to the layer's viewport so
            // scrolled-away content does not bleed outside the ScrollView. The SDF/gradient params
            // (P0/P1/Center) stay at their absolute positions, so the shape is clipped, not distorted.
            if (viewportClip is Cascade.UI.Rect vc)
            {
                float minX = Math.Max(moved.MinX, vc.X);
                float minY = Math.Max(moved.MinY, vc.Y);
                float maxX = Math.Min(moved.MaxX, vc.X + vc.Width);
                float maxY = Math.Min(moved.MaxY, vc.Y + vc.Height);
                if (maxX <= minX || maxY <= minY)
                {
                    continue;
                }
                moved = moved with { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
            }

            EmitShape(moved);
        }
        return Math.Max(from, to);
    }

    private void EmitShape(in ShapeInstance inst)
    {
        int batch = _batcher.Place(DrawKind.Shape,
            inst.MinX - inst.Expand, inst.MinY - inst.Expand, inst.MaxX + inst.Expand, inst.MaxY + inst.Expand);
        _batcher.Record(DrawKind.Shape, _shapeItems, batch, _frameShapes.Count, 1);
        _frameShapes.Add(inst);
    }

    /// <summary>
    /// Builds one glyph run's instances (mono and colour) and places each part by its bounds.
    /// </summary>
    private void EmitGlyphRun(EtchBackend.GlyphOp op, EtchBackend backend, float dx, float dy,
        Cascade.UI.Rect? viewportClip, List<GlyphDrawRecord>? glyphRecords, string category, long paintOrder)
    {
        int monoStart = _frameGlyphs.Count;
        int colorStart = _frameColorGlyphs.Count;
        BuildGlyphInstances(op, backend, _glyphAtlas.Dimension, _frameGlyphs, _frameColorGlyphs, dx, dy,
            ref _culledGlyphs, glyphRecords, category, paintOrder, viewportClip);

        PlaceGlyphs(DrawKind.Glyph, _frameGlyphs, monoStart, _glyphItems);
        PlaceGlyphs(DrawKind.ColorGlyph, _frameColorGlyphs, colorStart, _colorGlyphItems);
    }

    private void PlaceGlyphs(DrawKind kind, List<GlyphInstance> list, int start, List<DrawItem> items)
    {
        int count = list.Count - start;
        if (count <= 0)
        {
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (ref readonly var g in CollectionsMarshal.AsSpan(list).Slice(start, count))
        {
            float l = g.PosX, t = g.PosY, r = g.PosX + g.SizeX, b = g.PosY + g.SizeY;
            if (g.ClipMaxX > g.ClipMinX || g.ClipMaxY > g.ClipMinY)
            {
                l = Math.Max(l, g.ClipMinX);
                t = Math.Max(t, g.ClipMinY);
                r = Math.Min(r, g.ClipMaxX);
                b = Math.Min(b, g.ClipMaxY);
            }
            minX = Math.Min(minX, l);
            minY = Math.Min(minY, t);
            maxX = Math.Max(maxX, r);
            maxY = Math.Max(maxY, b);
        }

        int batch = _batcher.Place(kind, minX, minY, maxX, maxY);
        _batcher.Record(kind, items, batch, start, count);
    }

    private void EmitMainImage(EtchBackend backend, EtchBackend.SceneOp cmd, Matrix3x2 transform)
    {
        var img = backend.GetImage(cmd.ImageHandle);
        if (img == null)
        {
            return;
        }

        // Apply current transform to local destination rect. The root DPI-scale PushTransform is
        // part of the stream, so this is device-correct at any DPI.
        var localRect = new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
        var deviceRect = localRect.Transform(EtchBackend.ToAffine(transform));
        if (deviceRect.IsEmpty)
        {
            return;
        }

        // Clamp the quad (and its UVs) to the intersection of the active clip rects — same
        // technique the layer path uses for the viewport.
        float cl = float.NegativeInfinity, ct = float.NegativeInfinity;
        float cr = float.PositiveInfinity, cb = float.PositiveInfinity;
        foreach (var c in _imageClipStack)
        {
            if (!c.Constrains)
            {
                continue;
            }

            cl = Math.Max(cl, c.L); ct = Math.Max(ct, c.T);
            cr = Math.Min(cr, c.R); cb = Math.Min(cb, c.B);
        }

        bool clipped = !float.IsNegativeInfinity(cl);
        EmitImage((int)cmd.ImageHandle, img,
            (float)deviceRect.MinX, (float)deviceRect.MinY, (float)deviceRect.MaxX, (float)deviceRect.MaxY,
            clipped, cl, ct, cr, cb);
    }

    private void EmitLayerImage(EtchBackend backend, in LayerImageOp image, float offX, float offY, Cascade.UI.Rect? viewportClip)
    {
        var img = backend.GetImage(image.ImageHandle);
        if (img == null)
        {
            return;
        }

        // Bake through the per-image local→device transform (the layer's initial transform — DPI
        // scale + the ScrollView's on-screen origin — composed with any intra-layer
        // PushTransforms), then apply the scroll delta.
        var localRect = new EGeometry.Rect(image.X, image.Y, image.X + image.W, image.Y + image.H);
        var deviceRect = localRect.Transform(EtchBackend.ToAffine(image.Transform));
        if (deviceRect.IsEmpty)
        {
            return;
        }

        // Clamp to the viewport AND to whatever clips were in force around the image inside the
        // layer (RENDER-009). The captured clip is in the layer's own device space, so it scrolls
        // with the content and takes the same delta as the quad; the viewport does not, being the
        // fixed hole the layer shows through.
        float cl = float.NegativeInfinity, ct = float.NegativeInfinity;
        float cr = float.PositiveInfinity, cb = float.PositiveInfinity;
        if (image.Clip is Cascade.UI.Rect ic)
        {
            cl = ic.X + offX; ct = ic.Y + offY;
            cr = ic.X + ic.Width + offX; cb = ic.Y + ic.Height + offY;
        }
        if (viewportClip is Cascade.UI.Rect vc)
        {
            cl = Math.Max(cl, vc.X); ct = Math.Max(ct, vc.Y);
            cr = Math.Min(cr, vc.X + vc.Width); cb = Math.Min(cb, vc.Y + vc.Height);
        }

        bool clipped = !float.IsNegativeInfinity(cl) || !float.IsNegativeInfinity(ct)
            || !float.IsPositiveInfinity(cr) || !float.IsPositiveInfinity(cb);
        EmitImage((int)image.ImageHandle, img,
            (float)deviceRect.MinX + offX, (float)deviceRect.MinY + offY,
            (float)deviceRect.MaxX + offX, (float)deviceRect.MaxY + offY,
            clipped, cl, ct, cr, cb);
    }

    /// <summary>
    /// Places one image quad, clamping it and its UVs to the clip rect when <paramref name="clipped"/>
    /// (there is no GPU scissor in the wgpu binding set, so clipping is emulated this way).
    /// </summary>
    private void EmitImage(int handle, EtchBackend.ImageEntry img, float dl, float dt, float dr, float db,
        bool clipped, float cl, float ct, float cr, float cb)
    {
        float u0 = 0f, v0 = 0f, u1 = 1f, v1 = 1f;
        if (clipped)
        {
            // A degenerate intersection means the enclosing clips do not overlap at all, so
            // nothing inside them can draw. Skipping the clamp in that case would blit the image
            // unclipped — which is how table rows below the visible area once escaped.
            if (cr <= cl || cb <= ct)
            {
                return;
            }

            float nl = Math.Max(dl, cl), nt = Math.Max(dt, ct);
            float nr = Math.Min(dr, cr), nb = Math.Min(db, cb);
            if (nr <= nl || nb <= nt)
            {
                return; // fully outside the clip
            }

            float w = dr - dl, h = db - dt;
            u0 = (nl - dl) / w; u1 = (nr - dl) / w;
            v0 = (nt - dt) / h; v1 = (nb - dt) / h;
            dl = nl; dt = nt; dr = nr; db = nb;
        }

        EnsureImageTexture(handle, img);
        int batch = _batcher.Place(DrawKind.Image, dl, dt, dr, db);
        _batcher.Record(DrawKind.Image, _imageItems, batch, _frameImages.Count, 1);
        _frameImages.Add(new ImageQuad
        {
            Handle = handle,
            L = dl, T = dt, R = dr, B = db,
            U0 = u0, V0 = v0, U1 = u1, V1 = v1,
        });
    }

    /// <summary>
    /// Places one frosted-glass backdrop blur (device-space rounded rect, baked at emission). Its
    /// bounds include the blur's sampling reach, so everything it samples is drawn before it.
    /// </summary>
    private void EmitBlur(EtchBackend.SceneOp cmd)
    {
        if (!cmd.Fill.HasValue || cmd.W <= 0f || cmd.H <= 0f)
        {
            return;
        }

        var (tr, tg, tb, ta) = PaintColor.ToLinear(EtchBackend.ToArgb(cmd.Fill.Value));
        var blur = new BlurInstance
        {
            MinX = cmd.X, MinY = cmd.Y,
            MaxX = cmd.X + cmd.W, MaxY = cmd.Y + cmd.H,
            TintR = tr, TintG = tg, TintB = tb, TintA = ta,
            Radius = cmd.Radius, Sigma = cmd.StrokeWidth,
        };

        // The shader taps ±3 steps of σ/2 around each pixel (σ clamped to ≥ 0.5), plus one texel
        // of bilinear filtering.
        float reach = 1.5f * Math.Max(cmd.StrokeWidth, 0.5f) + 2f;
        int batch = _batcher.Place(DrawKind.Blur, blur.MinX - reach, blur.MinY - reach, blur.MaxX + reach, blur.MaxY + reach);
        _batcher.Record(DrawKind.Blur, _blurItems, batch, _frameBlurs.Count, 1);
        _frameBlurs.Add(blur);
    }

    /// <summary>
    /// Assigns each batch its range in its kind's upload buffer, orders every kind by batch, and
    /// uploads shapes, glyphs, image quads and blur instances — one buffer write per kind.
    /// </summary>
    private void UploadSchedule()
    {
        _batcher.AssignStarts();

        var shapes = _batcher.Order(DrawKind.Shape, _frameShapes, _shapeItems, _orderedShapes);
        EnsureGeometryBufferCapacity(shapes.Count);
        UploadInstances(shapes);

        var glyphs = _batcher.Order(DrawKind.Glyph, _frameGlyphs, _glyphItems, _orderedGlyphs);
        if (glyphs.Count > 0)
        {
            int requiredBytes = glyphs.Count * sizeof(GlyphInstance);
            if (_glyphInstanceCapacity < requiredBytes)
            {
                _glyphInstanceCapacity = Math.Max(requiredBytes, _glyphInstanceCapacity * 2);
                if (!_glyphInstanceBuffer.IsInvalid)
                {
                    _glyphInstanceBuffer.Dispose();
                }
                _glyphInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
                {
                    Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
                    Size = (ulong)_glyphInstanceCapacity,
                });
                UpdateGlyphInstanceBindGroup();
            }
            _device.Queue.WriteBuffer(_glyphInstanceBuffer, 0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(glyphs)));
        }

        var colorGlyphs = _batcher.Order(DrawKind.ColorGlyph, _frameColorGlyphs, _colorGlyphItems, _orderedColorGlyphs);
        if (colorGlyphs.Count > 0)
        {
            int requiredBytes = colorGlyphs.Count * sizeof(GlyphInstance);
            if (_colorGlyphInstanceCapacity < requiredBytes)
            {
                _colorGlyphInstanceCapacity = Math.Max(requiredBytes, _colorGlyphInstanceCapacity * 2);
                if (!_colorGlyphInstanceBuffer.IsInvalid)
                {
                    _colorGlyphInstanceBuffer.Dispose();
                }
                _colorGlyphInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
                {
                    Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
                    Size = (ulong)_colorGlyphInstanceCapacity,
                });
                UpdateColorGlyphInstanceBindGroup();
            }
            _device.Queue.WriteBuffer(_colorGlyphInstanceBuffer, 0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(colorGlyphs)));
        }

        _uploadImages = _batcher.Order(DrawKind.Image, _frameImages, _imageItems, _orderedImages);
        if (_uploadImages.Count > 0)
        {
            UploadImageQuads(_uploadImages);
        }

        var blurs = _batcher.Order(DrawKind.Blur, _frameBlurs, _blurItems, _orderedBlurs);
        if (blurs.Count > 0)
        {
            int byteSize = blurs.Count * sizeof(BlurInstance);
            if (blurs.Count > _blurInstanceCapacity)
            {
                _blurInstanceBuffer.Dispose();
                _blurInstanceBuffer = _device.CreateBuffer(new BufferDescriptor
                {
                    Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
                    Size = (ulong)Math.Max(byteSize, 16),
                });
                _blurInstanceCapacity = blurs.Count;
            }
            _device.Queue.WriteBuffer(_blurInstanceBuffer, 0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(blurs)));
        }
    }

    /// <summary>
    /// Writes every image quad of the frame (device rect → clip space, with its UV sub-rect) into
    /// the image vertex buffer in one write; quad <c>i</c> occupies vertices [6i, 6i+6).
    /// </summary>
    private void UploadImageQuads(List<ImageQuad> quads)
    {
        int floats = quads.Count * ImageQuadFloats;
        if (_imageVertexScratch.Length < floats)
        {
            _imageVertexScratch = new float[Math.Max(floats, _imageVertexScratch.Length * 2)];
        }
        if (quads.Count > _imageQuadCapacity)
        {
            _imageVertexBuffer.Dispose();
            _imageQuadCapacity = Math.Max(quads.Count, _imageQuadCapacity * 2);
            _imageVertexBuffer = _device.CreateBuffer(new BufferDescriptor
            {
                Usage = (ulong)(BufferUsage.Vertex | BufferUsage.CopyDst),
                Size = (ulong)(_imageQuadCapacity * ImageQuadFloats * sizeof(float)),
            });
        }

        var verts = _imageVertexScratch.AsSpan(0, floats);
        int o = 0;
        foreach (ref readonly var q in CollectionsMarshal.AsSpan(quads))
        {
            float l = (float)(q.L / _currentWidth * 2.0 - 1.0);
            float r = (float)(q.R / _currentWidth * 2.0 - 1.0);
            float t = (float)(1.0 - q.T / _currentHeight * 2.0);
            float b = (float)(1.0 - q.B / _currentHeight * 2.0);
            // Triangle 1
            verts[o] = l; verts[o + 1] = t; verts[o + 2] = q.U0; verts[o + 3] = q.V0;
            verts[o + 4] = r; verts[o + 5] = t; verts[o + 6] = q.U1; verts[o + 7] = q.V0;
            verts[o + 8] = l; verts[o + 9] = b; verts[o + 10] = q.U0; verts[o + 11] = q.V1;
            // Triangle 2
            verts[o + 12] = r; verts[o + 13] = t; verts[o + 14] = q.U1; verts[o + 15] = q.V0;
            verts[o + 16] = r; verts[o + 17] = b; verts[o + 18] = q.U1; verts[o + 19] = q.V1;
            verts[o + 20] = l; verts[o + 21] = b; verts[o + 22] = q.U0; verts[o + 23] = q.V1;
            o += ImageQuadFloats;
        }
        _device.Queue.WriteBuffer(_imageVertexBuffer, 0, MemoryMarshal.AsBytes(verts));
    }

    /// <summary>
    /// Records the scheduled batches into as few render passes as possible: one pass, broken only
    /// where a mono-glyph or backdrop-blur batch needs a fresh copy of the framebuffer under it.
    /// Returns the number of framebuffer copies made.
    /// </summary>
    private int EncodeSchedule(CommandEncoder encoder, SurfaceTexture frame, SceneBuffer? stripScene)
    {
        int copies = 0;
        BindGroup blurBind0 = default;
        BindGroup blurInstanceBind = default;
        var pass = BeginFramePass(encoder, frame, clear: true);
        try
        {
            DrawKind? bound = null;
            foreach (ref readonly var batch in _batcher.Batches)
            {
                if (batch.Count == 0)
                {
                    continue;
                }

                // WP-3537: mono glyphs read the local background from a copy of the framebuffer
                // for their contrast-adaptive weight; a blur samples it. Both need the copy to hold
                // everything drawn before their batch, so end the pass, copy the batch's region
                // and continue in a new pass.
                if ((batch.Kind == DrawKind.Glyph && !SkipGlyphPass) || batch.Kind == DrawKind.Blur)
                {
                    if (CanCopyBackground())
                    {
                        pass.End();
                        pass.Dispose();
                        CopyFramebufferRegion(encoder, frame, batch.MinX, batch.MinY, batch.MaxX, batch.MaxY);
                        copies++;
                        pass = BeginFramePass(encoder, frame, clear: false);
                        bound = null;
                    }
                    else if (batch.Kind == DrawKind.Blur)
                    {
                        continue; // nothing valid to blur
                    }
                }

                switch (batch.Kind)
                {
                    case DrawKind.Shape:
                        if (bound != DrawKind.Shape)
                        {
                            pass.SetPipeline(_geomPipeline);
                            pass.SetBindGroup(0, _geomBindGroup);
                        }
                        pass.Draw(4, (uint)batch.Count, 0, (uint)batch.Start);
                        break;

                    case DrawKind.Glyph:
                        if (SkipGlyphPass)
                        {
                            continue;
                        }
                        if (bound != DrawKind.Glyph)
                        {
                            pass.SetPipeline(_glyphPipeline);
                            pass.SetBindGroup(0, _glyphAtlasBindGroup);
                            pass.SetBindGroup(1, _glyphInstanceBindGroup);
                        }
                        pass.Draw(4, (uint)batch.Count, 0, (uint)batch.Start);
                        break;

                    case DrawKind.ColorGlyph:
                        if (SkipGlyphPass)
                        {
                            continue;
                        }
                        if (bound != DrawKind.ColorGlyph)
                        {
                            pass.SetPipeline(_colorGlyphPipeline);
                            pass.SetBindGroup(0, _colorGlyphAtlasBindGroup);
                            pass.SetBindGroup(1, _colorGlyphInstanceBindGroup);
                        }
                        pass.Draw(4, (uint)batch.Count, 0, (uint)batch.Start);
                        break;

                    case DrawKind.Image:
                        if (bound != DrawKind.Image)
                        {
                            pass.SetPipeline(_textPipeline);
                            pass.SetVertexBuffer(0, _imageVertexBuffer, 0, (ulong)(_uploadImages.Count * ImageQuadFloats * sizeof(float)));
                        }
                        for (int i = batch.Start; i < batch.Start + batch.Count; i++)
                        {
                            pass.SetBindGroup(0, _imageBindGroups[_uploadImages[i].Handle]);
                            pass.Draw(6, 1, (uint)(i * 6), 0);
                        }
                        break;

                    case DrawKind.Blur:
                        if (blurBind0.IsInvalid)
                        {
                            CreateBlurBindGroups(out blurBind0, out blurInstanceBind);
                        }
                        if (bound != DrawKind.Blur)
                        {
                            pass.SetPipeline(_blurPipeline);
                            pass.SetBindGroup(0, blurBind0);
                            pass.SetBindGroup(1, blurInstanceBind);
                        }
                        pass.Draw(4, (uint)batch.Count, 0, (uint)batch.Start);
                        break;

                    case DrawKind.Strip:
                        if (stripScene is not null)
                        {
                            _ = _compositor.RecordRenderPass(pass, stripScene, (int)_currentWidth, (int)_currentHeight);
                        }
                        bound = null; // the compositor binds its own pipelines
                        continue;
                }
                bound = batch.Kind;
            }
        }
        finally
        {
            pass.End();
            pass.Dispose();
            if (!blurBind0.IsInvalid)
            {
                blurBind0.Dispose();
            }
            if (!blurInstanceBind.IsInvalid)
            {
                blurInstanceBind.Dispose();
            }
        }
        return copies;
    }

    private static unsafe RenderPass BeginFramePass(CommandEncoder encoder, SurfaceTexture frame, bool clear)
    {
        var colorAttachment = new RenderPassColorAttachment
        {
            View = (nint)frame.View,
            DepthSlice = 0xFFFFFFFFu,
            LoadOp = clear ? LoadOp.Clear : LoadOp.Load,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = 0, G = 0, B = 0, A = 1 },
        };
        var passDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (UIntPtr)1,
            ColorAttachments = (nint)(&colorAttachment),
        };
        return encoder.BeginRenderPass(passDesc);
    }

    private bool CanCopyBackground()
        => !_bgCopyTexture.IsInvalid && _bgCopyWidth == _currentWidth && _bgCopyHeight == _currentHeight;

    /// <summary>
    /// Copies the device-pixel region (rounded out, clamped to the frame) of the framebuffer into
    /// <see cref="_bgCopyTexture"/> at the same position.
    /// </summary>
    private unsafe void CopyFramebufferRegion(CommandEncoder encoder, SurfaceTexture frame,
        float minX, float minY, float maxX, float maxY)
    {
        uint x0 = (uint)Math.Clamp(MathF.Floor(minX), 0f, _currentWidth);
        uint y0 = (uint)Math.Clamp(MathF.Floor(minY), 0f, _currentHeight);
        uint x1 = (uint)Math.Clamp(MathF.Ceiling(maxX), 0f, _currentWidth);
        uint y1 = (uint)Math.Clamp(MathF.Ceiling(maxY), 0f, _currentHeight);
        if (x1 <= x0 || y1 <= y0)
        {
            return;
        }

        var origin = new WGPUOrigin3D { X = x0, Y = y0, Z = 0 };
        var extent = new Extent3D { Width = x1 - x0, Height = y1 - y0, DepthOrArrayLayers = 1 };
        var copySrc = new WGPUTexelCopyTextureInfo { Aspect = (uint)TextureAspect.All, MipLevel = 0, Origin = origin, Texture = frame.Texture };
        var copyDst = new WGPUTexelCopyTextureInfo { Aspect = (uint)TextureAspect.All, MipLevel = 0, Origin = origin, Texture = _bgCopyTexture.Handle };
        WebGPU.CommandEncoderCopyTextureToTexture(encoder.Handle, (nint)(&copySrc), (nint)(&copyDst), (nint)(&extent));
    }

    private unsafe void CreateBlurBindGroups(out BindGroup bind0, out BindGroup instanceBind)
    {
        int byteSize = Math.Max(_frameBlurs.Count * sizeof(BlurInstance), 16);
        var bind0Entries = stackalloc BindGroupEntry[3];
        bind0Entries[0] = new BindGroupEntry { Binding = 0, Buffer = _geomUniformBuffer.Handle, Offset = 0, Size = (ulong)sizeof(SurfaceSizeData) };
        bind0Entries[1] = new BindGroupEntry { Binding = 1, TextureView = _bgCopyView.Handle };
        bind0Entries[2] = new BindGroupEntry { Binding = 2, Sampler = _blurSampler.Handle };
        bind0 = _device.CreateBindGroup(new BindGroupDescriptor { Layout = _blurBind0Layout.Handle, EntryCount = (UIntPtr)3, Entries = (nint)bind0Entries });

        var instEntries = stackalloc BindGroupEntry[1];
        instEntries[0] = new BindGroupEntry { Binding = 0, Buffer = _blurInstanceBuffer.Handle, Offset = 0, Size = (ulong)byteSize };
        instanceBind = _device.CreateBindGroup(new BindGroupDescriptor { Layout = _blurInstanceLayout.Handle, EntryCount = (UIntPtr)1, Entries = (nint)instEntries });
    }

    /// <summary>
    /// Blits a CPU-rasterized pixel buffer directly to the swapchain.
    /// Used as a fallback when the scene contains unsupported GPU ops.
    /// </summary>
    public void PresentCpuFallback(byte[] pixels, uint srcWidth, uint srcHeight)
    {
        if (_disposed)
        {
            return;
        }

        _device.Poll(false);

        var status = _swapChain.AcquireFrame(out SurfaceTexture frame);
        if (status != SurfaceTextureResult.Ok || !frame.IsValid)
        {
            if (status == SurfaceTextureResult.Outdated || status == SurfaceTextureResult.Lost)
            {
                _swapChain.Resize(_currentWidth, _currentHeight);
            }
            frame.Dispose();
            return;
        }

        EnsureFallbackTexture(srcWidth, srcHeight);

        var origin = new WGPUOrigin3D { X = 0, Y = 0, Z = 0 };
        var writeSize = new Extent3D { Width = srcWidth, Height = srcHeight, DepthOrArrayLayers = 1 };
        _device.Queue.WriteTexture(_fallbackTexture, 0, origin, pixels, srcWidth * 4, srcHeight, writeSize);

        using var encoder = _device.CreateCommandEncoder();

        var colorAttachment = new RenderPassColorAttachment
        {
            View = (nint)frame.View,
            DepthSlice = 0xFFFFFFFFu,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = 0, G = 0, B = 0, A = 1 },
        };
        var passDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = (UIntPtr)1,
            ColorAttachments = (nint)(&colorAttachment),
        };

        using (var pass = encoder.BeginRenderPass(passDesc))
        {
            pass.SetPipeline(_textPipeline);
            pass.SetVertexBuffer(0, _textVertexBuffer, 0, (ulong)(TextQuadVertices.Length * sizeof(float)));
            pass.SetBindGroup(0, _fallbackBindGroup);
            pass.Draw(6);
            pass.End();
        }

        using var cb = encoder.Finish();
        Span<CommandBuffer> cmdsSpan = stackalloc CommandBuffer[1];
        cmdsSpan[0] = cb;
        _device.Queue.Submit(cmdsSpan);
        PollValidationErrors("PresentCpuFallback");

        // Capture to CPU only on request (see PresentScene) — the readback stalls
        // the present, so it must not run every frame.
        if (System.Threading.Interlocked.Exchange(ref _captureRequested, 0) == 1)
        {
            EnsureStagingBuffer();
            PerformCapture(frame);
            Cascade.UI.Diagnostics.PresentMonitor.MarkCapture();
        }

        _swapChain.Present(frame);
        Cascade.UI.Diagnostics.PresentMonitor.CpuRenderActive = true;
        Cascade.UI.Diagnostics.PresentMonitor.NotifyPresented();
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
        int bufferCount = 7 + (_imageTextures.Count > 0 ? 1 : 0);
        if (!_glyphInstanceBuffer.IsInvalid) { bufferCount++; }

        int textureCount = 2 + _imageTextures.Count;
        int textureViewCount = 2 + _imageTextureViews.Count + _imageBindGroups.Count;
        int bindGroupCount = 2 + _imageBindGroups.Count;

        return new NativeMemorySnapshot
        {
            Version = 1,
            CountersFeatureEnabled = true,

            DeviceCount = 1,
            SurfaceCount = 1,
            SceneFrameCount = (ulong)_frameCount,

            SurfaceIntermediateBytes = (ulong)(_currentWidth * _currentHeight * 4),
            SurfaceSwapchainBytesEst = (ulong)(_currentWidth * _currentHeight * 4 * 2),

            WgpuShaderModules = 3,
            WgpuRenderPipelines = 3,
            WgpuBuffers = (ulong)bufferCount,
            WgpuTextures = (ulong)textureCount,
            WgpuTextureViews = (ulong)textureViewCount,
            WgpuBindGroups = (ulong)bindGroupCount,
        };
    }

    /// <summary>
    /// Requests that the next presented frame be captured to CPU memory.
    /// Capture happens during the next PresentScene/PresentCpuFallback call.
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

    private void BuildGlyphInstances(EtchBackend.GlyphOp cmd, EtchBackend backend, float atlasDim, List<GlyphInstance> instances, List<GlyphInstance> colorInstances, float offsetX, float offsetY, ref int culledGlyphs, List<GlyphDrawRecord>? records, string recordCategory, long paintOrder, Cascade.UI.Rect? viewportClip)
    {
        // Rasterize glyphs at physical pixel size so the bitmap is 1:1 with the
        // screen.  cmd.ScaleX/Y carry the DPI scale from DrawGlyphs; using them
        // here eliminates the 1.5× nearest-filter upscale that caused pixelation.
        float rasterScale = Math.Max(cmd.ScaleX, cmd.ScaleY);
        float rasterFontSize = cmd.FontSize * rasterScale;

        // Determine active clip bounds for glyph culling.  Glyphs whose quad
        // falls entirely outside the clip region are skipped.  This prevents
        // text from scrolling outside controls like DataGrid that apply their
        // own internal clip (PushClip) for virtualized content.
        // When rendering layer glyphs, offsetX/Y shifts glyph positions to
        // composited coordinates — the clip bounds must be shifted too.
        bool emptyClip = cmd.HasClipBounds && (cmd.ClipBounds.Width <= 0 || cmd.ClipBounds.Height <= 0);
        if (emptyClip)
        {
            culledGlyphs += cmd.GlyphIds.Length;
            return;
        }
        bool hasClip = cmd.HasClipBounds && cmd.ClipBounds.Width > 0 && cmd.ClipBounds.Height > 0;
        float clipMinX = hasClip ? cmd.ClipBounds.X + offsetX : float.MinValue;
        float clipMinY = hasClip ? cmd.ClipBounds.Y + offsetY : float.MinValue;
        float clipMaxX = hasClip ? cmd.ClipBounds.X + cmd.ClipBounds.Width + offsetX : float.MaxValue;
        float clipMaxY = hasClip ? cmd.ClipBounds.Y + cmd.ClipBounds.Height + offsetY : float.MaxValue;

        // WP-3517: intersect the layer's compositing-time viewport clip (already
        // in screen space, like the glyph positions) so layer text does not
        // bleed outside the ScrollView. Applies even to glyphs with no content
        // clip of their own.
        if (viewportClip is Cascade.UI.Rect vc)
        {
            clipMinX = Math.Max(clipMinX, vc.X);
            clipMinY = Math.Max(clipMinY, vc.Y);
            clipMaxX = Math.Min(clipMaxX, vc.X + vc.Width);
            clipMaxY = Math.Min(clipMaxY, vc.Y + vc.Height);
            hasClip = true;
        }

        var face = backend.GetOrCreateFontFace(cmd.FontHandle, rasterFontSize);
        if (face == null)
        {
            return;
        }

        int faceId = (int)cmd.FontHandle;
        float colorR = cmd.Color.R;
        float colorG = cmd.Color.G;
        float colorB = cmd.Color.B;
        float colorA = cmd.Color.A;

        face.TryGetGlyph(0x0020, out uint spaceGid);

        for (int i = 0; i < cmd.GlyphIds.Length; i++)
        {
            ushort glyphId = cmd.GlyphIds[i];
            if (glyphId == spaceGid)
            {
                continue;
            }

            float gx = cmd.Positions[i * 2] + offsetX;
            float gy = cmd.Positions[i * 2 + 1] + offsetY;

            byte subpixelQuant = GlyphPlacement.SubpixelBucket(gx);

            // Check if this is a color glyph (COLR/CPAL emoji)
            bool isColorGlyph = GlyphOutlineBuilder.HasColorLayers(face, glyphId);

            if (isColorGlyph)
            {
                var colorKey = global::Etch.Text.Atlas.GlyphCacheKey.FromSizeAndSubpixel(rasterFontSize, faceId, glyphId, subpixelQuant);
                bool colorCacheHit = _colorGlyphAtlas.TryLookup(colorKey, out var colorRegion, out int colorPageIndex);
                if (!colorCacheHit)
                {
                    byte[]? rgbaRented = ArrayPool<byte>.Shared.Rent(256 * 256 * 4);
                    try
                    {
                        if (GlyphRasterizer.RasterizeColorGlyph(face, glyphId, rgbaRented.AsSpan(), out int cw, out int ch, out int cminX, out int cminY))
                        {
                            if (cw > 0 && ch > 0)
                            {
                                _colorGlyphAtlas.TryInsert(colorKey, rgbaRented.AsSpan(0, cw * ch * 4), cw, ch, out colorRegion, out colorPageIndex, (short)cminX, (short)cminY);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rgbaRented);
                    }
                }

                if (colorRegion.W > 0 && colorRegion.H > 0)
                {
                    // Color glyphs carry no baked subpixel offset and draw
                    // through the linear sampler, so the fractional quad
                    // position is their single (correct) subpixel source —
                    // unlike monochrome glyphs, these are NOT floored to the
                    // pixel grid (see GlyphPlacement, WP-3508).
                    float cpx = gx + colorRegion.OffsetX;
                    float cpy = gy - (colorRegion.OffsetY + colorRegion.H);

                    // Clip cull: skip glyphs entirely outside the active clip region
                    if (hasClip && (cpx + colorRegion.W <= clipMinX || cpx >= clipMaxX ||
                                    cpy + colorRegion.H <= clipMinY || cpy >= clipMaxY))
                    {
                        culledGlyphs++;
                        continue;
                    }

                    colorInstances.Add(new GlyphInstance
                    {
                        PosX = cpx,
                        PosY = cpy,
                        SizeX = colorRegion.W,
                        SizeY = colorRegion.H,
                        AtlasU0 = colorRegion.U / atlasDim,
                        AtlasV0 = (colorRegion.V + colorRegion.H) / atlasDim,
                        AtlasU1 = (colorRegion.U + colorRegion.W) / atlasDim,
                        AtlasV1 = colorRegion.V / atlasDim,
                        R = 1.0f,
                        G = 1.0f,
                        B = 1.0f,
                        A = 1.0f,
                        ClipMinX = hasClip ? clipMinX : 0,
                        ClipMinY = hasClip ? clipMinY : 0,
                        ClipMaxX = hasClip ? clipMaxX : 0,
                        ClipMaxY = hasClip ? clipMaxY : 0,
                    });
                    records?.Add(new GlyphDrawRecord(
                        cpx, cpy, colorRegion.W, colorRegion.H,
                        glyphId, rasterFontSize, cmd.FontHandle,
                        colorRegion.U, colorRegion.V, colorRegion.W, colorRegion.H,
                        cmd.Color,
                        hasClip ? clipMinX : 0, hasClip ? clipMinY : 0,
                        hasClip ? clipMaxX : 0, hasClip ? clipMaxY : 0,
                        hasClip, IsColorGlyph: true, recordCategory, cmd.DebugNodeId, paintOrder));
                    continue;
                }
                // Color glyph rasterization failed — fall through to monochrome
            }

            var key = global::Etch.Text.Atlas.GlyphCacheKey.FromSizeAndSubpixel(rasterFontSize, faceId, glyphId, subpixelQuant);

            bool cacheHit = _glyphAtlas.TryLookup(key, out var region, out int pageIndex);
            if (!cacheHit)
            {
                GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, subpixelQuant / 4f);
                if (gw > 0 && gh > 0)
                {
                    // Custom rasterizer expands width by 1 when subpixel shift > 0
                    int bufSize = (gw + 1) * gh;
                    byte[]? rented = ArrayPool<byte>.Shared.Rent(bufSize);
                    try
                    {
                        GlyphRasterizer.Rasterize(face, glyphId, subpixelQuant / 4f, rented.AsSpan(0, bufSize), out int rw, out int rh, out int minX, out int minY);
                        if (rw > 0 && rh > 0)
                        {
                            if (GlyphDumpEnabled)
                            {
                                DumpGlyphBitmap(rented.AsSpan(0, rw * rh), rw, rh, faceId, glyphId, rasterFontSize, subpixelQuant, gw, gh);
                            }
                            _glyphAtlas.TryInsert(key, rented.AsSpan(0, rw * rh), rw, rh, out region, out pageIndex, (short)minX, (short)minY);
                            if (GlyphDumpEnabled)
                            {
                                LogGlyphRegion(faceId, glyphId, rasterFontSize, subpixelQuant, pageIndex, region);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            if (region.W == 0 || region.H == 0)
            {
                continue;
            }

            // Bitmap is already at physical size — render 1:1. The horizontal
            // subpixel offset is baked into the bitmap (subpixelQuant bucket),
            // so the quad sits at the integer pen origin; the baseline snaps
            // to the nearest pixel row. See GlyphPlacement (WP-3508).
            float px = GlyphPlacement.QuadOriginX(gx, region.OffsetX);
            float py = GlyphPlacement.QuadOriginY(gy, region.OffsetY, region.H);

            // Clip cull: skip glyphs entirely outside the active clip region
            if (hasClip && (px + region.W <= clipMinX || px >= clipMaxX ||
                            py + region.H <= clipMinY || py >= clipMaxY))
            {
                culledGlyphs++;
                continue;
            }

            instances.Add(new GlyphInstance
            {
                PosX = px,
                PosY = py,
                SizeX = region.W,
                SizeY = region.H,
                AtlasU0 = region.U / atlasDim,
                AtlasV0 = (region.V + region.H) / atlasDim,
                AtlasU1 = (region.U + region.W) / atlasDim,
                AtlasV1 = region.V / atlasDim,
                R = colorR,
                G = colorG,
                B = colorB,
                A = colorA,
                ClipMinX = hasClip ? clipMinX : 0,
                ClipMinY = hasClip ? clipMinY : 0,
                ClipMaxX = hasClip ? clipMaxX : 0,
                ClipMaxY = hasClip ? clipMaxY : 0,
            });
            records?.Add(new GlyphDrawRecord(
                px, py, region.W, region.H,
                glyphId, rasterFontSize, cmd.FontHandle,
                region.U, region.V, region.W, region.H,
                cmd.Color,
                hasClip ? clipMinX : 0, hasClip ? clipMinY : 0,
                hasClip ? clipMaxX : 0, hasClip ? clipMaxY : 0,
                hasClip, IsColorGlyph: false, recordCategory, cmd.DebugNodeId, paintOrder));
        }
    }

    private void EnsureImageTexture(int imageId, EtchBackend.ImageEntry img)
    {
        if (_imageBindGroups.ContainsKey(imageId))
        {
            return;
        }

        // Image pixels are sRGB-encoded (like every 8-bit image file); see the fallback texture.
        var texture = _device.CreateTexture(new TextureDescriptor
        {
            Size = new Extent3D { Width = (uint)img.Width, Height = (uint)img.Height, DepthOrArrayLayers = 1 },
            Format = TextureFormat.Rgba8UnormSrgb,
            Usage = (ulong)(TextureUsage.TextureBinding | TextureUsage.CopyDst),
            Dimension = TextureDimension.D2,
            MipLevelCount = 1,
            SampleCount = 1,
        });
        var view = texture.CreateView();

        var origin = new WGPUOrigin3D { X = 0, Y = 0, Z = 0 };
        var writeSize = new Extent3D { Width = (uint)img.Width, Height = (uint)img.Height, DepthOrArrayLayers = 1 };
        _device.Queue.WriteTexture(texture, 0, origin, img.Pixels, (uint)img.Width * 4, (uint)img.Height, writeSize);

        var bgEntries = stackalloc BindGroupEntry[2];
        bgEntries[0] = new BindGroupEntry { Binding = 0, TextureView = view.Handle };
        bgEntries[1] = new BindGroupEntry { Binding = 1, Sampler = _textSampler.Handle };
        var bindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _textBgLayout.Handle,
            EntryCount = (UIntPtr)2,
            Entries = (nint)bgEntries,
        });

        _imageTextures[imageId] = texture;
        _imageTextureViews[imageId] = view;
        _imageBindGroups[imageId] = bindGroup;
    }

    private static int ComputeGlyphHash(EtchBackend backend, IReadOnlyList<LayerRenderInfo>? layers = null)
    {
        int hash = 17;
        foreach (var cmd in backend.GlyphCommands)
        {
            hash = hash * 31 + cmd.FontHandle.GetHashCode();
            hash = hash * 31 + cmd.FontSize.GetHashCode();
            hash = hash * 31 + cmd.Color.GetHashCode();
            foreach (var id in cmd.GlyphIds)
            {
                hash = hash * 31 + id.GetHashCode();
            }
            foreach (var p in cmd.Positions)
            {
                hash = hash * 31 + p.GetHashCode();
            }
        }

        if (layers != null)
        {
            foreach (var layer in layers)
            {
                hash = hash * 31 + layer.OffsetX.GetHashCode();
                hash = hash * 31 + layer.OffsetY.GetHashCode();
                if (backend.LayerCaptures.TryGetValue(layer.LayerHandle, out var capture))
                {
                    foreach (var cmd in capture.GlyphCommands)
                    {
                        hash = hash * 31 + cmd.FontHandle.GetHashCode();
                        hash = hash * 31 + cmd.FontSize.GetHashCode();
                        hash = hash * 31 + cmd.Color.GetHashCode();
                        foreach (var id in cmd.GlyphIds)
                        {
                            hash = hash * 31 + id.GetHashCode();
                        }
                        foreach (var p in cmd.Positions)
                        {
                            hash = hash * 31 + p.GetHashCode();
                        }
                    }
                }
            }
        }

        return hash;
    }

    private static int ComputeStripCoverageHash(SceneBuffer scene)
    {
        int hash = 17;
        Affine cur = Affine.Identity;
        foreach (ref readonly var cmd in scene.Commands)
        {
            switch (cmd.Op)
            {
                case SceneOpcode.SetTransform:
                    cur = scene.GetTransform(cmd.SetTransform.TransformId);
                    hash = hash * 31 + cur.GetHashCode();
                    break;

                case SceneOpcode.FillPath:
                    {
                        var paint = scene.GetPaint(cmd.FillPath.PaintId);
                        if (paint.Kind != PaintKind.Solid && paint.Kind != PaintKind.LinearGradient && paint.Kind != PaintKind.RadialGradient)
                        {
                            continue;
                        }

                        if (!scene.TryGetPath(cmd.FillPath.PathId, out var pathData))
                        {
                            continue;
                        }

                        var (isCircle, _, _) = TryDetectCircle(pathData.Path);
                        if (isCircle || TryDetectRect(pathData.Path, out _))
                        {
                            continue;
                        }

                        var xf = cur * scene.GetTransform(cmd.FillPath.TransformId);
                        hash = hash * 31 + cmd.FillPath.PathId.GetHashCode();
                        hash = hash * 31 + cmd.FillPath.PaintId.GetHashCode();
                        hash = hash * 31 + xf.GetHashCode();
                        break;
                    }

                case SceneOpcode.StrokePath:
                    {
                        var paint = scene.GetPaint(cmd.StrokePath.PaintId);
                        if (paint.Kind != PaintKind.Solid && paint.Kind != PaintKind.LinearGradient && paint.Kind != PaintKind.RadialGradient)
                        {
                            continue;
                        }

                        if (!scene.TryGetPath(cmd.StrokePath.PathId, out var pathData))
                        {
                            continue;
                        }

                        if (TryDetectCircle(pathData.Path).Item1 || TryDetectLine(pathData.Path, out _, out _) || TryDetectRect(pathData.Path, out _) || TryDetectRoundedRect(pathData.Path, out _, out _))
                        {
                            continue;
                        }

                        var xf = cur * scene.GetTransform(cmd.StrokePath.TransformId);
                        hash = hash * 31 + cmd.StrokePath.PathId.GetHashCode();
                        hash = hash * 31 + cmd.StrokePath.PaintId.GetHashCode();
                        hash = hash * 31 + xf.GetHashCode();
                        hash = hash * 31 + cmd.StrokePath.StrokeWidth.GetHashCode();
                        break;
                    }
            }
        }
        return hash;
    }

    /// <summary>
    /// Builds a SceneBuffer containing only non-circle, non-rect, non-line paths
    /// (both fills and strokes) for the strip-coverage pipeline.
    /// </summary>
    private static SceneBuffer BuildStripCoverageScene(SceneBuffer scene, float offsetX = 0, float offsetY = 0)
    {
        var builder = SceneBuilder.Begin();
        builder.BeginFrame();
        int added = 0;
        Affine cur = Affine.Identity;
        var offsetTransform = (offsetX != 0 || offsetY != 0)
            ? new Affine(1, 0, 0, 1, offsetX, offsetY)
            : (Affine?)null;
        var gradientIdMap = new Dictionary<int, int>();

        foreach (ref readonly var cmd in scene.Commands)
        {
            switch (cmd.Op)
            {
                case SceneOpcode.SetTransform:
                    cur = scene.GetTransform(cmd.SetTransform.TransformId);
                    break;

                case SceneOpcode.FillPath:
                    {
                        var paint = scene.GetPaint(cmd.FillPath.PaintId);
                        if (paint.Kind != PaintKind.Solid && paint.Kind != PaintKind.LinearGradient && paint.Kind != PaintKind.RadialGradient)
                        {
                            continue;
                        }

                        if (!scene.TryGetPath(cmd.FillPath.PathId, out var pathData))
                        {
                            continue;
                        }

                        var (isCircle, _, _) = TryDetectCircle(pathData.Path);
                        if (isCircle || TryDetectRect(pathData.Path, out _))
                        {
                            continue;
                        }

                        var xf = cur * scene.GetTransform(cmd.FillPath.TransformId);
                        if (offsetTransform.HasValue)
                        {
                            xf = offsetTransform.Value * xf;
                        }
                        int newTransformId = builder.AddTransform(xf);

                        int newPaintId;
                        if (paint.Kind == PaintKind.LinearGradient || paint.Kind == PaintKind.RadialGradient)
                        {
                            int oldGradId = (int)paint.GradientId;
                            if (!gradientIdMap.TryGetValue(oldGradId, out int newGradId))
                            {
                                var stops = scene.GetGradientStops(oldGradId);
                                newGradId = builder.AddGradientStops(stops);
                                gradientIdMap[oldGradId] = newGradId;
                            }
                            var newPaint = paint.Kind == PaintKind.LinearGradient
                                ? Paint.LinearGradient((uint)newGradId, paint.BlendModeId)
                                : Paint.RadialGradient((uint)newGradId, paint.BlendModeId);
                            newPaintId = builder.AddPaint(newPaint);
                        }
                        else
                        {
                            newPaintId = builder.AddPaint(paint);
                        }

                        int newPathId = builder.AddPath(pathData.Path);
                        builder.FillPath(newPathId, newPaintId, newTransformId, FillRule.NonZero);
                        added++;
                        break;
                    }

                case SceneOpcode.StrokePath:
                    {
                        var paint = scene.GetPaint(cmd.StrokePath.PaintId);
                        if (paint.Kind != PaintKind.Solid && paint.Kind != PaintKind.LinearGradient && paint.Kind != PaintKind.RadialGradient)
                        {
                            continue;
                        }

                        if (!scene.TryGetPath(cmd.StrokePath.PathId, out var pathData))
                        {
                            continue;
                        }

                        if (TryDetectCircle(pathData.Path).Item1 || TryDetectLine(pathData.Path, out _, out _) || TryDetectRect(pathData.Path, out _) || TryDetectRoundedRect(pathData.Path, out _, out _))
                        {
                            continue;
                        }

                        var xf = cur * scene.GetTransform(cmd.StrokePath.TransformId);
                        if (offsetTransform.HasValue)
                        {
                            xf = offsetTransform.Value * xf;
                        }
                        int newTransformId = builder.AddTransform(xf);

                        int newPaintId;
                        if (paint.Kind == PaintKind.LinearGradient || paint.Kind == PaintKind.RadialGradient)
                        {
                            int oldGradId = (int)paint.GradientId;
                            if (!gradientIdMap.TryGetValue(oldGradId, out int newGradId))
                            {
                                var stops = scene.GetGradientStops(oldGradId);
                                newGradId = builder.AddGradientStops(stops);
                                gradientIdMap[oldGradId] = newGradId;
                            }
                            var newPaint = paint.Kind == PaintKind.LinearGradient
                                ? Paint.LinearGradient((uint)newGradId, paint.BlendModeId)
                                : Paint.RadialGradient((uint)newGradId, paint.BlendModeId);
                            newPaintId = builder.AddPaint(newPaint);
                        }
                        else
                        {
                            newPaintId = builder.AddPaint(paint);
                        }

                        int newPathId = builder.AddPath(pathData.Path);
                        builder.StrokePath(newPathId, newPaintId, newTransformId, cmd.StrokePath.StrokeWidth, default);
                        added++;
                        break;
                    }
            }
        }

        builder.EndFrame();
        var result = builder.End();

        int strokeCount = 0, fillCount = 0, setXformCount = 0;
        foreach (ref readonly var c in result.Commands)
        {
            switch (c.Op)
            {
                case SceneOpcode.StrokePath: strokeCount++; break;
                case SceneOpcode.FillPath: fillCount++; break;
                case SceneOpcode.SetTransform: setXformCount++; break;
            }
        }
        return result;
    }

    /// <summary>
    /// Builds the scene's shape instances. When <paramref name="sceneMarks"/> is given (scene
    /// command index before each backend command, see <see cref="EtchBackendProvider.SceneMarks"/>),
    /// also fills <paramref name="instanceAt"/> with the number of instances emitted before each
    /// backend command (plus one entry for the end) — the paint-order positions glyph runs and
    /// images are drawn at. Without marks <paramref name="instanceAt"/> is left empty.
    /// </summary>
    private static void BuildInstances(SceneBuffer scene, List<ShapeInstance> instances,
        IReadOnlyList<int>? sceneMarks, List<int> instanceAt, bool skipArbitraryPaths = false)
    {
        instances.Clear();
        instanceAt.Clear();
        int markIndex = 0;
        int markCount = sceneMarks?.Count ?? 0;
        Affine cur = Affine.Identity;
        var clipStack = new Stack<EGeometry.Rect>();

        EGeometry.Rect GetClip()
        {
            if (clipStack.Count == 0)
            {
                return EGeometry.Rect.Empty;
            }
            var result = clipStack.Peek();
            foreach (var r in clipStack)
            {
                result = result.Intersect(r);
                if (result.IsEmpty)
                {
                    break;
                }
            }
            return result;
        }

        for (int i = 0; i < scene.Commands.Length; i++)
        {
            while (markIndex < markCount && sceneMarks![markIndex] <= i)
            {
                instanceAt.Add(instances.Count);
                markIndex++;
            }

            ref readonly var cmd = ref scene.Commands[i];
            switch (cmd.Op)
            {
                case SceneOpcode.SetTransform:
                    cur = scene.GetTransform(cmd.SetTransform.TransformId);
                    break;

                case SceneOpcode.PushClip:
                    {
                        if (scene.TryGetPath(cmd.PushClip.ClipId, out var pathData))
                        {
                            var aabb = pathData.Path.Aabb().Transform(cur);
                            if (!aabb.IsEmpty)
                            {
                                clipStack.Push(aabb);
                            }
                        }
                        break;
                    }

                case SceneOpcode.PopClip:
                    if (clipStack.Count > 0)
                    {
                        clipStack.Pop();
                    }
                    break;

                case SceneOpcode.FillRect:
                    {
                        var paint = scene.GetPaint(cmd.FillRect.PaintId);
                        var rect = scene.GetRect(cmd.FillRect.RectId);
                        var xf = cur * scene.GetTransform(cmd.FillRect.TransformId);
                        var deviceRect = rect.Transform(xf);
                        if (deviceRect.IsEmpty)
                        {
                            break;
                        }

                        var clip = GetClip();
                        if (clipStack.Count > 0)
                        {
                            if (clip.IsEmpty)
                            {
                                break;
                            }
                            deviceRect = deviceRect.Intersect(clip);
                            if (deviceRect.IsEmpty)
                            {
                                break;
                            }
                        }

                        if (paint.Kind == PaintKind.Solid)
                        {
                            instances.Add(BuildSolidInstance(deviceRect, paint.Color, 0));
                        }
                        else if (paint.Kind == PaintKind.LinearGradient || paint.Kind == PaintKind.RadialGradient)
                        {
                            var stops = scene.GetGradientStops((int)paint.GradientId);
                            if (stops.Count >= 2)
                            {
                                var (stop0, color0) = stops.GetStop(0);
                                var (stop1, color1) = stops.GetStop(stops.Count - 1);
                                instances.Add(BuildGradientInstance(deviceRect, paint.Kind, stop0, color0, stop1, color1));
                            }
                            else if (stops.Count == 1)
                            {
                                var (stop0, color0) = stops.GetStop(0);
                                instances.Add(BuildSolidInstance(deviceRect, color0, 0));
                            }
                        }
                        break;
                    }

                case SceneOpcode.FillPath:
                    {
                        var paint = scene.GetPaint(cmd.FillPath.PaintId);
                        if (paint.Kind != PaintKind.Solid && paint.Kind != PaintKind.LinearGradient && paint.Kind != PaintKind.RadialGradient)
                        {
                            break;
                        }
                        if (!scene.TryGetPath(cmd.FillPath.PathId, out var pathData))
                        {
                            break;
                        }
                        var xf = cur * scene.GetTransform(cmd.FillPath.TransformId);
                        float fillScale = (float)Math.Sqrt(xf.M00 * xf.M00 + xf.M10 * xf.M10);
                        var (isCircle, center, radius) = TryDetectCircle(pathData.Path);
                        if (isCircle)
                        {
                            var tc = xf.Transform(center);
                            float r = (float)radius * fillScale;
                            var bbox = new EGeometry.Rect(tc.X - r, tc.Y - r, tc.X + r, tc.Y + r);
                            var clip = GetClip();
                            if (clipStack.Count > 0)
                            {
                                if (clip.IsEmpty)
                                {
                                    break;
                                }
                                bbox = bbox.Intersect(clip);
                                if (bbox.IsEmpty)
                                {
                                    break;
                                }
                            }
                            if (paint.Kind == PaintKind.Solid)
                            {
                                instances.Add(BuildCircleInstance(bbox, tc, r, paint.Color));
                            }
                            else
                            {
                                var stops = scene.GetGradientStops((int)paint.GradientId);
                                if (stops.Count >= 2)
                                {
                                    var (stop0, color0) = stops.GetStop(0);
                                    var (stop1, color1) = stops.GetStop(stops.Count - 1);
                                    instances.Add(BuildGradientInstance(bbox, paint.Kind, stop0, color0, stop1, color1));
                                }
                                else if (stops.Count == 1)
                                {
                                    var (stop0, color0) = stops.GetStop(0);
                                    instances.Add(BuildSolidInstance(bbox, color0, 0));
                                }
                            }
                        }
                        else if (TryDetectRoundedRect(pathData.Path, out var rrRect, out var rrRadius))
                        {
                            var deviceRect = rrRect.Transform(xf);
                            if (!deviceRect.IsEmpty)
                            {
                                var clip = GetClip();
                                if (clipStack.Count > 0)
                                {
                                    if (clip.IsEmpty)
                                    {
                                        break;
                                    }
                                    deviceRect = deviceRect.Intersect(clip);
                                    if (deviceRect.IsEmpty)
                                    {
                                        break;
                                    }
                                }
                                float physicalRadius = rrRadius * fillScale;
                                if (paint.Kind == PaintKind.Solid)
                                {
                                    instances.Add(BuildRoundedRectFillInstance(deviceRect, physicalRadius, paint.Color));
                                }
                                else
                                {
                                    var stops = scene.GetGradientStops((int)paint.GradientId);
                                    if (stops.Count >= 2)
                                    {
                                        var (stop0, color0) = stops.GetStop(0);
                                        var (stop1, color1) = stops.GetStop(stops.Count - 1);
                                        instances.Add(BuildRoundedRectGradientInstance(deviceRect, physicalRadius, paint.Kind, stop0, color0, stop1, color1));
                                    }
                                    else if (stops.Count == 1)
                                    {
                                        var (stop0, color0) = stops.GetStop(0);
                                        instances.Add(BuildRoundedRectFillInstance(deviceRect, physicalRadius, color0));
                                    }
                                }
                            }
                        }
                        else if (!skipArbitraryPaths)
                        {
                            // Arbitrary paths (non-circle, non-rounded-rect): AABB fallback.
                            // Only used when strip-coverage is disabled.
                            var aabb = pathData.Path.Aabb();
                            if (!aabb.IsEmpty)
                            {
                                var deviceRect = aabb.Transform(xf);
                                if (!deviceRect.IsEmpty)
                                {
                                    var clip = GetClip();
                                    if (clipStack.Count > 0)
                                    {
                                        if (clip.IsEmpty)
                                        {
                                            break;
                                        }
                                        deviceRect = deviceRect.Intersect(clip);
                                        if (deviceRect.IsEmpty)
                                        {
                                            break;
                                        }
                                    }
                                    if (paint.Kind == PaintKind.Solid)
                                    {
                                        instances.Add(BuildSolidInstance(deviceRect, paint.Color, 0));
                                    }
                                    else
                                    {
                                        var stops = scene.GetGradientStops((int)paint.GradientId);
                                        if (stops.Count >= 2)
                                        {
                                            var (stop0, color0) = stops.GetStop(0);
                                            var (stop1, color1) = stops.GetStop(stops.Count - 1);
                                            instances.Add(BuildGradientInstance(deviceRect, paint.Kind, stop0, color0, stop1, color1));
                                        }
                                        else if (stops.Count == 1)
                                        {
                                            var (stop0, color0) = stops.GetStop(0);
                                            instances.Add(BuildSolidInstance(deviceRect, color0, 0));
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }

                case SceneOpcode.DrawShadow:
                    {
                        if (!scene.TryGetPath(cmd.DrawShadow.PathId, out var shadowPath)
                            || !ShadowShape.TryResolve(shadowPath.Path, out var shadowRect, out double shadowCorner))
                        {
                            break;
                        }
                        var xf = cur * scene.GetTransform(cmd.DrawShadow.TransformId);
                        float scale = (float)Math.Sqrt(xf.M00 * xf.M00 + xf.M10 * xf.M10);
                        var shifted = new EGeometry.Rect(
                            shadowRect.MinX + cmd.DrawShadow.ShadowOffsetX, shadowRect.MinY + cmd.DrawShadow.ShadowOffsetY,
                            shadowRect.MaxX + cmd.DrawShadow.ShadowOffsetX, shadowRect.MaxY + cmd.DrawShadow.ShadowOffsetY);
                        var device = shifted.Transform(xf);
                        if (device.IsEmpty)
                        {
                            break;
                        }
                        float sigma = (float)ShadowShape.EffectiveSigma(cmd.DrawShadow.BlurRadius * scale);
                        float extent = (float)ShadowShape.Extent(sigma);
                        var quad = new EGeometry.Rect(device.MinX - extent, device.MinY - extent, device.MaxX + extent, device.MaxY + extent);
                        var clip = GetClip();
                        if (clipStack.Count > 0)
                        {
                            if (clip.IsEmpty)
                            {
                                break;
                            }
                            quad = quad.Intersect(clip);
                            if (quad.IsEmpty)
                            {
                                break;
                            }
                        }
                        instances.Add(BuildShadowInstance(quad, device, (float)(shadowCorner * scale), sigma, cmd.DrawShadow.ShadowColor));
                        break;
                    }

                case SceneOpcode.FillSector:
                    {
                        var paint = scene.GetPaint(cmd.FillSector.PaintId);
                        if (paint.Kind != PaintKind.Solid)
                        {
                            break;
                        }
                        var xf = cur * scene.GetTransform(cmd.FillSector.TransformId);
                        float cx = (float)(cmd.FillSector.CenterX * xf.M00 + cmd.FillSector.CenterY * xf.M01 + xf.M02);
                        float cy = (float)(cmd.FillSector.CenterX * xf.M10 + cmd.FillSector.CenterY * xf.M11 + xf.M12);
                        float scale = (float)Math.Sqrt(xf.M00 * xf.M00 + xf.M10 * xf.M10);
                        float outerR = cmd.FillSector.OuterRadius * scale;
                        float innerR = cmd.FillSector.InnerRadius * scale;
                        var clip = GetClip();
                        if (clipStack.Count > 0 && clip.IsEmpty)
                        {
                            break;
                        }
                        instances.Add(BuildSectorInstance(cx, cy, outerR, innerR,
                            cmd.FillSector.StartRad, cmd.FillSector.SweepRad, paint.Color, clip));
                        break;
                    }

                case SceneOpcode.StrokePath:
                    {
                        var paint = scene.GetPaint(cmd.StrokePath.PaintId);
                        if (paint.Kind != PaintKind.Solid)
                        {
                            break;
                        }
                        if (!scene.TryGetPath(cmd.StrokePath.PathId, out var pathData))
                        {
                            break;
                        }
                        var xf = cur * scene.GetTransform(cmd.StrokePath.TransformId);
                        float scale = (float)Math.Sqrt(xf.M00 * xf.M00 + xf.M10 * xf.M10);
                        float strokeWidth = cmd.StrokePath.StrokeWidth * scale;
                        var clip = GetClip();

                        // Try circle first
                        var (isCircle, center, radius) = TryDetectCircle(pathData.Path);
                        if (isCircle)
                        {
                            var tc = xf.Transform(center);
                            float r = (float)radius * scale;
                            float halfSw = strokeWidth * 0.5f;
                            var bbox = new EGeometry.Rect(tc.X - r - halfSw, tc.Y - r - halfSw,
                                tc.X + r + halfSw, tc.Y + r + halfSw);
                            if (clipStack.Count > 0)
                            {
                                if (clip.IsEmpty)
                                {
                                    break;
                                }
                                bbox = bbox.Intersect(clip);
                                if (bbox.IsEmpty)
                                {
                                    break;
                                }
                            }
                            instances.Add(BuildRingInstance(bbox, tc, r + halfSw, strokeWidth, paint.Color));
                            break;
                        }

                        // Try line next
                        if (TryDetectLine(pathData.Path, out var lp0, out var lp1))
                        {
                            var t0 = xf.Transform(lp0);
                            var t1 = xf.Transform(lp1);
                            float halfSw = strokeWidth * 0.5f;
                            var minX = Math.Min(t0.X, t1.X) - halfSw;
                            var minY = Math.Min(t0.Y, t1.Y) - halfSw;
                            var maxX = Math.Max(t0.X, t1.X) + halfSw;
                            var maxY = Math.Max(t0.Y, t1.Y) + halfSw;
                            var bbox = new EGeometry.Rect(minX, minY, maxX, maxY);
                            if (clipStack.Count > 0)
                            {
                                if (clip.IsEmpty)
                                {
                                    break;
                                }
                                bbox = bbox.Intersect(clip);
                                if (bbox.IsEmpty)
                                {
                                    break;
                                }
                            }
                            instances.Add(BuildLineStrokeInstance(bbox, t0, t1, strokeWidth, paint.Color));
                            break;
                        }

                        // Try arc (stroked circular arc — render as true sector instead of faceted polyline)
                        if (TryDetectArc(pathData.Path, out var arcCenter, out var arcRadius, out var arcStartRad, out var arcSweepRad))
                        {
                            // Arc fast path only works for uniform scale + translation (no rotation or shear).
                            // If the transform contains rotation, fall through to the polyline path which
                            // correctly transforms each vertex.
                            if (Math.Abs(xf.M01) < 0.0001f && Math.Abs(xf.M10) < 0.0001f)
                            {
                                var tc = xf.Transform(arcCenter);
                                float r = (float)(arcRadius * scale);
                                float halfSw = strokeWidth * 0.5f;
                                var bbox = new EGeometry.Rect(tc.X - r - halfSw, tc.Y - r - halfSw,
                                    tc.X + r + halfSw, tc.Y + r + halfSw);
                                if (clipStack.Count > 0)
                                {
                                    if (clip.IsEmpty)
                                    {
                                        break;
                                    }
                                    bbox = bbox.Intersect(clip);
                                    if (bbox.IsEmpty)
                                    {
                                        break;
                                    }
                                }
                                instances.Add(BuildSectorInstance((float)tc.X, (float)tc.Y, r + halfSw, Math.Max(0f, r - halfSw),
                                    (float)arcStartRad, (float)arcSweepRad, paint.Color, clip));
                                break;
                            }
                        }

                        // Try polyline (multi-segment line, e.g. checkmark)
                        if (TryDetectPolyline(pathData.Path, out var polyPoints))
                        {
                            bool anyVisible = false;
                            for (int segIdx = 0; segIdx < polyPoints.Count - 1; segIdx++)
                            {
                                var t0 = xf.Transform(polyPoints[segIdx]);
                                var t1 = xf.Transform(polyPoints[segIdx + 1]);
                                float halfSw = strokeWidth * 0.5f;
                                var minX = Math.Min(t0.X, t1.X) - halfSw;
                                var minY = Math.Min(t0.Y, t1.Y) - halfSw;
                                var maxX = Math.Max(t0.X, t1.X) + halfSw;
                                var maxY = Math.Max(t0.Y, t1.Y) + halfSw;
                                var bbox = new EGeometry.Rect(minX, minY, maxX, maxY);
                                if (clipStack.Count > 0)
                                {
                                    if (clip.IsEmpty)
                                    {
                                        continue;
                                    }
                                    bbox = bbox.Intersect(clip);
                                    if (bbox.IsEmpty)
                                    {
                                        continue;
                                    }
                                }
                                instances.Add(BuildLineStrokeInstance(bbox, t0, t1, strokeWidth, paint.Color));
                                anyVisible = true;
                            }
                            if (anyVisible)
                            {
                                break;
                            }
                        }

                        // Try rect
                        if (TryDetectRect(pathData.Path, out var rect))
                        {
                            var deviceRect = rect.Transform(xf);
                            if (clipStack.Count > 0)
                            {
                                if (clip.IsEmpty)
                                {
                                    break;
                                }
                                deviceRect = deviceRect.Intersect(clip);
                                if (deviceRect.IsEmpty)
                                {
                                    break;
                                }
                            }
                            AddRectStrokeInstances(deviceRect, strokeWidth, paint.Color, instances);
                            break;
                        }

                        // Try rounded rect
                        if (TryDetectRoundedRect(pathData.Path, out var rrRect, out var rrRadius))
                        {
                            var deviceRect = rrRect.Transform(xf);
                            if (clipStack.Count > 0)
                            {
                                if (clip.IsEmpty)
                                {
                                    break;
                                }
                                deviceRect = deviceRect.Intersect(clip);
                                if (deviceRect.IsEmpty)
                                {
                                    break;
                                }
                            }
                            float physicalRRRadius = rrRadius * scale;
                            instances.Add(BuildRoundedRectStrokeInstance(deviceRect, physicalRRRadius, strokeWidth, paint.Color));
                            break;
                        }

                        // Complex path strokes are handled by the strip-coverage pipeline
                        break;
                    }

            }
        }

        while (markIndex < markCount)
        {
            instanceAt.Add(instances.Count);
            markIndex++;
        }
    }

    // Paint colours are sRGB-encoded (PaintColor); the shaders blend into the sRGB swapchain in
    // linear light, so instances carry linear channels.
    private static ShapeInstance BuildSolidInstance(EGeometry.Rect rect, uint argb, uint shapeType)
    {
        var (r, g, b, a) = PaintColor.ToLinear(argb);
        return new ShapeInstance
        {
            MinX = (float)rect.MinX, MinY = (float)rect.MinY,
            MaxX = (float)rect.MaxX, MaxY = (float)rect.MaxY,
            R0 = r, G0 = g, B0 = b, A0 = a,
            ShapeType = shapeType,
        };
    }

    private static ShapeInstance BuildCircleInstance(EGeometry.Rect bbox, EGeometry.Point center, float radius, uint argb)
    {
        var inst = BuildSolidInstance(bbox, argb, 1);
        inst.CenterX = (float)center.X;
        inst.CenterY = (float)center.Y;
        inst.Radius = radius;
        inst.Expand = SdfAntialiasBand;
        return inst;
    }

    private static ShapeInstance BuildGradientInstance(EGeometry.Rect rect, PaintKind kind, float stop0, uint color0, float stop1, uint color1)
    {
        var (r0, g0, b0, a0) = PaintColor.ToLinear(color0);
        var (r1, g1, b1, a1) = PaintColor.ToLinear(color1);

        float x0 = (float)rect.MinX;
        float y0 = (float)rect.MinY;
        float x1 = (float)rect.MaxX;
        float y1 = (float)rect.MaxY;

        var inst = new ShapeInstance
        {
            MinX = x0, MinY = y0, MaxX = x1, MaxY = y1,
            R0 = r0, G0 = g0, B0 = b0, A0 = a0,
            R1 = r1, G1 = g1, B1 = b1, A1 = a1,
            ShapeType = 2,
        };

        if (kind == PaintKind.LinearGradient)
        {
            inst.P0X = x0;
            inst.P0Y = y0;
            inst.P1X = x1;
            inst.P1Y = y0;
        }
        else
        {
            // Radial — approximate as linear for now
            float cx = (x0 + x1) * 0.5f;
            float cy = (y0 + y1) * 0.5f;
            inst.P0X = cx;
            inst.P0Y = cy;
            inst.P1X = x1;
            inst.P1Y = cy;
        }

        return inst;
    }

    private static ShapeInstance BuildRingInstance(EGeometry.Rect bbox, EGeometry.Point center, float outerRadius, float strokeWidth, uint argb)
    {
        var inst = BuildSolidInstance(bbox, argb, 3);
        inst.CenterX = (float)center.X;
        inst.CenterY = (float)center.Y;
        inst.Radius = outerRadius;
        inst.StrokeWidth = strokeWidth;
        inst.Expand = strokeWidth * 0.5f + SdfAntialiasBand;
        return inst;
    }

    private static ShapeInstance BuildLineStrokeInstance(EGeometry.Rect bbox, EGeometry.Point p0, EGeometry.Point p1, float strokeWidth, uint argb)
    {
        var inst = BuildSolidInstance(bbox, argb, 4);
        inst.P0X = (float)p0.X;
        inst.P0Y = (float)p0.Y;
        inst.P1X = (float)p1.X;
        inst.P1Y = (float)p1.Y;
        inst.StrokeWidth = strokeWidth;
        inst.Expand = strokeWidth * 0.5f + SdfAntialiasBand;
        return inst;
    }

    // Shape 9: the quad is the (clipped) 3σ-grown extent; the shader evaluates the blur of the
    // shadow rect in P0..P1, so clipping the quad never reshapes the shadow.
    private static ShapeInstance BuildShadowInstance(EGeometry.Rect quad, EGeometry.Rect shadow, float corner, float sigma, uint argb)
    {
        var inst = BuildSolidInstance(quad, argb, 9);
        inst.P0X = (float)shadow.MinX;
        inst.P0Y = (float)shadow.MinY;
        inst.P1X = (float)shadow.MaxX;
        inst.P1Y = (float)shadow.MaxY;
        inst.Radius = corner;
        inst.StrokeWidth = sigma;
        return inst;
    }

    private static ShapeInstance BuildRoundedRectFillInstance(EGeometry.Rect rect, float radius, uint argb)
    {
        var inst = BuildSolidInstance(rect, argb, 5);
        inst.Radius = radius;
        inst.Expand = SdfAntialiasBand;
        return inst;
    }

    private static ShapeInstance BuildRoundedRectGradientInstance(EGeometry.Rect rect, float radius, PaintKind kind, float stop0, uint color0, float stop1, uint color1)
    {
        var inst = BuildGradientInstance(rect, kind, stop0, color0, stop1, color1);
        inst.ShapeType = 7;
        inst.Radius = radius;
        inst.Expand = SdfAntialiasBand;
        return inst;
    }

    private static ShapeInstance BuildRoundedRectStrokeInstance(EGeometry.Rect rect, float radius, float strokeWidth, uint argb)
    {
        var inst = BuildSolidInstance(rect, argb, 6);
        inst.Radius = radius;
        inst.StrokeWidth = strokeWidth;
        inst.Expand = strokeWidth * 0.5f + SdfAntialiasBand;
        return inst;
    }

    private static ShapeInstance BuildSectorInstance(
        float cx, float cy, float outerRadius, float innerRadius,
        float startRad, float sweepRad, uint argb, EGeometry.Rect clip)
    {
        // Compute bounding box of the sector
        float endRad = startRad + sweepRad;
        float minX = cx, minY = cy, maxX = cx, maxY = cy;

        // Check outer arc endpoints
        float ox1 = cx + outerRadius * MathF.Cos(startRad);
        float oy1 = cy + outerRadius * MathF.Sin(startRad);
        float ox2 = cx + outerRadius * MathF.Cos(endRad);
        float oy2 = cy + outerRadius * MathF.Sin(endRad);
        minX = MathF.Min(minX, MathF.Min(ox1, ox2));
        minY = MathF.Min(minY, MathF.Min(oy1, oy2));
        maxX = MathF.Max(maxX, MathF.Max(ox1, ox2));
        maxY = MathF.Max(maxY, MathF.Max(oy1, oy2));

        // Check inner arc endpoints
        float ix1 = cx + innerRadius * MathF.Cos(startRad);
        float iy1 = cy + innerRadius * MathF.Sin(startRad);
        float ix2 = cx + innerRadius * MathF.Cos(endRad);
        float iy2 = cy + innerRadius * MathF.Sin(endRad);
        minX = MathF.Min(minX, MathF.Min(ix1, ix2));
        minY = MathF.Min(minY, MathF.Min(iy1, iy2));
        maxX = MathF.Max(maxX, MathF.Max(ix1, ix2));
        maxY = MathF.Max(maxY, MathF.Max(iy1, iy2));

        // Check cardinal directions on outer arc
        float step = MathF.Sign(sweepRad) * MathF.PI / 2f;
        float checkAngle = MathF.Floor(startRad / step) * step + step;
        if (sweepRad > 0)
        {
            while (checkAngle < endRad)
            {
                float x = cx + outerRadius * MathF.Cos(checkAngle);
                float y = cy + outerRadius * MathF.Sin(checkAngle);
                minX = MathF.Min(minX, x);
                minY = MathF.Min(minY, y);
                maxX = MathF.Max(maxX, x);
                maxY = MathF.Max(maxY, y);
                checkAngle += step;
            }
        }
        else
        {
            while (checkAngle > endRad)
            {
                float x = cx + outerRadius * MathF.Cos(checkAngle);
                float y = cy + outerRadius * MathF.Sin(checkAngle);
                minX = MathF.Min(minX, x);
                minY = MathF.Min(minY, y);
                maxX = MathF.Max(maxX, x);
                maxY = MathF.Max(maxY, y);
                checkAngle += step;
            }
        }

        if (!clip.IsEmpty)
        {
            minX = MathF.Max(minX, (float)clip.MinX);
            minY = MathF.Max(minY, (float)clip.MinY);
            maxX = MathF.Min(maxX, (float)clip.MaxX);
            maxY = MathF.Min(maxY, (float)clip.MaxY);
        }
        if (minX >= maxX || minY >= maxY)
        {
            return default;
        }

        var inst = BuildSolidInstance(new EGeometry.Rect(minX, minY, maxX, maxY), argb, 8);
        inst.CenterX = cx;
        inst.CenterY = cy;
        inst.Radius = outerRadius;
        inst.StrokeWidth = innerRadius;
        inst.P0X = startRad;
        inst.P0Y = sweepRad;
        inst.Expand = SdfAntialiasBand;
        return inst;
    }

    private static void AddRectStrokeInstances(EGeometry.Rect rect, float strokeWidth, uint argb, List<ShapeInstance> instances)
    {
        float x0 = (float)rect.MinX, y0 = (float)rect.MinY;
        float x1 = (float)rect.MaxX, y1 = (float)rect.MaxY;
        float sw = strokeWidth;

        // Top edge
        instances.Add(BuildSolidInstance(new EGeometry.Rect(x0, y0, x1, y0 + sw), argb, 0));
        // Bottom edge
        instances.Add(BuildSolidInstance(new EGeometry.Rect(x0, y1 - sw, x1, y1), argb, 0));
        // Left edge
        instances.Add(BuildSolidInstance(new EGeometry.Rect(x0, y0, x0 + sw, y1), argb, 0));
        // Right edge
        instances.Add(BuildSolidInstance(new EGeometry.Rect(x1 - sw, y0, x1, y1), argb, 0));
    }

    private static bool TryDetectLine(BezPath path, out EGeometry.Point p0, out EGeometry.Point p1)
    {
        p0 = default;
        p1 = default;
        var enumerator = path.Iterate();
        bool hasMove = false, hasLine = false;

        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    if (hasMove)
                    {
                        return false;
                    }
                    hasMove = true;
                    p0 = seg.End;
                    break;
                case PathVerb.LineTo:
                    if (!hasMove || hasLine)
                    {
                        return false;
                    }
                    hasLine = true;
                    p1 = seg.End;
                    break;
                case PathVerb.Close:
                    return false;
                default:
                    return false;
            }
        }

        return hasMove && hasLine;
    }

    private static bool TryDetectPolyline(BezPath path, out List<EGeometry.Point> points)
    {
        points = new List<EGeometry.Point>();
        var enumerator = path.Iterate();
        bool hasMove = false;

        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    if (hasMove)
                    {
                        return false;
                    }
                    hasMove = true;
                    points.Add(seg.End);
                    break;
                case PathVerb.LineTo:
                    if (!hasMove)
                    {
                        return false;
                    }
                    points.Add(seg.End);
                    break;
                case PathVerb.Close:
                    return false;
                default:
                    return false;
            }
        }

        return hasMove && points.Count >= 2;
    }

    /// <summary>
    /// Detects whether a polyline path is a circular arc approximation.
    /// Returns true if all points lie on a common circle with consistent angular spacing.
    /// </summary>
    private static bool TryDetectArc(BezPath path, out EGeometry.Point center, out double radius, out double startRad, out double sweepRad)
    {
        center = default;
        radius = 0;
        startRad = 0;
        sweepRad = 0;

        var points = new List<EGeometry.Point>();
        var enumerator = path.Iterate();
        bool hasMove = false;

        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    if (hasMove) { return false; }
                    hasMove = true;
                    points.Add(seg.End);
                    break;
                case PathVerb.LineTo:
                    if (!hasMove) { return false; }
                    points.Add(seg.End);
                    break;
                default:
                    return false;
            }
        }

        if (points.Count < 5) { return false; }

        // Compute circumcenter from first, middle, and last points
        var p0 = points[0];
        var pm = points[points.Count / 2];
        var pn = points[points.Count - 1];

        double d = 2 * ((p0.X - pn.X) * (pm.Y - pn.Y) - (pm.X - pn.X) * (p0.Y - pn.Y));
        if (Math.Abs(d) < 1e-10) { return false; }

        double ux = ((p0.Y - pn.Y) * (p0.Y - pm.Y) * (pm.Y - pn.Y)
                   + (p0.X - pn.X) * (p0.X - pm.X) * (pm.X - pn.X)
                   - (p0.X - pm.X) * (pm.Y - pn.Y) * (p0.Y - pn.Y)) / d;
        double uy = ((p0.X - pn.X) * (p0.X - pm.X) * (pm.X - pn.X)
                   + (p0.Y - pn.Y) * (p0.Y - pm.Y) * (pm.Y - pn.Y)
                   - (p0.Y - pm.Y) * (pm.X - pn.X) * (p0.X - pn.X)) / d;

        // Actually, let me use the standard circumcenter formula
        double ax = p0.X, ay = p0.Y;
        double bx = pm.X, by = pm.Y;
        double cx = pn.X, cy = pn.Y;
        double d2 = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d2) < 1e-10) { return false; }

        double ux2 = ((ax * ax + ay * ay) * (by - cy)
                    + (bx * bx + by * by) * (cy - ay)
                    + (cx * cx + cy * cy) * (ay - by)) / d2;
        double uy2 = ((ax * ax + ay * ay) * (cx - bx)
                    + (bx * bx + by * by) * (ax - cx)
                    + (cx * cx + cy * cy) * (bx - ax)) / d2;

        center = new EGeometry.Point(ux2, uy2);
        radius = Math.Sqrt((ax - ux2) * (ax - ux2) + (ay - uy2) * (ay - uy2));

        if (radius < 1.0) { return false; }

        // Verify all points are on this circle
        double maxDev = 0;
        foreach (var p in points)
        {
            double dist = Math.Sqrt((p.X - ux2) * (p.X - ux2) + (p.Y - uy2) * (p.Y - uy2));
            maxDev = Math.Max(maxDev, Math.Abs(dist - radius));
        }
        if (maxDev > Math.Max(radius * 0.02, 0.5))
        {
            return false;
        }

        // Compute start and sweep angles
        startRad = Math.Atan2(points[0].Y - uy2, points[0].X - ux2);
        double endRad = Math.Atan2(points[points.Count - 1].Y - uy2, points[points.Count - 1].X - ux2);
        sweepRad = endRad - startRad;

        // Determine the correct sweep direction by checking intermediate points
        double signedArea = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            signedArea += (points[i].X - ux2) * (points[i + 1].Y - uy2)
                        - (points[i + 1].X - ux2) * (points[i].Y - uy2);
        }

        // Normalize sweep to match the direction of the points
        if (signedArea > 0 && sweepRad < 0)
        {
            sweepRad += 2 * Math.PI;
        }
        else if (signedArea < 0 && sweepRad > 0)
        {
            sweepRad -= 2 * Math.PI;
        }

        // Handle wrap-around: if |sweep| is very small but points span nearly 2PI,
        // the arc likely goes the long way around
        if (Math.Abs(sweepRad) < 0.1 && points.Count > 10)
        {
            if (signedArea > 0)
            {
                sweepRad = 2 * Math.PI;
            }
            else
            {
                sweepRad = -2 * Math.PI;
            }
        }

        return true;
    }

    private static bool TryDetectRect(BezPath path, out EGeometry.Rect rect)
    {
        rect = EGeometry.Rect.Empty;
        var enumerator = path.Iterate();
        int lineCount = 0;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasMove = false;

        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    hasMove = true;
                    minX = Math.Min(minX, seg.End.X);
                    minY = Math.Min(minY, seg.End.Y);
                    maxX = Math.Max(maxX, seg.End.X);
                    maxY = Math.Max(maxY, seg.End.Y);
                    break;
                case PathVerb.LineTo:
                    lineCount++;
                    minX = Math.Min(minX, seg.End.X);
                    minY = Math.Min(minY, seg.End.Y);
                    maxX = Math.Max(maxX, seg.End.X);
                    maxY = Math.Max(maxY, seg.End.Y);
                    break;
                case PathVerb.Close:
                    break;
                default:
                    return false;
            }
        }

        if (!hasMove || lineCount != 4)
        {
            return false;
        }

        rect = new EGeometry.Rect(minX, minY, maxX, maxY);
        return true;
    }

    private static bool TryDetectRoundedRect(BezPath path, out EGeometry.Rect rect, out float radius)
    {
        rect = EGeometry.Rect.Empty;
        radius = 0;
        var enumerator = path.Iterate();
        int lineCount = 0;
        int cubicCount = 0;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasMove = false;
        EGeometry.Point firstMove = default;
        EGeometry.Point firstCubicEnd = default;

        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    hasMove = true;
                    firstMove = seg.End;
                    minX = Math.Min(minX, seg.End.X);
                    minY = Math.Min(minY, seg.End.Y);
                    maxX = Math.Max(maxX, seg.End.X);
                    maxY = Math.Max(maxY, seg.End.Y);
                    break;
                case PathVerb.LineTo:
                    lineCount++;
                    minX = Math.Min(minX, seg.End.X);
                    minY = Math.Min(minY, seg.End.Y);
                    maxX = Math.Max(maxX, seg.End.X);
                    maxY = Math.Max(maxY, seg.End.Y);
                    break;
                case PathVerb.CubicTo:
                    if (cubicCount == 0)
                    {
                        firstCubicEnd = seg.End;
                    }
                    cubicCount++;
                    minX = Math.Min(minX, seg.End.X);
                    minY = Math.Min(minY, seg.End.Y);
                    maxX = Math.Max(maxX, seg.End.X);
                    maxY = Math.Max(maxY, seg.End.Y);
                    break;
                case PathVerb.Close:
                    break;
                default:
                    return false;
            }
        }

        if (!hasMove || lineCount != 4 || cubicCount != 4)
        {
            return false;
        }

        rect = new EGeometry.Rect(minX, minY, maxX, maxY);

        float rx = (float)(firstMove.X - minX);
        float ry = (float)(firstCubicEnd.Y - minY);
        radius = (rx + ry) * 0.5f;

        float halfMinDim = (float)(Math.Min(rect.Width, rect.Height) * 0.5);
        if (radius <= 0 || radius > halfMinDim + 1)
        {
            return false;
        }

        return true;
    }

    private static (bool, EGeometry.Point, double) TryDetectCircle(BezPath path)
    {
        var enumerator = path.Iterate();
        int cubicCount = 0;
        double cx = 0, cy = 0, rx = 0, ry = 0;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool hasMove = false;
        bool hasLine = false;
        while (enumerator.MoveNext())
        {
            var seg = enumerator.Current;
            switch (seg.Verb)
            {
                case PathVerb.MoveTo:
                    rx = seg.End.X; ry = seg.End.Y; hasMove = true;
                    Extend(seg.End);
                    break;
                case PathVerb.LineTo: hasLine = true; break;
                case PathVerb.CubicTo:
                    cubicCount++;
                    cx += seg.Control0.X + seg.Control1.X + seg.End.X;
                    cy += seg.Control0.Y + seg.Control1.Y + seg.End.Y;
                    Extend(seg.Control0);
                    Extend(seg.Control1);
                    Extend(seg.End);
                    break;
            }
        }
        if (cubicCount == 4 && hasMove && !hasLine)
        {
            double avgX = cx / (cubicCount * 3), avgY = cy / (cubicCount * 3);
            double dx = rx - avgX, dy = ry - avgY;
            double radius = Math.Sqrt(dx * dx + dy * dy);

            // Validate: bounding box must be roughly square with size ≈ 2*radius
            // (rejects rounded rects which also have 4 cubic segments). The box of the control
            // points is used: for a cubic circle the controls lie on the tangents at the extremes,
            // so it equals the curve's box, and it costs nothing. The exact curve AABB (solving
            // for cubic extrema) made this check ~17% of a canvas-heavy frame (EnergyOrbs).
            double width = maxX - minX;
            double height = maxY - minY;
            double diameter = radius * 2;
            double tolerance = diameter * 0.15;

            if (Math.Abs(width - diameter) <= tolerance && Math.Abs(height - diameter) <= tolerance)
            {
                return (true, new EGeometry.Point(avgX, avgY), radius);
            }
        }
        return (false, default, 0);

        void Extend(EGeometry.Point p)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }
    }

    /// <summary>
    /// Ensures the geometry storage buffer can hold at least <paramref name="minCapacity"/> instances.
    /// Must be called <em>before</em> BeginRenderPass so the bind group is stable during the pass.
    /// </summary>
    private void EnsureGeometryBufferCapacity(int minCapacity)
    {
        int requiredCapacity = minCapacity == 0 ? 1 : minCapacity;
        if (_geomStorageCapacity < requiredCapacity)
        {
            if (!_geomStorageBuffer.IsInvalid)
            {
                _geomStorageBuffer.Dispose();
            }
            _geomStorageCapacity = Math.Max(256, requiredCapacity * 2);
            _geomStorageBuffer = _device.CreateBuffer(new BufferDescriptor
            {
                Usage = (ulong)(BufferUsage.Storage | BufferUsage.CopyDst),
                Size = (ulong)((long)_geomStorageCapacity * sizeof(ShapeInstance)),
            });
            UpdateGeometryBindGroup(_geomStorageCapacity);
        }
    }

    private void UploadInstances(List<ShapeInstance> instances)
    {
        int count = instances.Count;
        if (count > 0)
        {
            var span = CollectionsMarshal.AsSpan(instances);
            var bytes = MemoryMarshal.AsBytes(span);
            _device.Queue.WriteBuffer(_geomStorageBuffer, 0, bytes);
        }
    }

    private void UploadSurfaceSize(uint width, uint height, float offsetX = 0, float offsetY = 0)
    {
        var data = new SurfaceSizeData { Width = width, Height = height, OffsetX = offsetX, OffsetY = offsetY, TextGamma = TextGamma, LightWeight = LightWeight, Dissolve = FrameDissolve };
        var span = MemoryMarshal.CreateReadOnlySpan(ref data, 1);
        var byteSpan = MemoryMarshal.AsBytes(span);
        _device.Queue.WriteBuffer(_geomUniformBuffer, 0, byteSpan);
        _device.Queue.WriteBuffer(_glyphUniformBuffer, 0, byteSpan);
    }

    private unsafe RenderPipeline BuildGeometryPipeline(out PipelineLayout pipelineLayout, out BindGroupLayout bindGroupLayout)
    {
        var uniformBglEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform, MinBindingSize = (ulong)sizeof(SurfaceSizeData) },
        };
        var storageBglEntry = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 0 },
        };

        var bglEntries = stackalloc BindGroupLayoutEntry[2];
        bglEntries[0] = uniformBglEntry;
        bglEntries[1] = storageBglEntry;
        var bglDesc = new BindGroupLayoutDescriptor { EntryCount = (UIntPtr)2, Entries = (nint)bglEntries };
        bindGroupLayout = _device.CreateBindGroupLayout(bglDesc);

        var bglHandle = bindGroupLayout.Handle;
        var plDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (UIntPtr)1,
            BindGroupLayouts = (nint)(&bglHandle),
        };
        pipelineLayout = _device.CreatePipelineLayout(plDesc);

        Span<byte> vsName = stackalloc byte[3];
        Span<byte> fsName = stackalloc byte[3];
        int vsLen = Encoding.UTF8.GetBytes("vs", vsName);
        int fsLen = Encoding.UTF8.GetBytes("fs", fsName);

        RenderPipeline pipeline;
        fixed (byte* vsPtr = vsName)
        fixed (byte* fsPtr = fsName)
        {
            var vertex = new VertexState
            {
                Module = _geomShader.Handle,
                EntryPoint = new StringView { Data = (nint)vsPtr, Length = (UIntPtr)vsLen },
            };
            var blendState = new BlendState
            {
                Color = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha },
                Alpha = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha },
            };
            var colorTarget = new ColorTargetState
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = (nint)(&blendState),
                WriteMask = (ulong)ColorWriteMask.All,
            };
            var fragment = new FragmentState
            {
                Module = _geomShader.Handle,
                EntryPoint = new StringView { Data = (nint)fsPtr, Length = (UIntPtr)fsLen },
                TargetCount = (UIntPtr)1,
                Targets = (nint)(&colorTarget),
            };
            var desc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout.Handle,
                Vertex = vertex,
                Fragment = (nint)(&fragment),
                Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleStrip },
                Multisample = new MultisampleState { Count = 1, Mask = ~0u },
            };
            pipeline = _device.CreateRenderPipeline(desc);
        }
        return pipeline;
    }

    private unsafe RenderPipeline BuildTextPipeline(out PipelineLayout pipelineLayout, out BindGroupLayout bindGroupLayout)
    {
        var bgLayoutEntries = stackalloc BindGroupLayoutEntry[2];
        bgLayoutEntries[0] = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)ShaderStage.Fragment,
            Texture = new TextureBindingLayout { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.D2, Multisampled = 0 },
        };
        bgLayoutEntries[1] = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = (ulong)ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
        };
        bindGroupLayout = _device.CreateBindGroupLayout(new BindGroupLayoutDescriptor
        {
            EntryCount = (UIntPtr)2,
            Entries = (nint)bgLayoutEntries,
        });

        var bgLayoutHandle = bindGroupLayout.Handle;
        var plDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (UIntPtr)1,
            BindGroupLayouts = (nint)(&bgLayoutHandle),
        };
        pipelineLayout = _device.CreatePipelineLayout(plDesc);

        var vertexAttributes = stackalloc VertexAttribute[2];
        vertexAttributes[0] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 0, ShaderLocation = 0 };
        vertexAttributes[1] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 8, ShaderLocation = 1 };

        var vertexBuffers = stackalloc VertexBufferLayout[1];
        vertexBuffers[0] = new VertexBufferLayout
        {
            StepMode = VertexStepMode.Vertex,
            ArrayStride = 16,
            AttributeCount = (UIntPtr)2,
            Attributes = (nint)vertexAttributes,
        };

        Span<byte> vsName = stackalloc byte[3];
        Span<byte> fsName = stackalloc byte[3];
        int vsLen = Encoding.UTF8.GetBytes("vs", vsName);
        int fsLen = Encoding.UTF8.GetBytes("fs", fsName);

        // Alpha blending for text overlay
        var blendState = new BlendState
        {
            Color = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha },
            Alpha = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha },
        };

        RenderPipeline pipeline;
        fixed (byte* vsPtr = vsName)
        fixed (byte* fsPtr = fsName)
        {
            BlendState* blendPtr = &blendState;
            var colorTarget = new ColorTargetState
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = (nint)blendPtr,
                WriteMask = (ulong)ColorWriteMask.All,
            };

            var vertex = new VertexState
            {
                Module = _textShader.Handle,
                EntryPoint = new StringView { Data = (nint)vsPtr, Length = (UIntPtr)vsLen },
                BufferCount = (UIntPtr)1,
                Buffers = (nint)vertexBuffers,
            };
            var fragment = new FragmentState
            {
                Module = _textShader.Handle,
                EntryPoint = new StringView { Data = (nint)fsPtr, Length = (UIntPtr)fsLen },
                TargetCount = (UIntPtr)1,
                Targets = (nint)(&colorTarget),
            };
            var desc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout.Handle,
                Vertex = vertex,
                Fragment = (nint)(&fragment),
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None,
                },
                Multisample = new MultisampleState { Count = 1, Mask = ~0u },
            };
            pipeline = _device.CreateRenderPipeline(desc);
        }
        return pipeline;
    }

    private unsafe RenderPipeline BuildGlyphPipeline(out PipelineLayout pipelineLayout, out BindGroupLayout atlasLayout, out BindGroupLayout instanceLayout)
    {
        var uniformEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform, MinBindingSize = (ulong)sizeof(SurfaceSizeData) },
        };
        var textureEntry = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = (ulong)ShaderStage.Fragment,
            Texture = new TextureBindingLayout { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.D2, Multisampled = 0 },
        };
        var samplerEntry = new BindGroupLayoutEntry
        {
            Binding = 2,
            Visibility = (ulong)ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
        };
        // WP-3537 binding 3: the framebuffer copy (local background), read via
        // textureLoad in the mono glyph shader for contrast-adaptive weight.
        var bgCopyEntry = new BindGroupLayoutEntry
        {
            Binding = 3,
            Visibility = (ulong)ShaderStage.Fragment,
            Texture = new TextureBindingLayout { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.D2, Multisampled = 0 },
        };

        var atlasEntries = stackalloc BindGroupLayoutEntry[4];
        atlasEntries[0] = uniformEntry;
        atlasEntries[1] = textureEntry;
        atlasEntries[2] = samplerEntry;
        atlasEntries[3] = bgCopyEntry;
        atlasLayout = _device.CreateBindGroupLayout(new BindGroupLayoutDescriptor { EntryCount = (UIntPtr)4, Entries = (nint)atlasEntries });

        var instanceEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 0 },
        };
        var instanceEntries = stackalloc BindGroupLayoutEntry[1];
        instanceEntries[0] = instanceEntry;
        instanceLayout = _device.CreateBindGroupLayout(new BindGroupLayoutDescriptor { EntryCount = (UIntPtr)1, Entries = (nint)instanceEntries });

        var layoutHandles = stackalloc nint[2];
        layoutHandles[0] = atlasLayout.Handle;
        layoutHandles[1] = instanceLayout.Handle;
        var plDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (UIntPtr)2,
            BindGroupLayouts = (nint)layoutHandles,
        };
        pipelineLayout = _device.CreatePipelineLayout(plDesc);

        Span<byte> vsName = stackalloc byte[3];
        Span<byte> fsName = stackalloc byte[3];
        int vsLen = Encoding.UTF8.GetBytes("vs", vsName);
        int fsLen = Encoding.UTF8.GetBytes("fs", fsName);

        var blendState = new BlendState
        {
            Color = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
            Alpha = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
        };

        RenderPipeline pipeline;
        fixed (byte* vsPtr = vsName)
        fixed (byte* fsPtr = fsName)
        {
            BlendState* blendPtr = &blendState;
            var colorTarget = new ColorTargetState
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = (nint)blendPtr,
                WriteMask = (ulong)ColorWriteMask.All,
            };
            var vertex = new VertexState
            {
                Module = _glyphShader.Handle,
                EntryPoint = new StringView { Data = (nint)vsPtr, Length = (UIntPtr)vsLen },
            };
            var fragment = new FragmentState
            {
                Module = _glyphShader.Handle,
                EntryPoint = new StringView { Data = (nint)fsPtr, Length = (UIntPtr)fsLen },
                TargetCount = (UIntPtr)1,
                Targets = (nint)(&colorTarget),
            };
            var desc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout.Handle,
                Vertex = vertex,
                Fragment = (nint)(&fragment),
                Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleStrip },
                Multisample = new MultisampleState { Count = 1, Mask = ~0u },
            };
            pipeline = _device.CreateRenderPipeline(desc);
        }
        return pipeline;
    }

    private unsafe RenderPipeline BuildBlurPipeline(out PipelineLayout pipelineLayout, out BindGroupLayout bind0Layout, out BindGroupLayout instanceLayout)
    {
        var uniformEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform, MinBindingSize = (ulong)sizeof(SurfaceSizeData) },
        };
        var bgTexEntry = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = (ulong)ShaderStage.Fragment,
            Texture = new TextureBindingLayout { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.D2, Multisampled = 0 },
        };
        var samplerEntry = new BindGroupLayoutEntry
        {
            Binding = 2,
            Visibility = (ulong)ShaderStage.Fragment,
            Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
        };
        var bind0Entries = stackalloc BindGroupLayoutEntry[3];
        bind0Entries[0] = uniformEntry;
        bind0Entries[1] = bgTexEntry;
        bind0Entries[2] = samplerEntry;
        bind0Layout = _device.CreateBindGroupLayout(new BindGroupLayoutDescriptor { EntryCount = (UIntPtr)3, Entries = (nint)bind0Entries });

        var instanceEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = (ulong)(ShaderStage.Vertex | ShaderStage.Fragment),
            Buffer = new BufferBindingLayout { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 0 },
        };
        var instanceEntries = stackalloc BindGroupLayoutEntry[1];
        instanceEntries[0] = instanceEntry;
        instanceLayout = _device.CreateBindGroupLayout(new BindGroupLayoutDescriptor { EntryCount = (UIntPtr)1, Entries = (nint)instanceEntries });

        var layoutHandles = stackalloc nint[2];
        layoutHandles[0] = bind0Layout.Handle;
        layoutHandles[1] = instanceLayout.Handle;
        pipelineLayout = _device.CreatePipelineLayout(new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (UIntPtr)2,
            BindGroupLayouts = (nint)layoutHandles,
        });

        Span<byte> vsName = stackalloc byte[3];
        Span<byte> fsName = stackalloc byte[3];
        int vsLen = Encoding.UTF8.GetBytes("vs", vsName);
        int fsLen = Encoding.UTF8.GetBytes("fs", fsName);

        var blendState = new BlendState
        {
            Color = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
            Alpha = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
        };

        RenderPipeline pipeline;
        fixed (byte* vsPtr = vsName)
        fixed (byte* fsPtr = fsName)
        {
            BlendState* blendPtr = &blendState;
            var colorTarget = new ColorTargetState
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = (nint)blendPtr,
                WriteMask = (ulong)ColorWriteMask.All,
            };
            var vertex = new VertexState
            {
                Module = _blurShader.Handle,
                EntryPoint = new StringView { Data = (nint)vsPtr, Length = (UIntPtr)vsLen },
            };
            var fragment = new FragmentState
            {
                Module = _blurShader.Handle,
                EntryPoint = new StringView { Data = (nint)fsPtr, Length = (UIntPtr)fsLen },
                TargetCount = (UIntPtr)1,
                Targets = (nint)(&colorTarget),
            };
            var desc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout.Handle,
                Vertex = vertex,
                Fragment = (nint)(&fragment),
                Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleStrip },
                Multisample = new MultisampleState { Count = 1, Mask = ~0u },
            };
            pipeline = _device.CreateRenderPipeline(desc);
        }
        return pipeline;
    }

    private unsafe RenderPipeline BuildColorGlyphPipeline(out PipelineLayout pipelineLayout)
    {
        // Reuse the same bind group layouts as the regular glyph pipeline
        var layoutHandles = stackalloc nint[2];
        layoutHandles[0] = _glyphAtlasLayout.Handle;
        layoutHandles[1] = _glyphInstanceLayout.Handle;
        var plDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (UIntPtr)2,
            BindGroupLayouts = (nint)layoutHandles,
        };
        pipelineLayout = _device.CreatePipelineLayout(plDesc);

        Span<byte> vsName = stackalloc byte[3];
        Span<byte> fsName = stackalloc byte[3];
        int vsLen = Encoding.UTF8.GetBytes("vs", vsName);
        int fsLen = Encoding.UTF8.GetBytes("fs", fsName);

        var blendState = new BlendState
        {
            Color = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
            Alpha = new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.OneMinusSrcAlpha },
        };

        RenderPipeline pipeline;
        fixed (byte* vsPtr = vsName)
        fixed (byte* fsPtr = fsName)
        {
            BlendState* blendPtr = &blendState;
            var colorTarget = new ColorTargetState
            {
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = (nint)blendPtr,
                WriteMask = (ulong)ColorWriteMask.All,
            };
            var vertex = new VertexState
            {
                Module = _colorGlyphShader.Handle,
                EntryPoint = new StringView { Data = (nint)vsPtr, Length = (UIntPtr)vsLen },
            };
            var fragment = new FragmentState
            {
                Module = _colorGlyphShader.Handle,
                EntryPoint = new StringView { Data = (nint)fsPtr, Length = (UIntPtr)fsLen },
                TargetCount = (UIntPtr)1,
                Targets = (nint)(&colorTarget),
            };
            var desc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout.Handle,
                Vertex = vertex,
                Fragment = (nint)(&fragment),
                Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleStrip },
                Multisample = new MultisampleState { Count = 1, Mask = ~0u },
            };
            pipeline = _device.CreateRenderPipeline(desc);
        }
        return pipeline;
    }

    private unsafe void UpdateGlyphAtlasBindGroup()
    {
        if (!_glyphAtlasBindGroup.IsInvalid)
        {
            _glyphAtlasBindGroup.Dispose();
        }

        var entries = stackalloc BindGroupEntry[4];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = _glyphUniformBuffer.Handle, Offset = 0, Size = (ulong)sizeof(SurfaceSizeData) };
        entries[1] = new BindGroupEntry { Binding = 1, TextureView = _glyphAtlasView.Handle };
        entries[2] = new BindGroupEntry { Binding = 2, Sampler = _glyphSampler.Handle };
        entries[3] = new BindGroupEntry { Binding = 3, TextureView = _bgCopyView.Handle };

        _glyphAtlasBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _glyphAtlasLayout.Handle,
            EntryCount = (UIntPtr)4,
            Entries = (nint)entries,
        });
    }

    private unsafe void UpdateGlyphInstanceBindGroup()
    {
        if (!_glyphInstanceBindGroup.IsInvalid)
        {
            _glyphInstanceBindGroup.Dispose();
        }

        var entries = stackalloc BindGroupEntry[1];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = _glyphInstanceBuffer.Handle, Offset = 0, Size = (ulong)_glyphInstanceCapacity };

        _glyphInstanceBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _glyphInstanceLayout.Handle,
            EntryCount = (UIntPtr)1,
            Entries = (nint)entries,
        });
    }

    private unsafe void UpdateColorGlyphInstanceBindGroup()
    {
        if (!_colorGlyphInstanceBindGroup.IsInvalid)
        {
            _colorGlyphInstanceBindGroup.Dispose();
        }

        var entries = stackalloc BindGroupEntry[1];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = _colorGlyphInstanceBuffer.Handle, Offset = 0, Size = (ulong)_colorGlyphInstanceCapacity };

        _colorGlyphInstanceBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _glyphInstanceLayout.Handle,
            EntryCount = (UIntPtr)1,
            Entries = (nint)entries,
        });
    }

    private unsafe void UpdateColorGlyphAtlasBindGroup()
    {
        if (!_colorGlyphAtlasBindGroup.IsInvalid)
        {
            _colorGlyphAtlasBindGroup.Dispose();
        }

        var entries = stackalloc BindGroupEntry[4];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = _glyphUniformBuffer.Handle, Offset = 0, Size = (ulong)sizeof(SurfaceSizeData) };
        entries[1] = new BindGroupEntry { Binding = 1, TextureView = _colorGlyphAtlasView.Handle };
        entries[2] = new BindGroupEntry { Binding = 2, Sampler = _colorGlyphSampler.Handle };
        // Binding 3 is required by the shared layout; the color glyph shader does not
        // sample it (emoji carry their own colour and need no weight).
        entries[3] = new BindGroupEntry { Binding = 3, TextureView = _bgCopyView.Handle };

        _colorGlyphAtlasBindGroup = _device.CreateBindGroup(new BindGroupDescriptor
        {
            Layout = _glyphAtlasLayout.Handle,
            EntryCount = (UIntPtr)4,
            Entries = (nint)entries,
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _stripCoverageScene?.Dispose();
        _geomBindGroup.Dispose();
        if (!_geomStorageBuffer.IsInvalid)
        {
            _geomStorageBuffer.Dispose();
        }
        _geomUniformBuffer.Dispose();
        _geomBgLayout.Dispose();
        _geomPipeline.Dispose();
        _geomPipelineLayout.Dispose();
        _geomShader.Dispose();

        _textVertexBuffer.Dispose();
        _textSampler.Dispose();
        _textBgLayout.Dispose();
        _textPipeline.Dispose();
        _textPipelineLayout.Dispose();
        _textShader.Dispose();

        _fallbackBindGroup.Dispose();
        _fallbackTextureView.Dispose();
        _fallbackTexture.Dispose();

        foreach (var bg in _imageBindGroups.Values)
        {
            bg.Dispose();
        }
        foreach (var view in _imageTextureViews.Values)
        {
            view.Dispose();
        }
        foreach (var tex in _imageTextures.Values)
        {
            tex.Dispose();
        }
        _imageBindGroups.Clear();
        _imageTextureViews.Clear();
        _imageTextures.Clear();
        _imageVertexBuffer.Dispose();

        _compositor.Dispose();

        if (!_colorGlyphInstanceBindGroup.IsInvalid)
        {
            _colorGlyphInstanceBindGroup.Dispose();
        }
        _colorGlyphInstanceBuffer.Dispose();
        _colorGlyphAtlasBindGroup.Dispose();
        _colorGlyphSampler.Dispose();
        _colorGlyphAtlasView.Dispose();
        _colorGlyphAtlas.Dispose();

        _glyphInstanceBindGroup.Dispose();
        _glyphAtlasBindGroup.Dispose();
        _glyphInstanceBuffer.Dispose();
        _glyphUniformBuffer.Dispose();
        if (!_bgCopyView.IsInvalid) { _bgCopyView.Dispose(); }
        if (!_bgCopyTexture.IsInvalid) { _bgCopyTexture.Dispose(); }
        _glyphAtlasView.Dispose();
        _glyphAtlas.Dispose();
        _glyphSampler.Dispose();
        _glyphInstanceLayout.Dispose();
        _glyphAtlasLayout.Dispose();
        _glyphPipeline.Dispose();
        _glyphPipelineLayout.Dispose();
        _glyphShader.Dispose();

        _blurPipeline.Dispose();
        _blurPipelineLayout.Dispose();
        _blurBind0Layout.Dispose();
        _blurInstanceLayout.Dispose();
        _blurSampler.Dispose();
        _blurShader.Dispose();
        if (!_blurInstanceBuffer.IsInvalid) { _blurInstanceBuffer.Dispose(); }

        if (!_stagingBuffer.IsInvalid)
        {
            _stagingBuffer.Dispose();
        }

        _swapChain.Dispose();
        _surface.Dispose();
        _device.Dispose();
        _adapter.Dispose();
        _instance.Dispose();
    }

    private static void SaveRgbaAsBmp(byte[] rgba, int width, int height, string path)
    {
        int rowSize = ((width * 4 + 3) / 4) * 4;
        int imageSize = rowSize * height;
        int fileSize = 54 + imageSize;

        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
        using var bw = new System.IO.BinaryWriter(fs);

        // BMP header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0);
        bw.Write(54);

        // DIB header (BITMAPINFOHEADER)
        bw.Write(40);
        bw.Write(width);
        bw.Write(height);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write(0);
        bw.Write(imageSize);
        bw.Write(2835);
        bw.Write(2835);
        bw.Write(0);
        bw.Write(0);

        // Pixel data (BGRA, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = (y * width + x) * 4;
                bw.Write(rgba[srcIdx + 2]); // B
                bw.Write(rgba[srcIdx + 1]); // G
                bw.Write(rgba[srcIdx + 0]); // R
                bw.Write(rgba[srcIdx + 3]); // A
            }
            for (int p = width * 4; p < rowSize; p++)
            {
                bw.Write((byte)0);
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;
using Etch.Gpu.Descriptors;

#pragma warning disable CA2263

Console.WriteLine("RenderPipelineDescriptor: " + Marshal.SizeOf<RenderPipelineDescriptor>());
Console.WriteLine("VertexState: " + Marshal.SizeOf<VertexState>());
Console.WriteLine("FragmentState: " + Marshal.SizeOf<FragmentState>());
Console.WriteLine("PrimitiveState: " + Marshal.SizeOf<PrimitiveState>());
Console.WriteLine("MultisampleState: " + Marshal.SizeOf<MultisampleState>());
Console.WriteLine("ColorTargetState: " + Marshal.SizeOf<ColorTargetState>());
Console.WriteLine("VertexBufferLayout: " + Marshal.SizeOf<VertexBufferLayout>());
Console.WriteLine("VertexAttribute: " + Marshal.SizeOf<VertexAttribute>());
Console.WriteLine("BindGroupLayoutEntry: " + Marshal.SizeOf<BindGroupLayoutEntry>());
Console.WriteLine("BindGroupLayoutDescriptor: " + Marshal.SizeOf<BindGroupLayoutDescriptor>());
Console.WriteLine("PipelineLayoutDescriptor: " + Marshal.SizeOf<PipelineLayoutDescriptor>());
Console.WriteLine("SamplerDescriptor: " + Marshal.SizeOf<SamplerDescriptor>());
Console.WriteLine("RenderPassDescriptor: " + Marshal.SizeOf<RenderPassDescriptor>());
Console.WriteLine("RenderPassColorAttachment: " + Marshal.SizeOf<RenderPassColorAttachment>());
Console.WriteLine("ShaderModuleDescriptor: " + Marshal.SizeOf<ShaderModuleDescriptor>());
Console.WriteLine("ShaderSourceWGSL: " + Marshal.SizeOf<ShaderSourceWGSL>());
Console.WriteLine("TextureDescriptor: " + Marshal.SizeOf<TextureDescriptor>());
Console.WriteLine("BufferDescriptor: " + Marshal.SizeOf<BufferDescriptor>());
Console.WriteLine("StringView: " + Marshal.SizeOf<StringView>());
Console.WriteLine("Color: " + Marshal.SizeOf<Color>());
Console.WriteLine("WGPUStringView: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUStringView>());
Console.WriteLine("WGPUChainedStruct: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUChainedStruct>());
Console.WriteLine("WGPUSurfaceConfiguration: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUSurfaceConfiguration>());
Console.WriteLine("WGPUSurfaceTexture: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUSurfaceTexture>());
Console.WriteLine("BindGroupEntry: " + Marshal.SizeOf<BindGroupEntry>());
Console.WriteLine("BindGroupDescriptor: " + Marshal.SizeOf<BindGroupDescriptor>());
Console.WriteLine("Extent3D: " + Marshal.SizeOf<Extent3D>());
Console.WriteLine("WGPUOrigin3D: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUOrigin3D>());
Console.WriteLine("WGPUTexelCopyBufferLayout: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUTexelCopyBufferLayout>());
Console.WriteLine("WGPUTexelCopyTextureInfo: " + Marshal.SizeOf<Etch.Gpu.Native.WGPUTexelCopyTextureInfo>());

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Mathematics.Interop;
using D3D = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;
using D2D = SharpDX.Direct2D1;
using DC = SharpDX.DirectComposition;

public sealed class EspOverlay : Form {
 const int ExLayered = 0x00080000;
 const int ExTopmost = 0x8;
 const int ExNoActivate = 0x08000000;
 const int ExTransparent = 0x20;
 const int ExToolWindow = 0x80;
 const int ExNoRedirectionBitmap = 0x00200000;
 const uint NoSize = 0x1, NoMove = 0x2, NoActivate = 0x10, NoOwnerZOrder = 0x200;
 static readonly IntPtr HwndTopmost = new IntPtr(-1);
 [DllImport("user32.dll", SetLastError = true)]
 static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
 [DllImport("user32.dll", SetLastError = true)]
 static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
 [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
 static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
 D3D.Device device;
 DXGI.Device dxgi;
 DXGI.Factory2 factory;
 DXGI.SwapChain1 swap;
 D2D.Device d2device;
 D2D.DeviceContext draw;
 D2D.Bitmap1 bitmap;
 D2D.SolidColorBrush brush;
 DC.Device composition;
 DC.Target target;
 DC.Visual visual;
 readonly Stopwatch clock = Stopwatch.StartNew();
 long next;
 Size surfaceSize;
 public int Updates, Count, PresentSkipped, LastPresentCode;
 public double LastUpdateMs;
 public bool IsTopmost { get { return IsHandleCreated && (GetWindowLongPtr(Handle, -20).ToInt64() & ExTopmost) != 0; } }
 public bool InputTransparent { get { const int required = ExLayered | ExTransparent; return IsHandleCreated && (GetWindowLongPtr(Handle, -20).ToInt64() & required) == required; } }
 public long WindowHandle { get { return IsHandleCreated ? Handle.ToInt64() : 0; } }

 public EspOverlay() {
  FormBorderStyle = FormBorderStyle.None;
  ShowInTaskbar = false;
  TopMost = true;
  AutoScaleMode = AutoScaleMode.None;
  TabStop = false;
  SetStyle(ControlStyles.Selectable, false);
  SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
 }
 protected override bool ShowWithoutActivation { get { return true; } }
 protected override CreateParams CreateParams {
  get {
   var p = base.CreateParams;
   p.ExStyle |= ExTopmost | ExNoActivate | ExTransparent | ExToolWindow | ExNoRedirectionBitmap | ExLayered;
   return p;
  }
 }
 protected override void OnHandleCreated(EventArgs e) {
  base.OnHandleCreated(e);
  // Layered + transparent routes mouse input to windows in other processes too.
  // HTTRANSPARENT alone only continues hit-testing within the same thread.
  if (!SetLayeredWindowAttributes(Handle, 0, 255, 2))
   throw new Win32Exception(Marshal.GetLastWin32Error(), "显示窗口鼠标穿透初始化失败");
 }
 protected override void WndProc(ref Message m) {
  if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
  if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
  base.WndProc(ref m);
 }
 protected override void OnPaint(PaintEventArgs e) { }
 protected override void OnPaintBackground(PaintEventArgs e) { }
 protected override void OnHandleDestroyed(EventArgs e) {
  ReleaseRenderer();
  base.OnHandleDestroyed(e);
 }
 void EnsureTopmost() {
  if (!SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, NoSize | NoMove | NoActivate | NoOwnerZOrder))
   throw new Win32Exception(Marshal.GetLastWin32Error(), "显示窗口置顶失败");
 }
 void Setup(Size size) {
  if (device == null) {
   device = new D3D.Device(SharpDX.Direct3D.DriverType.Hardware, D3D.DeviceCreationFlags.BgraSupport);
   dxgi = device.QueryInterface<DXGI.Device>();
   using (var adapter = dxgi.Adapter) factory = adapter.GetParent<DXGI.Factory2>();
   d2device = new D2D.Device(dxgi);
   draw = new D2D.DeviceContext(d2device, D2D.DeviceContextOptions.None);
   draw.AntialiasMode = D2D.AntialiasMode.Aliased;
   brush = new D2D.SolidColorBrush(draw, new RawColor4(1f, 0.59f, 0.35f, 1f));
   composition = new DC.Device(dxgi);
   var arguments = new object[] { Handle, new RawBool(true), null };
   typeof(DC.Device).GetMethod("CreateTargetForHwnd", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(composition, arguments);
   target = (DC.Target)arguments[2];
   visual = new DC.Visual(composition);
   target.Root = visual;
  }
  if (swap != null && size == surfaceSize) return;
  draw.Target = null;
  if (bitmap != null) { bitmap.Dispose(); bitmap = null; }
  if (swap != null) { visual.Content = null; swap.Dispose(); swap = null; }
  var desc = new DXGI.SwapChainDescription1 {
   Width = size.Width, Height = size.Height,
   Format = DXGI.Format.B8G8R8A8_UNorm, Stereo = false,
   SampleDescription = new DXGI.SampleDescription(1, 0), Usage = DXGI.Usage.RenderTargetOutput,
   BufferCount = 2, Scaling = DXGI.Scaling.Stretch,
   SwapEffect = DXGI.SwapEffect.FlipSequential, AlphaMode = DXGI.AlphaMode.Premultiplied
  };
  swap = new DXGI.SwapChain1(factory, device, ref desc);
  using (var surface = swap.GetBackBuffer<DXGI.Surface>(0))
   bitmap = new D2D.Bitmap1(draw, surface, new D2D.BitmapProperties1(
    new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
    96, 96, D2D.BitmapOptions.Target | D2D.BitmapOptions.CannotDraw));
  draw.Target = bitmap;
  visual.Content = swap;
  composition.Commit();
  surfaceSize = size;
 }
 public void UpdateDisplay(Rectangle bounds, IList<EspMarker> marks, bool active, bool boxes, bool lines) {
  if (!active || marks.Count == 0 || (!boxes && !lines)) { Hide(); Count = 0; return; }
  if (Visible && clock.ElapsedMilliseconds < next) return;
  next = clock.ElapsedMilliseconds + 25;
  var watch = Stopwatch.StartNew();
  Bounds = bounds;
  if (!Visible) Show();
  EnsureTopmost();
  Setup(bounds.Size);
  draw.BeginDraw();
  draw.Clear(new RawColor4(0, 0, 0, 0));
  foreach (var marker in marks) {
   if (boxes) draw.DrawRectangle(new RawRectangleF((float)marker.Left, (float)marker.Top, (float)marker.Right, (float)marker.Bottom), brush, 1.5f);
   if (lines) draw.DrawLine(new RawVector2(bounds.Width / 2f, bounds.Height - 2), new RawVector2((float)(marker.Left + marker.Right) / 2, (float)marker.Bottom), brush, 1f);
  }
  draw.EndDraw();
  var result = swap.Present(0, DXGI.PresentFlags.DoNotWait);
  LastPresentCode = result.Code;
  if (result.Code == unchecked((int)0x887A000A) || result.Code == 0x087A0001) {
   PresentSkipped++;
   Count = 0;
   return;
  }
  result.CheckError();
  Count = marks.Count;
  Updates++;
  LastUpdateMs = watch.Elapsed.TotalMilliseconds;
 }
 void ReleaseRenderer() {
  if (draw != null) draw.Target = null;
  foreach (var resource in new IDisposable[] { brush, bitmap, visual, target, composition, swap, draw, d2device, factory, dxgi, device })
   if (resource != null) resource.Dispose();
  brush = null; bitmap = null; visual = null; target = null; composition = null;
  swap = null; draw = null; d2device = null; factory = null; dxgi = null; device = null;
  surfaceSize = Size.Empty;
  Count = 0;
 }
 protected override void Dispose(bool disposing) {
  if (disposing) ReleaseRenderer();
  base.Dispose(disposing);
 }
 public static void Smoke() {
  using (var overlay = new EspOverlay()) {
   overlay.Setup(new Size(800, 600));
   overlay.draw.BeginDraw();
   overlay.draw.Clear(new RawColor4(0, 0, 0, 0));
   overlay.draw.DrawRectangle(new RawRectangleF(10, 10, 50, 100), overlay.brush, 2);
   overlay.draw.EndDraw();
   using (var buffer = overlay.swap.GetBackBuffer<D3D.Texture2D>(0)) {
    var desc = buffer.Description;
    desc.Usage = D3D.ResourceUsage.Staging; desc.BindFlags = D3D.BindFlags.None;
    desc.CpuAccessFlags = D3D.CpuAccessFlags.Read; desc.OptionFlags = D3D.ResourceOptionFlags.None;
    using (var copy = new D3D.Texture2D(overlay.device, desc)) {
     overlay.device.ImmediateContext.CopyResource(buffer, copy);
     var mapped = overlay.device.ImmediateContext.MapSubresource(copy, 0, D3D.MapMode.Read, D3D.MapFlags.None);
     try {
      int edge = Marshal.ReadByte(mapped.DataPointer, 50 * mapped.RowPitch + 10 * 4 + 3);
      int center = Marshal.ReadByte(mapped.DataPointer, 50 * mapped.RowPitch + 25 * 4 + 3);
      if (edge != 255 || center != 0) throw new Exception("GPU alpha readback mismatch: " + edge + "," + center);
     } finally { overlay.device.ImmediateContext.UnmapSubresource(copy, 0); }
    }
   }
   overlay.swap.Present(0, DXGI.PresentFlags.None);
  }
 }
}

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.Win32;
using Hexa.NET.ImPlot;
using HexaGen.Runtime;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Bonsai.Ephys.Design
{
    internal class ImGuiControl : GLControl, IGLContext
    {
        static readonly object ConfigureEvent = new();
        static readonly object RenderEvent = new();
        ImGuiContextPtr guiContext;
        ImPlotContextPtr plotContext;
        bool disposed;
        bool resizing;

        public ImGuiControl()
        {
            GraphicsContext.ShareContexts = false;
            Size = new Size(640, 480);
        }

        public event EventHandler Configure
        {
            add { Events.AddHandler(ConfigureEvent, value); }
            remove { Events.RemoveHandler(ConfigureEvent, value); }
        }

        public event EventHandler Render
        {
            add { Events.AddHandler(RenderEvent, value); }
            remove { Events.RemoveHandler(RenderEvent, value); }
        }

        protected virtual void OnConfigure(EventArgs e)
        {
            if (Events[ConfigureEvent] is EventHandler handler)
            {
                handler(this, e);
            }
        }

        protected virtual void OnRender(EventArgs e)
        {
            if (Events[RenderEvent] is EventHandler handler)
            {
                handler(this, e);
            }
        }

        protected unsafe override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (HasValidContext)
            {
                var parentForm = FindForm();
                parentForm.ResizeBegin += (sender, e) => resizing = true;
                parentForm.ResizeEnd += (sender, e) => resizing = false;
                parentForm.FormClosing += (sender, e) => MakeCurrent();

                guiContext = ImGui.CreateContext(null);
                ImGui.SetCurrentContext(guiContext);
                ImGuiImplOpenGL3.SetCurrentContext(guiContext);
                ImGuiImplWin32.SetCurrentContext(guiContext);

                var io = ImGui.GetIO();
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;     // Enable Keyboard Controls
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;      // Enable Gamepad Controls
                io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
                io.IniFilename = null;

                ImGuiImplWin32.InitForOpenGL(Handle);
                ImGuiImplOpenGL3.Init((string)null);

                ImPlot.SetImGuiContext(guiContext);
                plotContext = ImPlot.CreateContext();
                ImPlot.SetCurrentContext(plotContext);
                OnConfigure(EventArgs.Empty);
            }
        }

        public new virtual void MakeCurrent()
        {
            base.MakeCurrent();
            ImGui.SetCurrentContext(guiContext);
            ImGuiImplWin32.SetCurrentContext(guiContext);
            ImGuiImplOpenGL3.SetCurrentContext(guiContext);

            ImPlot.SetCurrentContext(plotContext);
            ImPlot.SetImGuiContext(guiContext);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!DesignMode && HasValidContext && !resizing)
            {
                MakeCurrent();
                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplWin32.NewFrame();
                ImGui.NewFrame();

                OnRender(EventArgs.Empty);

                ImGui.Render();
                GL.Viewport(0, 0, Width, Height);
                GL.ClearColor(Color.Black);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

                SwapBuffers();
            }

            base.OnPaint(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (!disposed)
            {
                ImGuiImplWin32.SetCurrentContext(guiContext);
                if (ImGuiImplWin32.WndProcHandler(Handle, (uint)m.Msg, (nuint)m.WParam.ToInt64(), m.LParam) != 0)
                    return;
            }

            base.WndProc(ref m);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (HasValidContext && !disposed)
            {
                ImPlot.SetCurrentContext(null);
                ImPlot.DestroyContext(plotContext);

                ImGuiImplOpenGL3.Shutdown();
                ImGuiImplWin32.Shutdown();
                ImGui.SetCurrentContext(null);
                ImGui.DestroyContext(guiContext);
                disposed = true;
            }

            base.OnHandleDestroyed(e);
        }

        bool IGLContext.IsCurrent => Context.IsCurrent;

        nint INativeContext.GetProcAddress(string procName)
        {
            return ((IGraphicsContextInternal)Context).GetAddress(procName);
        }

        bool INativeContext.IsExtensionSupported(string extensionName)
        {
            return true; // TODO*
        }

        void IGLContext.SwapInterval(int interval)
        {
            Context.SwapInterval = interval;
        }

        bool INativeContext.TryGetProcAddress(string procName, out nint procAddress)
        {
            procAddress = ((IGraphicsContextInternal)Context).GetAddress(procName);
            return procAddress != 0;
        }
    }
}

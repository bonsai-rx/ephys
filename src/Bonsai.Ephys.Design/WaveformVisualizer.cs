using Bonsai;
using Bonsai.Design;
using Bonsai.Expressions;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reactive;
using System.Windows.Forms;

[assembly: TypeVisualizer(typeof(Bonsai.Ephys.Design.WaveformVisualizer), Target = typeof(Mat))]

namespace Bonsai.Ephys.Design
{
    public class WaveformVisualizer : BufferedVisualizer
    {
        const int TextBoxWidth = 100;
        const int MinChannelHeight = 10;
        const int TimeChannelHeight = 25;
        static readonly string[] themeNames = Enum.GetNames(typeof(ColorTheme));

        ImGuiControl imGuiCanvas;
        Decimator decimatorMin;
        Decimator decimatorMax;
        Mat timeRange;

        int channelHeight = 25;
        int sampleRate = 44100;
        int maxSamplesPerChannel = 1920;
        double timeBase = 1.0;

        public ColorTheme ColorTheme { get; set; } = ColorTheme.Light;

        public int ChannelHeight
        {
            get => channelHeight;
            set => channelHeight = value;
        }

        public double TimeBase
        {
            get => timeBase;
            set => timeBase = value;
        }

        public override void Show(object value)
        {
            if (value is Mat data)
            {
                var totalSamples = (int)(timeBase * sampleRate);
                var samplesPerBin = Math.Max(1, totalSamples / maxSamplesPerChannel);
                if (timeRange is null || decimatorMin.Buffer.Rows != data.Rows || decimatorMin.DownsampleFactor != samplesPerBin)
                {
                    decimatorMin = new Decimator(data, maxSamplesPerChannel, samplesPerBin, ReduceOperation.Min);
                    decimatorMax = new Decimator(data, maxSamplesPerChannel, samplesPerBin, ReduceOperation.Max);
                    timeRange = new Mat(1, maxSamplesPerChannel, Depth.F32, 1);
                    CV.Range(timeRange, 0, timeBase);
                }

                decimatorMin.Process(data);
                decimatorMax.Process(data);
            }
        }

        protected override void ShowBuffer(IList<Timestamped<object>> values)
        {
            imGuiCanvas.Invalidate();
            base.ShowBuffer(values);
        }

        public unsafe override void Load(IServiceProvider provider)
        {
            var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
            if (ExpressionBuilder.GetVisualizerElement(context.Source).Builder is WaveformVisualizerBuilder visualizerBuilder)
            {
                sampleRate = visualizerBuilder.SampleRate;
                maxSamplesPerChannel = visualizerBuilder.MaxSamplesPerChannel;
                if (visualizerBuilder.ChannelHeight.HasValue)
                    channelHeight = visualizerBuilder.ChannelHeight.GetValueOrDefault();
                if (visualizerBuilder.TimeBase.HasValue)
                    timeBase = visualizerBuilder.TimeBase.GetValueOrDefault();
            }

            imGuiCanvas = new ImGuiControl();
            imGuiCanvas.Dock = DockStyle.Fill;
            imGuiCanvas.Render += (sender, e) =>
            {
                var dockspaceId = ImGui.DockSpaceOverViewport(
                    dockspaceId: 0,
                    ImGui.GetMainViewport(),
                    ImGuiDockNodeFlags.AutoHideTabBar | ImGuiDockNodeFlags.NoUndocking);

                switch (ColorTheme)
                {
                    case ColorTheme.Light:
                        ImGui.StyleColorsLight();
                        ImPlot.StyleColorsLight(ImPlot.GetStyle());
                        break;
                    case ColorTheme.Dark:
                        ImGui.StyleColorsDark();
                        ImPlot.StyleColorsDark(ImPlot.GetStyle());
                        break;
                }

                ImGui.Begin(nameof(WaveformVisualizer));
                ImGui.PushItemWidth(TextBoxWidth);

                var editTimeBase = timeBase;
                ImGui.InputDouble("Timebase (s)", ref editTimeBase, "%.3g");
                if (ImGui.IsItemDeactivatedAfterEdit())
                    timeBase = editTimeBase;

                ImGui.SameLine();
                if (ImGui.InputInt("Channel Height", ref channelHeight))
                    channelHeight = Math.Max(MinChannelHeight, channelHeight);
                ImGui.SameLine();

                var selectedTheme = ColorTheme.ToString();
                if (ImGui.BeginCombo("Color Style", selectedTheme))
                {
                    for (int i = 0; i < themeNames.Length; i++)
                    {
                        var isSelected = themeNames[i] == selectedTheme;
                        if (ImGui.Selectable(themeNames[i], isSelected))
                            ColorTheme = (ColorTheme)Enum.Parse(typeof(ColorTheme), themeNames[i]);
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();

                if (timeRange is null)
                    goto end;

                decimatorMin.Buffer.GetRawData(out IntPtr minPtr, out int minStep, out Size minShape);
                decimatorMax.Buffer.GetRawData(out IntPtr maxPtr, out int maxStep, out Size maxShape);
                timeRange.GetRawData(out IntPtr timeRangePtr, out int timeRangeStep, out Size _);
                ImPlot.PushStyleVar(ImPlotStyleVar.FitPadding, new Vector2(0, 0.1f));
                ImPlot.PushStyleVar(ImPlotStyleVar.Padding, new Vector2(0, 0));
                ImPlot.PushStyleVar(ImPlotStyleVar.BorderSize, 0);

                var tableFlags = ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
                var dataPlotFlags = ImPlotFlags.CanvasOnly | ImPlotFlags.NoFrame;
                var axesFlags = ImPlotAxisFlags.NoHighlight | ImPlotAxisFlags.NoInitialFit | ImPlotAxisFlags.AutoFit;
                var bareAxesFlags = axesFlags | ImPlotAxisFlags.NoDecorations;

                ImGui.BeginChild("##data");
                if (ImGui.BeginTable("##table", 2, tableFlags, new Vector2(-1, -1)))
                {
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 10);
                    ImGui.TableSetupColumn(string.Empty);
                    ImGui.TableSetupScrollFreeze(0, 1);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var timeLabel = "Time";
                    var cursorPosY = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cursorPosY + TimeChannelHeight / 2);
                    ImGui.Text(timeLabel);
                    ImGui.TableNextColumn();
                    if (ImPlot.BeginPlot(timeLabel, new(-1, TimeChannelHeight), dataPlotFlags))
                    {
                        ImPlot.SetupAxes(string.Empty, string.Empty, axesFlags, bareAxesFlags);
                        ImPlot.PlotInfLines(string.Empty, (float*)timeRangePtr, minShape.Width);
                        ImPlot.EndPlot();
                    }

                    for (int i = 0; i < minShape.Height; i++)
                    {
                        var channelLabel = $"CH{i}";
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        cursorPosY = ImGui.GetCursorPosY();
                        ImGui.SetCursorPosY(cursorPosY + channelHeight / 2 - 5);
                        ImGui.Text(channelLabel);
                        ImGui.TableNextColumn();
                        if (ImPlot.BeginPlot(channelLabel, new(-1, channelHeight), dataPlotFlags))
                        {
                            ImPlot.SetupAxes(string.Empty, channelLabel, bareAxesFlags, bareAxesFlags);
                            var minLinePtr = (float*)((byte*)minPtr + i * minStep);
                            var maxLinePtr = (float*)((byte*)maxPtr + i * maxStep);
                            ImPlot.PlotShaded(string.Empty, (float*)timeRangePtr, minLinePtr, maxLinePtr, minShape.Width);
                            ImPlot.PlotLine(string.Empty, (float*)timeRangePtr, minLinePtr, minShape.Width);
                            ImPlot.PlotLine(string.Empty, (float*)timeRangePtr, maxLinePtr, maxShape.Width);
                            ImPlot.EndPlot();
                        }
                    }
                    ImGui.EndTable();
                }
                
                ImPlot.PopStyleVar();
                ImGui.EndChild();

                end:
                ImGui.End();
                if (!ImGui.IsWindowDocked() &&
                    ImGuiP.DockBuilderGetCentralNode(dockspaceId) is ImGuiDockNodePtr node &&
                    !node.IsNull)
                {
                    ImGuiP.DockBuilderDockWindow(nameof(WaveformVisualizer), node.ID);
                }
            };

            var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            if (visualizerService != null)
            {
                visualizerService.AddControl(imGuiCanvas);
            }
        }

        public override void Unload()
        {
            imGuiCanvas?.Dispose();
            imGuiCanvas = null;
        }
    }

    public enum ColorTheme
    {
        Light,
        Dark
    }
}

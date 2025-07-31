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

namespace Bonsai.Ephys.Design
{
    /// <summary>
    /// Provides a type visualizer for displaying a matrix as a multi-channel waveform,
    /// with peak-preserving downsampling.
    /// </summary>
    public class WaveformVisualizer : BufferedVisualizer
    {
        const int TextBoxWidth = 80;
        const int MinChannelHeight = 10;
        const int TimeChannelHeight = 25;
        static readonly string[] ThemeNames = Enum.GetNames(typeof(ColorTheme));

        ImGuiControl imGuiCanvas;
        Decimator decimatorMin;
        Decimator decimatorMax;
        Mat timeRange;
        Mat minSnap;
        Mat maxSnap;

        int channelHeight = 20;
        int sampleRate = 30000;
        int maxSamplesPerChannel = 1920;
        double timebase = 1.0;
        int colorGrouping = 1;

        /// <summary>
        /// Gets or sets a value specifying the color theme used to style the
        /// visualizer contents.
        /// </summary>
        public ColorTheme ColorTheme { get; set; } = ColorTheme.Light;

        /// <summary>
        /// Gets or sets the height of each channel plot, in pixels.
        /// </summary>
        public int ChannelHeight
        {
            get => channelHeight;
            set => channelHeight = value;
        }

        /// <summary>
        /// Gets or sets how much time to represent in the visualizer display, in seconds.
        /// </summary>
        public double Timebase
        {
            get => timebase;
            set => timebase = value;
        }

        /// <summary>
        /// Gets or sets the number of adjacent channels to group under the same color.
        /// </summary>
        public int ColorGrouping
        {
            get => colorGrouping;
            set => colorGrouping = value;
        }

        /// <inheritdoc/>
        public override void Show(object value)
        {
            if (value is Mat data)
            {
                var totalSamples = (int)(timebase * sampleRate);
                var samplesPerBin = Math.Max(1, totalSamples / maxSamplesPerChannel);
                if (timeRange is null || decimatorMin.Buffer.Rows != data.Rows || decimatorMin.DownsampleFactor != samplesPerBin)
                {
                    timeRange?.Dispose();
                    decimatorMin?.Dispose();
                    decimatorMax?.Dispose();
                    decimatorMin = new Decimator(data, maxSamplesPerChannel, samplesPerBin, ReduceOperation.Min);
                    decimatorMax = new Decimator(data, maxSamplesPerChannel, samplesPerBin, ReduceOperation.Max);
                    timeRange = new Mat(1, maxSamplesPerChannel, Depth.F32, 1);
                    CV.Range(timeRange, 0, timebase);
                }

                decimatorMin.Process(data);
                decimatorMax.Process(data);
            }
        }

        /// <inheritdoc/>
        protected override void ShowBuffer(IList<Timestamped<object>> values)
        {
            imGuiCanvas.Invalidate();
            base.ShowBuffer(values);
        }

        void StyleColors()
        {
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
        }

        void MenuWidgets()
        {
            var tableFlags = ImGuiTableFlags.NoSavedSettings;
            if (ImGui.BeginTable("##menu", 5, tableFlags))
            {
                ImGui.TableNextRow();
                ImGui.PushItemWidth(TextBoxWidth);

                ImGui.TableNextColumn();
                if (ImGui.BeginTable("##timebaseT", 1, tableFlags))
                {
                    ImGui.TableNextColumn();
                    ImGui.Text("Timebase (s)");
                    var editTimebase = timebase;
                    ImGui.InputDouble("##timebase", ref editTimebase, "%.3g");
                    if (ImGui.IsItemDeactivatedAfterEdit())
                        timebase = editTimebase;
                    ImGui.EndTable();
                }

                ImGui.TableNextColumn();
                if (ImGui.BeginTable("##channelHeightT", 1, tableFlags))
                {
                    ImGui.TableNextColumn();
                    ImGui.Text("Channel Height");
                    if (ImGui.InputInt("##channelHeight", ref channelHeight))
                        channelHeight = Math.Max(MinChannelHeight, channelHeight);
                    ImGui.EndTable();
                }

                ImGui.TableNextColumn();
                var buttonSize = new Vector2(TextBoxWidth, ImGui.GetFrameHeight() * 2);
                if (ImGui.Button("Pause", buttonSize))
                {
                    if (minSnap is not null)
                    {
                        minSnap = null;
                        maxSnap = null;
                    }
                    else
                    {
                        minSnap = decimatorMin.Buffer.Clone();
                        maxSnap = decimatorMax.Buffer.Clone();
                    }
                }

                ImGui.TableNextColumn();
                if (ImGui.BeginTable("##colorThemeT", 1, tableFlags))
                {
                    ImGui.TableNextColumn();
                    ImGui.Text("Colour Scheme");
                    var selectedTheme = ColorTheme.ToString();
                    if (ImGui.BeginCombo("##colorTheme", selectedTheme))
                    {
                        for (int i = 0; i < ThemeNames.Length; i++)
                        {
                            var isSelected = ThemeNames[i] == selectedTheme;
                            if (ImGui.Selectable(ThemeNames[i], isSelected))
                                ColorTheme = (ColorTheme)Enum.Parse(typeof(ColorTheme), ThemeNames[i]);
                            if (isSelected)
                                ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.EndTable();
                }

                ImGui.TableNextColumn();
                if (ImGui.BeginTable("##colorGroupingT", 1, tableFlags))
                {
                    ImGui.TableNextColumn();
                    ImGui.Text("Colour Groups");
                    if (ImGui.InputInt("##colorGrouping", ref colorGrouping))
                        colorGrouping = Math.Max(1, colorGrouping);
                    ImGui.EndTable();
                }

                ImGui.PopItemWidth();
                ImGui.EndTable();
            }
        }

        unsafe void WaveformPlot(Mat minBuffer, Mat maxBuffer)
        {
            minBuffer.GetRawData(out IntPtr minPtr, out int minStep, out Size minShape);
            maxBuffer.GetRawData(out IntPtr maxPtr, out int maxStep, out Size maxShape);
            timeRange.GetRawData(out IntPtr timeRangePtr, out int timeRangeStep, out Size _);
            ImPlot.PushStyleVar(ImPlotStyleVar.FitPadding, new Vector2(0, 0.1f));
            ImPlot.PushStyleVar(ImPlotStyleVar.Padding, new Vector2(0, 0));
            ImPlot.PushStyleVar(ImPlotStyleVar.BorderSize, 0);

            var tableFlags = ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
            var dataPlotFlags = ImPlotFlags.CanvasOnly | ImPlotFlags.NoFrame;
            var axesFlags = ImPlotAxisFlags.NoHighlight | ImPlotAxisFlags.NoInitialFit | ImPlotAxisFlags.AutoFit;
            var bareAxesFlags = axesFlags | ImPlotAxisFlags.NoDecorations;

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
                    var channelColor = ImPlot.GetColormapColor(i / colorGrouping);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    cursorPosY = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cursorPosY + channelHeight / 2 - 5);
                    ImGui.Text(channelLabel);
                    ImGui.TableNextColumn();
                    if (ImPlot.BeginPlot(channelLabel, new(-1, channelHeight), dataPlotFlags))
                    {
                        ImPlot.PushStyleColor(ImPlotCol.Line, channelColor);
                        ImPlot.SetupAxes(string.Empty, channelLabel, bareAxesFlags, bareAxesFlags);
                        var minLinePtr = (float*)((byte*)minPtr + i * minStep);
                        var maxLinePtr = (float*)((byte*)maxPtr + i * maxStep);
                        ImPlot.PlotShaded(string.Empty, (float*)timeRangePtr, minLinePtr, maxLinePtr, minShape.Width);
                        ImPlot.PlotLine(string.Empty, (float*)timeRangePtr, minLinePtr, minShape.Width);
                        ImPlot.PlotLine(string.Empty, (float*)timeRangePtr, maxLinePtr, maxShape.Width);
                        ImPlot.PopStyleColor();
                        ImPlot.EndPlot();
                    }
                }
                ImGui.EndTable();
            }

            ImPlot.PopStyleVar();
        }

        /// <inheritdoc/>
        public override void Load(IServiceProvider provider)
        {
            var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
            if (ExpressionBuilder.GetVisualizerElement(context.Source).Builder is WaveformVisualizerBuilder visualizerBuilder)
            {
                sampleRate = visualizerBuilder.SampleRate;
                maxSamplesPerChannel = visualizerBuilder.MaxSamplesPerChannel;
                if (visualizerBuilder.ChannelHeight.HasValue)
                    channelHeight = visualizerBuilder.ChannelHeight.GetValueOrDefault();
                if (visualizerBuilder.Timebase.HasValue)
                    timebase = visualizerBuilder.Timebase.GetValueOrDefault();
                if (visualizerBuilder.ColorGrouping.HasValue)
                    colorGrouping = visualizerBuilder.ColorGrouping.GetValueOrDefault();
            }

            imGuiCanvas = new ImGuiControl();
            imGuiCanvas.Dock = DockStyle.Fill;
            imGuiCanvas.Render += (sender, e) =>
            {
                var dockspaceId = ImGui.DockSpaceOverViewport(
                    dockspaceId: 0,
                    ImGui.GetMainViewport(),
                    ImGuiDockNodeFlags.AutoHideTabBar | ImGuiDockNodeFlags.NoUndocking);

                StyleColors();
                if (ImGui.Begin(nameof(WaveformVisualizer)))
                {
                    MenuWidgets();
                    if (timeRange is not null)
                    {
                        ImGui.BeginChild("##data");
                        WaveformPlot(
                            minSnap ?? decimatorMin.Buffer,
                            maxSnap ?? decimatorMax.Buffer);
                        ImGui.EndChild();
                    }
                }

                ImGui.End();

                if (!ImGui.IsWindowDocked() &&
                    ImGuiP.DockBuilderGetCentralNode(dockspaceId) is ImGuiDockNodePtr node &&
                    !node.IsNull)
                {
                    ImGuiP.DockBuilderDockWindow(nameof(WaveformVisualizer), node.ID);
                }
            };

            var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            visualizerService?.AddControl(imGuiCanvas);
        }

        /// <inheritdoc/>
        public override void Unload()
        {
            timeRange?.Dispose();
            decimatorMin?.Dispose();
            decimatorMax?.Dispose();
            minSnap?.Dispose();
            maxSnap?.Dispose();
            imGuiCanvas?.Dispose();
            imGuiCanvas = null;
            timeRange = null;
            minSnap = null;
            maxSnap = null;
        }
    }

    /// <summary>
    /// Specifies the color theme used to style visualizer contents.
    /// </summary>
    public enum ColorTheme
    {
        /// <summary>
        /// Specifies a color scheme using dark-colored text on a light background.
        /// </summary>
        Light,

        /// <summary>
        /// Specifies a color scheme using light-colored text on a dark background.
        /// </summary>
        Dark
    }
}

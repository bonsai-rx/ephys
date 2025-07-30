using Bonsai.Expressions;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace Bonsai.Ephys.Design
{
    /// <summary>
    /// Represents an operator that configures a visualizer to display a matrix as a
    /// multi-channel waveform, with peak-preserving downsampling.
    /// </summary>
    [TypeVisualizer(typeof(WaveformVisualizer))]
    [WorkflowElementCategory(ElementCategory.Sink)]
    [WorkflowElementIcon("Bonsai:ElementIcon.Neuro")]
    [Description("A visualizer that displays a matrix as a multi-channel waveform, with peak-preserving downsampling.")]
    public class WaveformVisualizerBuilder : SingleArgumentExpressionBuilder
    {
        /// <summary>
        /// Gets or sets the sample rate of the input waveform, in Hz.
        /// </summary>
        /// <remarks>
        /// This value is used to calculate how many samples are in the specified timebase.
        /// </remarks>
        [Description("The sample rate of the input waveform, in Hz.")]
        public int SampleRate { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum number of samples to plot across the visualizer display.
        /// </summary>
        /// <remarks>
        /// If the selected timebase contains a number of samples above this maximum, peak-preserving
        /// downsampling is used to reduce the number of points in the display.
        /// </remarks>
        [Description("The maximum number of samples to plot across the visualizer display.")]
        public int MaxSamplesPerChannel { get; set; } = 1920;

        /// <summary>
        /// Gets or sets the height of each channel plot, in pixels.
        /// </summary>
        [Description("The height of each channel plot, in pixels.")]
        public int? ChannelHeight { get; set; }

        /// <summary>
        /// Gets or sets how much time to represent in the visualizer display, in seconds.
        /// </summary>
        [Description("How much time to represent in the visualizer display, in seconds.")]
        public double? Timebase { get; set; }

        /// <summary>
        /// Gets or sets the number of adjacent channels to group under the same color.
        /// </summary>
        [Description("The number of adjacent channels to group under the same color.")]
        public int? ColorGrouping { get; set; }

        /// <inheritdoc/>
        public override Expression Build(IEnumerable<Expression> arguments)
        {
            var source = arguments.First();
            var parameterType = source.Type.GetGenericArguments()[0];
            if (parameterType != typeof(Mat))
                throw new InvalidOperationException($"The input type must be {typeof(Mat)}.");

            return Expression.Call(typeof(WaveformVisualizerBuilder), nameof(Process), null, source);
        }

        static IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source;
        }
    }
}

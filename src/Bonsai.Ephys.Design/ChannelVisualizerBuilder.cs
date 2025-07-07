using Bonsai.Expressions;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Bonsai.Ephys.Design
{
    [TypeVisualizer(typeof(ChannelVisualizer))]
    [WorkflowElementCategory(ElementCategory.Sink)]
    [WorkflowElementIcon("Bonsai:ElementIcon.Neuro")]
    public class ChannelVisualizerBuilder : SingleArgumentExpressionBuilder
    {
        public int SampleRate { get; set; } = 44100;

        public int MaxSamplesPerChannel { get; set; } = 1920;

        public int? ChannelHeight { get; set; }

        public double? TimeBase { get; set; }

        public override Expression Build(IEnumerable<Expression> arguments)
        {
            var source = arguments.First();
            var parameterType = source.Type.GetGenericArguments()[0];
            if (parameterType != typeof(Mat))
                throw new InvalidOperationException($"The input type must be {typeof(Mat)}.");

            return Expression.Call(typeof(ChannelVisualizerBuilder), nameof(Process), null, source);
        }

        static IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source;
        }
    }
}

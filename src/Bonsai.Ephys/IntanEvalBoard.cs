using System;
using OpenCV.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Xml.Serialization;
using System.ComponentModel;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents an operator that generates a sequence of buffered samples acquired
    /// from an RHA2000-EVAL board.
    /// </summary>
    [Description("Generates a sequence of buffered samples acquired from an RHA2000-EVAL board.")]
    [Editor("Bonsai.Ephys.Design.IntanEvalBoardEditor, Bonsai.Ephys.Design", typeof(ComponentEditor))]
    public class IntanEvalBoard : Source<EvalBoardData>
    {
        bool settle;
        readonly IntanUsbSource usbSource = new();
        readonly IObservable<EvalBoardData> source;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntanEvalBoard"/> class.
        /// </summary>
        public IntanEvalBoard()
        {
            source = Observable.Create<EvalBoardData>(observer =>
            {
                settle = false;
                int firmwareID1 = 0;
                int firmwareID2 = 0;
                int firmwareID3 = 0;
                usbSource.Open(ref firmwareID1, ref firmwareID2, ref firmwareID3);

                var running = true;
                usbSource.Start();
                var thread = new Thread(() =>
                {
                    while (running)
                    {
                        var data = usbSource.ReadUsbData();
                        if (data != null)
                        {
                            var dataOutput = Mat.FromArray(data.DataFrame);
                            var auxOutput = Mat.FromArray(data.AuxFrame);
                            observer.OnNext(new EvalBoardData(dataOutput, auxOutput));
                        }
                    }
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    usbSource.Stop();
                    usbSource.Close();
                };
            })
            .PublishReconnectable()
            .RefCount();
        }

        internal IntanUsbSource UsbSource
        {
            get { return usbSource; }
        }

        /// <summary>
        /// Gets or sets a value used online to reset the state of the amplifiers.
        /// </summary>
        [XmlIgnore]
        [Description("Used online to reset the state of the amplifiers.")]
        public bool AmplifierSettle
        {
            get { return settle; }
            set
            {
                settle = value;
                if (settle) usbSource.SettleOn();
                else usbSource.SettleOff();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the software high-pass filter is enabled.
        /// </summary>
        [Description("Indicates whether the software high-pass filter is enabled.")]
        public bool HighPassFilter
        {
            get { return usbSource.EnableHPF; }
            set { usbSource.EnableHPF = value; }
        }

        /// <summary>
        /// Gets or sets the cutoff frequency of the software high-pass filter.
        /// </summary>
        [Description("The cutoff frequency of the software high-pass filter.")]
        public double HighPassFilterCutoff
        {
            get { return usbSource.FHPF; }
            set { usbSource.FHPF = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the software notch filter is enabled.
        /// </summary>
        [Description("Indicates whether the software notch filter is enabled.")]
        public bool NotchFilter
        {
            get { return usbSource.EnableNotch; }
            set { usbSource.EnableNotch = value; }
        }

        /// <summary>
        /// Gets or sets the frequency of the software notch filter.
        /// </summary>
        [Description("The frequency of the software notch filter.")]
        public double NotchFrequency
        {
            get { return usbSource.FNotch; }
            set { usbSource.FNotch = value; }
        }

        /// <summary>
        /// Generates an observable sequence of buffered samples acquired from an
        /// RHA2000-EVAL board.
        /// </summary>
        /// <returns>
        /// A sequence of <see cref="EvalBoardData"/> objects containing buffered amplifier
        /// voltage and auxiliary TTL input data sampled from an RHA2000-EVAL board.
        /// </returns>
        public override IObservable<EvalBoardData> Generate()
        {
            return source;
        }
    }
}

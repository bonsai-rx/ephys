using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.ComponentModel;

namespace Bonsai.Ephys
{
    [Description("Produces a sequence of buffered samples acquired from a RHA2000-EVAL board.")]
    [Editor("Bonsai.Ephys.Design.IntanEvalBoardEditor, Bonsai.Ephys.Design", typeof(ComponentEditor))]
    public class IntanEvalBoard : Source<EvalBoardData>
    {
        bool settle;
        IntanUsbSource usbSource = new IntanUsbSource();
        IObservable<EvalBoardData> source;

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

        [XmlIgnore]
        [Browsable(false)]
        public IntanUsbSource UsbSource
        {
            get { return usbSource; }
        }

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

        [Description("Indicates whether the software high-pass filter is enabled.")]
        public bool HighPassFilter
        {
            get { return usbSource.EnableHPF; }
            set { usbSource.EnableHPF = value; }
        }

        [Description("The cutoff frequency of the software high-pass filter.")]
        public double HighPassFilterCutoff
        {
            get { return usbSource.FHPF; }
            set { usbSource.FHPF = value; }
        }

        [Description("Indicates whether the software notch filter is enabled.")]
        public bool NotchFilter
        {
            get { return usbSource.EnableNotch; }
            set { usbSource.EnableNotch = value; }
        }

        [Description("The frequency of the software notch filter.")]
        public double NotchFrequency
        {
            get { return usbSource.FNotch; }
            set { usbSource.FNotch = value; }
        }

        public override IObservable<EvalBoardData> Generate()
        {
            return source;
        }
    }
}

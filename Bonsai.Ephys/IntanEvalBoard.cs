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
    [Editor("Bonsai.Ephys.Design.IntanEvalBoardEditor, Bonsai.Ephys.Design", typeof(ComponentEditor))]
    public class IntanEvalBoard : Source<CvMat>
    {
        bool settle;
        IntanUsbSource source = new IntanUsbSource();

        [XmlIgnore]
        [Browsable(false)]
        public IntanUsbSource UsbSource
        {
            get { return source; }
        }

        [XmlIgnore]
        public bool AmplifierSettle
        {
            get { return settle; }
            set
            {
                settle = value;
                if (settle) source.SettleOn();
                else source.SettleOff();
            }
        }

        public bool HighPassFilter
        {
            get { return source.EnableHPF; }
            set { source.EnableHPF = value; }
        }

        public double HighPassFilterCutoff
        {
            get { return source.FHPF; }
            set { source.FHPF = value; }
        }

        public bool NotchFilter
        {
            get { return source.EnableNotch; }
            set { source.EnableNotch = value; }
        }

        public double NotchFrequency
        {
            get { return source.FNotch; }
            set { source.FNotch = value; }
        }

        public override IDisposable Load()
        {
            settle = false;
            int firmwareID1 = 0;
            int firmwareID2 = 0;
            int firmwareID3 = 0;
            source.Open(ref firmwareID1, ref firmwareID2, ref firmwareID3);
            return base.Load();
        }

        protected override void Unload()
        {
            source.Close();
            base.Unload();
        }

        protected override IObservable<CvMat> Generate()
        {
            return Observable.Create<CvMat>(observer =>
            {
                var running = true;
                source.Start();
                var thread = new Thread(() =>
                {
                    while (running)
                    {
                        var data = source.ReadUsbData();
                        if (data != null)
                        {
                            var dataFrame = data.DataFrame;
                            var numChannels = dataFrame.GetLength(0);
                            var numSamples = dataFrame.GetLength(1);
                            var dataHandle = GCHandle.Alloc(dataFrame, GCHandleType.Pinned);
                            var dataHeader = new CvMat(numChannels, numSamples, CvMatDepth.CV_32F, 1, dataHandle.AddrOfPinnedObject());
                            var output = dataHeader.Clone();
                            dataHandle.Free();
                            observer.OnNext(output);

                        }
                    }
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    source.Stop();
                };
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Linq;
using Rhythm.Net;
using System.Reactive.Disposables;
using System.ComponentModel;
using System.Drawing.Design;
using System.Threading;

namespace Bonsai.Ephys
{
    public class Rhd2000EvalBoard : Source<Rhd2000DataFrame>
    {
        Rhd2000Registers chipRegisters;
        Rhythm.Net.Rhd2000EvalBoard evalBoard;
        double cableLengthPortA;
        double cableLengthPortB;
        double cableLengthPortC;
        double cableLengthPortD;

        public Rhd2000EvalBoard()
        {
            BitFileName = "main.bit";
            SampleRate = AmplifierSampleRate.SampleRate20000Hz;
            LowerBandwidth = 0.1;
            UpperBandwidth = 7500.0;
            DspCutoffFrequency = 1.0;
            DspEnabled = true;
        }

        [FileNameFilter("BIT Files|*.bit")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", typeof(UITypeEditor))]
        public string BitFileName { get; set; }

        public AmplifierSampleRate SampleRate { get; set; }

        public bool FastSettle { get; set; }

        public double LowerBandwidth { get; set; }

        public double UpperBandwidth { get; set; }

        public double DspCutoffFrequency { get; set; }

        public bool DspEnabled { get; set; }

        Rhd2000DataBlock RunSingleCommandSequence()
        {
            // Start SPI interface.
            evalBoard.Run();

            // Wait for the 60-sample run to complete.
            while (evalBoard.IsRunning()) Thread.Sleep(0);

            // Read the resulting single data block from the USB interface.
            var dataBlock = new Rhd2000DataBlock(evalBoard.GetNumEnabledDataStreams());
            evalBoard.ReadDataBlock(dataBlock);
            return dataBlock;
        }

        int ReadDeviceId(Rhd2000DataBlock dataBlock, int stream)
        {
            // First, check ROM registers 32-36 to verify that they hold 'INTAN'.
            // This is just used to verify that we are getting good data over the SPI
            // communication channel.
            var intanChipPresent = (
                (char)dataBlock.AuxiliaryData[stream][2, 32] == 'I' &&
                (char)dataBlock.AuxiliaryData[stream][2, 33] == 'N' &&
                (char)dataBlock.AuxiliaryData[stream][2, 34] == 'T' &&
                (char)dataBlock.AuxiliaryData[stream][2, 35] == 'A' &&
                (char)dataBlock.AuxiliaryData[stream][2, 36] == 'N');

            // If the SPI communication is bad, return -1.  Otherwise, return the Intan
            // chip ID number stored in ROM regstier 63.
            if (!intanChipPresent)
            {
                return -1;
            }
            else
            {
                return dataBlock.AuxiliaryData[stream][2, 19]; // chip ID (Register 63)
            }
        }

        void ScanConnectedAmplifiers()
        {
            // Set sampling rate to highest value for maximum temporal resolution.
            ChangeSampleRate(AmplifierSampleRate.SampleRate30000Hz);

            // Enable all data streams, and set sources to cover one or two chips
            // on Ports A-D.
            evalBoard.SetDataSource(0, BoardDataSource.PortA1);
            evalBoard.SetDataSource(1, BoardDataSource.PortA2);
            evalBoard.SetDataSource(2, BoardDataSource.PortB1);
            evalBoard.SetDataSource(3, BoardDataSource.PortB2);
            evalBoard.SetDataSource(4, BoardDataSource.PortC1);
            evalBoard.SetDataSource(5, BoardDataSource.PortC2);
            evalBoard.SetDataSource(6, BoardDataSource.PortD1);
            evalBoard.SetDataSource(7, BoardDataSource.PortD2);

            var numDataStreams = Rhythm.Net.Rhd2000EvalBoard.MaxNumDataStreams();
            for (int i = 0; i < numDataStreams; i++)
            {
                evalBoard.EnableDataStream(i, true);
            }

            // Select RAM Bank 0 for AuxCmd3
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples.
            evalBoard.SetMaxTimeStep(60);
            evalBoard.SetContinuousRunMode(false);

            // Run SPI command sequence at all 16 possible FPGA MISO delay settings
            // to find optimum delay for each SPI interface cable.
            var optimumDelays = new int[numDataStreams];
            var secondDelays = new int[numDataStreams];
            var goodDelayCounts = new int[numDataStreams];
            for (int i = 0; i < optimumDelays.Length; i++)
            {
                optimumDelays[i] = -1;
                secondDelays[i] = -1;
                goodDelayCounts[i] = 0;
            }

            for (int delay = 0; delay < 16; delay++)
            {
                evalBoard.SetCableDelay(BoardPort.PortA, delay);
                evalBoard.SetCableDelay(BoardPort.PortB, delay);
                evalBoard.SetCableDelay(BoardPort.PortC, delay);
                evalBoard.SetCableDelay(BoardPort.PortD, delay);

                // Run SPI command sequence
                var dataBlock = RunSingleCommandSequence();

                // Read the Intan chip ID number from each RHD2000 chip found.
                // Record delay settings that yield good communication with the chip.
                for (int stream = 0; stream < optimumDelays.Length; stream++)
                {
                    var id = ReadDeviceId(dataBlock, stream);
                    if (id > 0)
                    {
                        goodDelayCounts[stream]++;
                        switch (goodDelayCounts[stream])
                        {
                            case 1: optimumDelays[stream] = delay; break;
                            case 2: secondDelays[stream] = delay; break;
                            case 3: optimumDelays[stream] = secondDelays[stream]; break;
                        }
                    }
                }
            }

            // Now, disable data streams where we did not find chips present.
            for (int stream = 0; stream < optimumDelays.Length; stream++)
            {
                var enabled = optimumDelays[stream] >= 0;
                evalBoard.EnableDataStream(stream, enabled);
                if (!enabled) optimumDelays[stream] = 0;
            }

            // Set cable delay settings that yield good communication with each
            // RHD2000 chip.
            var optimumDelayA = Math.Max(optimumDelays[0], optimumDelays[1]);
            var optimumDelayB = Math.Max(optimumDelays[2], optimumDelays[3]);
            var optimumDelayC = Math.Max(optimumDelays[4], optimumDelays[5]);
            var optimumDelayD = Math.Max(optimumDelays[6], optimumDelays[7]);
            evalBoard.SetCableDelay(BoardPort.PortA, optimumDelayA);
            evalBoard.SetCableDelay(BoardPort.PortB, optimumDelayB);
            evalBoard.SetCableDelay(BoardPort.PortC, optimumDelayC);
            evalBoard.SetCableDelay(BoardPort.PortD, optimumDelayD);
            cableLengthPortA = evalBoard.EstimateCableLengthMeters(optimumDelayA);
            cableLengthPortB = evalBoard.EstimateCableLengthMeters(optimumDelayB);
            cableLengthPortC = evalBoard.EstimateCableLengthMeters(optimumDelayC);
            cableLengthPortD = evalBoard.EstimateCableLengthMeters(optimumDelayD);

            // Return sample rate to original user-selected value.
            ChangeSampleRate(SampleRate);
        }

        void ChangeSampleRate(AmplifierSampleRate amplifierSampleRate)
        {
            evalBoard.SetSampleRate(amplifierSampleRate);

            // Now that we have set our sampling rate, we can set the MISO sampling delay
            // which is dependent on the sample rate.
            evalBoard.SetCableLengthMeters(BoardPort.PortA, cableLengthPortA);
            evalBoard.SetCableLengthMeters(BoardPort.PortB, cableLengthPortB);
            evalBoard.SetCableLengthMeters(BoardPort.PortC, cableLengthPortC);
            evalBoard.SetCableLengthMeters(BoardPort.PortD, cableLengthPortD);

            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = evalBoard.GetSampleRate();
            chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();

            // Create a command list for the AuxCmd1 slot.  This command sequence will create a 250 Hz,
            // zero-amplitude sine wave (i.e., a flatline).  We will change this when we want to perform
            // impedance testing.
            var sequenceLength = chipRegisters.CreateCommandListZcheckDac(commandList, 250, 0);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, sequenceLength - 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);

            // Next, we'll create a command list for the AuxCmd2 slot.  This command sequence
            // will sample the temperature sensor and other auxiliary ADC inputs.
            sequenceLength = chipRegisters.CreateCommandListTempSensor(commandList);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, sequenceLength - 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);

            // For the AuxCmd3 slot, we will create three command sequences.  All sequences
            // will configure and read back the RHD2000 chip registers, but one sequence will
            // also run ADC calibration.  Another sequence will enable amplifier 'fast settle'.

            // Before generating register configuration command sequences, set amplifier
            // bandwidth parameters.
            chipRegisters.SetDspCutoffFreq(DspCutoffFrequency);
            chipRegisters.SetLowerBandwidth(LowerBandwidth);
            chipRegisters.SetUpperBandwidth(UpperBandwidth);
            chipRegisters.EnableDsp(DspEnabled);

            // Upload version with ADC calibration to AuxCmd3 RAM Bank 0.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, true);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 1);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with fast settle enabled to AuxCmd3 RAM Bank 2.
            chipRegisters.SetFastSettle(true);
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 2);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);
            chipRegisters.SetFastSettle(false);

            UpdateRegisterConfiguration();
        }

        void UpdateRegisterConfiguration()
        {
            var fastSettle = FastSettle;
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
        }

        void RunCalibration()
        {
            // Select RAM Bank 0 for AuxCmd3 initially, so the ADC is calibrated.
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples.
            evalBoard.SetMaxTimeStep(60);
            evalBoard.SetContinuousRunMode(false);

            // Run ADC calibration command sequence
            RunSingleCommandSequence();

            // Now that ADC calibration has been performed, we switch to the command sequence
            // that does not execute ADC calibration.
            UpdateRegisterConfiguration();
        }

        void SetDacDefaultConfiguration()
        {
            for (int i = 0; i < 8; i++)
            {
                evalBoard.EnableDac(i, false);
                evalBoard.SelectDacDataStream(i, 0);
                evalBoard.SelectDacDataChannel(i, 0);
            }

            evalBoard.SetDacManual(DacManual.DacManual1, 32768);
            evalBoard.SetDacManual(DacManual.DacManual2, 32768);
            evalBoard.SetDacGain(0);
            evalBoard.SetAudioNoiseSuppress(0);
        }

        int GetDataBlockReadCount(AmplifierSampleRate sampleRate)
        {
            switch (sampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                case AmplifierSampleRate.SampleRate1250Hz:
                case AmplifierSampleRate.SampleRate1500Hz:
                case AmplifierSampleRate.SampleRate2000Hz:
                case AmplifierSampleRate.SampleRate2500Hz:
                    return 1;
                case AmplifierSampleRate.SampleRate3000Hz:
                case AmplifierSampleRate.SampleRate3333Hz:
                case AmplifierSampleRate.SampleRate4000Hz:
                    return 2;
                case AmplifierSampleRate.SampleRate5000Hz:
                case AmplifierSampleRate.SampleRate6250Hz:
                    return 3;
                case AmplifierSampleRate.SampleRate8000Hz:
                    return 4;
                case AmplifierSampleRate.SampleRate10000Hz:
                    return 6;
                case AmplifierSampleRate.SampleRate12500Hz:
                    return 7;
                case AmplifierSampleRate.SampleRate15000Hz:
                    return 8;
                case AmplifierSampleRate.SampleRate20000Hz:
                    return 12;
                case AmplifierSampleRate.SampleRate25000Hz:
                    return 14;
                case AmplifierSampleRate.SampleRate30000Hz:
                    return 16;
                default:
                    throw new ArgumentException("Invalid amplifier sample rate.", "sampleRate");
            }
        }

        public override IDisposable Load()
        {
            evalBoard = new Rhythm.Net.Rhd2000EvalBoard();

            // Open Opal Kelly XEM6010 board.
            evalBoard.Open();

            // Load Rhythm FPGA configuration bitfile (provided by Intan Technologies).
            evalBoard.UploadFpgaBitfile(BitFileName);

            // Initialize interface board.
            evalBoard.Initialize();

            // Set sample rate and upload all auxiliary SPI command sequences.
            ChangeSampleRate(SampleRate);

            // Run ADC calibration
            RunCalibration();

            // Set default configuration for all eight DACs on interface board.
            SetDacDefaultConfiguration();

            // Find amplifier chips connected to interface board and compute their
            // optimal delay parameters.
            ScanConnectedAmplifiers();
            return base.Load();
        }

        protected override void Unload()
        {
            evalBoard = null;
            chipRegisters = null;
            base.Unload();
        }

        protected override IObservable<Rhd2000DataFrame> Generate()
        {
            return Observable.Create<Rhd2000DataFrame>(observer =>
            {
                var running = true;
                evalBoard.SetContinuousRunMode(true);
                evalBoard.Run();
                var thread = new Thread(() =>
                {
                    var blocksToRead = GetDataBlockReadCount(SampleRate);
                    var fifoCapacity = Rhythm.Net.Rhd2000EvalBoard.FifoCapacityInWords();
                    var ledArray = new[] { 1, 0, 0, 0, 0, 0, 0, 0 };
                    var ledIndex = 0;

                    var queue = new Queue<Rhd2000DataBlock>();
                    evalBoard.SetLedDisplay(ledArray);
                    while (running)
                    {
                        if (evalBoard.ReadDataBlocks(blocksToRead, queue))
                        {
                            var wordsInFifo = evalBoard.NumWordsInFifo();
                            var bufferPercentageCapacity = 100.0 * wordsInFifo / fifoCapacity;
                            foreach (var dataBlock in queue)
                            {
                                observer.OnNext(new Rhd2000DataFrame(dataBlock, bufferPercentageCapacity));
                            }
                            queue.Clear();

                            ledArray[ledIndex] = 0;
                            ledIndex = (ledIndex + 1) % ledArray.Length;
                            ledArray[ledIndex] = 1;
                            evalBoard.SetLedDisplay(ledArray);
                        }
                    }

                    ledArray[ledIndex] = 0;
                    evalBoard.SetLedDisplay(ledArray);
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    evalBoard.SetContinuousRunMode(false);
                    evalBoard.SetMaxTimeStep(0);
                    evalBoard.Flush();
                };
            });
        }
    }
}

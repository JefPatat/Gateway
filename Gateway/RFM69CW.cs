using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;

using GHI.Utilities;
using System.Collections;

// The general model of this class is to be always receiving messages except when sending
//
// 100% bidirectional throughput cannot be guaranteed, messages can be lost due to:
// - radio interference from other devices
// - collision of transmissions
// - a message is in the process of being received when switching to transmit mode 
//
// Additional complexity arises due to the .NET MF interrupt architecture. Interrupts are not
// real time interrupts but rather delayed events. This means that an interrupt might be 'waiting'
// while the main loop is executing.
// 
// Due to the very low throughput required, the limited time to test all this and the fact that
// this wireless transmission cannot be perfectly guaranteed no efforts were made to optimize it.
//
// Ideas that could increase throughput:
// - check the RSSI to ensure the radio channel is free before starting a transmission
// - check if a receive interrupt is pending before starting a transmission and if so handle this
//   interrupt first

namespace Gateway
{
	public class RFM69CW
	{
		private enum ConfigurationRegister
		{
			Fifo,			// 0x00
			OpMode,			// 0x01
			DataModul,		// 0x02
			BitrateMsb,		// 0x03
			BitrateLsb,		// 0x04
			FdevMsb,		// 0x05
			FdevLsb,		// 0x06
			FrfMsb,			// 0x07
			FrfMid,			// 0x08
			FrfLsb,			// 0x09
			Osc1,			// 0x0A
			AfcCtrl,		// 0x0B
			Reserved0C,		// 0x0C
			Listen1,		// 0x0D
			Listen2,		// 0x0E
			Listen3,		// 0x0F
			Version,		// 0x10
			PaLevel,		// 0x11
			PaRamp,			// 0x12
			Ocp,			// 0x13
			Reserved14,		// 0x14
			Reserved15,		// 0x15
			Reserved16,		// 0x16
			Reserved17,		// 0x17
			Lna,			// 0x18
			RxBw,			// 0x19
			AfcBw,			// 0x1A
			OokPeak,		// 0x1B
			OokAvg,			// 0x1C
			OokFix,			// 0x1D
			AfcFei,			// 0x1E
			AfcMsb,			// 0x1F
			AfcLsb,			// 0x20
			FeiMsb,			// 0x21
			FeiLsb,			// 0x22
			RssiConfig,		// 0x23
			RssiValue,		// 0x24
			DioMapping1,	// 0x25
			DioMapping2,	// 0x26
			IrqFlags1,		// 0x27
			IrqFlags2,		// 0x28
			RssiThresh,		// 0x29
			RxTimeout1,		// 0x2A
			RxTimeout2,		// 0x2B
			PreambleMsb,	// 0x2C
			PreambleLsv,	// 0x2D
			SyncConfig,		// 0x2E
			SyncValue1,		// 0x2F
			SyncValue2,		// 0x30
			SyncValue3,		// 0x31
			SyncValue4,		// 0x32
			SyncValue5,		// 0x33
			SyncValue6,		// 0x34
			SyncValue7,		// 0x35
			SyncValue8,		// 0x36
			PacketConfig1,	// 0x37
			PayloadLength,	// 0x38
			NodeAdrs,		// 0x39
			BroadcastAdrs,	// 0x3A
			AutoModes,		// 0x3B
			FifoThresh,		// 0x3C
			PacketConfig2,	// 0x3D
			AesKey1,		// 0x3E
			AesKey2,		// 0x3F
			AesKey3,		// 0x40
			AesKey4,		// 0x41
			AesKey5,		// 0x42
			AesKey6,		// 0x43
			AesKey7,		// 0x44
			AesKey8,		// 0x45
			AesKey9,		// 0x46
			AesKey10,		// 0x47
			AesKey11,		// 0x48
			AesKey12,		// 0x49
			AesKey13,		// 0x4A
			AesKey14,		// 0x4B
			AesKey15,		// 0x4C
			AesKey16,		// 0x4D
			Temp1,			// 0x4E
			Temp2,			// 0x4F
			TestLna,		// 0x58
			TestPa1,		// 0x5A
			TestPa2,		// 0x5C
			TestDagc = 0x6F,// 0x6F
			TestAfc = 0x71,	// 0x71
		}

		public enum Mode
		{
			MODE_SLEEP,
			MODE_STANDBY,
			MODE_SYNTH,
			MODE_RX,
			MODE_TX,
		}

		private AutoResetEvent transmissionCompleteEvent = new AutoResetEvent(false);
		private byte nodeID = 0;
		// Maximum  payload length with AES and address filtering enabled, datasheet p52
		// AES and address filtering are not enabled for the moment, but 64 is enough for now
		private const byte maxPayloadLength = 64;
		private InterruptPort interruptPin = new InterruptPort(GHI.Pins.Generic.GetPin('B', 12), false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeHigh);
		private Mode currentMode;
		private object receptionQueueLock = new object();
		private Queue receptionQueue = new Queue();
		private SPI spi = null;

		public RFM69CW(byte networkID, byte nodeID)
		{
			interruptPin.OnInterrupt += interruptPin_OnInterrupt;
			this.nodeID = nodeID;
			spi = new SPI(new SPI.Configuration(GHI.Pins.Generic.GetPin('A', 15), false, 0, 0, false, true, 2000, SPI.SPI_module.SPI1));

			do
			{
				WriteRegister(ConfigurationRegister.SyncValue1, 0xAA);
			}while (ReadRegister(ConfigurationRegister.SyncValue1) != 0xAA);
			do
			{
				WriteRegister(ConfigurationRegister.SyncValue1, 0x55);
			}while (ReadRegister(ConfigurationRegister.SyncValue1) != 0x55);

			// Most settings are taken over from Monteino lib for compatibility
			// Some settings are default
			// TODO: clean up, warning: when cleaning up reset the device by repowering
			//       previous attempts failed, be carefull when doing this

			// Custom bitrate of 55555 to match Monteino lib
			WriteRegister(ConfigurationRegister.BitrateMsb, 0x02);
			WriteRegister(ConfigurationRegister.BitrateLsb, 0x40);
			
			// Frequency deviation of 50 kHz to match Monteino lib
			WriteRegister(ConfigurationRegister.FdevMsb, 0x03);
			WriteRegister(ConfigurationRegister.FdevLsb, 0x33);
			
			// 868 MHz
			WriteRegister(ConfigurationRegister.FrfMsb, 0xD9);
			WriteRegister(ConfigurationRegister.FrfMid, 0x00);
			WriteRegister(ConfigurationRegister.FrfLsb, 0x00);
			
			// Custom Channel Filter BW Control to match Monteino lib
			WriteRegister(ConfigurationRegister.RxBw, 0x42);
			WriteRegister(ConfigurationRegister.DioMapping1, 0x40);
			WriteRegister(ConfigurationRegister.DioMapping2, 0x07);
			WriteRegister(ConfigurationRegister.IrqFlags2, 0x10);
			WriteRegister(ConfigurationRegister.RssiThresh, 220);
			
			// Custom syncing to match Monteino lib
			WriteRegister(ConfigurationRegister.SyncConfig, 0x88);
			WriteRegister(ConfigurationRegister.SyncValue1, 0x2D);
			WriteRegister(ConfigurationRegister.SyncValue2, networkID);
			
			// Variable packet length with CRC enabled
			WriteRegister(ConfigurationRegister.PacketConfig1, 0x90);

			// Maximum payload length
			WriteRegister(ConfigurationRegister.PayloadLength, 66);
			WriteRegister(ConfigurationRegister.FifoThresh, 0x8F);
			// InterPacketRxDelay of 2 bits, AutoRxRestartOn on, AES off
			WriteRegister(ConfigurationRegister.PacketConfig2, 0x12);
			WriteRegister(ConfigurationRegister.TestDagc, 0x30);
			
			// TODO: SetHighPower for H version

			SetMode(Mode.MODE_STANDBY);


			EnableReceiver();
		}

		void interruptPin_OnInterrupt(uint data1, uint data2, DateTime time)
		{
			byte IrqFlags2 = ReadRegister(ConfigurationRegister.IrqFlags2);
			if ((IrqFlags2 & 0x04) != 0)
			{
				// Setting the mode to standby and then back to RX ensures the FIFO is cleared
				// and a new reception can begin
				SetMode(Mode.MODE_STANDBY);

				SWPMessage message = new SWPMessage();
				byte messageLength = ReadRegister(ConfigurationRegister.Fifo);
				message.RSSI = (short)-(ReadRegister(ConfigurationRegister.RssiValue) / 2);
				message.DestinationAddress = ReadRegister(ConfigurationRegister.Fifo);
				message.SourceAddress = ReadRegister(ConfigurationRegister.Fifo);
				message.ServiceIdentifier = ReadRegister(ConfigurationRegister.Fifo);
				message.Data = new byte[messageLength - 3];
				writeBuffer[0] = (byte)((int)ConfigurationRegister.Fifo & 0x7F);
				spi.WriteRead(writeBuffer, message.Data, 1);

				SetMode(Mode.MODE_RX);

				lock (receptionQueueLock)
				{
					if (receptionQueue.Count < 10)
					{
						receptionQueue.Enqueue(message);
					}
				}
			}
		}

		private byte[] writeBuffer = new byte[2];
		private byte[] readBuffer = new byte[2];
		private void WriteRegister(ConfigurationRegister register, byte value)
		{
			writeBuffer[0] = (byte)((int)register | 0x80);
			writeBuffer[1] = value;
			spi.Write(writeBuffer);
		}
		
		private byte ReadRegister(ConfigurationRegister register)
		{
			writeBuffer[0] = (byte)((int)register & 0x7F);
			spi.WriteRead(writeBuffer, readBuffer);
			return readBuffer[1];
		}
		
		public int NumberOfQueuedReceivedMessage()
		{
			byte IrqFlags2 = ReadRegister(ConfigurationRegister.IrqFlags2);
			int result;
			lock (receptionQueueLock)
			{
				result = receptionQueue.Count;
			}
			return result;
		}

		public SWPMessage DequeueReceivedMessage()
		{
			SWPMessage result;
			lock(receptionQueueLock)
			{
				result = (SWPMessage)receptionQueue.Dequeue();
			}
			return result;
		}

		public void SendMessage(SWPMessage message)
		{
			// Disable reception and clear the FIFO
			SetMode(Mode.MODE_STANDBY);
			while ((ReadRegister(ConfigurationRegister.IrqFlags2) & 0x40) == 0x40)
			{
				ReadRegister(ConfigurationRegister.Fifo);
			}

			// set DIO0 to PacketSent
			WriteRegister(ConfigurationRegister.DioMapping1, 0x00);

			if (message.Data.Length > maxPayloadLength - 2)
			{
				throw new Exception("data too long");
			}

			byte[] txData = new byte[message.Data.Length + 5];
			txData[0] = 0x00 | 0x80;
			txData[1] = (byte)(message.Data.Length + 3);
			txData[2] = message.DestinationAddress;
			txData[3] = message.SourceAddress;
			txData[4] = message.ServiceIdentifier;
			Array.Copy(message.Data, 0, txData, 5, message.Data.Length);
			spi.Write(txData);
			SetMode(Mode.MODE_TX);

			// We could handle the interrupt in the interrupt handler but due to the .NET MF
			// interrupt model that would be rather slow. It is faster to poll the interrupt.
			// Speeding things up here allows the other side to respond faster.

			byte IrqFlags2 = ReadRegister(ConfigurationRegister.IrqFlags2);
			while ((IrqFlags2 & 0x08) == 0)
			{
				IrqFlags2 = ReadRegister(ConfigurationRegister.IrqFlags2);
			}
			SetMode(Mode.MODE_RX);
		}

		public void EnableReceiver()
		{
			SetMode(Mode.MODE_STANDBY);
			
			// Clear the FIFO
			while ((ReadRegister(ConfigurationRegister.IrqFlags2) & 0x40) == 0x40)
			{
				ReadRegister(ConfigurationRegister.Fifo);
			}
			
			// Set DIO0 to PayloadReady
			WriteRegister(ConfigurationRegister.DioMapping1, 0x40);
			SetMode(Mode.MODE_RX);
		}

		public void SetMode(Mode newMode)
		{
			if (newMode == currentMode)
				return;

			switch (newMode)
			{
				case Mode.MODE_TX:
					WriteRegister(ConfigurationRegister.OpMode, (byte)((ReadRegister(ConfigurationRegister.OpMode) & 0xE3) | 0x0C));
					break;
				case Mode.MODE_RX:
					WriteRegister(ConfigurationRegister.OpMode, (byte)((ReadRegister(ConfigurationRegister.OpMode) & 0xE3) | 0x10));
					break;
				case Mode.MODE_SYNTH:
					WriteRegister(ConfigurationRegister.OpMode, (byte)((ReadRegister(ConfigurationRegister.OpMode) & 0xE3) | 0x08));
					break;
				case Mode.MODE_STANDBY:
					WriteRegister(ConfigurationRegister.OpMode, (byte)((ReadRegister(ConfigurationRegister.OpMode) & 0xE3) | 0x04));
					break;
				case Mode.MODE_SLEEP:
					WriteRegister(ConfigurationRegister.OpMode, (byte)((ReadRegister(ConfigurationRegister.OpMode) & 0xE3) | 0x00));
					break;
				default:
					return;
			}

			while ((ReadRegister(ConfigurationRegister.IrqFlags1) & 0x80) == 0x00)
			{
				// Wait for ModeReady
			}

			currentMode = newMode;
		}
	}
}
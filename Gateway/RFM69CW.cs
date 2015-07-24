using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;

using GHI.Utilities;

namespace Gateway
{
	public class RFM69CW
	{
		private SPI spi = null;
		private uint previousUptime = 0;
		private uint lastMissedUptime = 0;
		private uint missedFrames = 0;
		private uint correctFrames = 0;

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
			RF69_MODE_SLEEP,	// 0: XTAL OFF
			RF69_MODE_STANDBY,	// 1: XTAL ON
			RF69_MODE_SYNTH,	// 2: PLL ON
			RF69_MODE_RX,		// 3: RX MODE
			RF69_MODE_TX,		// 4: TX MODE
		}

		private byte nodeID = 0;
		private byte PAYLOADLEN = 0;
		private Mode currentMode;
		private const short CSMA_LIMIT = -90; // upper RX signal sensitivity threshold in dBm for carrier sense access
		private const long RF69_CSMA_LIMIT_MS = 1000;
		private InterruptPort interruptPin = new InterruptPort(GHI.Pins.Generic.GetPin('B', 12), false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeHigh);

		public RFM69CW(byte networkID, byte nodeID)
		{
			interruptPin.OnInterrupt += interruptPin_OnInterrupt;
			this.nodeID = nodeID;
			spi = new SPI(new SPI.Configuration(GHI.Pins.Generic.GetPin('A', 15), false, 0, 0, false, true, 500, SPI.SPI_module.SPI1));

			do WriteRegister(ConfigurationRegister.SyncValue1, 0xAA); while (ReadRegister(ConfigurationRegister.SyncValue1) != 0xAA);
			do WriteRegister(ConfigurationRegister.SyncValue1, 0x55); while (ReadRegister(ConfigurationRegister.SyncValue1) != 0x55);

			WriteRegister(ConfigurationRegister.OpMode, 0x04);		// Sequencer on, listen off, standby mode
			WriteRegister(ConfigurationRegister.DataModul, 0x00);	// Packet mode, FSK, no shaping
			WriteRegister(ConfigurationRegister.BitrateMsb, 0x02);	// Default 4.8 KBPS
			WriteRegister(ConfigurationRegister.BitrateLsb, 0x40);
			WriteRegister(ConfigurationRegister.FdevMsb, 0x03);		// Default: 5KHz, (FDEV + BitRate / 2 <= 500KHz)
			WriteRegister(ConfigurationRegister.FdevLsb, 0x33);
			WriteRegister(ConfigurationRegister.FrfMsb, 0xD9);		// 868 MHz
			WriteRegister(ConfigurationRegister.FrfMid, 0x00);
			WriteRegister(ConfigurationRegister.FrfLsb, 0x00);
			WriteRegister(ConfigurationRegister.RxBw, 0x42);
			WriteRegister(ConfigurationRegister.DioMapping1, 0x40);
			WriteRegister(ConfigurationRegister.DioMapping2, 0x07);
			WriteRegister(ConfigurationRegister.IrqFlags2, 0x10);
			WriteRegister(ConfigurationRegister.RssiThresh, 220);
			WriteRegister(ConfigurationRegister.SyncConfig, 0x88);
			WriteRegister(ConfigurationRegister.SyncValue1, 0x2D);
			WriteRegister(ConfigurationRegister.SyncValue2, networkID);
			WriteRegister(ConfigurationRegister.PacketConfig1, 0x90);
			WriteRegister(ConfigurationRegister.PayloadLength, 66);
			WriteRegister(ConfigurationRegister.FifoThresh, 0x8F);
			WriteRegister(ConfigurationRegister.PacketConfig2, 0x12);
			WriteRegister(ConfigurationRegister.TestDagc, 0x30);

			//setHighPower(_isRFM69HW); // called regardless if it's a RFM69W or RFM69HW
			SetMode(Mode.RF69_MODE_STANDBY);
			while ((ReadRegister(0x27) & 0x80) == 0x00) ; // wait for ModeReady
			OutputAllRegs();
		}

		void interruptPin_OnInterrupt(uint data1, uint data2, DateTime time)
		{
			// TODO: Can't send ACK here since in interrupt?

			byte IrqFlags1 = ReadRegister(ConfigurationRegister.IrqFlags1);
			byte IrqFlags2 = ReadRegister(ConfigurationRegister.IrqFlags2);
			//Debug.Print("Interrupt1: " + IrqFlags1.ToString("X2") + " Interrupt2: " + IrqFlags2.ToString("X2"));
			if ((IrqFlags2 & 0x04) != 0)
			{
				//RSSI = readRSSI();
				SetMode(Mode.RF69_MODE_STANDBY);
				PAYLOADLEN = ReadRegister(ConfigurationRegister.Fifo);
				byte[] receiveBuffer = new byte[PAYLOADLEN];
				writeBuffer[0] = (byte)((int)ConfigurationRegister.Fifo & 0x7F);
				spi.WriteRead(writeBuffer, receiveBuffer, 1);
				byte TARGETID = receiveBuffer[0];
				byte SENDERID = receiveBuffer[1];
				byte CTLbyte = receiveBuffer[2];

				short nodeId = (short)(receiveBuffer[3] + (receiveBuffer[4] << 8));
				uint uptime= (uint)Arrays.ExtractInt32(receiveBuffer, 5); //uptime in ms
				float temp = Arrays.ExtractFloat(receiveBuffer, 9); //temperature maybe?

				Debug.Print("Node ID: " + nodeId + ", uptime: " + uptime + ", temperature: " + temp + ", missed frames: " + missedFrames + ", correct frames: " + correctFrames + ", last missed frame: " + lastMissedUptime);

				if(previousUptime == 0)
				{
					previousUptime = uptime;
				}
				else
				{
					if(uptime - previousUptime > 1500)
					{
						missedFrames++;
						lastMissedUptime = uptime;
						Debug.Print("Missed: " + missedFrames + ", correct: " + correctFrames);
					}
					else
					{
						correctFrames++;
						if(correctFrames%100 == 0)
						{
							Debug.Print("Missed: " + missedFrames + ", correct: " + correctFrames);
						}
					}
					previousUptime = uptime;
				}
				SetMode(Mode.RF69_MODE_RX);
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

		private void WriteRegister(byte register, byte value)
		{
			writeBuffer[0] = (byte)(register | 0x80);
			writeBuffer[1] = value;
			spi.Write(writeBuffer);
		}

		private byte ReadRegister(ConfigurationRegister register)
		{
			writeBuffer[0] = (byte)((int)register & 0x7F);
			spi.WriteRead(writeBuffer, readBuffer);
			return readBuffer[1];
		}

		private byte ReadRegister(byte register)
		{
			writeBuffer[0] = (byte)((int)register & 0x7F);
			spi.WriteRead(writeBuffer, readBuffer);
			return readBuffer[1];
		}

		// internal function
		public void SendFrame(byte remoteAddress, byte[] data, bool requestACK, bool sendACK)
		{
			interruptPin.DisableInterrupt();
			SetMode(Mode.RF69_MODE_STANDBY); // turn off receiver to prevent reception while filling fifo
			while ((ReadRegister(ConfigurationRegister.IrqFlags1) & 0x80) == 0x00)
			{ // wait for ModeReady
			}
			WriteRegister(ConfigurationRegister.DioMapping1, 0x00); // DIO0 is "Packet Sent"
			if (data.Length > 61)
			{
				throw new Exception("data too long");
			}

			// control byte

			byte CTLbyte = 0x00;
			if (sendACK)
			{
				CTLbyte = 0x80;
			}
			else if (requestACK)
			{
				CTLbyte = 0x40;
			}
			byte[] txData = new byte[data.Length + 5];
			txData[0] = 0x00 | 0x80;
			txData[1] = (byte)(data.Length + 3);
			txData[2] = remoteAddress;
			txData[3] = nodeID;
			txData[4] = CTLbyte;
			Array.Copy(data, 0, txData, 5, data.Length);
			//// write to FIFO
			//select();
			//SPI.transfer(REG_FIFO | 0x80);
			//SPI.transfer(bufferSize + 3);
			//SPI.transfer(toAddress);
			//SPI.transfer(_address);
			//SPI.transfer(CTLbyte);

			//for (uint8_t i = 0; i < bufferSize; i++)
			//SPI.transfer(((uint8_t*) buffer)[i]);
			//unselect();
			spi.Write(txData);
			bool status = interruptPin.Read();

			// no need to wait for transmit mode to be ready since its handled by the radio
			SetMode(Mode.RF69_MODE_TX);
			//status = interruptPin.Read();
			long txStart = DateTime.Now.Ticks / 10000;
			int counter = 0;
			while (interruptPin.Read() == false && DateTime.Now.Ticks / 10000 - txStart < 1000)
			{
				// wait for DIO0 to turn HIGH signalling transmission finish
				counter++;
			}
			long txStop = DateTime.Now.Ticks / 10000;
			long time = txStop - txStart;
			//while (readReg(REG_IRQFLAGS2) & RF_IRQFLAGS2_PACKETSENT == 0x00); // wait for ModeReady
			SetMode(Mode.RF69_MODE_STANDBY);
			interruptPin.EnableInterrupt();
		}

		public void ReceiveBegin()
		{
			byte temp = ReadRegister(ConfigurationRegister.IrqFlags2);
			if ((ReadRegister(ConfigurationRegister.IrqFlags2) & 0x04) == 0x04)
			{
				WriteRegister(ConfigurationRegister.PacketConfig2, (byte)((ReadRegister(ConfigurationRegister.PacketConfig2) & 0xFB) | 0x04)); // avoid RX deadlocks
			}
			WriteRegister(ConfigurationRegister.DioMapping1, 0x40); // set DIO0 to "PAYLOADREADY" in receive mode
			SetMode(Mode.RF69_MODE_RX);
		}

		// To enable encryption: radio.encrypt("ABCDEFGHIJKLMNOP");
		// To disable encryption: radio.encrypt(null) or radio.encrypt(0)
		// KEY HAS TO BE 16 bytes !!!
		void Encrypt(string key)
		{
			SetMode(Mode.RF69_MODE_STANDBY);
			if (key != null)
			{
				// TODO: Bruno: reenable
				//select();
				//SPI.transfer(REG_AESKEY1 | 0x80);
				//for (uint8_t i = 0; i < 16; i++)
				//SPI.transfer(key[i]);
				//unselect();
			}
			WriteRegister(ConfigurationRegister.PacketConfig2, (byte)((ReadRegister(ConfigurationRegister.PacketConfig2) & 0xFE) | (key != null ? 1 : 0)));
		}

		void OutputAllRegs()
		{
			byte regVal;

			for (byte regAddr = 1; regAddr <= 0x4F; regAddr++)
			{
				regVal = ReadRegister(regAddr);
				Debug.Print(regAddr.ToString("X2") + " - " + regVal.ToString("X2"));
			}
		}

		public void SetMode(Mode newMode)
		{
			if (newMode == currentMode)
				return;

			switch (newMode)
			{
				case Mode.RF69_MODE_TX:
					WriteRegister(0x01, (byte)((ReadRegister(0x01) & 0xE3) | 0x0C));
					break;
				case Mode.RF69_MODE_RX:
					WriteRegister(0x01, (byte)((ReadRegister(0x01) & 0xE3) | 0x10));
					break;
				case Mode.RF69_MODE_SYNTH:
					WriteRegister(0x01, (byte)((ReadRegister(0x01) & 0xE3) | 0x08));
					break;
				case Mode.RF69_MODE_STANDBY:
					WriteRegister(0x01, (byte)((ReadRegister(0x01) & 0xE3) | 0x04));
					break;
				case Mode.RF69_MODE_SLEEP:
					WriteRegister(0x01, (byte)((ReadRegister(0x01) & 0xE3) | 0x00));
					break;
				default:
					return;
			}

			// we are using packet mode, so this check is not really needed
			// but waiting for mode ready is necessary when going from sleep because the FIFO may not be immediately available from previous mode
			while (currentMode == 0 && (ReadRegister(0x27) & 0x80) == 0x00) ; // wait for ModeReady

			currentMode = newMode;
		}
	}
}
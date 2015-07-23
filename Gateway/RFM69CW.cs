using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;

namespace Gateway
{
	public class RFM69CW
	{
		private SPI spi = null;

		private enum ConfigurationRegister
		{
			Fifo,			// 0x00
			OpMode,			// 0x01
			DataModul,		// 0x02
			BitrateMsb,		// 0x03
			BitrateLsb,		// 0x04
			FdevMsb,		// 0x05
			Fdevlsb,		// 0x06
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
//#define REG_RSSITHRESH    0x29
//#define REG_RXTIMEOUT1    0x2A
//#define REG_RXTIMEOUT2    0x2B
//#define REG_PREAMBLEMSB   0x2C
//#define REG_PREAMBLELSB   0x2D
//#define REG_SYNCCONFIG    0x2E
//#define REG_SYNCVALUE1    0x2F
//#define REG_SYNCVALUE2    0x30
//#define REG_SYNCVALUE3    0x31
//#define REG_SYNCVALUE4    0x32
//#define REG_SYNCVALUE5    0x33
//#define REG_SYNCVALUE6    0x34
//#define REG_SYNCVALUE7    0x35
//#define REG_SYNCVALUE8    0x36
//#define REG_PACKETCONFIG1 0x37
//#define REG_PAYLOADLENGTH 0x38
//#define REG_NODEADRS      0x39
//#define REG_BROADCASTADRS 0x3A
//#define REG_AUTOMODES     0x3B
//#define REG_FIFOTHRESH    0x3C
//#define REG_PACKETCONFIG2 0x3D
//#define REG_AESKEY1       0x3E
//#define REG_AESKEY2       0x3F
//#define REG_AESKEY3       0x40
//#define REG_AESKEY4       0x41
//#define REG_AESKEY5       0x42
//#define REG_AESKEY6       0x43
//#define REG_AESKEY7       0x44
//#define REG_AESKEY8       0x45
//#define REG_AESKEY9       0x46
//#define REG_AESKEY10      0x47
//#define REG_AESKEY11      0x48
//#define REG_AESKEY12      0x49
//#define REG_AESKEY13      0x4A
//#define REG_AESKEY14      0x4B
//#define REG_AESKEY15      0x4C
//#define REG_AESKEY16      0x4D
//#define REG_TEMP1         0x4E
//#define REG_TEMP2         0x4F
//#define REG_TESTLNA       0x58
//#define REG_TESTPA1       0x5A  // only present on RFM69HW/SX1231H
//#define REG_TESTPA2       0x5C  // only present on RFM69HW/SX1231H
//#define REG_TESTDAGC      0x6F
		}


		public RFM69CW()
		{
			spi = new SPI(new SPI.Configuration(GHI.Pins.Generic.GetPin('A', 15), false, 0, 0, false, true, 1000, SPI.SPI_module.SPI1));

			WriteRegister(ConfigurationRegister.OpMode,	0x04);		// Sequencer on, listen off, standby mode
			WriteRegister(ConfigurationRegister.DataModul, 0x00);	// Packet mode, FSK, no shaping
			WriteRegister(ConfigurationRegister.BitrateMsb, 0x02);	// Default 4.8 KBPS
			WriteRegister(ConfigurationRegister.BitrateLsb, 0x40);
			///* 0x05 */ { REG_FDEVMSB, RF_FDEVMSB_50000}, // default: 5KHz, (FDEV + BitRate / 2 <= 500KHz)
			///* 0x06 */ { REG_FDEVLSB, RF_FDEVLSB_50000},

			///* 0x07 */ { REG_FRFMSB, (uint8_t) (freqBand==RF69_315MHZ ? RF_FRFMSB_315 : (freqBand==RF69_433MHZ ? RF_FRFMSB_433 : (freqBand==RF69_868MHZ ? RF_FRFMSB_868 : RF_FRFMSB_915))) },
			///* 0x08 */ { REG_FRFMID, (uint8_t) (freqBand==RF69_315MHZ ? RF_FRFMID_315 : (freqBand==RF69_433MHZ ? RF_FRFMID_433 : (freqBand==RF69_868MHZ ? RF_FRFMID_868 : RF_FRFMID_915))) },
			///* 0x09 */ { REG_FRFLSB, (uint8_t) (freqBand==RF69_315MHZ ? RF_FRFLSB_315 : (freqBand==RF69_433MHZ ? RF_FRFLSB_433 : (freqBand==RF69_868MHZ ? RF_FRFLSB_868 : RF_FRFLSB_915))) },

			//// looks like PA1 and PA2 are not implemented on RFM69W, hence the max output power is 13dBm
			//// +17dBm and +20dBm are possible on RFM69HW
			//// +13dBm formula: Pout = -18 + OutputPower (with PA0 or PA1**)
			//// +17dBm formula: Pout = -14 + OutputPower (with PA1 and PA2)**
			//// +20dBm formula: Pout = -11 + OutputPower (with PA1 and PA2)** and high power PA settings (section 3.3.7 in datasheet)
			/////* 0x11 */ { REG_PALEVEL, RF_PALEVEL_PA0_ON | RF_PALEVEL_PA1_OFF | RF_PALEVEL_PA2_OFF | RF_PALEVEL_OUTPUTPOWER_11111},
			/////* 0x13 */ { REG_OCP, RF_OCP_ON | RF_OCP_TRIM_95 }, // over current protection (default is 95mA)

			//// RXBW defaults are { REG_RXBW, RF_RXBW_DCCFREQ_010 | RF_RXBW_MANT_24 | RF_RXBW_EXP_5} (RxBw: 10.4KHz)
			///* 0x19 */ { REG_RXBW, RF_RXBW_DCCFREQ_010 | RF_RXBW_MANT_16 | RF_RXBW_EXP_2 }, // (BitRate < 2 * RxBw)
			////for BR-19200: /* 0x19 */ { REG_RXBW, RF_RXBW_DCCFREQ_010 | RF_RXBW_MANT_24 | RF_RXBW_EXP_3 },
			///* 0x25 */ { REG_DIOMAPPING1, RF_DIOMAPPING1_DIO0_01 }, // DIO0 is the only IRQ we're using
			///* 0x26 */ { REG_DIOMAPPING2, RF_DIOMAPPING2_CLKOUT_OFF }, // DIO5 ClkOut disable for power saving
			///* 0x28 */ { REG_IRQFLAGS2, RF_IRQFLAGS2_FIFOOVERRUN }, // writing to this bit ensures that the FIFO & status flags are reset
			///* 0x29 */ { REG_RSSITHRESH, 220 }, // must be set to dBm = (-Sensitivity / 2), default is 0xE4 = 228 so -114dBm
			/////* 0x2D */ { REG_PREAMBLELSB, RF_PREAMBLESIZE_LSB_VALUE } // default 3 preamble bytes 0xAAAAAA
			///* 0x2E */ { REG_SYNCCONFIG, RF_SYNC_ON | RF_SYNC_FIFOFILL_AUTO | RF_SYNC_SIZE_2 | RF_SYNC_TOL_0 },
			///* 0x2F */ { REG_SYNCVALUE1, 0x2D },      // attempt to make this compatible with sync1 byte of RFM12B lib
			///* 0x30 */ { REG_SYNCVALUE2, networkID }, // NETWORK ID
			///* 0x37 */ { REG_PACKETCONFIG1, RF_PACKET1_FORMAT_VARIABLE | RF_PACKET1_DCFREE_OFF | RF_PACKET1_CRC_ON | RF_PACKET1_CRCAUTOCLEAR_ON | RF_PACKET1_ADRSFILTERING_OFF },
			///* 0x38 */ { REG_PAYLOADLENGTH, 66 }, // in variable length mode: the max frame size, not used in TX
			/////* 0x39 */ { REG_NODEADRS, nodeID }, // turned off because we're not using address filtering
			///* 0x3C */ { REG_FIFOTHRESH, RF_FIFOTHRESH_TXSTART_FIFONOTEMPTY | RF_FIFOTHRESH_VALUE }, // TX on FIFO not empty
			///* 0x3D */ { REG_PACKETCONFIG2, RF_PACKET2_RXRESTARTDELAY_2BITS | RF_PACKET2_AUTORXRESTART_ON | RF_PACKET2_AES_OFF }, // RXRESTARTDELAY must match transmitter PA ramp-down time (bitrate dependent)
			////for BR-19200: /* 0x3D */ { REG_PACKETCONFIG2, RF_PACKET2_RXRESTARTDELAY_NONE | RF_PACKET2_AUTORXRESTART_ON | RF_PACKET2_AES_OFF }, // RXRESTARTDELAY must match transmitter PA ramp-down time (bitrate dependent)
			///* 0x6F */ { REG_TESTDAGC, RF_DAGC_IMPROVED_LOWBETA0 }, // run DAGC continuously in RX mode for Fading Margin Improvement, recommended default for AfcLowBetaOn=0
			//{255, 0}
		}

		private byte[] writeBuffer = new byte[2];
		private void WriteRegister(ConfigurationRegister register, byte value)
		{
			writeBuffer[0] = (byte)((int)register | 0x80);
			writeBuffer[1] = value;
			spi.Write(writeBuffer);
		}
	}
}
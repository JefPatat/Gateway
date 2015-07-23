using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;


namespace Gateway
{
	public class Program
	{
		static SPI MySPI = null;

		public static void Main()
		{
			SPI.Configuration MyConfig = null;
			try
			{
				MyConfig = new SPI.Configuration(GHI.Pins.Generic.GetPin('A', 15), false, 0, 0, false, true, 1000, SPI.SPI_module.SPI1);
				MySPI = new SPI(MyConfig);
			}
			catch (Exception ex)
			{

			}

			for (byte address = 0; address < 0x10; address++)
			{
				byte contents = ReadReg(address);
				Debug.Print(address.ToString("x2") + ": " + contents.ToString("x2"));
			}

			for (byte address = 0; address < 0x10; address++)
			{
				byte contents = ReadRegAlternative(address);
				Debug.Print(address.ToString("X2") + ": " + contents.ToString("x2"));
			}
		}

		static byte ReadReg(byte addr)
		{
			byte[] result = new byte[1];
			MySPI.WriteRead(new byte[] { (byte)(addr & 0x7F) }, result, 1);
			return result[0];
		}

		static byte ReadRegAlternative(byte addr)
		{
			byte[] result = new byte[2];
			MySPI.WriteRead(new byte[] { (byte)(addr & 0x7F) }, result);
			return result[1];
		}
	}
}

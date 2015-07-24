using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;


namespace Gateway
{
	public class Program
	{
		static RFM69CW rfm69CW = null;

		public static void Main()
		{
			try
			{
				rfm69CW = new RFM69CW(100, 1);
			}
			catch (Exception ex)
			{

			}

			rfm69CW.ReceiveBegin();
			while (true)
			{
				//rfm69CW.DumpIRQRegisters();
				System.Threading.Thread.Sleep(30);
			}
			//byte[] data = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			//while(true)
			//{
			//	for (byte i = 0; i < 10; i++)
			//	{
			//		data[2] = i;
			//		//rfm69CW.SendFrame(1, data, true, false);
			//		Debug.Print("Tx: " + i);
			//		System.Threading.Thread.Sleep(1000);
			//	}
			//}
		}
	}
}

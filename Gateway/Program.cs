using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using System.Collections;


namespace Gateway
{
	public class Program
	{
		static SWPManager swpManager = new SWPManager();

		public static void Main()
		{
			swpManager.Add(new IntelliStatNode(99));

			while (true)
			{
				swpManager.Cycle();
				System.Threading.Thread.Sleep(100);
			}
		}
	}
}

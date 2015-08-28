using System;
using Microsoft.SPOT;
using System.Collections;

namespace Gateway
{
	class SWPManager
	{
		private ArrayList swpNodes = new ArrayList();
		private RFM69CW rfm69CW = null;

		public SWPManager()
		{
			try
			{
				rfm69CW = new RFM69CW(100, 1);
			}
			catch (Exception ex)
			{

			}
		}

		public void Add(SimpleWirelessProtocolNode node)
		{
			node.SWPManager = this;
			swpNodes.Add(node);
		}

		public void Cycle()
		{
			while (rfm69CW.NumberOfQueuedReceivedMessage() > 0)
			{
				SWPMessage message = rfm69CW.DequeueReceivedMessage();
				if (message.DestinationAddress == 1)
				{
					foreach (SimpleWirelessProtocolNode swpNode in swpNodes)
					{
						if (swpNode.NodeAddress == message.SourceAddress)
						{
							swpNode.OnMessageReceived(message);
						}
					}
				}
			}

			foreach (SimpleWirelessProtocolNode swpNode in swpNodes)
			{
				swpNode.OnCycle();
			}
		}

		public void SendMessage(SWPMessage message)
		{
			rfm69CW.SendMessage(message);
		}
	}
}

using System;
using Microsoft.SPOT;

namespace Gateway
{
	class IntelliStatNode : SimpleWirelessProtocolNode
	{
		private bool pongTestMessageReceived = true;
		private uint gatewayTxCounter = 0;

		
		public IntelliStatNode(byte nodeAddress) : base(nodeAddress)
		{

		}

		public override void OnCycle()
		{
			base.OnCycle();
			if(pongTestMessageReceived)
			{
				pongTestMessageReceived = false;
				ReadParameter(1, 99);
				gatewayTxCounter++;
				Debug.Print("Ping " + gatewayTxCounter);
			}
		}

		protected override void OnParameterRead(byte parameter, byte[] data)
		{
			pongTestMessageReceived = true;
			Debug.Print("Pong received: " + lastRSSI.ToString());
		}

		protected override void OnCommunicationError()
		{
			Debug.Print("Communication error");
			gatewayTxCounter++;
			Debug.Print("Ping " + gatewayTxCounter);
			ReadParameter(1, 99);
		}
	}
}

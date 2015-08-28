using System;
using Microsoft.SPOT;

namespace Gateway
{
	public class SWPMessage
	{
		public byte SourceAddress { get; set; }
		public byte DestinationAddress { get; set; }
		public byte ServiceIdentifier { get; set; }
		public byte[] Data { get; set; }
		public short RSSI { get; set; }

		public SWPMessage()
		{

		}

		public SWPMessage(byte sourceAddress, byte destinationAddress, byte serviceIdentifier, byte[] data)
		{
			SourceAddress = sourceAddress;
			DestinationAddress = destinationAddress;
			ServiceIdentifier = serviceIdentifier;
			Data = data;
		}
	}

	class SimpleWirelessProtocolNode
	{
		private bool waitingForResponse;
		private byte lastServiceIdentifier;
		private byte[] lastData;
		private byte retryCounter;
		private byte retryIntervalTicks;
		protected short lastRSSI;
		private const uint maximumRetries = 3;
		private const uint maximumRetryIntervalTicks = 10;

		public SimpleWirelessProtocolNode(byte nodeAddress)
		{
			NodeAddress = nodeAddress;
		}
		public byte NodeAddress { get; set; }
		public SWPManager SWPManager { get; set; }
		
		public virtual void OnCycle()
		{
			if(waitingForResponse)
			{
				retryIntervalTicks++;
				if (retryIntervalTicks == maximumRetryIntervalTicks)
				{
					if(retryCounter == maximumRetries)
					{
						// Timeout
						waitingForResponse = false;
						OnCommunicationError();
					}
					else
					{
						retryCounter++;
						retryIntervalTicks = 0;
						SWPManager.SendMessage(new SWPMessage(1, NodeAddress, lastServiceIdentifier, lastData));
					}
				}
			}
		}
		
		protected virtual void OnCommunicationError()
		{

		}

		public void OnMessageReceived(SWPMessage message)
		{
			lastRSSI = message.RSSI;
			switch(message.ServiceIdentifier)
			{
				case 2:
					waitingForResponse = false;
					byte[] data = new byte[message.Data.Length - 1];
					Array.Copy(message.Data, data, data.Length);
					OnParameterRead(message.Data[0], data);
					break;
				default:
					break;
			}
		}

		protected void ReadParameter(byte parameter, byte destinationAddress)
		{
			lastServiceIdentifier = 1;
			lastData = new byte[] { parameter };
			retryIntervalTicks = 0;
			retryCounter = 0;
			waitingForResponse = true;
			SWPManager.SendMessage(new SWPMessage(1, NodeAddress, lastServiceIdentifier, lastData));
		}

		protected virtual void OnParameterRead(byte parameter, byte[] data)
		{

		}
	}
}

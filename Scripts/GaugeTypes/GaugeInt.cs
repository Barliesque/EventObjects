using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(int)", menuName = "Barliesque/Event Objects/Gauges/int", order = 1)]
	public class GaugeInt : Gauge<int>, Gauge<int>.ISerializable
	{
		public string Serialize(int value)
		{
			return value.ToString();
		}

		public int Deserialize(string serial)
		{
			return int.Parse(serial);
		}
	}

}
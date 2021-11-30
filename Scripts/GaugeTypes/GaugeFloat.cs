using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(float)", menuName = "Barliesque/Event Objects/Gauges/float", order = 1)]
	public class GaugeFloat : Gauge<float>, Gauge<float>.ISerializable
	{
		public string Serialize(float value)
		{
			return value.ToString();
		}

		public float Deserialize(string serial)
		{
			return float.Parse(serial);
		}

	}

}
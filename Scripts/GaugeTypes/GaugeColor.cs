using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(Color)", menuName = "Barliesque/Event Objects/Gauges/Color", order = 1)]
	public class GaugeColor : Gauge<Color>, Gauge<Color>.ISerializable
	{
		public string Serialize(Color value)
		{
			return $"{value.r},{value.g},{value.b},{value.a}";
		}

		public Color Deserialize(string serial)
		{
			var part = serial.Split(',');
			return new Color(float.Parse(part[0]), float.Parse(part[1]), float.Parse(part[2]), float.Parse(part[3]));
		}
	}
}
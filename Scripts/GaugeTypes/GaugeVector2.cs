using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(Vector2)", menuName = "Barliesque/Event Objects/Gauges/Vector2", order = 1)]
	public class GaugeVector2 : Gauge<Vector2>, Gauge<Vector2>.ISerializable
	{
		public string Serialize(Vector2 value)
		{
			return $"{value.x},{value.y}";
		}

		public Vector2 Deserialize(string serial)
		{
			var part = serial.Split(',');
			return new Vector2(float.Parse(part[0]), float.Parse(part[1]));
		}
	}

}

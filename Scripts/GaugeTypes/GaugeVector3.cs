using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(Vector3)", menuName = "Barliesque/Event Objects/Gauges/Vector3", order = 1)]
	public class GaugeVector3 : Gauge<Vector3>, Gauge<Vector3>.ISerializable
	{
		public string Serialize(Vector3 value)
		{
			return $"{value.x},{value.y},{value.z}";
		}

		public Vector3 Deserialize(string serial)
		{
			var part = serial.Split(',');
			return new Vector3(float.Parse(part[0]), float.Parse(part[1]), float.Parse(part[2]));
		}
	}

}
using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(bool)", menuName = "Barliesque/Event Objects/Gauges/bool", order = 1)]
	public class GaugeBool : Gauge<bool>, Gauge<bool>.ISerializable
	{
		public string Serialize(bool value)
		{
			return (value ? "T" : "F");
		}

		public bool Deserialize(string serial)
		{
			return (serial == "T");
		}
	}

}

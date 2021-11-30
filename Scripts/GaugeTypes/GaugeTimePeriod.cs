using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(TimePeriod)", menuName = "Barliesque/Event Objects/Gauges/TimePeriod", order = 1)]
	public class GaugeTimePeriod : Gauge<TimePeriod>, Gauge<TimePeriod>.ISerializable
	{
		public string Serialize(TimePeriod value)
		{
			return $"{value.Days},{value.Hours},{value.Minutes},{value.Seconds}";
		}

		public TimePeriod Deserialize(string serial)
		{
			var part = serial.Split(',');
			return new TimePeriod(int.Parse(part[0]), int.Parse(part[1]), int.Parse(part[2]), float.Parse(part[3]));
		}
	}

}

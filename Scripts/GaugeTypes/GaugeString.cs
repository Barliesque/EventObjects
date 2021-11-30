using UnityEngine;
using Barliesque.EventObjects;

[CreateAssetMenu(fileName = "Gauge(string)", menuName = "Barliesque/Event Objects/Gauges/string")]
public class GaugeString : Gauge<string>, Gauge<string>.ISerializable
{
	public string Serialize(string value)
	{
		return value;
	}

	public string Deserialize(string serial)
	{
		return serial;
	}

}

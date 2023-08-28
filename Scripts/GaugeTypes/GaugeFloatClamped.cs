using System.Globalization;
using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(float-clamped)", menuName = "Barliesque/Event Objects/Gauges/float (Clamped)", order = 1)]
	public class GaugeFloatClamped : Gauge<float>, Gauge<float>.ISerializable
	{
		[SerializeField] private float _min = 0f;
		public float Min => _min;

		[SerializeField] private float _max = 1f;
		public float Max => _max;

		public string Serialize(float value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		public float Deserialize(string serial)
		{
			return float.Parse(serial);
		}

		override protected float OnChange(float value)
		{
			return base.OnChange(Mathf.Clamp(value, _min, _max));
		}

	}

}

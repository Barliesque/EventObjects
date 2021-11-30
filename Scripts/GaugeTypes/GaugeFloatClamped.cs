using UnityEngine;

namespace Barliesque.EventObjects
{
	[CreateAssetMenu(fileName = "Gauge(float-clamped)", menuName = "Barliesque/Event Objects/Gauges/float (Clamped)", order = 1)]
	public class GaugeFloatClamped : Gauge<float>, Gauge<float>.ISerializable
	{
		[SerializeField] float _min = 0f;
		public float Min { get { return _min; } }

		[SerializeField] float _max = 1f;
		public float Max { get { return _max; } }

		public string Serialize(float value)
		{
			return value.ToString();
		}

		public float Deserialize(string serial)
		{
			return float.Parse(serial);
		}

		protected override float OnChange(float value)
		{
			return base.OnChange(Mathf.Clamp(value, _min, _max));
		}

	}

}

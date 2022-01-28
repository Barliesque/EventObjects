using UnityEngine;
using System;


[Serializable]
public struct TimePeriod : ISerializationCallbackReceiver
{
	[SerializeField] private int _days;
	public int Days {
		get { return _days; }
		set {
			_days = value;
			Validate();
		}
	}

	[SerializeField] private int _hours;
	public int Hours {
		get { return _hours; }
		set {
			_hours = value;
			Validate();
		}
	}

	[SerializeField] private int _minutes;
	public int Minutes {
		get { return _minutes; }
		set {
			_minutes = value;
			Validate();
		}
	}

	[SerializeField] private float _seconds;
	public float Seconds {
		get { return _seconds; }
		set {
			_seconds = value;
			Validate();
		}
	}


	public TimePeriod(int days, int hours, int minutes, float seconds)
	{
		_days = days;
		_hours = hours;
		_minutes = minutes;
		_seconds = seconds;
		Validate();
	}

	private void Validate()
	{
		Overflow(ref _seconds, 60, ref _minutes);
		Overflow(ref _minutes, 60, ref _hours);
		Overflow(ref _hours, 24, ref _days);
	}

	private void Overflow(ref int first, int max, ref int second)
	{
		int over = first / max;
		first -= over * max;
		second += over;
	}

	private void Overflow(ref float first, int max, ref int second)
	{
		int over = (int)first / max;
		first -= over * max;
		second += over;
	}


	public TimeSpan ToTimeSpan()
	{
		Validate();

		int sec = (int)_seconds;
		int ms = (int)((_seconds - sec) * 1000f);

		return new TimeSpan(_days, _hours, _minutes, sec, ms);
	}


	public void OnBeforeSerialize()
	{
		Validate();
	}

	public void OnAfterDeserialize()
	{ }


	public override string ToString()
	{
		return $"[TimePeriod: Days={_days} Hours={_hours} Minutes={_minutes} Seconds={_seconds}]";
	}
}


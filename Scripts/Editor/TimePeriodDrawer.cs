using UnityEditor;
using Barliesque.InspectorTools.Editor;


namespace Barliesque.EventObjects.Editor
{

	[CustomPropertyDrawer(typeof(TimePeriod))]
	public class TimePeriodDrawer : PropertyDrawerHelper
	{
		protected override float LeftMargin => 200f;

		public override void CustomDrawer()
		{
			Field(37f, "Days", 35f, "_days");
			Field(26f, "Hrs", 35f, "_hours");
			Field(32f, "Mins", 35f, "_minutes");
			Field(34f, "Secs", 50f, "_seconds");
		}

	}

}
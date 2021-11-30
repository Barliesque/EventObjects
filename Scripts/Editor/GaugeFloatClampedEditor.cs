using UnityEditor;
using Barliesque.InspectorTools.Editor;

namespace Barliesque.EventObjects.Editor
{
	[CustomEditor(typeof(GaugeFloatClamped), true)]
	public class GaugeFloatClampedEditor : EditorBase
	{

		protected override void CustomInspector()
		{
			GaugeEditor.InspectorMain(this);
			PropertyField("_min", "Min Value");
			PropertyField("_max", "Max Value");

			GaugeEditor.InspectorRuntime(this);
		}

	}

}
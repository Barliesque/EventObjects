using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Barliesque.InspectorTools.Editor;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects.Editor
{
	[CustomEditor(typeof(PartyLine), true)]
	public class PartyLineEditor : EditorBase
	{
		static bool _ownersFolded = true;
		static bool _listenersFolded = true;
		static List<MonoBehaviour> _refs = new List<MonoBehaviour> { };


		protected override void CustomInspector()
		{
			Inspector(this);
		}

		/// <summary>
		/// Draw the standard PartyLine inspector
		/// </summary>
		/// <param name="editor"></param>
		static public void Inspector(EditorBase editor)
		{
			var inst = (PartyLine)editor.target;

			PropertyField(editor, "Comments");
			EditorGUILayout.Space();

			PropertyField(editor, "_logMessages");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Runtime Info:");

			EditorTools.BeginInfoBox();

			if (inst.KeyCount > 0 || inst.ListenerCount > 0) {
				EditorGUILayout.LabelField("Message Type", (inst.MessageType == null) ? "(none)" : $"<{inst.MessageType.Name}>");
				bool enabled = GUI.enabled;
				GUI.enabled = false;
				inst.__getOwners(_refs);
				EditorTools.ArrayEditor("Key Owners", _refs, ref _ownersFolded);
				inst.__getListeners(_refs);
				EditorTools.ArrayEditor("Listeners", _refs, ref _listenersFolded);
				GUI.enabled = enabled;

			} else {
				var none = Application.isPlaying ? "(not in use)" : "---";
				EditorGUILayout.LabelField("Message Type", none);
				EditorGUILayout.LabelField("Key Owners");
				EditorTools.BeginInfoBox();
				EditorTools.EndInfoBox();
				EditorGUILayout.LabelField("Listeners");
				EditorTools.BeginInfoBox();
				EditorTools.EndInfoBox();
			}

			EditorTools.EndInfoBox();
		}

	}

}
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Barliesque.InspectorTools.Editor;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects.Editor
{
	[CustomEditor(typeof(Messenger), true)]
	public class MessengerEditor : EditorBase
	{

		static bool _subscribersFolded = true;
		static List<MonoBehaviour> _refs = new List<MonoBehaviour> { };

		protected override void CustomInspector()
		{
			Inspector(this);
		}

		/// <summary>
		/// Draw the standard Messenger inspector
		/// </summary>
		/// <param name="editor"></param>
		static public void Inspector(EditorBase editor)
		{
			var inst = (Messenger)editor.target;

			PropertyField(editor, "Comments");
			EditorGUILayout.Space();

			PropertyField(editor, "_logMessages");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Runtime Info:");

			EditorTools.BeginInfoBox();
			bool enabled = GUI.enabled;

			if (inst.HasKey) {
				MonoBehaviour owner;
				if (inst.__getOwner(out owner)) {

					GUILayout.BeginHorizontal();
					EditorGUILayout.LabelField("Sender");
					GUI.enabled = false;
					EditorGUILayout.ObjectField(owner, owner.GetType());
					GUI.enabled = enabled;
					GUILayout.EndHorizontal();

				} else {
					EditorGUILayout.LabelField("Sender", "(none)");
				}

				EditorGUILayout.LabelField("Message Type", inst.MessageType?.Name ?? "(none)");
				EditorGUILayout.LabelField("Response Type", inst.ResponseType?.Name ?? "(none)");

				GUI.enabled = false;
				inst.__getSubscribers(_refs);
				EditorTools.ArrayEditor("Subscribers", _refs, ref _subscribersFolded);
				GUI.enabled = enabled;

			} else {

				var none = Application.isPlaying ? "(not in use)" : "---";
				EditorGUILayout.LabelField("Sender", "---");
				EditorGUILayout.LabelField("Message Type", none);
				EditorGUILayout.LabelField("Response Type", none);

				EditorGUILayout.LabelField("Subscribers");
				EditorTools.BeginInfoBox();
				EditorTools.EndInfoBox();
			}

			EditorTools.EndInfoBox();
		}

	}

}
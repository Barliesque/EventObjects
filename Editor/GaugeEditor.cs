using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Barliesque.InspectorTools.Editor;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects.Editor
{

	[CustomEditor(typeof(Gauge), true)]
	public class GaugeEditor : EditorBase
	{

		static SerializedProperty _editableCopy;

		static bool _ownersFolded = true;
		static bool _watchersFolded = true;
		static List<MonoBehaviour> _refs = new List<MonoBehaviour> { };


		private void OnEnable()
		{
			_editableCopy = null;
		}

		private void OnDisable()
		{
			// If an edit was not applied, undo it now.
			if (_editableCopy != null) {
				UndoEdit();
			}
		}

		protected override void CustomInspector()
		{
			InspectorMain(this);
			InspectorRuntime(this);
		}


		static public void InspectorMain(EditorBase editor)
		{
			var inst = (Gauge)editor.target;
			var enabled = GUI.enabled;

			PropertyField(editor, "Comments");
			EditorGUILayout.Space();
			PropertyField(editor, "_logChanges");

			if (inst.IsSerializable) {
				var wasPersistent = inst.IsPersistent;
				if (PropertyField(editor, "_persistent").boolValue) {
					EditorGUILayout.LabelField("PlayerPrefs Path", inst.PrefsPath);
					if (!wasPersistent) {
						// Persistent checkbox was just checked, so set current value
						inst.__initFromSaved();
						// Update the serialized object from the instance
						editor.serializedObject.Update();
					}
				}
				EditorGUILayout.Space();
			}

			PropertyField(editor, "_default", "Default Value");

			GUI.enabled = enabled;
		}

		static public void InspectorRuntime(EditorBase editor)
		{
			var inst = (Gauge)editor.target;
			var enabled = GUI.enabled;

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Runtime Info:");
			EditorTools.BeginInfoBox();

			if (Application.isPlaying) {

				bool isEditing = (_editableCopy != null);

				// Current Property Field
				EditorTools.BeginInfoBox();
				GUI.enabled = isEditing;
				if (isEditing) {
					EditorGUILayout.PropertyField(_editableCopy, new GUIContent("Current Value"));
				} else {
					PropertyField(editor, "_current", "Current Value");
				}
				EditorTools.EndInfoBox();

				// Editing buttons
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();

				GUI.enabled = enabled;
				if (GUILayout.Button("Reset")) ResetValue(inst);
				if (isEditing) {
					if (GUILayout.Button("Apply")) ApplyEdit(editor, inst);
				} else {
					if (GUILayout.Button("Edit")) BeginEdit(inst);
				}
				GUI.enabled = isEditing;
				if (GUILayout.Button("Cancel")) UndoEdit();

				GUILayout.EndHorizontal();
				EditorGUILayout.Space();

				GUI.enabled = false;
				inst.__getOwners(_refs);
				EditorTools.ArrayEditor("Key Owners", _refs, ref _ownersFolded);
				inst.__getWatchers(_refs);
				EditorTools.ArrayEditor("Watchers", _refs, ref _watchersFolded);
				GUI.enabled = enabled;
			} else {

				EditorTools.BeginInfoBox();

				if (inst.IsPersistent) {
					EditorGUI.BeginChangeCheck();
					PropertyField(editor, "_current", "Current Value");
					if (EditorGUI.EndChangeCheck()) {
						editor.serializedObject.ApplyModifiedProperties();
						inst.__changed();
					}
				} else {
					EditorGUILayout.LabelField("Current Value", "---");
				}

				EditorTools.EndInfoBox();

				if (inst.IsPersistent) {
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Reset")) ResetValue(inst);
					GUILayout.EndHorizontal();
					EditorGUILayout.Space();
				}

				EditorGUILayout.LabelField("Key Owners");
				EditorTools.BeginInfoBox();
				EditorTools.EndInfoBox();

				EditorGUILayout.LabelField("Watchers");
				EditorTools.BeginInfoBox();
				EditorTools.EndInfoBox();
			}

			EditorTools.EndInfoBox();

		}


		private static void ResetValue(Gauge inst)
		{
			inst.__reset();
		}

		private static void BeginEdit(Gauge inst)
		{
			// Make a copy of the serialized property that's disconnected from the instance
			_editableCopy = new SerializedObject(inst).FindProperty("_current");
		}

		private static void UndoEdit()
		{
			// Dispose of the editable copy, so that the instance's current value is again displayed
			_editableCopy.Dispose();
			_editableCopy = null;
		}

		private static void ApplyEdit(EditorBase editor, Gauge inst)
		{
			// Copy the edited property to the instance
			editor.serializedObject.CopyFromSerializedProperty(_editableCopy);
			editor.serializedObject.ApplyModifiedProperties();

			// Invoke changed events
			inst.__changed();

			// Exit edit mode by disposing of the editable copy
			_editableCopy.Dispose();
			_editableCopy = null;
		}

	}

}
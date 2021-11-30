using UnityEngine;
using UnityEditor;
using Barliesque.InspectorTools.Editor;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects.Editor
{
	[CustomEditor(typeof(Mailbox), true)]
	public class MailboxEditor : EditorBase
	{

		protected override void CustomInspector()
		{
			Inspector(this);
		}

		/// <summary>
		/// Draw the standard Mailbox inspector
		/// </summary>
		/// <param name="editor"></param>
		static public void Inspector(EditorBase editor)
		{
			bool enabled = GUI.enabled;
			var inst = (Mailbox)editor.target;

			PropertyField(editor, "Comments");
			EditorGUILayout.Space();

			PropertyField(editor, "_logMessages");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Runtime Info:");

			EditorTools.BeginInfoBox();

			if (Application.isPlaying) {

				MonoBehaviour owner;
				if (inst.__getOwner(out owner)) {

					GUILayout.BeginHorizontal();
					EditorGUILayout.LabelField("Mailbox Owner");
					GUI.enabled = false;
					EditorGUILayout.ObjectField(owner, owner.GetType());
					GUI.enabled = enabled;
					GUILayout.EndHorizontal();

				} else {
					EditorGUILayout.LabelField("Mailbox Owner", "(none)");
				}

				EditorGUILayout.LabelField("Message Type", (inst.MessageType == null) ? "(none)" : $"<{inst.MessageType.Name}>");
				EditorGUILayout.LabelField("Response Type", (inst.ResponseType == null) ? "(none)" : $"<{inst.ResponseType.Name}>");
				EditorGUILayout.LabelField("Holds Messages?", inst.HasKey ? (inst.HoldsMail ? "Yes" : "No") : "---");
				EditorGUILayout.LabelField("Message Dies With Sender?", inst.HasKey ? (inst.MessageDiesWithSender ? "Yes" : "No") : "---");
				EditorGUILayout.LabelField("Message Count", inst.MessageCount.ToString());
				EditorGUILayout.LabelField("Max Capacity", inst.MaxCapacity.ToString());

			} else {

				EditorGUILayout.LabelField("Mailbox Owner", "---");
				EditorGUILayout.LabelField("Message Type", "---");
				EditorGUILayout.LabelField("Response Type", "---");
				EditorGUILayout.LabelField("Holds Messages?", "---");
				EditorGUILayout.LabelField("Message Dies With Sender?", "---");
				EditorGUILayout.LabelField("Message Count", "---");
				EditorGUILayout.LabelField("Max Capacity", "---");
			}

			EditorTools.EndInfoBox();
			GUI.enabled = enabled;
		}

	}

}
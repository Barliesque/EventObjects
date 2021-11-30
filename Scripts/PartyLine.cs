#if DEVELOPMENT_BUILD || UNITY_EDITOR
#define LOGGING
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



namespace Barliesque.EventObjects
{

	/// <summary>
	/// PartyLine is a specialized Observer implementation for a group of objects
	/// that need to talk to each other.  When a member of the group sends a
	/// message, each of the other members receives it without receiving its
	/// own message.
	/// </summary> 
	/// 
	/// Author(s): 
	/// - 6/26/2018		David Barlia
	/// -
	///
	[CreateAssetMenu(fileName = "New Party Line", menuName = "Barliesque/Event Objects/Party Line", order = 2)]
	public class PartyLine : ScriptableObject
	{
		static Dictionary<string, PartyLine> _instances;
		[NonSerialized] PartyLine _inst;
		PartyLine Instance
		{
			get {
				if (_inst == null)
				{
					if (_instances == null)
					{
						_instances = new Dictionary<string, PartyLine>();
					}
					if (_instances.ContainsKey(this.name))
					{
						_inst = _instances[this.name];
					}
					else
					{
						_instances.Add(this.name, this);
						_inst = this;
					}
				}
				return _inst;
			}
		}



		[SerializeField, TextArea]
		private string Comments;

		List<KeyBase> _keys;  // List<Key<T>> or List<Key>
		List<IWeakDelegate> _listeners;  // List<WeakDelegate<MessageHandler<T>>> or List<WeakDelegate<MessageHandler>>

		public delegate void MessageHandler();
		public delegate void MessageHandler<T>(T message);

		/// <summary>
		/// Count of the number of undisposed Keys that currently communicate through this PartyLine
		/// </summary>
		public int KeyCount {
			get {
				if (_keys == null) return 0;
				return ((ICollection)_keys).Count;
			}
		}

		/// <summary>
		/// Count of the number of listeners receiving messages from this PartyLine
		/// </summary>
		public int ListenerCount {
			get {
				if (_listeners == null) return 0;
				return ((ICollection)_listeners).Count;
			}
		}

		/// <summary>
		/// The type of value or object to be sent across this PartyLine
		/// </summary>
		public Type MessageType { get { return _messageType; } }
		Type _messageType;

		[SerializeField]
		private bool _logMessages;
		bool LogMessages { get { return _logMessages && (Application.isEditor || Debug.isDebugBuild); } }


#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		public void __getOwners(List<MonoBehaviour> owners)
		{
			owners.Clear();
			if (_keys != null) {
				for (int i = _keys.Count - 1; i >= 0; i--) {
					MonoBehaviour owner;
					var success = _keys[i].GetOwner(out owner);
					owners.Insert(0, success ? owner : null);
				}
			}
		}
#endif

		//===
		#region INITIALIZATION


#if UNITY_EDITOR

		private void OnEnable()
		{
			Application.wantsToQuit -= OnApplicationQuit;
			Application.wantsToQuit += OnApplicationQuit;
		}

		bool OnApplicationQuit()
		{
			// ScriptableObject fields remain populated even outside of runtime in the Editor.
			// So, all fields should be returned to initial value.
			_keys = null;
			_listeners = null;
			_messageType = null;
			return true;
		}
#endif


		void CheckInitialization(Type messageType, bool newKey, MonoBehaviour initializer)
		{
			if (_keys == null) {
				_keys = new List<KeyBase>();
				_listeners = new List<IWeakDelegate>();
				_messageType = messageType;
#if LOGGING
				if (LogMessages) {
					Debug.Log($"PartyLine [{name}] initialized when [{initializer.GetType().Name}] on [{initializer.name}] called {(newKey ? "CreateKey()" : "AddListener()")}");
				}
#endif
			} else {
				//  Check for matching type
				if (_messageType != messageType) {
					if (_messageType == null) {
						throw new Exception($"Type mismatch!  PartyLine [{name}] has been initialized for messages with no parameter.");
					} else {
						throw new Exception($"Type mismatch!  PartyLine [{name}] has been initialized for messages of type <{_messageType.Name}>");
					}
				}
			}
		}

		#endregion
		//===
		#region KEYS


		/// <summary>
		/// Each member of the PartyLine group must have a key, which it will use to send messages.  The first key created determines what message type all keys must handle.
		/// </summary>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected without Key.Dispose() being called, an error is thrown.</param>
		/// <param name="handler">A method to handle incoming messages from the PartyLine</param>
		public IKey CreateKey(MonoBehaviour owner, MessageHandler handler)
		{
			CheckInitialization(null, true, owner);
			var key = new Key(owner, this, handler);
			_keys.Add(key);

			return key;
		}


		/// <summary>
		/// Each member of the PartyLine group must have a key, which it will use to send messages.  The first key created determines what message type all keys must handle.
		/// </summary>
		/// <param name="handler">A method to handle incoming messages from the PartyLine</param>
		public IKey<T> CreateKey<T>(MonoBehaviour owner, MessageHandler<T> handler)
		{
			CheckInitialization(typeof(T), true, owner);
			var key = new Key<T>(owner, this, handler);
			_keys.Add(key);

			return key;
		}


		private void DisposeKey(KeyBase key)
		{
			if (_keys == null) return;
			if (_keys.Contains(key)) {
				_keys.Remove(key);
			}
		}


		private void DisposeKey<T>(Key<T> key)
		{
			if (_keys == null) return;
			if (_keys.Contains(key)) {
				_keys.Remove(key);
			}
		}


		public interface IKey
		{
			/// <summary>
			/// Send a message to all other members of the PartyLine
			/// </summary>
			void SendMessage();

			/// <summary>
			/// Remove this Key from the PartyLine
			/// </summary>
			void Dispose();

#if UNITY_EDITOR
			/// <summary>
			/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
			/// </summary>
			[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
			bool GetOwner(out MonoBehaviour owner);
#endif
		}


		public interface IKey<T>
		{
			/// <summary>
			/// Send a message to all other members of the PartyLine
			/// </summary>
			/// <param name="message">Parameter data to send</param>
			void SendMessage(T message);

			/// <summary>
			/// Remove this Key from the PartyLine.  Must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();

#if UNITY_EDITOR
			/// <summary>
			/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
			/// </summary>
			[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
			bool GetOwner(out MonoBehaviour owner);
#endif
		}

		abstract class KeyBase
		{
			public IWeakDelegate Handler;
			protected PartyLine _party;
			internal bool Sending = false;

			public void Dispose()
			{
				_party.DisposeKey(this);
				_party = null;
				Handler.Dispose();
			}

			public bool GetOwner(out MonoBehaviour owner)
			{
				bool success = Handler.GetOwner(out owner);
				if (!success && _party != null) {
					Debug.LogException(new Exception($"PartyLine [{_party.name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					Dispose();
				}
				return success;
			}

			public bool GetCallback(out MessageHandler callback)
			{
				var handler = (WeakDelegate<MessageHandler>)Handler;
				return handler.GetCallback(out callback);
			}

			public bool GetCallback<T>(out MessageHandler<T> callback)
			{
				var handler = (WeakDelegate<MessageHandler<T>>)Handler;
				return handler.GetCallback(out callback);
			}
		}


		private class Key : KeyBase, IKey
		{
			public Key(MonoBehaviour owner, PartyLine party, MessageHandler handler)
			{
				_party = party;
				Handler = new WeakDelegate<MessageHandler>(owner, handler);
			}

			public void SendMessage()
			{
				_party.SendMessage(this);
			}
		}


		private class Key<T> : KeyBase, IKey<T>
		{
			public Key(MonoBehaviour owner, PartyLine party, MessageHandler<T> handler)
			{
				_party = party;
				Handler = new WeakDelegate<MessageHandler<T>>(owner, handler);
			}

			public void SendMessage(T message)
			{
				_party.SendMessage(this, message);
			}
		}


		#endregion
		//===
		#region LISTENERS


		/// <summary>
		/// Listen for messages being distributed on the PartyLine.
		/// </summary>
		/// <param name="listener">A reference to the script that contains the handler (typically "this").  If the listener is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A method to receive messages distributed by this PartyLine</param>
		public void AddListener(MonoBehaviour listener, MessageHandler handler)
		{
			CheckInitialization(null, false, listener);

			// Check for duplication
			if (handler == null || FindListener(listener, handler) >= 0) return;

			// Add new listener
			_listeners.Add(new WeakDelegate<MessageHandler>(listener, handler));
		}


		/// <summary>
		/// Listen for messages being distributed on the PartyLine.
		/// </summary>
		/// <typeparam name="T">Type of message distributed by this PartyLine</typeparam>
		/// <param name="handler">A listener method to receive messages distributed by this PartyLine</param>
		public void AddListener<T>(MonoBehaviour listener, MessageHandler<T> handler)
		{
			CheckInitialization(typeof(T), false, listener);

			// Check for duplication
			if (handler == null || FindListener<T>(listener, handler) >= 0) return;

			// Add new listener
			_listeners.Add(new WeakDelegate<MessageHandler<T>>(listener, handler));
		}


		/// <summary>
		/// Stop receiving messages from this PartyLine
		/// </summary>
		/// <param name="listener">The component that contains the handler, previously registered with AddListener()</param>
		/// <param name="handler">A listener method previously added to this PartyLine</param>
		public void RemoveListener(MonoBehaviour listener, MessageHandler handler)
		{
			if (_listeners == null) return;
			int i = FindListener(listener, handler);
			if (i >= 0) {
				_listeners.RemoveAt(i);
			}
		}


		/// <summary>
		/// Stop receiving messages from this PartyLine
		/// </summary>
		/// <typeparam name="T">Type of message distributed by this PartyLine</typeparam>
		/// <param name="listener">The component that contains the handler, previously registered with AddListener()</param>
		/// <param name="handler">A listener method previously added to this PartyLine</param>
		public void RemoveListener<T>(MonoBehaviour listener, MessageHandler<T> handler)
		{
			if (_listeners == null) return;
			int i = FindListener(listener, handler);
			if (i >= 0) {
				_listeners.RemoveAt(i);
			}
		}


		int FindListener(MonoBehaviour listener, MessageHandler handler)
		{
			for (int i = _listeners.Count - 1; i >= 0; i--) {
				MonoBehaviour listening = null;
				if (_listeners[i].GetOwner(out listening)) {
					if (listening != listener) continue;
					// Found the listener
					MessageHandler found;
					var del = (WeakDelegate<MessageHandler>)_listeners[i];
					if (del.GetCallback(out found)) {
						// Handler found?
						if (found == handler) return i;
					}
				} else {
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>");
					_listeners.RemoveAt(i);
				}
			}
			return -1;
		}


		int FindListener<T>(MonoBehaviour listener, MessageHandler<T> handler)
		{
			// Check for duplication
			for (int i = _listeners.Count - 1; i >= 0; i--) {
				MonoBehaviour listening;
				var del = (WeakDelegate<MessageHandler<T>>)_listeners[i];
				if (del.GetOwner(out listening)) {
					if (listening != listener) continue;
					// Found the listener
					MessageHandler<T> found;
					if (del.GetCallback(out found)) {
						// Handler found?
						if (found == handler) return i;
					}
				} else {
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>");
					_listeners.RemoveAt(i);
				}
			}
			return -1;
		}

#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		public void __getListeners(List<MonoBehaviour> listeners)
		{
			listeners.Clear();
			if (_listeners != null) {
				for (int i = _listeners.Count - 1; i >= 0; i--) {
					MonoBehaviour listener;
					var success = _listeners[i].GetOwner(out listener);
					listeners.Insert(0, success ? listener : null);
				}
			}
		}
#endif


		#endregion
		//===
		#region MESSAGE SENDING


		private void SendMessage(Key sender)
		{
			MonoBehaviour member;
			if (!sender.GetOwner(out member)) return;

			//  If sender is already sending, then this is a recursive call
			if (sender.Sending) {
				Debug.LogException(new Exception($"PartyLine [{name}] blocked a recursive attempt by [{member.name}] to send a message!"));
				return;
			}
			sender.Sending = true;

#if LOGGING
			if (LogMessages) {
				Debug.Log($"[{member.GetType().Name}] on [{member.name}] sent (void) to PartyLine [{name}]");
			}
#endif

			for (int i = _keys.Count - 1; i >= 0; i--) {
				var receiver = (Key)_keys[i];
				if (receiver == sender) continue;

				MessageHandler callback = null;
				if (receiver.GetCallback(out callback)) {
					try {
						callback?.Invoke();
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.LogException(new Exception($"PartyLine [{name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					continue;
				}
			}

			if (_listeners != null) {
				for (int i = _listeners.Count - 1; i >= 0; i--) {
					MessageHandler handler;
					var del = (WeakDelegate<MessageHandler>)_listeners[i];
					if (del.GetCallback(out handler) && handler != null) {
						try {
							handler.Invoke();
						}
						catch (Exception e) {
							Debug.LogException(e);
						}
					} else {
						// Listener was Garbage Collected - Remove handler
						Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>");
						_listeners.RemoveAt(i);
					}
				}
			}

			if (sender != null) {
				sender.Sending = false;
			}
		}


		private void SendMessage<T>(Key<T> sender, T message)
		{
			MonoBehaviour member;
			if (!sender.GetOwner(out member)) return;

			//  If sender is already sending, then this is a recursive call
			if (sender.Sending) {
				Debug.LogException(new Exception($"PartyLine [{name}] blocked a recursive attempt by {member.name} to send a message!"));
				return;
			}
			sender.Sending = true;

#if LOGGING
			if (LogMessages) {
				Debug.Log($"[{member.GetType().Name}] on [{member.name}] sent [{message?.ToString() ?? "null"}] to PartyLine [{name}]");
			}
#endif

			for (int i = _keys.Count - 1; i >= 0; i--) {
				var receiver = (Key<T>)_keys[i];
				if (receiver == sender) continue;

				MessageHandler<T> callback = null;
				if (receiver.GetCallback(out callback)) {
					try {
						callback?.Invoke(message);
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.LogException(new Exception($"PartyLine [{name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					continue;
				}
			}

			if (_listeners != null) {
				for (int i = _listeners.Count - 1; i >= 0; i--) {
					MessageHandler<T> handler;
					var del = (WeakDelegate<MessageHandler<T>>)_listeners[i];
					if (del.GetCallback(out handler) && handler != null) {
						try {
							handler.Invoke(message);
						}
						catch (Exception e) {
							Debug.LogException(e);
						}
					} else {
						// Listener was Garbage Collected - Remove handler
						Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>");
						_listeners.RemoveAt(i);
					}
				}
			}

			if (sender != null) {
				sender.Sending = false;
			}
		}


		#endregion
		//===

	}
}
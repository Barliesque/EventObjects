#if DEVELOPMENT_BUILD || UNITY_EDITOR
#define LOGGING
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;


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
		static private Dictionary<string, PartyLine> _instances;
		[NonSerialized] private PartyLine _inst;

		private PartyLine Instance
		{
			get
			{
				if (_inst) return _inst;

				_instances ??= new Dictionary<string, PartyLine>();

				if (_instances.ContainsKey(this.name))
				{
					_inst = _instances[this.name];
				}
				else
				{
					_instances.Add(this.name, this);
					_inst = this;
				}

				return _inst;
			}
		}


		[SerializeField, TextArea(4, 16)]
		private string Comments;

		private List<KeyBase> _keys;
		private List<IWeakDelegate> _listeners;

		public delegate void MessageHandler();

		public delegate void MessageHandler<in T>(T message);

		/// <summary>
		/// Count of the number of Keys that currently communicate through this PartyLine
		/// </summary>
		public int KeyCount => ((ICollection)_keys)?.Count ?? 0;

		/// <summary>
		/// Count of the number of listeners receiving messages from this PartyLine
		/// </summary>
		public int ListenerCount => ((ICollection)_listeners)?.Count ?? 0;

		/// <summary>
		/// The type of value or object to be sent across this PartyLine
		/// </summary>
		public Type MessageType => _messageType;

		private Type _messageType;

		[SerializeField]
		private bool _logMessages;

		private bool LogMessages => _logMessages && (Application.isEditor || Debug.isDebugBuild);


#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		public void __getOwners(List<MonoBehaviour> owners)
		{
			owners.Clear();
			if (_keys == null) return;

			for (int i = _keys.Count - 1; i >= 0; i--)
			{
				var success = _keys[i].GetOwner(out var owner);
				owners.Insert(0, success ? owner : null);
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

		private bool OnApplicationQuit()
		{
			// ScriptableObject fields remain populated even outside of runtime in the Editor.
			// So, all fields should be returned to initial value.
			_keys = null;
			_listeners = null;
			_messageType = null;
			return true;
		}
#endif


		private void CheckInitialization(Type messageType, bool newKey, Object initializer)
		{
			if (_keys == null)
			{
				_keys = new List<KeyBase>();
				_listeners = new List<IWeakDelegate>();
				_messageType = messageType;
#if LOGGING
				if (LogMessages)
				{
					Debug.Log(
						$"PartyLine [{name}] initialized when [{initializer.GetType().Name}] on [{initializer.name}] called {(newKey ? "CreateKey()" : "AddListener()")}", this);
				}
#endif
			}
			else
			{
				//  Check for matching type
				if (_messageType != messageType)
				{
					if (_messageType == null)
					{
						throw new Exception($"Type mismatch!  PartyLine [{name}] has been initialized for messages with no parameter.");
					}
					else
					{
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
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected without Key.Dispose() being called, an error is thrown.</param>
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
			if (_keys.Contains(key))
			{
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


		public interface IKey<in T>
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

		abstract private class KeyBase
		{
			protected IWeakDelegate Handler;
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
				if (!success && _party != null)
				{
					Debug.LogException(new Exception(
						$"PartyLine [{_party.name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"), _party);
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
		/// <param name="listener">A reference to the script that contains the handler (typically "this").  If the listener is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A listener method to receive messages distributed by this PartyLine</param>
		public void AddListener<T>(MonoBehaviour listener, MessageHandler<T> handler)
		{
			CheckInitialization(typeof(T), false, listener);

			// Check for duplication
			if (handler == null || FindListener(listener, handler) >= 0) return;

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
			if (i >= 0)
			{
				_listeners.RemoveAt(i);
			}
		}


		/// <summary>
		/// Remove all handlers registered to the given listener.
		/// </summary>
		/// <param name="listener"></param>
		public void RemoveListener(MonoBehaviour listener)
		{
			if (_listeners == null) return;
			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				if (!_listeners[i].GetOwner(out var listening)) continue;
				if (listening != listener) continue;
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
			if (i >= 0)
			{
				_listeners.RemoveAt(i);
			}
		}


		private int FindListener(Object listener, MessageHandler handler)
		{
			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				if (_listeners[i].GetOwner(out var listening))
				{
					if (listening != listener) continue;
					// Found the listener
					var del = (WeakDelegate<MessageHandler>)_listeners[i];
					// Handler found?
					if (!del.GetCallback(out var found)) continue;
					if (found == handler) return i;
				}
				else
				{
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>", this);
					_listeners.RemoveAt(i);
				}
			}

			return -1;
		}


		private int FindListener<T>(Object listener, MessageHandler<T> handler)
		{
			// Check for duplication
			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				var del = (WeakDelegate<MessageHandler<T>>)_listeners[i];
				if (del.GetOwner(out var listening))
				{
					if (listening != listener) continue;
					// Found the listener
					if (!del.GetCallback(out var found)) continue;
					// Handler found?
					if (found == handler) return i;
				}
				else
				{
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>", this);
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
			if (_listeners == null) return;
			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				var success = _listeners[i].GetOwner(out var listener);
				listeners.Insert(0, success ? listener : null);
			}
		}
#endif

		#endregion

		//===

		#region MESSAGE SENDING

		private void SendMessage(Key sender)
		{
			if (!sender.GetOwner(out var member)) return;

			//  If sender is already sending, then this is a recursive call
			if (sender.Sending)
			{
				Debug.LogException(new Exception($"PartyLine [{name}] blocked a recursive attempt by [{member.name}] to send a message!"), this);
				return;
			}

			sender.Sending = true;

#if LOGGING
			if (LogMessages)
			{
				Debug.Log($"[{member.GetType().Name}] on [{member.name}] sent (void) to PartyLine [{name}]", this);
			}
#endif

			for (int i = _keys.Count - 1; i >= 0; i--)
			{
				var receiver = (Key)_keys[i];
				if (receiver == sender) continue;

				if (receiver.GetCallback(out var callback))
				{
					try
					{
						callback?.Invoke();
					}
					catch (Exception e)
					{
						Debug.LogException(e, this);
					}
				}
				else
				{
					Debug.LogException(new Exception(
						$"PartyLine [{name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"), this);
				}
			}

			if (_listeners != null)
			{
				for (int i = _listeners.Count - 1; i >= 0; i--)
				{
					var del = (WeakDelegate<MessageHandler>)_listeners[i];
					if (del.GetCallback(out var handler) && handler != null)
					{
						try
						{
							handler.Invoke();
						}
						catch (Exception e)
						{
							Debug.LogException(e, this);
						}
					}
					else
					{
						// Listener was Garbage Collected - Remove handler
						Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>", this);
						_listeners.RemoveAt(i);
					}
				}
			}

			sender.Sending = false;
		}


		private void SendMessage<T>(KeyBase sender, T message)
		{
			if (!sender.GetOwner(out var member)) return;

			//  If sender is already sending, then this is a recursive call
			if (sender.Sending)
			{
				Debug.LogException(new Exception($"PartyLine [{name}] blocked a recursive attempt by {member.name} to send a message!"), this);
				return;
			}

			sender.Sending = true;

#if LOGGING
			if (LogMessages)
			{
				Debug.Log($"[{member.GetType().Name}] on [{member.name}] sent [{message?.ToString() ?? "null"}] to PartyLine [{name}]", this);
			}
#endif

			for (int i = _keys.Count - 1; i >= 0; i--)
			{
				var receiver = (Key<T>)_keys[i];
				if (receiver == sender) continue;

				if (receiver.GetCallback(out MessageHandler<T> callback))
				{
					try
					{
						callback?.Invoke(message);
					}
					catch (Exception e)
					{
						Debug.LogException(e, this);
					}
				}
				else
				{
					Debug.LogException(new Exception(
						$"PartyLine [{name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"), this);
				}
			}

			if (_listeners != null)
			{
				for (int i = _listeners.Count - 1; i >= 0; i--)
				{
					var del = (WeakDelegate<MessageHandler<T>>)_listeners[i];
					if (del.GetCallback(out var handler) && handler != null)
					{
						try
						{
							handler.Invoke(message);
						}
						catch (Exception e)
						{
							Debug.LogException(e, this);
						}
					}
					else
					{
						// Listener was Garbage Collected - Remove handler
						Debug.Log($"<color=yellow>PartyLine [{name}]:  Listener was garbage collected.  Handler removed.</color>", this);
						_listeners.RemoveAt(i);
					}
				}
			}

			sender.Sending = false;
		}

		#endregion

		//===
	}
}
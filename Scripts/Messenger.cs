#if DEVELOPMENT_BUILD || UNITY_EDITOR
#define LOGGING
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barliesque.EventObjects
{
	///
	/// <summary>
	/// Messenger is a specialized Observer implementation that enforces a one-to-many 
	/// relationship where only one entity may send messages, and many entities may
	/// subscribe to receive messages.  The Messenger object lives in the library and
	/// references to it can easily be set up in the inspector, avoiding the need for 
	/// Singletons.  Messengers can be made to send any type of data object--or none
	/// at all--to subscribers.
	/// </summary> 
	/// 
	/// Author(s): 
	/// - 6/19/2018 date		David Barlia
	/// -
	///
	[CreateAssetMenu(fileName = "New Messenger", menuName = "Barliesque/Event Objects/Messenger", order = 0)]
	public class Messenger : ScriptableObject
	{
		static Dictionary<string, Messenger> _instances;
		[NonSerialized] Messenger _inst;
		Messenger Instance
		{
			get {
				if (_inst == null)
				{
					if (_instances == null)
					{
						_instances = new Dictionary<string, Messenger>();
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

		public delegate void MessageHandler();
		public delegate void MessageHandler<T>(T data);
		public delegate R MessageHandler<M, R>(M data);
		public delegate void ResponseHandler<R>(R data);

		private Type _messageType;
		public Type MessageType { get { return Instance._messageType; } }

		private Type _responseType;
		public Type ResponseType { get { return Instance._responseType; } }

		private KeyBase _key;
		public bool HasKey { get { return Instance._key != null; } }

		[SerializeField]
		private bool _logMessages;
		bool LogMessages { get { return Instance._logMessages && (Application.isEditor || Debug.isDebugBuild); } }

		List<IWeakDelegate> _subscribers;
		bool _sending = false;

		//---
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
			_subscribers = null;
			_sending = false;
			_key = null;
			_messageType = null;

			return true;
		}
#endif

		/// <summary>
		/// Ensure subscribers list is initialized for WeakDelegate to a MessageHandler with no message parameter.
		/// </summary>
		/// <param name="newKey">Should be true if initialization is being called because a new key is being created.</param>
		/// <param name="initializer">The entity responsible for initialization (if not already initialized)</param>
		void CheckInitialization(Type messageType, Type responseType, bool newKey, MonoBehaviour initializer)
		{
			if (Instance != this)
			{
				Instance.CheckInitialization(messageType, responseType, newKey, initializer);
				return;
			}

			if (newKey && _key != null) {
				MonoBehaviour owner;
				_key.GetOwner(out owner);
				throw new Exception($"Messenger [{name}] key is already in use by {owner.GetType().Name} on [{owner.gameObject.name}]");
			}
			if (_subscribers == null) {
				_subscribers = new List<IWeakDelegate>();
				_messageType = messageType;
				_responseType = responseType;
#if LOGGING
				if (LogMessages) {
					Debug.Log($"Messenger [{name}] initialized when [{initializer.GetType().Name}] on [{initializer.name}] called {(newKey ? "CreateKey()" : "Subscribe()")}");
				}
#endif
			} else {
				if (_messageType != messageType) {
					if (_messageType == null) {
						throw new Exception($"Type mismatch!  Messenger [{name}] has been initialized for messages with no parameter.");
					} else {
						throw new Exception($"Type mismatch!  Messenger [{name}] has been initialized for messages of type <{_messageType.Name}>");
					}
				}
				if (_responseType != responseType) {
					if (_responseType == null) {
						throw new Exception($"Type mismatch!  Messenger [{name}] has been initialized for subscribers that do not return any response.");
					} else {
						throw new Exception($"Type mismatch!  Messenger [{name}] has been initialized for subscribers that return a response of type <{_responseType.Name}>");
					}
				}
			}
		}


		#endregion
		//---
		#region MESSENGER KEY


		public interface IKey
		{
			/// <summary>
			/// This method must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();

			/// <summary>
			/// Remove all subscribers from the Messenger.
			/// </summary>
			void UnsubscribeAll();

			/// <summary>
			/// Invoke all subscribers to the Messenger.
			/// </summary>
			void SendMessage();
		}

		public interface IKey<T>
		{
			/// <summary>
			/// This method must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();

			/// <summary>
			/// Remove all subscribers from the Messenger.
			/// </summary>
			void UnsubscribeAll();

			/// <summary>
			/// Invoke all subscribers to the Messenger.
			/// </summary>
			/// <param name="data">Data to be passed to all subscribers.</param>
			void SendMessage(T data);

			/// <summary>
			/// Type of data to be sent to subscribers of this Messenger.
			/// </summary>
			Type MessageType { get; }
		}

		public interface IKey<M, R>
		{
			/// <summary>
			/// This method must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();

			/// <summary>
			/// Remove all subscribers from the Messenger.
			/// </summary>
			void UnsubscribeAll();

			/// <summary>
			/// Invoke all subscribers to the Messenger.
			/// </summary>
			/// <param name="data">Data to be passed to all subscribers.</param>
			/// <param name="resonseHandler">A method to handle returned data from subscribers.</param>
			void SendMessage(M data, ResponseHandler<R> resonseHandler);

			/// <summary>
			/// Type of data to be sent to subscribers of this Messenger.
			/// </summary>
			Type MessageType { get; }
		}



		abstract class KeyBase
		{
			protected WeakReference<MonoBehaviour> _owner;
			protected Messenger _messenger;

			public void Dispose()
			{
				_messenger?.DisposeKey();
				_messenger = null;
			}

			public void UnsubscribeAll()
			{
				_messenger?.UnsubscribeAll();
			}

			public bool GetOwner(out MonoBehaviour owner)
			{
				bool success = _owner.TryGetTarget(out owner) && (owner != null);
				if (!success && _messenger != null) {
					Debug.LogException(new Exception($"Messenger [{_messenger.name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					Dispose();
				}
				return success;
			}
		}

		// The Key class should never be instantiated outside this class.
		// See interface for documentation.
		private class Key : KeyBase, IKey
		{

			public Key(MonoBehaviour owner, Messenger messenger)
			{
				_messenger = messenger;
				_owner = new WeakReference<MonoBehaviour>(owner);
			}

			public void SendMessage()
			{
				// Ensure key is still valid
				MonoBehaviour owner;
				GetOwner(out owner);

				_messenger.SendMessage();
			}
		}


		// The Key class should never be instantiated outside this class.
		// See interface for documentation.
		private class Key<M> : KeyBase, IKey<M>
		{
			public Type MessageType => typeof(M);

			public Key(MonoBehaviour owner, Messenger messenger)
			{
				_messenger = messenger;
				_owner = new WeakReference<MonoBehaviour>(owner);
			}


			public void SendMessage(M data)
			{
				// Ensure key is still valid
				MonoBehaviour owner;
				GetOwner(out owner);

				_messenger.SendMessage(data);
			}
		}


		// The Key class should never be instantiated outside this class.
		// See interface for documentation.
		private class Key<M, R> : KeyBase, IKey<M, R>
		{
			public Type MessageType => typeof(M);

			public Key(MonoBehaviour owner, Messenger messenger)
			{
				_messenger = messenger;
				_owner = new WeakReference<MonoBehaviour>(owner);
			}


			public void SendMessage(M data, ResponseHandler<R> responseHandler)
			{
				// Ensure key is still valid
				MonoBehaviour owner;
				GetOwner(out owner);

				_messenger.SendMessage(data, responseHandler);
			}
		}


		/// <summary>
		/// The key is required to send messages to subscribers of the Messenger.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <returns>Returns a Key that may be used for sending messages.</returns>
		public IKey CreateKey(MonoBehaviour owner)
		{
			if (Instance != this)
			{
				return Instance.CreateKey(owner);
			}

			CheckInitialization(null, null, true, owner);
			_key = new Key(owner, this);
			return (Key)_key;
		}


		/// <summary>
		/// The key is required to send messages to subscribers of the Messenger.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="T">Type of message to be delivered by this Messenger.</typeparam>
		/// <returns>Returns a Key that may be used for sending messages.</returns>
		public IKey<T> CreateKey<T>(MonoBehaviour owner)
		{
			if (Instance != this)
			{
				return Instance.CreateKey<T>(owner);
			}

			CheckInitialization(typeof(T), null, true, owner);
			_key = new Key<T>(owner, this);
			return (Key<T>)_key;
		}


		/// <summary>
		/// The key is required to send messages to subscribers of the Messenger.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="T">Type of message to be delivered by this Messenger.</typeparam>
		/// <returns>Returns a Key that may be used for sending messages.</returns>
		public IKey<M,R> CreateKey<M,R>(MonoBehaviour owner)
		{
			if (Instance != this)
			{
				return Instance.CreateKey<M, R>(owner);
			}

			CheckInitialization(typeof(M), typeof(R), true, owner);
			_key = new Key<M,R>(owner, this);
			return (Key<M,R>)_key;
		}


		/// <summary>
		/// Only accessible via Messenger.Key
		/// </summary>
		void DisposeKey()
		{
			_key = null;
		}



#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		public bool __getOwner(out MonoBehaviour owner)
		{
			return Instance.GetOwner(out owner);
		}
#endif

		bool GetOwner(out MonoBehaviour owner)
		{
			owner = null;
			return Instance._key?.GetOwner(out owner) ?? false;
		}



		#endregion
		//---
		#region SUBSCRIBERS


		/// <summary>
		/// Count of subscribers to this Messenger.
		/// </summary>
		public int SubscriberCount {
			get {
				if (Instance != this) return Instance.SubscriberCount;
				if (_subscribers == null) return 0;
				return ((ICollection)_subscribers).Count;
			}
		}


		/// <summary>
		/// Subscribe to receive messages from this Messenger.
		/// </summary>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").  If the subscriber is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A method to be invoked by the Messenger.</param>
		public void Subscribe(MonoBehaviour subscriber, MessageHandler handler)
		{
			if (Instance != this)
			{
				Instance.Subscribe(subscriber, handler);
				return;
			}

			CheckInitialization(null, null, false, subscriber);

			// Check for duplication
			if (handler == null || FindSubscriber(subscriber, handler) >= 0) return;

			// Add new subscriber
			_subscribers.Add(new WeakDelegate<MessageHandler>(subscriber, handler));
		}


		/// <summary>
		/// Subscribe to receive messages from this Messenger.
		/// </summary>
		/// <typeparam name="T">Type of message sent by this Messenger.</typeparam>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").  If the subscriber is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A method to be invoked by the Messenger.</param>
		public void Subscribe<T>(MonoBehaviour subscriber, MessageHandler<T> handler)
		{
			if (Instance != this)
			{
				Instance.Subscribe(subscriber, handler);
				return;
			}

			CheckInitialization(typeof(T), null, false, subscriber);

			// Check for duplication
			if (handler == null || FindSubscriber<T>(subscriber, handler) >= 0) return;

			// Add new subscriber
			_subscribers.Add(new WeakDelegate<MessageHandler<T>>(subscriber, handler));
		}


		/// <summary>
		/// Subscribe to receive messages from this Messenger.
		/// </summary>
		/// <typeparam name="M">Type of message sent by this Messenger.</typeparam>
		/// <typeparam name="R">Type of response to be returned by the subscriber.</typeparam>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").  If the subscriber is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A method to be invoked by the Messenger.</param>
		public void Subscribe<M,R>(MonoBehaviour subscriber, MessageHandler<M,R> handler)
		{
			if (Instance != this)
			{
				Instance.Subscribe(subscriber, handler);
				return;
			}

			CheckInitialization(typeof(M), typeof(R), false, subscriber);

			// Check for duplication
			if (handler == null || FindSubscriber<M,R>(subscriber, handler) >= 0) return;

			// Add new subscriber
			_subscribers.Add(new WeakDelegate<MessageHandler<M,R>>(subscriber, handler));
		}


		/// <summary>
		/// Unsubscribe to no longer receive messages from this Messenger.
		/// </summary>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").</param>
		/// <param name="handler">A previously subscribed message handler method.</param>
		public void Unsubscribe(MonoBehaviour subscriber, MessageHandler handler)
		{
			if (Instance != this)
			{
				Instance.Unsubscribe(subscriber, handler);
				return;
			}

			if (_subscribers == null) return;
			int i = FindSubscriber(subscriber, handler);
			if (i >= 0) {
				_subscribers.RemoveAt(i);
			}
		}


		/// <summary>
		/// Unsubscribe to no longer receive messages from this Messenger.
		/// </summary>
		/// <typeparam name="T">Type of message sent by this Messenger.</typeparam>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").</param>
		/// <param name="handler">A previously subscribed message handler method.</param>
		public void Unsubscribe<T>(MonoBehaviour subscriber, MessageHandler<T> handler)
		{
			if (Instance != this)
			{
				Instance.Unsubscribe(subscriber, handler);
				return;
			}

			if (_subscribers == null) return;
			int i = FindSubscriber(subscriber, handler);
			if (i >= 0) {
				_subscribers.RemoveAt(i);
			}
		}


		/// <summary>
		/// Unsubscribe to no longer receive messages from this Messenger.
		/// </summary>
		/// <typeparam name="M">Type of message sent by this Messenger.</typeparam>
		/// <typeparam name="R">Type of response returned by the subscriber.</typeparam>
		/// <param name="subscriber">A reference to the script that contains the handler (typically "this").</param>
		/// <param name="handler">A previously subscribed message handler method.</param>
		public void Unsubscribe<M,R>(MonoBehaviour subscriber, MessageHandler<M,R> handler)
		{
			if (Instance != this)
			{
				Instance.Unsubscribe(subscriber, handler);
				return;
			}

			if (_subscribers == null) return;
			int i = FindSubscriber(subscriber, handler);
			if (i >= 0) {
				_subscribers.RemoveAt(i);
			}
		}


		/// <summary>
		/// Only accessible via Messenger.Key
		/// </summary>
		void UnsubscribeAll()
		{
			if (_subscribers == null) return;
			var subscribers = (List<IWeakDelegate>)_subscribers;
			subscribers.Clear();
		}


		int FindSubscriber(MonoBehaviour subscriber, MessageHandler handler)
		{
			for (int i = _subscribers.Count - 1; i >= 0; i--) {
				MonoBehaviour subscribed = null;
				var weak = (WeakDelegate<MessageHandler>)_subscribers[i];
				if (weak.GetOwner(out subscribed)) {
					if (subscribed != subscriber) continue;
					// Found the listener
					MessageHandler found;
					if (weak.GetCallback(out found)) {
						// Handler found?
						if (found == handler) return i;
					}
				} else {
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}
			return -1;
		}


		int FindSubscriber<T>(MonoBehaviour subscriber, MessageHandler<T> handler)
		{
			for (int i = _subscribers.Count - 1; i >= 0; i--) {
				MonoBehaviour subscribed;
				var weak = (WeakDelegate<MessageHandler<T>>)_subscribers[i];
				if (weak.GetOwner(out subscribed)) {
					if (subscribed != subscriber) continue;
					// Found the listener
					MessageHandler<T> found;
					if (weak.GetCallback(out found)) {
						// Handler found?
						if (found == handler) return i;
					}
				} else {
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}
			return -1;
		}


		int FindSubscriber<M,R>(MonoBehaviour subscriber, MessageHandler<M,R> handler)
		{
			for (int i = _subscribers.Count - 1; i >= 0; i--) {
				MonoBehaviour subscribed;
				var weak = (WeakDelegate<MessageHandler<M,R>>)_subscribers[i];
				if (weak.GetOwner(out subscribed)) {
					if (subscribed != subscriber) continue;
					// Found the listener
					MessageHandler<M,R> found;
					if (weak.GetCallback(out found)) {
						// Handler found?
						if (found == handler) return i;
					}
				} else {
					// Listener was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}
			return -1;
		}


#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		public void __getSubscribers(List<MonoBehaviour> subscribers)
		{
			subscribers.Clear();
			if (Instance._subscribers != null) {
				for (int i = Instance._subscribers.Count - 1; i >= 0; i--) {
					MonoBehaviour subscriber;
					var success = Instance._subscribers[i].GetOwner(out subscriber);
					subscribers.Insert(0, success ? subscriber : null);
				}
			}
		}
#endif


		#endregion
		//---
		#region MESSAGE DELIVERY


		void SendMessage()
		{
			if (_sending) {
				throw new Exception($"Recursive call to SendMessage() on Messenger [{name}] aborted!");
			}
			_sending = true;
#if LOGGING
			if (LogMessages) {
				MonoBehaviour owner;
				if (!GetOwner(out owner)) return;
				Debug.Log($"[{owner.GetType().Name}] on [{owner.name}] sent (void) to {_subscribers.Count} subscriber(s) of Messenger [{name}]");
			}
#endif
			for (int i = _subscribers.Count - 1; i >= 0; i--) {

				MessageHandler handler;
				var weak = (WeakDelegate<MessageHandler>)_subscribers[i];
				if (weak.GetCallback(out handler) && handler != null) {
					try {
						handler.Invoke();
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}

			_sending = false;
		}


		/// <summary>
		/// Strictly called via Messenger.Key
		/// </summary>
		/// <param name="message">An object or value to be passed to subscribers</param>
		void SendMessage<T>(T message)
		{
			if (_sending) {
				throw new Exception($"Recursive call to SendMessage() on Messenger \"{name}\" aborted!");
			}
			_sending = true;
#if LOGGING
			if (LogMessages) {
				MonoBehaviour owner;
				if (!GetOwner(out owner)) return;
				Debug.Log($"[{owner.GetType().Name}] on [{owner.name}] sent [{message?.ToString() ?? "null"}] to {_subscribers.Count} subscriber(s) of Messenger [{name}]");
			}
#endif
			for (int i = _subscribers.Count - 1; i >= 0; i--) {

				MessageHandler<T> handler;
				var weak = (WeakDelegate<MessageHandler<T>>)_subscribers[i];
				if (weak.GetCallback(out handler) && handler != null) {
					try {
						handler.Invoke(message);
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}

			_sending = false;
		}


		/// <summary>
		/// Strictly called via Messenger.Key
		/// </summary>
		/// <param name="message">An object or value to be passed to subscribers</param>
		void SendMessage<M, R>(M message, ResponseHandler<R> responseHandler)
		{
			if (_sending) {
				throw new Exception($"Recursive call to SendMessage() on Messenger \"{name}\" aborted!");
			}
			_sending = true;
#if LOGGING
			if (LogMessages) {
				MonoBehaviour owner;
				if (!GetOwner(out owner)) return;
				Debug.Log($"[{owner.GetType().Name}] on [{owner.name}] sent [{message?.ToString() ?? "null"}] to {_subscribers.Count} subscriber(s) of Messenger [{name}]");
			}
#endif
			for (int i = _subscribers.Count - 1; i >= 0; i--) {
				MessageHandler<M, R> handler;
				var weak = (WeakDelegate<MessageHandler<M, R>>)_subscribers[i];
				if (weak.GetCallback(out handler) && handler != null) {
					var response = default(R);
					try {
						response = handler.Invoke(message);
						responseHandler?.Invoke(response);
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.Log($"<color=yellow>Messenger [{name}]:  Subscriber was garbage collected.  Handler removed.</color>");
					_subscribers.RemoveAt(i);
				}
			}

			_sending = false;
		}


		#endregion

	}

}
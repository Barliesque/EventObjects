#if DEVELOPMENT_BUILD || UNITY_EDITOR
#define LOGGING
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects
{

	///
	/// <summary>
	/// Mailbox is a specialized Observer implementation that enforces a many-to-one 
	/// relationship where many entities may send messages, but only one entity may 
	/// receive those messages.  The Mailbox can be made to store messages until the
	/// owner is ready to process them, or it can simply receive them immediately.
	/// Optionally, the Mailbox owner may send response data back to the sender 
	/// after processing a message.
	/// </summary> 
	/// 
	/// Author(s): 
	/// - 6/27/2018		David Barlia
	/// -
	///
	[CreateAssetMenu(fileName = "New Mailbox", menuName = "Barliesque/Event Objects/Mailbox", order = 1)]
	public class Mailbox : ScriptableObject
	{
		static Dictionary<string, Mailbox> _instances;
		[NonSerialized] Mailbox _inst;
		Mailbox Instance { get {
				if (_inst == null)
				{
					if (_instances == null)
					{
						_instances = new Dictionary<string, Mailbox>();
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

		private Key _key;  // Key<I> or Key<I,O>

		public delegate void MessageHandler<I>(I message);
		public delegate O MessageHandler<I, O>(I message);

		public delegate void ResponseHandler<O>(O response);
		public delegate void ResponseHandler();

		[SerializeField]
		private bool _logMessages = false;
		bool LogMessages { get { return Instance._logMessages && (Application.isEditor || Debug.isDebugBuild); } }

		/// <summary>
		/// Has a Key been created to handle messages in this Mailbox?
		/// </summary>
		public bool HasKey { get { return Instance._key != null; } }

		/// <summary>
		/// If true, the MailHandler routine will only be called when ReceiveMail() is called.  If false, the MailHandler is called immediately and no mail accumulates.  
		/// If mail has accumulated when HoldMail is set to false, then the MailHandler will immediately be called repeatedly until the Mailbox is empty.
		/// </summary>
		public bool HoldsMail { get { return Instance._holdMail; } }
		private bool _holdMail;

		/// <summary>
		/// Messages sent to a Mailbox that holds mail may not be processed immediately.  If this option is true, when the sender of a message is destroyed, its message is discarded.
		/// </summary>
		public bool MessageDiesWithSender { get { return Instance._messageDiesWithSender; } }
		private bool _messageDiesWithSender = true;

		/// <summary>
		/// Type of value to be sent to the Mailbox.
		/// </summary>
		public Type MessageType { get { return Instance._messageType; } }
		private Type _messageType;

		/// <summary>
		/// Type of value to be returned to the response handler.
		/// </summary>
		public Type ResponseType { get { return Instance._responseType; } }
		private Type _responseType;

		/// <summary>
		/// The total number of messages currently held in the Mailbox.  If HoldMail is false, mail is processed immediately, and so this will always be zero.
		/// </summary>
		public int MessageCount {
			get {
				return Instance._messages?.Count ?? 0;
			}
		}
		List<IMessage> _messages;

		/// <summary>
		/// The maximum number of messages this Mailbox will hold before rejecting incoming mail.
		/// </summary>
		public int MaxCapacity { get { return Instance._maxCapacity; } }
		int _maxCapacity = 1024;


#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		public bool __getOwner(out MonoBehaviour owner)
		{
			var key = (Key)Instance._key;
			owner = null;
			return key?.GetOwner(out owner) ?? false;
		}
#endif


		//---
		#region INITIALIZATION


		private void OnEnable()
		{
#if UNITY_EDITOR
			Application.wantsToQuit -= OnApplicationQuit;
			Application.wantsToQuit += OnApplicationQuit;
#endif
		}

#if UNITY_EDITOR
		bool OnApplicationQuit()
		{
			// ScriptableObject fields remain populated even outside of runtime in the Editor.
			// So, all fields should be returned to initial value.
			_key = null;
			_messageType = null;
			_responseType = null;
			_messages = null;
			return true;
		}
#endif


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
				throw new Exception($"Mailbox [{name}] key is already in use by {owner.GetType().Name} on [{owner.gameObject.name}]");
			}

			if (_messages == null) {
				_messages = new List<IMessage>();
				_messageType = messageType;
				_responseType = responseType;

#if LOGGING
				if (LogMessages) {
					Debug.Log($"Mailbox [{name}] initialized when [{initializer.GetType().Name}] on [{initializer.name}] called {(newKey ? "CreateKey()" : "SendMessage()")}");
				}
#endif

			} else {
				if (newKey) {
					// New key? Then we were initialized via SendMessage()
					if (messageType != _messageType) {
						throw new Exception($"Type mismatch!  {MessageCount} message(s) of type <{_messageType.Name}> already sent to Mailbox [{name}]");
					}
					if (responseType != _responseType) {
						throw new Exception($"Type mismatch!  {MessageCount} message(s) already sent to Mailbox [{name}] specified a <{_responseType?.Name ?? "void"}> response handler.");
					}
				} else {
					if (_responseType != null && responseType == null) {
						throw new Exception($"Type mismatch!  Mailbox[{name}] expects messages to be sent with:  SendMail<{_messageType.Name},{_responseType.Name}>()  Try specifying generic types explicitly.");
					}

					// Sending a message?
					if (messageType != _messageType) {
						throw new Exception($"Type mismatch!  Mailbox [{name}] expects messages of type <{_messageType.Name}> but {initializer.GetType().Name} is sending <{messageType.Name}>");
					}
					if (responseType != _responseType) {
						if (_responseType == null) {
							throw new Exception($"Type mismatch!  Mailbox [{name}] responds without a parameter, but your response handler expects <{responseType.Name}>");
						} else {
							throw new Exception($"Type mismatch!  Mailbox [{name}] responds with type <{_responseType.Name}> but your response handler expects <{responseType?.Name ?? "void"}>");
						}
					}
				}
			}
		}


		#endregion
		//---
		#region MAILBOX KEY


		/// <summary>
		/// The key gives access to messages received through the Mailbox and provides options to control how it functions.  Only one Mailbox key may exist at a time.
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		public interface IKey<I>
		{
			/// <summary>
			/// If true, the MailHandler routine will only be called when ReceiveMail() is called.  If false, the MailHandler is called immediately and no mail accumulates.  
			/// If mail has accumulated when HoldMail is set to false, then the MailHandler will immediately be called repeatedly until the Mailbox is empty.
			/// </summary>
			bool HoldMail { get; set; }

			/// <summary>
			/// Messages sent to a Mailbox that holds mail may not be processed immediately.  If this option is true, when the sender of a message is destroyed, its message is also trashed.
			/// </summary>
			bool MessageDiesWithSender { get; set; }

			/// <summary>
			/// If mail has accumulated in the Mailbox, then calling this method will invoke the MailHandler with each accumulated Mail object in the order they were sent.  See: Mailbox.HoldMail
			/// </summary>
			/// <see cref="HoldMail"/>
			void GetMail();

			/// <summary>
			/// Moves the currently processed message to the back of the queue, to be processed the next time GetMail() is called.  This method may only be called from within the response handler, and only if the Mailbox holds messages.  See: Mailbox.HoldMail
			/// </summary>
			void PostponeMessage();

			/// <summary>
			/// Sets aside all remaining messages (excluding the currently processed message) until the next time GetMail() is called  This method may only be called from within the response handler, and only if the Mailbox holds messages.  See: Mailbox.HoldMail
			/// </summary>
			void PostponeRemaining();

			/// <summary>
			/// The total number of messages currently held in the Mailbox.  If HoldMail is false, mail is processed immediately, and so this will always be zero.
			/// </summary>
			int MessageCount { get; }

			/// <summary>
			/// The maximum number of messages the Mailbox will hold before rejecting incoming mail.
			/// </summary>
			int MaxCapacity { get; set; }

			/// <summary>
			/// This method must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();
		}

		/// <summary>
		/// The key gives access to messages received through the Mailbox and provides options to control how it functions.  Only one Mailbox key may exist at a time.
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <typeparam name="O">Type of value to be returned to the response handler.</typeparam>
		public interface IKey<I, O> : IKey<I>
		{ }


		// The Key class should never be instantiated outside this class.
		// See interface for documentation.
		abstract class Key
		{
			protected IWeakDelegate _handler;
			public bool Receiving;
			public bool PostponedThis;
			public bool PostponedRest;

			public Mailbox mailbox { get; protected set; }

			public void Dispose()
			{
				mailbox.DisposeKey();
				_handler.Dispose();
				mailbox = null;
			}

			public int MessageCount => mailbox.MessageCount;

			public bool MessageDiesWithSender {
				get { return mailbox._messageDiesWithSender; }
				set { mailbox._messageDiesWithSender = value; }
			}

			public int MaxCapacity {
				get { return mailbox._maxCapacity; }
				set { mailbox._maxCapacity = value; }
			}

			public void PostponeMessage()
			{
				if (!Receiving) throw new Exception("PostponeMessage() may only be called from within the message response handler specified with Mailbox.CreatKey()");
				if (!mailbox._holdMail) throw new Exception("PostponeMessage() may only be called if the Mailbox holds messages!  See: Mailbox.HoldMail");
				PostponedThis = true;
			}

			public void PostponeRemaining()
			{
				if (!Receiving) throw new Exception("PostponeMessage() may only be called from within the message response handler specified with Mailbox.CreatKey()");
				if (!mailbox._holdMail) throw new Exception("PostponeMessage() may only be called if the Mailbox holds messages!  See: Mailbox.HoldMail");
				PostponedRest = true;
			}

			internal bool GetOwner(out MonoBehaviour owner)
			{
				bool success = _handler.GetOwner(out owner);
				if (!success && mailbox != null) {
					Debug.LogException(new Exception($"Mailbox [{mailbox.name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					Dispose();
				}
				return success;
			}
		}


		// The Key class should never be instantiated outside this class.
		// See interface for documentation.
		private class Key<I> : Key, IKey<I>
		{
			readonly public Comparison<I> _prioritizer;

			public Key(Mailbox mailbox, MonoBehaviour owner, MessageHandler<I> handler, Comparison<I> prioritizer = null)
			{
				this.mailbox = mailbox;
				_handler = new WeakDelegate<MessageHandler<I>>(owner, handler);
				_prioritizer = prioritizer;
			}

			public bool HoldMail {
				get {
					return mailbox._holdMail;
				}
				set {
					mailbox._holdMail = value;
					if (!value) {
						mailbox.GetMail<I>();
					}
				}
			}

			public void GetMail()
			{
				mailbox.GetMail<I>();
			}

			public int Compare(IMessage x, IMessage y)
			{
				return _prioritizer(((Message<I>)x).Content, ((Message<I>)y).Content);
			}

			internal void ProcessMessage(Message<I> mail)
			{
				// Send message to key holder
				MessageHandler<I> send;
				var handler = (WeakDelegate<MessageHandler<I>>)_handler;
				if (handler.GetCallback(out send)) {
					try {
						send.Invoke(mail.Content);
#if LOGGING
						if (mailbox.LogMessages) {
							MonoBehaviour sender;
							if (mail.responseHandler.GetOwner(out sender)) {
								Debug.Log($"Mailbox [{mailbox.name}] processed message {mail.Content.ToString()} from [{sender.GetType().Name}] on [{sender.name}]");
							} else {
								Debug.Log($"Mailbox [{mailbox.name}] processed message {mail.Content.ToString()} from a destroyed sender.");
							}
						}
#endif
					}
					catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					Debug.LogException(new Exception($"Mailbox [{mailbox.name}] key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
					Dispose();
				}
			}
		}


		private class Key<I, O> : Key, IKey<I, O>
		{
			readonly public Comparison<I> _prioritizer;

			public Key(Mailbox mailbox, MonoBehaviour owner, MessageHandler<I, O> handler, Comparison<I> prioritizer = null)
			{
				this.mailbox = mailbox;
				_handler = new WeakDelegate<MessageHandler<I, O>>(owner, handler);
				_prioritizer = prioritizer;
			}

			public bool HoldMail {
				get {
					return mailbox._holdMail;
				}
				set {
					mailbox._holdMail = value;
					if (!value) {
						mailbox.GetMail<I, O>();
					}
				}
			}

			public void GetMail()
			{
				mailbox.GetMail<I, O>();
			}

			public int Compare(IMessage x, IMessage y)
			{
				return _prioritizer(((Message<I,O>)x).Content, ((Message<I,O>)y).Content);
			}

			internal void ProcessMessage(Message<I, O> mail, ResponseHandler<O> respond)
			{
				// Get the message handler
				MessageHandler<I, O> send;
				var handler = (WeakDelegate<MessageHandler<I, O>>)_handler;
				if (handler.GetCallback(out send)) {
					try {
						// Send message to key holder
						O response = send.Invoke(mail.Content);
#if LOGGING
						if (mailbox.LogMessages) {
							MonoBehaviour sender;
							if (mail.responseHandler.GetOwner(out sender)) {
								Debug.Log($"Mailbox [{mailbox.name}] processed message [{mail.Content?.ToString() ?? "null"}] from [{sender.GetType().Name}] on [{sender.name}]");
							} else {
								Debug.Log($"Mailbox [{mailbox.name}] processed message [{mail.Content?.ToString() ?? "null"}] from a destroyed sender.");
							}
						}
#endif
						try {
							// Send response
							if (respond != null && !PostponedThis) {
#if LOGGING
								if (mailbox.LogMessages) {
									Debug.Log($"Mailbox [{mailbox.name}] responded with [{response?.ToString() ?? "null"}]");
								}
#endif
								respond.Invoke(response);
							}
						}
						catch (Exception e) {
							// Catch error on respond()
							Debug.LogException(e);
						}
					}
					catch (Exception e) {
						// Catch error on send()
						Debug.LogException(e);
					}
				} else {
					throw new Exception($"Mailbox [{mailbox.name}] key was not disposed correctly!  Be sure to call Key.Dispose() from the key owner.");
				}
			}
		}


		/// <summary>
		/// The key is required to get messages from the Mailbox.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected and the Key.Dispose() was not called, an error is thrown.</param>
		/// <param name="handler">A function that will process the incoming messages.</param>
		/// <param name="holdMail">If true, Key.GetMail() must be called to process incoming messages.</param>
		/// <returns>Returns a Key object to be used for processing messages received by the Mailbox.</returns>
		public IKey<I> CreateKey<I>(MonoBehaviour owner, MessageHandler<I> handler, bool holdMail = false)
		{
			if (handler == null) throw new Exception("Mailbox handler can not be null!");

			if (Instance != this) return Instance.CreateKey(owner, handler, holdMail);

			CheckInitialization(typeof(I), null, true, owner);

			_key = new Key<I>(this, owner, handler);
			_holdMail = holdMail;
			_messageDiesWithSender = true;

			// If messages have been queued up and the mailbox doesn't hold messages, then they must be processed now.
			if (_messages.Count > 0 && !holdMail) {
				GetMail<I>();
			}

			return (Key<I>)_key;
		}


		/// <summary>
		/// The key is required to get messages from the Mailbox.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected and the Key.Dispose() was not called, an error is thrown.</param>
		/// <param name="handler">A function that will process the incoming messages.</param>
		/// <param name="prioritizer">A comparison method to ensure messages are handled in order of priority.  By default, this Mailbox will hold mail.</param>
		/// <param name="holdMail">If true, Key.GetMail() must be called to process incoming messages.  Sorting of messages with a prioritizer only happens when this is true.</param>
		/// <returns>Returns a Key object to be used for processing messages received by the Mailbox.</returns>
		public IKey<I> CreateKey<I>(MonoBehaviour owner, MessageHandler<I> handler, Comparison<I> prioritizer, bool holdMail = true)
		{
			if (handler == null) throw new Exception("Mailbox handler can not be null!");

			if (Instance != this) return Instance.CreateKey(owner, handler, prioritizer, holdMail);

			CheckInitialization(typeof(I), null, true, owner);

			_key = new Key<I>(this, owner, handler, prioritizer);
			_holdMail = holdMail;
			_messageDiesWithSender = true;

			// If messages have been queued up and the mailbox doesn't hold messages, then they must be processed now.
			if (_messages.Count > 0 && !holdMail) {
				GetMail<I>();
			}

			return (Key<I>)_key;
		}


		/// <summary>
		/// The key is required to get messages from the Mailbox.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <typeparam name="O">Type of value to be returned to the response handler.</typeparam>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected and the Key.Dispose() was not called, an error is thrown.</param>
		/// <param name="handler">A function that will process the incoming messages.</param>
		/// <param name="holdMail">If true, Key.GetMail() must be called to process incoming messages.</param>
		/// <returns>Returns a Key object to be used for processing messages received by the Mailbox.</returns>
		public IKey<I, O> CreateKey<I, O>(MonoBehaviour owner, MessageHandler<I, O> handler, bool holdMail = false)
		{
			if (handler == null) throw new Exception("Mailbox handler can not be null!");

			if (Instance != this) return Instance.CreateKey(owner, handler, holdMail);

			CheckInitialization(typeof(I), typeof(O), true, owner);

			_key = new Key<I, O>(this, owner, handler);
			_holdMail = holdMail;
			_messageDiesWithSender = true;

			// If messages have been queued up and the mailbox doesn't hold messages, then they must be processed now.
			if (_messages.Count > 0 && !holdMail) {
				GetMail<I, O>();
			}

			return (Key<I, O>)_key;
		}


		/// <summary>
		/// The key is required to get messages from the Mailbox.  Only one key may exist at any one time.  See: Key.Dispose()
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <typeparam name="O">Type of value to be returned to the response handler.</typeparam>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected and the Key.Dispose() was not called, an error is thrown.</param>
		/// <param name="handler">A function that will process the incoming messages.</param>
		/// <param name="prioritizer">A comparison method to ensure messages are handled in order of priority.  By default, this Mailbox will hold mail.</param>
		/// <param name="holdMail">If true, Key.GetMail() must be called to process incoming messages.  Sorting of messages with a prioritizer only happens when this is true.</param>
		/// <returns>Returns a Key object to be used for processing messages received by the Mailbox.</returns>
		public IKey<I, O> CreateKey<I, O>(MonoBehaviour owner, MessageHandler<I, O> handler, Comparison<I> prioritizer, bool holdMail = true)
		{
			if (handler == null) throw new Exception("Mailbox handler can not be null!");

			if (Instance != this) return Instance.CreateKey(owner, handler, prioritizer, holdMail);

			CheckInitialization(typeof(I), typeof(O), true, owner);

			_key = new Key<I, O>(this, owner, handler, prioritizer);
			_holdMail = holdMail;
			_messageDiesWithSender = true;

			// If messages have been queued up and the mailbox doesn't hold messages, then they must be processed now.
			if (_messages.Count > 0 && !holdMail) {
				GetMail<I, O>();
			}

			return (Key<I, O>)_key;
		}


		/// <summary>
		/// Only accessible via the Key
		/// </summary>
		void DisposeKey()
		{
			_key = null;
		}



		#endregion
		//---
		#region SENDING AND RECEIVING


		/// <summary>
		/// Send a message to the Mailbox.
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <param name="sender">A reference to the script that is sending the message (typically "this").</param>
		/// <param name="content">The data to be sent to the Mailbox.</param>
		/// <param name="responseHandler">An optional handler to be called after the Mailbox has processed the message.</param>
		public void SendMail<I>(MonoBehaviour sender, I content, ResponseHandler responseHandler = null)
		{
			if (Instance != this)
			{
				Instance.SendMail(sender, content, responseHandler);
				return;
			}

			//  Handle recursion
			bool receiving = _key?.Receiving ?? false;
			if (receiving && !_holdMail) {
				Debug.LogException(new Exception($"Mailbox [{name}] blocked a recursive attempt to send a message!  Response handlers may only send a message if Mailbox.HoldsMail is true."));
				return;
			}

			// Ensure matching initialization
			CheckInitialization(typeof(I), null, false, sender);

			if (_messages.Count >= _maxCapacity) {
				Debug.Log($"<color=yellow>Mailbox [{name}] is full and cannot accept any more messages!</color>");
				return;
			}
#if LOGGING
			if (LogMessages) {
				Debug.Log($"[{sender.GetType().Name}] on [{sender.name}] sent {content.ToString()} to Mailbox [{name}]");
			}
#endif
			_messages.Add(new Message<I>(sender, content, responseHandler));

			if (_key != null) {
				if (!_holdMail) {
					GetMail<I>();
				} else {
#if UNITY_EDITOR  //TODO  Can this be moved into an Editor-only frame-based event handler?
					// Check for improperly disposed Key -- only in the Editor, for the sake of timely feedback
					MonoBehaviour check;
					_key.GetOwner(out check);
#endif
				}
			}
		}


		/// <summary>
		/// Send a message to the Mailbox.
		/// </summary>
		/// <typeparam name="I">Type of value to be sent to the Mailbox.</typeparam>
		/// <typeparam name="O">Type of value to be returned to the response handler.</typeparam>
		/// <param name="sender">A reference to the script that is sending the message (typically "this").</param>
		/// <param name="content">The data to be sent to the Mailbox.</param>
		/// <param name="responseHandler">An optional handler to be called after the Mailbox has processed the message.</param>
		public void SendMail<I, O>(MonoBehaviour sender, I content, ResponseHandler<O> responseHandler)
		{
			if (Instance != this)
			{
				Instance.SendMail(sender, content, responseHandler);
				return;
			}

			//  Handle recursion
			bool receiving = _key?.Receiving ?? false;
			if (receiving && !_holdMail) {
				Debug.LogException(new Exception($"Mailbox [{name}] blocked a recursive attempt to send a message.  Response handlers may only send a message if Mailbox.HoldsMail is true."));
				return;
			}

			// Ensure matching initialization
			CheckInitialization(typeof(I), typeof(O), false, sender);

			if (_messages.Count >= _maxCapacity) {
				Debug.Log($"<color=yellow>Mailbox [{name}] is full and cannot accept any more messages!</color>");
				return;
			}
#if LOGGING
			if (LogMessages) {
				Debug.Log($"[{sender.GetType().Name}] on [{sender.name}] sent {content.ToString()} to Mailbox [{name}]");
			}
#endif
			_messages.Add(new Message<I, O>(sender, content, responseHandler));

			if (_key != null) {
				if (!_holdMail) {
					GetMail<I, O>();
				} else {
#if UNITY_EDITOR  //TODO  Can this be moved into an Editor-only frame-based event handler?
					// Check for improperly disposed Key -- only in the Editor, for the sake of timely feedback
					MonoBehaviour check;
					_key.GetOwner(out check);
#endif
				}
			}
		}


		private void GetMail<I>()
		{
			var key = (Key<I>)_key;

			// If messages need to be prioritized, then sort them now
			if (key._prioritizer != null) {
				_messages.Sort(key.Compare);
			}

			key.Receiving = true;
			key.PostponedThis = false;
			key.PostponedRest = false;

			// Take the count of messages right now, and then process only that many.
			// Any new message added to the queue will have to be handled next time.
			int len = _messages.Count;
			for (int i = 0; i < len; i++) {

				// Check for postponement of remaining messages
				if (key.PostponedRest) {
					len = i + 1;
					break;
				}

				// Get the next message from the Queue
				var mail = (Message<I>)_messages[i];

				// Make sure it's sender is still alive
				ResponseHandler respond;
				if (mail.responseHandler.GetCallback(out respond) || !_messageDiesWithSender) {
					key.ProcessMessage(mail);
				} else {
					// The sender has been garbage collected and the message dies with the sender
					continue;
				}

				// Has this message been postponed?
				if (key.PostponedThis) {
					// Add it back on to the end of the queue to be processed next time
					_messages.Add(mail);
					key.PostponedThis = false;
					continue;
				}

				// Call response handler
				try {
					respond?.Invoke();
				}
				catch (Exception e) {
					Debug.LogException(e);
				}
			}
			// Remove processed messages from the queue
			_messages.RemoveRange(0, len);

			key.Receiving = false;
		}


		private void GetMail<I, O>()
		{
			var key = (Key<I, O>)_key;

			// If messages need to be prioritized, then sort them now
			if (key._prioritizer != null) {
				_messages.Sort(key.Compare);
			}

			key.Receiving = true;
			key.PostponedThis = false;
			key.PostponedRest = false;

			// Take the count of messages right now, and then process only that many.
			// Any new message added to the queue will have to be handled next time.
			int len = _messages.Count;
			for (int i = 0; i < len; i++) {

				// Check for postponement of remaining messages
				if (key.PostponedRest) {
					// Make sure only processed messages get removed from the front of the queue
					len = i + 1;
					break;
				}

				// Get the next message from the Queue
				var mail = (Message<I, O>)_messages[i];

				// Make sure it's sender is still alive
				ResponseHandler<O> respond;

				if (mail.responseHandler.GetCallback(out respond) || !_messageDiesWithSender) {
					// Send message to key holder and send response back
					key.ProcessMessage(mail, respond);
				} else {
					// The sender has been garbage collected and the message dies with the sender
					continue;
				}

				// Has this message been postponed?
				if (key.PostponedThis) {
					// Add the message to the end of the queue to be processed next time
					_messages.Add(mail);
					key.PostponedThis = false;
					continue;
				}

			}
			// Remove processed messages from the queue
			_messages.RemoveRange(0, len);

			key.Receiving = false;
		}


		#endregion
		//---
		#region MESSAGES


		interface IMessage { }

		struct Message<I, O> : IMessage
		{
			public I Content;
			public WeakDelegate<ResponseHandler<O>> responseHandler;

			public Message(MonoBehaviour sender, I content, ResponseHandler<O> responseHandler)
			{
				Content = content;
				this.responseHandler = new WeakDelegate<ResponseHandler<O>>(sender, responseHandler);
			}
		}

		struct Message<I> : IMessage
		{
			public I Content;
			public WeakDelegate<ResponseHandler> responseHandler;

			public Message(MonoBehaviour sender, I content, ResponseHandler responseHandler)
			{
				Content = content;
				this.responseHandler = new WeakDelegate<ResponseHandler>(sender, responseHandler);
			}
		}


		#endregion

	}
}
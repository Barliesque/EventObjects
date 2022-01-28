#if DEVELOPMENT_BUILD || UNITY_EDITOR
#define LOGGING
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 618 // To allow restricted properties to be appropriately accessed here without obsolete warning


namespace Barliesque.EventObjects
{
	/// <summary>
	/// A Gauge provides a home for a value that needs to be broadly accessible.  Similarly to PartyLine,
	/// multiple keys may be created by entities that need to change the value of the Gauge; whenever the
	/// value is changed, all other key-holders are notified as well as any other watchers of the Gauge.
	/// </summary> 
	/// 
	/// Author(s): 
	/// - 7/12/2018		David Barlia
	/// -
	///
	abstract public class Gauge : ScriptableObject
	{
		static protected Dictionary<string, Gauge> _instances;


#if UNITY_EDITOR
		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		abstract public void __changed();

		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		abstract public void __reset();

		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		abstract public void __initFromSaved();

		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		abstract public void __getOwners(List<MonoBehaviour> owners);

		/// <summary>
		/// **** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****
		/// </summary>
		[Obsolete("**** INTERNAL USE ONLY! **** EXPOSED ONLY IN EDITOR ****")]
		abstract public void __getWatchers(List<MonoBehaviour> owners);
#endif

		/// <summary>
		/// The number of keys that have been created to control the value of this Gauge.
		/// </summary>
		abstract public int KeyCount { get; }

		/// <summary>
		/// The number of delegates watching this Gauge for a change of value.
		/// </summary>
		abstract public int WatcherCount { get; }

		/// <summary>
		/// If true, the current value of the Gauge is maintained across game sessions.
		/// </summary>
		abstract public bool IsPersistent { get; }

		/// <summary>
		/// True if the Gauge implements Serialize() and Deserialize() functions.
		/// </summary>
		abstract public bool IsSerializable { get; }

		/// <summary>
		/// The path in PlayerPrefs where the current value of the Gauge is stored for persistence.
		/// </summary>
		abstract public string PrefsPath { get; }
	}


	/// <summary>
	/// A Gauge provides a home for a value that needs to be broadly accessible.  Similarly to PartyLine,
	/// multiple keys may be created by entities that need to change the value of the Gauge; whenever the
	/// value is changed, all other key-holders are notified as well as any other watchers of the Gauge.
	/// </summary> 
	/// <typeparam name="T">Type of value to be stored by this Gauge</typeparam>
	abstract public class Gauge<T> : Gauge
	{
		[NonSerialized] private Gauge<T> _inst;

		protected Gauge<T> Instance
		{
			get
			{
				if (_inst) return _inst;
				
				_instances ??= new Dictionary<string, Gauge>();

				if (_instances.ContainsKey(this.name))
				{
					_inst = _instances[this.name] as Gauge<T>;
					if (!_inst)
					{
						throw new Exception(
							$"Cannot resolve Gauge[{this.name}]  Are there multiple Gauges with the same name, but different types?  All Gauge names must be unique!  {_instances[this.name]} == this... {_instances[this.name] == this}");
					}
				}
				else
				{
					_instances.Add(this.name, this);
					_inst = this;
				}

				return _inst;
			}
		}


		/// <summary>
		/// Implement this interface to enable the "Persistent" option.
		/// </summary>
		public interface ISerializable
		{
			string Serialize(T value);
			T Deserialize(string serial);
		}

		[SerializeField,
		 Tooltip("If selected, the current value of the Gauge is maintained when the app is restarted.  Useful for things like player settings.")]
		protected bool _persistent;

		override public bool IsPersistent => (Instance._persistent && this is ISerializable);
		override public bool IsSerializable => (Instance is ISerializable);

		override public string PrefsPath => $"{typeof(T).Name}/{name}";

		[SerializeField, TextArea]
		private string Comments;

		[SerializeField]
		private bool _logChanges = false;

		private bool LogChanges => Instance._logChanges && (Application.isEditor || Debug.isDebugBuild);

		public delegate void ChangeHandler(T value);

		private List<Key> _keys;

		override public int KeyCount => Instance._keys?.Count ?? 0;

		private List<WeakDelegate<ChangeHandler>> _watchers;

		override public int WatcherCount => Instance._watchers?.Count ?? 0;

		[SerializeField] private T _default;

		/// <summary>
		/// The initial value of this Gauge.
		/// </summary>
		public T Default => Instance._default;

		/// <summary>
		/// The current value of this Gauge.
		/// </summary>
		[SerializeField] private T _current;

		public T Value
		{
			get
			{
				if (Instance != this)
				{
					return Instance.Value;
				}

				if (!IsInitialized) Initialize();
				return _current;
			}
		}


		//===

		#region INITIALIZATION

		private void Awake()
		{
			if (!IsInitialized) Initialize();
		}

		private void OnEnable()
		{
			if (!IsInitialized) Initialize();
		}

		/// <summary>
		/// Override this method to add custom handling when the Gauge is initializing.
		/// </summary>
		/// <param name="value">The value about to be assigned to this Gauge</param>
		/// <returns>The value returned will be assigned to this Gauge's current value field.</returns>
		virtual protected T OnInitialize(T value)
		{
			return value;
		}

		private bool IsInitialized
		{
			get
			{
				if (Instance != this)
				{
					return Instance.IsInitialized;
				}

				if (_watchers == null) return false;
				if (_keys == null) return false;
				return true;
			}
		}

		private void Initialize()
		{
			if (Instance != this)
			{
				Instance.Initialize();
				return;
			}

			if (IsPersistent)
			{
				InitFromSaved();
			}
			else
			{
				_current = OnInitialize(_default);

#if LOGGING
				if (LogChanges)
				{
					Debug.Log($"Gauge<{typeof(T)}> [{name}] initialized with default value: {_current?.ToString() ?? "null"}");
				}
#endif
			}

			if (_watchers == null)
			{
				_watchers = new List<WeakDelegate<ChangeHandler>>();
			}
			else
			{
				_watchers.Clear();
			}

			if (_keys != null)
			{
				_keys.Clear();
			}
			else
			{
				_keys = new List<Key>();
			}

#if UNITY_EDITOR
			Application.wantsToQuit -= OnApplicationQuit;
			Application.wantsToQuit += OnApplicationQuit;
#endif
		}


		private void InitFromSaved()
		{
			var gauge = (ISerializable)this;
#if LOGGING
			// Is this gauge already saved in prefs?
			var saved = PlayerPrefs.HasKey(PrefsPath);
#endif
			var serial = PlayerPrefs.GetString(PrefsPath, gauge.Serialize(_default));
			try
			{
				_current = OnInitialize(gauge.Deserialize(serial));
			}
			catch (Exception)
			{
				Debug.LogError(
					$"<color=red>Serialization Error in {this.GetType().Name}[{name}] attempting to deserialize: \"{serial}\" to {typeof(T).Name}</color>");
				throw;
			}
#if LOGGING
			if (LogChanges)
			{
				Debug.Log($"Gauge<{typeof(T)}> [{name}] initialized with {(saved ? "saved" : "default")} value: {_current?.ToString() ?? "null"}");
			}
#endif
		}


#if UNITY_EDITOR
		[Obsolete]
		override public void __initFromSaved()
		{
			_persistent = true;
			InitFromSaved();
		}


		private bool OnApplicationQuit()
		{
			// ScriptableObject fields remain populated even outside of runtime in the Editor.
			// So, all fields should be returned to initial value.
			_watchers = null;
			_keys = null;
			//if (!IsPersistent) {
			//	_current = _default;
			//}
			return true;
		}
#endif

		#endregion

		//===

		#region WATCHERS

		/// <summary>
		/// Watch for changes to the current value of this Gauge.
		/// </summary>
		/// <param name="watcher">A reference to the script that contains the handler (typically "this").  If the watcher is garbage collected, the handler is automatically removed.</param>
		/// <param name="handler">A method that will receive the Gauge's current value whenever it changes.</param>
		public void AddWatcher(MonoBehaviour watcher, ChangeHandler handler)
		{
			if (Instance != this)
			{
				Instance.AddWatcher(watcher, handler);
				return;
			}

			if (handler == null) return;
			if (!IsInitialized) Initialize();

			// Check for duplication
			for (int i = _watchers.Count - 1; i >= 0; i--)
			{
				if (_watchers[i].GetOwner(out var watching))
				{
					if (watching != watcher) continue;
					// Found the watcher...
					if (_watchers[i].GetCallback(out var found))
					{
						// Handler already added?
						if (found == handler) return;
					}
				}
				else
				{
					// Watcher was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>Gauge [{name}]:  Watcher was garbage collected.  Handler removed.</color>");
					_watchers.RemoveAt(i);
				}
			}

			_watchers.Add(new WeakDelegate<ChangeHandler>(watcher, handler));
		}

		/// <summary>
		/// Stop watching for changes to this Gauge.
		/// </summary>
		/// <param name="watcher">The component that contains the handler, previously registered with AddWatcher()</param>
		/// <param name="handler">The change handler method previously registered with AddWatcher()</param>
		public void RemoveWatcher(MonoBehaviour watcher, ChangeHandler handler)
		{
			if (Instance != this)
			{
				Instance.RemoveWatcher(watcher, handler);
				return;
			}

			if (handler == null) return;
			if (!IsInitialized) Initialize();

			for (int i = _watchers.Count - 1; i >= 0; i--)
			{
				if (_watchers[i].GetOwner(out var watching))
				{
					if (watching != watcher) continue;
					// Found the watcher...
					if (_watchers[i].GetCallback(out var found))
					{
						if (found != handler) continue;
						// ...and the handler.  Remove!
						_watchers.RemoveAt(i);
						return;
					}
				}
				else
				{
					// Owner was Garbage Collected - Remove handler
					Debug.Log($"<color=yellow>Gauge [{name}]:  Watcher was garbage collected.  Handler removed.</color>");
					_watchers.RemoveAt(i);
				}
			}
			// Couldn't find handler to remove
		}

#if UNITY_EDITOR
		[Obsolete]
		override public void __getWatchers(List<MonoBehaviour> watchers)
		{
			if (Instance != this)
			{
				Instance.__getWatchers(watchers);
				return;
			}

			watchers.Clear();
			if (_watchers == null) return;
			for (int i = _watchers.Count - 1; i >= 0; i--)
			{
				var success = _watchers[i].GetOwner(out var watcher);
				watchers.Insert(0, success ? watcher : null);
			}
		}
#endif

		#endregion

		//===

		#region KEYS

		/// <summary>
		/// To be able to modify the Gauge value, you must have a Key.
		/// </summary>
		public interface IKey
		{
			/// <summary>
			/// The current value of the Gauge.  Setting this value will invoke ChangeHandlers watching the Gauge.
			/// </summary>
			T Value { get; set; }

			/// <summary>
			/// The initial value of the Gauge.
			/// </summary>
			T Default { get; }

			/// <summary>
			/// Return the Gauge to its default value.
			/// </summary>
			void Reset();

			/// <summary>
			/// When a Gauge contains a reference object and a value within that object has been changed, this method must be called to invoke ChangeHandlers.
			/// </summary>
			void Changed();

			/// <summary>
			/// Remove this Key from the Gauge.  Must be called explicitly for proper cleanup, typically in an OnDestroy method.
			/// </summary>
			void Dispose();

			/// <summary>
			/// Invoked when the key is disposed.  Useful to release references to the key.
			/// </summary>
			event Action OnDispose;
		}


		/// <summary>
		/// Keys can only be created within this class
		/// </summary>
		private class Key : IKey
		{
			private Gauge<T> _gauge;
			private WeakDelegate<ChangeHandler> Handler;
			internal bool Sending;

			public event Action OnDispose;

			internal Key(MonoBehaviour owner, Gauge<T> gauge, ChangeHandler handler)
			{
				_gauge = gauge;
				Handler = new WeakDelegate<ChangeHandler>(owner, handler);
			}

			public T Value
			{
				get => _gauge._current;

				set => _gauge.SetValue(this, value);
			}

			public T Default => _gauge._default;

			public void Reset()
			{
				_gauge.SetValue(this, _gauge._default);
			}

			public void Changed()
			{
				_gauge.SendChangedValue(this);
			}

			public void Dispose()
			{
				_gauge.DisposeKey(this);
				_gauge = null;
				Handler.Dispose();
				OnDispose?.Invoke();
			}

			public void MissingDispose()
			{
				Debug.LogException(new Exception(
					$"Gauge [{_gauge.name}] Key was not properly disposed before owner was destroyed!  Make sure to call Key.Dispose()"));
				Dispose();
			}

			/// <summary>
			/// Called internally when this Key receives a change message from another Key.
			/// </summary>
			internal void InvokeHandler()
			{
				// If this Key was in the middle of sending a change,
				// then it's been overridden by a change by another Key.
				if (Sending)
				{
					Debug.Log($"<color=yellow>Gauge [{_gauge.name}] send cancelled because another key is changing the Gauge value.</color>");
					Sending = false;
				}

				try
				{
					if (Handler.GetCallback(out var callback))
					{
						callback?.Invoke(_gauge._current);
					}
					else
					{
						MissingDispose();
					}
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}

			public bool GetOwner(out MonoBehaviour owner)
			{
				bool success = Handler.GetOwner(out owner);
				if (!success && _gauge != null)
				{
					MissingDispose();
				}

				return success;
			}
		}


		/// <summary>
		/// A Key is required to make changes to the Gauge value
		/// </summary>
		/// <param name="owner">A reference to the script that owns the key (typically "this").  If the owner is garbage collected without Key.Dispose() being called, an error is thrown.</param>
		/// <param name="handler">A method to handle changes to the Gauge's value.</param>
		/// <returns></returns>
		public IKey CreateKey(MonoBehaviour owner, ChangeHandler handler)
		{
			if (Instance != this)
			{
				return Instance.CreateKey(owner, handler);
			}

			if (!IsInitialized) Initialize();
			var key = new Key(owner, this, handler);
			_keys.Add(key);
			return key;
		}


		/// <summary>
		/// Strictly called via the Key
		/// </summary>
		private void DisposeKey(Key key)
		{
			if (_keys == null) return;
			if (!_keys.Contains(key)) return;
			_keys.Remove(key);
		}

#if UNITY_EDITOR
		[Obsolete("*** FOR INTERNAL USE ONLY ***")]
		override public void __getOwners(List<MonoBehaviour> owners)
		{
			if (Instance != this)
			{
				Instance.__getOwners(owners);
				return;
			}

			owners.Clear();
			if (_keys == null) return;
			for (int i = _keys.Count - 1; i >= 0; i--)
			{
				var success = _keys[i].GetOwner(out var owner);
				owners.Insert(0, success ? owner : null);
			}
		}
#endif

		#endregion

		//===

		#region CHANGE VALUE

		/// <summary>
		/// Override this method to add custom handling when the Gauge value is changed.
		/// </summary>
		/// <param name="value">The value about to be assigned to this Gauge</param>
		/// <returns>The value returned will be assigned to this Gauge's current value field.</returns>
		virtual protected T OnChange(T value)
		{
			return value;
		}


		/// <summary>
		/// Strictly called via the Key -- or by the Inspector via __changed()
		/// </summary>
		private void SetValue(Key key, T value)
		{
			if (EqualityComparer<T>.Default.Equals(_current, value)) return;
			_current = OnChange(value);
			SendChangedValue(key);
		}


		private void SendChangedValue(Key sender)
		{
			if (Application.isPlaying && IsInitialized)
			{
#if LOGGING
				if (LogChanges)
				{
					if (sender != null)
					{
						if (sender.GetOwner(out var owner))
						{
							Debug.Log($"[{owner.GetType().Name}] on [{owner.name}] changed Gauge [{name}] value to: {_current}");
						}
						else
						{
							sender.MissingDispose();
							return;
						}
					}
					else
					{
						Debug.Log($"Gauge [{name}] value changed in editor to: {_current}");
					}
				}
#endif
				// Invoke members
				if (sender != null) sender.Sending = true;

				for (int i = 0, len = _keys.Count; i < len; i++)
				{
					var receiver = _keys[i];
					// Don't send to sender
					if (receiver == sender) continue;
					receiver.InvokeHandler();
					// Check that sending hasn't been cancelled
					if (!(sender == null || sender.Sending)) return;
				}

				// Invoke watchers
				for (int i = _watchers.Count - 1; i >= 0; i--)
				{
					if (_watchers[i].GetCallback(out var handler) && handler != null)
					{
						try
						{
							handler.Invoke(_current);
						}
						catch (Exception e)
						{
							Debug.LogException(e);
						}
					}
					else
					{
						// Handler was Garbage Collected - Remove
						Debug.Log($"<color=yellow>Gauge [{name}]:  Watcher was garbage collected.  Handler removed.</color>");
						_watchers.RemoveAt(i);
					}

					// Check that sending hasn't been cancelled
					if (!(sender == null || sender.Sending)) return;
				}

				if (sender != null) sender.Sending = false;
			}

			// Store new value in PlayerPrefs
			if (IsPersistent)
			{
				var gauge = (ISerializable)this;
				PlayerPrefs.SetString(PrefsPath, gauge.Serialize(_current));
				PlayerPrefs.Save();
			}
		}


#if UNITY_EDITOR

		[Obsolete("*** RESTRICTED ACCESS ***")]
		override public void __changed()
		{
			if (Instance != this)
			{
				Instance.__changed();
				return;
			}

			_current = OnChange(_current);
			SendChangedValue(null);
		}

		[Obsolete("*** RESTRICTED ACCESS ***")]
		override public void __reset()
		{
			if (Instance != this)
			{
				Instance.__reset();
				return;
			}

			SetValue(null, _default);
		}
#endif

		#endregion

		//===
		override public string ToString()
		{
			if (Instance != this)
			{
				return $"Gauge<{typeof(T).Name}> [{name}](clone) = [{Instance._current}]";
			}

			return $"Gauge<{typeof(T).Name}> [{name}] = [{_current}]";
		}
	}
}
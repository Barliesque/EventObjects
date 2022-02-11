using System;
using UnityEngine;

namespace Barliesque.EventObjects
{

	/// <summary>
	/// A container for a delegate that allows it to be handled safely if its owner has been destroyed.
	/// </summary>
	internal interface IWeakDelegate {
		bool GetOwner(out MonoBehaviour owner);  //TODO  Make two versions:  Component & MonoBehaviour
		void Dispose();
	}


	/// <summary>
	/// A container for a delegate that allows it to be handled safely if its owner has been destroyed.
	/// </summary>
	/// <see>http://www.codewrecks.com/blog/index.php/2011/06/30/weak-reference-to-delegate-never-do-it/</see>
	public struct WeakDelegate<T> : IWeakDelegate
	{
		private WeakReference<MonoBehaviour> Owner;  //TODO  Use Unity.Object for the Owner type
		private T Callback;

		public WeakDelegate(MonoBehaviour owner, T callback)  //TODO  Make two versions:  Component & MonoBehaviour
		{
#if UNITY_EDITOR
			if (!typeof(T).IsSubclassOf(typeof(Delegate))) {
				throw new InvalidOperationException($"{typeof(T)} is not a delegate type!");
			}
#endif

			Owner = new WeakReference<MonoBehaviour>(owner);
			Callback = callback;
		}

		/// <summary>
		/// Returns callback only if the owner has not been garbage collected.
		/// </summary>
		public bool GetCallback(out T callback)
		{
			bool isAlive = Owner.TryGetTarget(out var owner) && (owner != null);
			if (isAlive) {
				callback = Callback;
				return true;
			}
			// Owner has expired, so remove the callback
			callback = Callback = default(T);
			return false;
		}

		/// <summary>
		/// Attempts to fetch owner and returns true if successful.
		/// </summary>
		public bool GetOwner(out MonoBehaviour owner)  //TODO  Make two versions:  Component & MonoBehaviour
		{
			if (Owner.TryGetTarget(out owner) && owner != null) {
				return true;
			}
			// Owner has expired, so remove the callback
			Callback = default(T);
			owner = null;
			return false;
		}

		override public string ToString()
		{
			if (GetOwner(out var owner)) {
				return $"[WeakDelegate: {owner.GetType()}.{Callback.ToString()} on {owner.name}]";
			} else {
				return "[WeakDelegate: Owner is null]";
			}
		}

		/// <summary>
		/// Release references to owner and callback.
		/// </summary>
		public void Dispose()
		{
			Owner = null;
			Callback = default(T);
		}

	}

}
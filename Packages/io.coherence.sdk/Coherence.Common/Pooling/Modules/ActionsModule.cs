// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Pooling.Modules
{
    using System;
    using System.Collections.Generic;

    internal class ActionsModule<T> : IPoolModule<T>
    {
        private List<Action<T>> rentActions;
        private List<Action<T>> returnActions;
        private List<Func<T, bool>> restoreFuncs;

        public ActionsModule<T> WithRentAction(Action<T> action)
        {
            AddRentAction(action);
            return this;
        }

        public ActionsModule<T> WithReturnAction(Action<T> action)
        {
            AddReturnAction(action);
            return this;
        }

        public ActionsModule<T> WithRestoreFunc(Func<T, bool> func)
        {
            AddRestoreFunc(func);
            return this;
        }

        public void OnRent(in T item)
        {
            if (rentActions == null)
            {
                return;
            }

            foreach (var action in rentActions)
            {
                action(item);
            }
        }

        public void OnReturn(in T item)
        {
            if (returnActions == null)
            {
                return;
            }

            foreach (var action in returnActions)
            {
                action(item);
            }
        }

        public bool ShouldRestore(in T item)
        {
            if (restoreFuncs == null)
            {
                return true;
            }

            foreach (var func in restoreFuncs)
            {
                if (!func(item))
                {
                    return false;
                }
            }

            return true;
        }

        public void AddRentAction(Action<T> action)
        {
            rentActions ??= new List<Action<T>>();
            rentActions.Add(action);
        }

        public void AddReturnAction(Action<T> action)
        {
            returnActions ??= new List<Action<T>>();
            returnActions.Add(action);
        }

        public void AddRestoreFunc(Func<T, bool> func)
        {
            restoreFuncs ??= new List<Func<T, bool>>();
            restoreFuncs.Add(func);
        }
    }
}

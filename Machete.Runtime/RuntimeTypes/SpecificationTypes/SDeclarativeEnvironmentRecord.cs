﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Machete.Runtime.RuntimeTypes.LanguageTypes;
using Machete.Runtime.NativeObjects.BuiltinObjects;
using Machete.Runtime.NativeObjects.BuiltinObjects.ConstructorObjects;
using Machete.Interfaces;

namespace Machete.Runtime.RuntimeTypes.SpecificationTypes
{
    public sealed class SDeclarativeEnvironmentRecord : IDeclarativeEnvironmentRecord
    {
        private readonly Dictionary<string, Binding> _bindings = new Dictionary<string, Binding>();


        public bool HasBinding(string n)
        {
            return _bindings.ContainsKey(n);
        }

        public void CreateMutableBinding(string n, bool d)
        {
            _bindings.Add(n, new Binding(d ? BFlags.Deletable : BFlags.None));
        }

        public void SetMutableBinding(string n, IDynamic v, bool s)
        {
            var binding = _bindings[n];
            if ((binding.Flags & BFlags.Immutable) == BFlags.Immutable)
            {
                Environment.ThrowTypeError();
            }
            binding.Value = v;
        }

        public IDynamic GetBindingValue(string n, bool s)
        {
            var binding = _bindings[n];
            if ((binding.Flags & BFlags.Uninitialized) == BFlags.Uninitialized)
            {
                if (!s) return LUndefined.Instance;
                Environment.ThrowReferenceError();
            }
            return binding.Value;
        }

        public bool DeleteBinding(string n)
        {
            var binding = default(Binding);
            if (!_bindings.TryGetValue(n, out binding))
            {
                return true;
            }
            if ((binding.Flags & BFlags.Deletable) != BFlags.Deletable)
            {
                return false;
            }
            _bindings.Remove(n);
            return true;
        }

        public IDynamic ImplicitThisValue()
        {
            return LUndefined.Instance;
        }

        public IDynamic Get(string name, bool strict)
        {
            return GetBindingValue(name, strict);
        }

        public void Set(string name, IDynamic value, bool strict)
        {
            SetMutableBinding(name, value, strict);
        }

        public void CreateImmutableBinding(string n)
        {
            _bindings.Add(n, new Binding(BFlags.Immutable | BFlags.Uninitialized));
        }

        public void InitializeImmutableBinding(string n, IDynamic v)
        {
            _bindings[n].Value = v;
        }


        [Flags]
        private enum BFlags
        {
            None,
            Deletable,
            Immutable,
            Initialized,
            Uninitialized,
        }

        private sealed class Binding
        {
            public IDynamic Value;
            public BFlags Flags;

            public Binding(BFlags flags)
            {
                Value = LUndefined.Instance;
                Flags = flags;
            }
        }
    }
}

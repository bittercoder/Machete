﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Machete.Runtime.RuntimeTypes.SpecificationTypes;
using Machete.Runtime.NativeObjects;
using Machete.Core;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using Machete.Runtime.NativeObjects.BuiltinObjects;
using System.Dynamic;

namespace Machete.Runtime.RuntimeTypes.LanguageTypes
{
    public class LObject : LType, IObject
    {
        private readonly Dictionary<string, IPropertyDescriptor> _map = new Dictionary<string, IPropertyDescriptor>();
        
        public IObject Prototype { get; set; }

        public string Class { get; set; }

        public bool Extensible { get; set; }

        public override LanguageTypeCode TypeCode
        {
            get { return LanguageTypeCode.Object; }
        }

        public override bool IsPrimitive
        {
            get { return false; }
        }

        public LObject(IEnvironment environment)
            : base(environment)
        {

        }


        public virtual IPropertyDescriptor GetOwnProperty(string p)
        {
            IPropertyDescriptor value;
            if (_map.TryGetValue(p, out value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        public virtual IPropertyDescriptor GetProperty(string p)
        {
            IObject obj = this;
            IPropertyDescriptor value;

            do
            {
                value = obj.GetOwnProperty(p);
                if (value != null)
                {
                    return value;
                }
                obj = obj.Prototype;
            } while (obj != null);

            return null;
        }

        public virtual IDynamic Get(string p)
        {
            var desc = GetProperty(p);
            if (desc == null)
            {
                return Environment.Undefined;
            }
            else if (desc.IsDataDescriptor)
            {
                return desc.Value;
            }
            else if (desc.Get is LUndefined)
            {
                return Environment.Undefined;
            }
            else
            {
                return ((ICallable)desc.Get).Call(Environment, this, new SArgs(Environment));
            }
        }

        public virtual bool CanPut(string p)
        {
            IPropertyDescriptor desc;
            if (_map.TryGetValue(p, out desc))
            {
                if (desc.IsAccessorDescriptor)
                {
                    return !(desc.Set is IUndefined);
                }
                else
                {
                    return desc.Writable ?? false;
                }
            }
            else if (Prototype == null)
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(p);
            if (inherited == null)
            {
                return Extensible;
            }
            else if (inherited.IsAccessorDescriptor)
            {
                return !(inherited.Set is IUndefined);
            }
            else if (Extensible)
            {
                return inherited.Writable.Value;
            }
            else
            {
                return false;
            }
        }

        public virtual void Put(string p, IDynamic value, bool @throw)
        {
            if (!CanPut(p))
            {
                if (@throw)
                {
                    throw Environment.CreateTypeError("");
                }
                return;
            }

            var ownDesc = GetOwnProperty(p);
            if (ownDesc != null && ownDesc.IsDataDescriptor)
            {
                var valueDesc = new SPropertyDescriptor() { Value = value };
                DefineOwnProperty(p, valueDesc, @throw);
                return;
            }

            var desc = GetProperty(p);
            if (desc != null && desc.IsAccessorDescriptor)
            {
                ((ICallable)desc.Set).Call(Environment, this, new SArgs(Environment, value));
                return;
            }

            var newDesc = Environment.CreateDataDescriptor(value, true, true, true);
            DefineOwnProperty(p, newDesc, @throw);
        }

        public virtual bool HasProperty(string p)
        {
            return _map.ContainsKey(p);
        }

        public virtual bool Delete(string p, bool @throw)
        {
            var desc = GetOwnProperty(p);
            if (desc == null)
            {
                return true;
            }
            else if (desc.Configurable.GetValueOrDefault())
            {
                return _map.Remove(p);
            }
            else if (@throw)
            {
                throw Environment.CreateTypeError("");
            }
            else
            {
                return false;
            }
        }

        public virtual IDynamic DefaultValue(string hint)
        {
            var toString = Get("toString") as ICallable;
            var valueOf = Get("valueOf") as ICallable;
            var first = hint == "String" ? toString : valueOf;
            var second = hint == "String" ? valueOf : toString;

            if (first != null)
            {
                var result = first.Call(Environment, this, Environment.EmptyArgs);
                if (result.IsPrimitive)
                {
                    return result;
                }
            }

            if (second != null)
            {
                var result = second.Call(Environment, this, Environment.EmptyArgs);
                if (result.IsPrimitive)
                {
                    return result;
                }
            }

            throw Environment.CreateTypeError("No primitive value was found for object.");
        }

        public virtual bool DefineOwnProperty(string p, IPropertyDescriptor desc, bool @throw)
        {
            var current = GetOwnProperty(p);
            if (current == null)
            {
                if (!Extensible)
                {
                    return Reject(p, @throw);
                }
                if (desc.IsGenericDescriptor || desc.IsDataDescriptor)
                {
                    _map.Add(p, new SPropertyDescriptor()
                    {
                        Value = desc.Value ?? Environment.Undefined,
                        Writable = desc.Writable ?? false,
                        Enumerable = desc.Enumerable ?? false,
                        Configurable = desc.Configurable ?? false
                    });
                }
                else
                {
                    _map.Add(p, new SPropertyDescriptor()
                    {
                        Get = desc.Get ?? Environment.Undefined,
                        Set = desc.Set ?? Environment.Undefined,
                        Enumerable = desc.Enumerable ?? false,
                        Configurable = desc.Configurable ?? false
                    });
                }
                return true;
            }
            else if (desc.IsEmpty || current.Equals(desc))
            {
                return true;
            }
            else if (!current.Configurable.GetValueOrDefault())
            {
                if (desc.Configurable.GetValueOrDefault())
                {
                    return Reject(p, @throw);
                }
                else if (desc.Enumerable != null && desc.Enumerable.GetValueOrDefault() ^ current.Enumerable.GetValueOrDefault())
                {
                    return Reject(p, @throw);
                }
            }
            else if (!desc.IsGenericDescriptor)
            {
                if (current.IsDataDescriptor ^ desc.IsDataDescriptor)
                {
                    if (!current.Configurable.Value)
                    {
                        return Reject(p, @throw);
                    }
                    else if (current.IsDataDescriptor)
                    {
                        current.Value = null;
                        current.Writable = null;
                        current.Get = desc.Get;
                        current.Set = desc.Set;
                    }
                    else
                    {
                        current.Value = desc.Value;
                        current.Writable = desc.Writable;
                        current.Get = null;
                        current.Set = null;
                    }
                }
                else if (current.IsDataDescriptor && desc.IsDataDescriptor)
                {
                    if (!current.Configurable.Value)
                    {
                        if (!desc.Writable.Value && current.Writable.Value)
                        {
                            return Reject(p, @throw);
                        }
                        else if (!current.Writable.Value && current.Value != desc.Value)
                        {
                            return Reject(p, @throw);
                        }
                    }
                }
                else
                {
                    if (!current.Configurable.Value)
                    {
                        if (desc.Set != null && desc.Set != current.Set)
                        {
                            return Reject(p, @throw);
                        }
                        else if (desc.Get != null && desc.Get != current.Get)
                        {
                            return Reject(p, @throw);
                        }
                    }
                }
            }
            current.Value = desc.Value ?? current.Value;
            current.Writable = desc.Writable ?? current.Writable;
            current.Get = desc.Get ?? current.Get;
            current.Set = desc.Set ?? current.Set;
            current.Enumerable = desc.Enumerable ?? current.Enumerable;
            current.Configurable = desc.Configurable ?? current.Configurable;
            _map[p] = current;
            return true;
        }

        private bool Reject(string name, bool @throw)
        {
            if (!@throw) return false;
            throw Environment.CreateTypeError("");
        }

        
        public override IDynamic Op_Equals(IDynamic other)
        {
            switch (other.TypeCode)
            {
                case LanguageTypeCode.String:
                case LanguageTypeCode.Number:
                    return ConvertToPrimitive(null).Op_Equals(other);
                case LanguageTypeCode.Object:
                    return Environment.CreateBoolean(this == other);
                default:
                    return Environment.False;
            }
        }

        public override IDynamic Op_StrictEquals(IDynamic other)
        {
            switch (other.TypeCode)
            {
                case LanguageTypeCode.Object:
                    return Environment.CreateBoolean(this == other.Value);
                default:
                    return Environment.False;
            }
        }

        public override IDynamic Op_Typeof()
        {
            return Environment.CreateString(this is ICallable ? "function" : "object");
        }

        public override IDynamic Op_Call(IArgs args)
        {
            var c = this as ICallable;
            if (c == null)
            {
                throw Environment.CreateTypeError("");
            }
            return c.Call(Environment, this, args);
        }

        public override IDynamic ConvertToPrimitive(string preferredType)
        {
            return DefaultValue(preferredType);
        }

        public override IBoolean ConvertToBoolean()
        {
            return Environment.True;
        }

        public override INumber ConvertToNumber()
        {
            return DefaultValue("Number").ConvertToNumber();
        }

        public override IString ConvertToString()
        {
            return DefaultValue("String").ConvertToString();
        }

        public override IObject ConvertToObject()
        {
            return this;
        }
        
        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            foreach (var prop in _map.Keys)
            {
                IPropertyDescriptor desc;
                if (_map.TryGetValue(prop, out desc))
                {
                    if (desc.Enumerable ?? false)
                    {
                        yield return prop;
                    }
                }
            }

            if (Prototype != null)
            {
                foreach (var prop in Prototype)
                {
                    yield return prop;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<string>)this).GetEnumerator();
        }
        
        public virtual void Initialize()
        {
            foreach (var m in this.GetType().GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                BuiltinFunctionAttribute nf = null;
                DataDescriptorAttribute dd = null;

                foreach (var a in m.GetCustomAttributes(false))
                {
                    nf = nf ?? a as BuiltinFunctionAttribute;
                    dd = dd ?? a as DataDescriptorAttribute;
                    if (nf != null && dd != null)
                    {
                        break;
                    }
                }

                // For now I want to assume native functions will always be stored in data descriptors.
                if (nf == null || dd == null)
                    continue;

                var code = (Code)Delegate.CreateDelegate(typeof(Code), m);
                var func = new BFunction(Environment, code, nf.FormalParameterList);
                var desc = Environment.CreateDataDescriptor(func, dd.Writable, dd.Enumerable, dd.Configurable);

                DefineOwnProperty(nf.Identifier, desc, false);
            }
        }
        
        public class Builder : IObjectBuilder
        {
            private readonly LObject _obj;
            private bool? _writable;
            private bool? _enumerable;
            private bool? _configurable;

            public Builder(IObject obj)
            {
                _obj = (LObject)obj;
            }

            public IObjectBuilder SetAttributes(bool? writable, bool? enumerable, bool? configurable)
            {
                _writable = writable;
                _enumerable = enumerable;
                _configurable = configurable;
                return this;
            }

            public IObjectBuilder AppendDataProperty(string name, IDynamic value)
            {
                var desc = _obj.Environment.CreateDataDescriptor(value, _writable, _enumerable, _configurable);
                _obj._map.Add(name, desc);
                return this;
            }

            public IObjectBuilder AppendAccessorProperty(string name, IDynamic get, IDynamic set)
            {
                var desc = _obj.Environment.CreateAccessorDescriptor(get, set, _enumerable, _configurable);
                _obj._map.Add(name, desc);
                return this;
            }

            public IObject ToObject()
            {
                return _obj;
            }
        }
    }
}
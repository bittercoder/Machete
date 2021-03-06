﻿using Machete.Core;
using Machete.Runtime.RuntimeTypes.LanguageTypes;

namespace Machete.Runtime.NativeObjects
{
    public sealed class NEvalError : LObject
    {
        public NEvalError(IEnvironment environment)
            : base(environment)
        {
            Class = "Error";
            Extensible = true;
        }
    }
}

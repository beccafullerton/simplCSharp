﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Simpl.Serialization;

namespace ecologylab.serialization.types.scalar
{
    /// <summary>
    ///     Class abstracting C# Float type
    /// </summary>
    class StringType : ScalarType
    {
        /// <summary>
        ///      Calls the parent constructor for String type
        /// </summary>
        public StringType()
            : base(typeof(String))
        { }

        /// <summary>
        ///     Creates and returns an instance of int type for the given
        ///     input value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="formatStrings"></param>
        /// <returns></returns>
        public override Object GetInstance(String value, String[] formatStrings, IScalarUnmarshallingContext scalarUnmarshallingContext)
        {
            return value; 
        }
    }
}
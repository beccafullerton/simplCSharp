using System;
using Simpl.Serialization.Context;

namespace Simpl.Serialization.Types.Scalar
{
    internal class TypeType : ReferenceType
    {
        public TypeType() 
            : base(typeof(Type), CLTypeConstants.JavaClass, CLTypeConstants.ObjCClass, null)
        {

        }

        public override object GetInstance(string value, string[] formatStrings, IScalarUnmarshallingContext unmarshallingContext)
        {
            throw new NotImplementedException();
        }
    }
}
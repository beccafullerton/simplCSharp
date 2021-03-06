﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Simpl.Fundamental.Generic;
using Simpl.Serialization.Attributes;
using Simpl.Serialization.Context;
using Simpl.Serialization.Types;
using Simpl.Serialization.PlatformSpecifics;

namespace Simpl.Serialization
{
    /// <summary>
    /// A class that "Describes" the different aspects of a field in a given class that can be handled via S.im.pl
    /// </summary>
    public class FieldDescriptor : DescriptorBase
    {
        public static readonly String Null = ScalarType.DefaultValueString;

        [SimplScalar] protected FieldInfo field;


        [SimplComposite] private ClassDescriptor _elementClassDescriptor;

        [SimplScalar] private String mapKeyFieldName;


        [SimplComposite] protected ClassDescriptor declaringClassDescriptor;

        [SimplScalar] private Type _elementClass;

        //generics
        [SimplScalar] private Boolean isGeneric;

        ///<sumary>
        ///<para>
        ///if is null, this field is not a cloned one.
        ///</para>
        ///<para>
        ///if not null, refers to the descriptor that this field is cloned from.
        ///</para>
        ///</sumary>
        private FieldDescriptor clonedFrom;

        ///<sumary>
        ///<para>
	    /// For composite or collection fields declared with generic type variables, this field stores the
	    /// binding to the resolved generic type from the ClassDescriptor.
	    /// </para>
	    /// Note: this will require cloning this during inheritance, when subtypes instantiate the generic
	    /// type var(s) with different values.
	    ///</sumary>
	    [SimplCollection("generic_type_var")] private List<GenericTypeVar> genericTypeVars;
	
	    private ClassDescriptor	genericTypeVarsContextCD;


        [SimplMap("polymorph_class_descriptor")] [SimplMapKeyField] private DictionaryList<String, ClassDescriptor>
            polymorphClassDescriptors;

        [SimplMap("polymorph_class")] private Dictionary<String, Type> polymorphClasses;

        [SimplMap("library_namespace")] private Dictionary<String, String> libraryNamespaces =
            new Dictionary<String, String>();

        [SimplScalar] private int type;

        [SimplScalar] private ScalarType scalarType;

        [SimplComposite] private CollectionType collectionType;

        public CollectionType CollectionType
        {
            get
            {
                return this.collectionType;
            }
        }

        [SimplScalar] private Hint xmlHint;

        [SimplScalar] private Boolean isEnum;


        private String[] dataFormat;

        [SimplScalar] private Boolean isCDATA;

        [SimplScalar] private Boolean needsEscaping;

        [SimplScalar] private Regex filterRegex;

        [SimplScalar] private String filterReplace;


        private FieldDescriptor wrappedFD;

        private FieldDescriptor wrapper;

        private Dictionary<Int32, ClassDescriptor> tlvClassDescriptors;

        [SimplScalar] private String unresolvedScopeAnnotation = null;

        private Type[]	unresolvedClassesAnnotation	= null;

        [SimplScalar] private String _collectionOrMapTagName;

        [SimplScalar] private String compositeTagName;


        [SimplScalar] private Boolean _wrapped;

        private MethodInfo setValueMethod;

        private String bibtexTag = "";

        private Boolean isBibtexKey = false;

        [SimplScalar] private String fieldType;

        [SimplScalar] private String genericParametersString;

        private List<Type> dependencies = new List<Type>();

        private FieldDescriptor _clonedFrom;

        /**
	     * Default constructor only for use by translateFromXML().
	     */
	    public FieldDescriptor() : base()
	    {

	    }

        public FieldDescriptor(ClassDescriptor baseClassDescriptor)
            : base(baseClassDescriptor.TagName, null)
        {

            declaringClassDescriptor = baseClassDescriptor;
            field = null;
            type = FieldTypes.Pseudo;
            ScalarType = null;
            bibtexTag = baseClassDescriptor.BibtexType;
        }


        public FieldDescriptor(ClassDescriptor baseClassDescriptor, FieldDescriptor wrappedFD,
                               String wrapperTag)
            : base(wrapperTag, null)
        {
            declaringClassDescriptor = baseClassDescriptor;
            this.wrappedFD = wrappedFD;
            wrappedFD.Wrapper = this;
            type = FieldTypes.Wrapper;
        }

        public FieldDescriptor(ClassDescriptor declaringClassDescriptor, FieldInfo field, int annotationType)
            : base(XmlTools.GetXmlTagName(field), field.Name)
        {
            this.declaringClassDescriptor = declaringClassDescriptor;
            this.field = field;

            fieldType = field.FieldType.Name;

            //generics
            if (field.FieldType.IsGenericParameter || field.FieldType.GetTypeInfo().IsGenericType)
            {
                Type realFieldType = field.FieldType;

                while (realFieldType.IsGenericParameter)
                {
                    Type[] realFieldTypeConstraints = realFieldType.GetTypeInfo().GetGenericParameterConstraints();

                    if (realFieldTypeConstraints == null || realFieldTypeConstraints.Length == 0)
                    {
                        realFieldType = typeof(Object);
                        break;
                    }
                    else
                        realFieldType = realFieldTypeConstraints[0];
                }

                fieldType = realFieldType.Name;

                if (realFieldType.GetTypeInfo().IsGenericType)//can also be a generic parameter that extends a generic type
                {
                    int pos = fieldType.IndexOf('`');
                    fieldType = fieldType.Substring(0, pos);
                }
            }

            if (XmlTools.IsAnnotationPresent<SimplMapKeyField>(field))
            {
                mapKeyFieldName = XmlTools.GetAnnotation<SimplMapKeyField>(field).FieldName;
            }

            DerivePolymorphicDescriptors(field);

            type = FieldTypes.UnsetType;

            if (annotationType == FieldTypes.Scalar)
            {
                type = DeriveScalarSerialization(field);
            }
            else
            {
                type = DeriveNestedSerialization(field, annotationType);
            }

            String fieldName = field.Name;
            StringBuilder capFieldName = new StringBuilder(fieldName);

            //generics
            Type genericType = field.DeclaringType;
            isGeneric = genericType.GetTypeInfo().IsGenericType || genericType.IsGenericParameter;
        }

        private int DeriveNestedSerialization(FieldInfo thatField, int annotationType)
        {
            int result = annotationType;
            Type thatFieldType = thatField.FieldType;

            switch (annotationType)
            {
                case FieldTypes.CompositeElement:
                    String compositeTag = XmlTools.GetAnnotation<SimplComposite>(thatField).TagName;
                    Boolean isWrap = XmlTools.IsAnnotationPresent<SimplWrap>(thatField);

                    Boolean compositeTagIsNullOrEmpty = String.IsNullOrEmpty(compositeTag);

                    if (!IsPolymorphic)
                    {
                        if (isWrap && compositeTagIsNullOrEmpty)
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate  [SimplComposite] " + thatField.Name
                                         + " because its tag argument is missing.";

                            Debug.WriteLine(msg);
                            return FieldTypes.IgnoredAttribute;
                        }

                        _elementClassDescriptor = ClassDescriptor.GetClassDescriptor(thatFieldType);
                        _elementClass = _elementClassDescriptor.DescribedClass;
                        compositeTag = XmlTools.GetXmlTagName(thatField);
                    }
                    else
                    {
                        if (!compositeTagIsNullOrEmpty)
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate  [SimplComposite] " + thatField.Name
                                         + " because its tag argument is missing.";

                            Debug.WriteLine(msg);
                        }
                    }
                    compositeTagName = compositeTag;
                    break;
                case FieldTypes.CollectionElement:
                    if (!(typeof(IList).GetTypeInfo().IsAssignableFrom(thatField.FieldType.GetTypeInfo())))
                    {
                        String msg = "In " + declaringClassDescriptor.DescribedClass + "\n\tCan't translate  "
                                     + "[SimplCollection] " + field.Name
                                     + " because the annotated field is not an instance of " +
                                     typeof (IList).Name
                                     + ".";

                        Debug.WriteLine(msg);
                        return FieldTypes.IgnoredAttribute;
                    }


                    String collectionTag = XmlTools.GetAnnotation<SimplCollection>(thatField).TagName;

                    if (!IsPolymorphic)
                    {
                        Type collectionElementType = GetTypeArgs(thatField, 0);

                        if (String.IsNullOrEmpty(collectionTag))
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate [SimplCollection]" + field.Name
                                         + " because its tag argument is missing.";
                            Debug.WriteLine(msg);
                            return FieldTypes.IgnoredElement;
                        }
                        
                        if (collectionElementType == null)
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate [SimplCollection] " + field.Name
                                         + " because the parameterized type argument for the Collection is missing.";
                            Debug.WriteLine(msg);
                            return FieldTypes.IgnoredElement;
                        }
                        
                        if (!TypeRegistry.ScalarTypes.Contains(collectionElementType))
                        {
                            _elementClassDescriptor = ClassDescriptor.GetClassDescriptor(collectionElementType);
                            _elementClass = _elementClassDescriptor.DescribedClass;
                        }
                        else
                        {
                            result = FieldTypes.CollectionScalar;
                            DeriveScalarSerialization(collectionElementType, field);
                            if (ScalarType == null)
                            {
                                result = FieldTypes.IgnoredElement;
                                String msg = "Can't identify ScalarType for serialization of " + collectionElementType;
                                Debug.WriteLine(msg);
                            }
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(collectionTag))
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tIgnoring argument to  [SimplCollection] " + field.Name
                                         + " because it is declared polymorphic with [SimplClasses].";
                        }
                    }

                    _collectionOrMapTagName = collectionTag;
                    collectionType = TypeRegistry.GetCollectionType(thatField);
                    break;

                case FieldTypes.MapElement:
                    if (!(typeof(IDictionary).GetTypeInfo().IsAssignableFrom(thatField.FieldType.GetTypeInfo())))
                    {
                        String msg = "In " + declaringClassDescriptor.DescribedClass + "\n\tCan't translate  "
                                     + "[SimplMap] " + field.Name
                                     + " because the annotated field is not an instance of " +
                                     typeof (IDictionary).Name
                                     + ".";

                        Debug.WriteLine(msg);
                        return FieldTypes.IgnoredAttribute;
                    }


                    String mapTag = XmlTools.GetAnnotation<SimplMap>(thatField).TagName;
                    
                    if (!IsPolymorphic)
                    {
                        Type mapElementType = GetTypeArgs(thatField, 1);
                        if (String.IsNullOrEmpty(mapTag))
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate [SimplMap]" + field.Name
                                         + " because its tag argument is missing.";
                            Debug.WriteLine(msg);
                            return FieldTypes.IgnoredElement;
                        }

                        if (mapElementType == null)
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tCan't translate [SimplMap] " + field.Name
                                         + " because the parameterized type argument for the map is missing.";
                            Debug.WriteLine(msg);
                            return FieldTypes.IgnoredElement;
                        }
                        
                        if (!TypeRegistry.ScalarTypes.Contains(mapElementType))
                        {
                            _elementClassDescriptor = ClassDescriptor.GetClassDescriptor(mapElementType);
                            _elementClass = _elementClassDescriptor.DescribedClass;
                        }

                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(mapTag))
                        {
                            String msg = "In " + declaringClassDescriptor.DescribedClass
                                         + "\n\tIgnoring argument to  [SimplMap] " + field.Name
                                         + " because it is declared polymorphic with [SimplClasses].";
                        }
                    }

                    _collectionOrMapTagName = mapTag;
                    collectionType = TypeRegistry.GetCollectionType(thatField);
                    break;
            }

            switch (annotationType)
            {
                case FieldTypes.CollectionElement:
                case FieldTypes.MapElement:
                    if (!XmlTools.IsAnnotationPresent<SimplNoWrap>(thatField))
                    {
                        _wrapped = true;
                    }
                    collectionType = TypeRegistry.GetCollectionType(thatField);
                    break;
                
                case FieldTypes.CompositeElement:
                    if (XmlTools.IsAnnotationPresent<SimplWrap>(thatField))
                    {
                        _wrapped = true;
                    }
                    break;
            }

            return result;
        }

        private Type GetTypeArgs(FieldInfo thatField, int i)
        {
            Type result = null;

            Type[] typeArgs;

            TypeInfo realFieldTypeInfo = field.FieldType.GetTypeInfo();

            while (!realFieldTypeInfo.IsGenericType)
            {
                realFieldTypeInfo = realFieldTypeInfo.BaseType.GetTypeInfo();
            }

            typeArgs = realFieldTypeInfo.GenericTypeArguments;


            if (typeArgs != null)
            {
                int max = typeArgs.Length - 1;
                if (i > max)
                    i = max;
                Type typeArg0 = typeArgs[i];
                TypeInfo typeInfo = typeArg0.GetTypeInfo();

                // case 1: arg is a concrete class
                if (!typeArg0.IsGenericParameter && !typeInfo.IsGenericType)
                {
                    result = typeArg0;
                }
                else if (typeInfo.IsGenericType)
                {
                    // nested parameterized type

                    result = typeArg0.GetGenericTypeDefinition();
                }
                else if (typeArg0.IsGenericParameter)
                {
                    Type[] tviBounds = typeInfo.GetGenericParameterConstraints();
                    result = tviBounds[0];
                    Debug.WriteLine("yo! " + result);
                }
                else
                {
                    Debug.WriteLine("getTypeArgClass(" + field + ", " + i
                            + " yucky! Consult s.im.mp serialization developers.");
                }
            }

            return result;
        }

        public bool IsPolymorphic
        {
            get { return (polymorphClassDescriptors != null) || (unresolvedScopeAnnotation != null) || (unresolvedClassesAnnotation != null); }
        }

        private int DeriveScalarSerialization(FieldInfo scalarField)
        {
            int result = DeriveScalarSerialization(scalarField.FieldType, scalarField);
            if (xmlHint == Hint.XmlText || xmlHint == Hint.XmlTextCdata)
                declaringClassDescriptor.ScalarTextFD = this;
            return result;
        }

        private int DeriveScalarSerialization(Type thatType, FieldInfo scalarField)
        {
            string s = scalarField.Name;
            isEnum = XmlTools.IsEnum(scalarField);
            xmlHint = XmlTools.SimplHint(scalarField);
            if (isEnum)
                ScalarType = TypeRegistry.ScalarTypes[typeof(Enum)];
            else
                ScalarType = TypeRegistry.ScalarTypes[thatType];

            if (ScalarType == null)
            {
                String msg = "Can't find ScalarType to serialize field: \t\t" + thatType.Name
                             + "\t" + scalarField.Name + ";";

                Debug.WriteLine(msg);
                return (xmlHint == Hint.XmlAttribute) ? FieldTypes.IgnoredAttribute : FieldTypes.IgnoredElement;
            }

            dataFormat = XmlTools.GetFormatAnnotation(field);

            if (xmlHint != Hint.XmlAttribute)
            {
                needsEscaping = ScalarType.NeedsEscaping;
                isCDATA = xmlHint == Hint.XmlLeafCdata || xmlHint == Hint.XmlTextCdata;
            }
            return FieldTypes.Scalar;
        }

        private void DerivePolymorphicDescriptors(FieldInfo pField)
        {
            SimplScope scopeAttribute = XmlTools.GetAnnotation<SimplScope>(pField);
            String scopeAttributeValue = scopeAttribute == null ? null : scopeAttribute.TranslationScope;

            if (!String.IsNullOrEmpty(scopeAttributeValue))
            {
                if (!ResolveScopeAttribute(scopeAttributeValue))
                {
                    unresolvedScopeAnnotation = scopeAttributeValue;
                    declaringClassDescriptor.RegisterUnresolvedScopeAnnotationFD(this);
                }
            }

            SimplClasses classesAttribute = XmlTools.GetAnnotation<SimplClasses>(pField);
            Type[] classesAttributeValue = classesAttribute == null ? null : classesAttribute.Classes;

            if ((classesAttribute != null) && classesAttributeValue.Length > 0)
            {
                unresolvedClassesAnnotation = classesAttributeValue;
                declaringClassDescriptor.RegisterUnresolvedScopeAnnotationFD(this);
                InitPolymorphicClassDescriptorsList(classesAttributeValue.Length);
                foreach (Type thatType in classesAttributeValue)
                {
                    ClassDescriptor classDescriptor = ClassDescriptor.GetClassDescriptor(thatType);
                    RegisterPolymorphicDescriptor(classDescriptor);
                    polymorphClasses.Put(classDescriptor.TagName, classDescriptor.DescribedClass);
                }
            }
        }

        private void RegisterPolymorphicDescriptor(ClassDescriptor classDescriptor)
        {
            if (polymorphClassDescriptors == null)
                InitPolymorphicClassDescriptorsList(1);

            String classTag = classDescriptor.TagName;
            polymorphClassDescriptors.Put(classTag, classDescriptor);
            tlvClassDescriptors.Put(classTag.GetHashCode(), classDescriptor);

            if (otherTags != null)
                foreach (String otherTag in classDescriptor.OtherTags)
                {
                    if (!String.IsNullOrEmpty(otherTag))
                    {
                        polymorphClassDescriptors.Put(otherTag, classDescriptor);
                        tlvClassDescriptors.Put(otherTag.GetHashCode(), classDescriptor);
                    }
                }
        }

        private void InitPolymorphicClassDescriptorsList(Int32 size)
        {
            if (polymorphClassDescriptors == null)
            {
                polymorphClassDescriptors = new DictionaryList<String, ClassDescriptor>(size);
            }
            if (polymorphClasses == null)
            {
                polymorphClasses = new Dictionary<String, Type>(size);
            }
            if (tlvClassDescriptors == null)
            {
                tlvClassDescriptors = new Dictionary<Int32, ClassDescriptor>(size);
            }
        }

        private bool ResolveScopeAttribute(string scopeAttributeValue)
        {
            SimplTypesScope scope = SimplTypesScope.Get(scopeAttributeValue);
            if (scope != null)
            {
                List<ClassDescriptor> scopeClassDescriptors = scope.ClassDescriptors;
                InitPolymorphicClassDescriptorsList(scopeClassDescriptors.Count);
                foreach (var scopeClassDescriptor in scopeClassDescriptors)
                {
                    polymorphClassDescriptors.Put(scopeClassDescriptor.TagName, scopeClassDescriptor);
                    polymorphClasses.Put(scopeClassDescriptor.TagName, scopeClassDescriptor.DescribedClass);
                    tlvClassDescriptors.Put(_tagName.GetHashCode(), scopeClassDescriptor);
                }
            }

            return scope != null;
        }

        public String UnresolvedScopeAnnotation
        {
            get 
            {
                return unresolvedScopeAnnotation; 
            }

            set
            {
                unresolvedScopeAnnotation = value; 
            }
        }

        public override string JavaTypeName
        {
            get { return _elementClassDescriptor != null ? _elementClassDescriptor.JavaTypeName : ScalarType.JavaTypeName; }
        }

        public override string CSharpTypeName
        {
            get { return _elementClassDescriptor != null ? _elementClassDescriptor.CSharpTypeName : ScalarType.CSharpTypeName; }
        }

        public override string ObjectiveCTypeName
        {
            get
            {
                return _elementClassDescriptor != null
                           ? _elementClassDescriptor.ObjectiveCTypeName
                           : ScalarType.ObjectiveCTypeName;
            }
        }

        public override string DbTypeName
        {
            get { return _elementClassDescriptor != null ? _elementClassDescriptor.DbTypeName : ScalarType.DbTypeName; }
        }

        public override List<string> OtherTags
        {
            get { throw new NotImplementedException(); }
        }

        public Int32 FdType
        {
            get { return type; }
        }

        public Hint XmlHint
        {
            get { return xmlHint; }
            set { xmlHint = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsMarshallOnly
        {
            get { return ScalarType != null && ScalarType.IsMarshallOnly; }
        }

        public FieldInfo Field
        {
            get { return field; }
            private set { field = value; }
        }

        public Boolean IsWrapped
        {
            get { return _wrapped; }
            set { _wrapped = value; }
        }

        public bool IsCollection
        {
            get
            {
                switch (type)
                {
                    case FieldTypes.MapElement:
                    case FieldTypes.CollectionElement:
                    case FieldTypes.CollectionScalar:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsScalar
        {
            get { return scalarType != null; }
        }

        public String CollectionOrMapTagName
        {
            get { return _collectionOrMapTagName; }
            set { _collectionOrMapTagName = value; }
        }

        public Boolean ResolveUnresolvedScopeAnnotation()
        {
            if (unresolvedScopeAnnotation == null)
                return true;

            Boolean result = ResolveScopeAnnotation(unresolvedScopeAnnotation);
            if (result)
            {
                unresolvedScopeAnnotation = null;
                declaringClassDescriptor.MapTagClassDescriptors(this);
            }
            return result;
        }

        private bool ResolveScopeAnnotation(string scopeAnnotation)
        {
            SimplTypesScope scope = SimplTypesScope.Get(scopeAnnotation);
            if (scope != null)
            {
                List<ClassDescriptor> scopeClassDescriptors = scope.GetClassDescriptors();
                InitTagClassDescriptorsArrayList(scopeClassDescriptors.Count);
                foreach (ClassDescriptor classDescriptor in scopeClassDescriptors)
                {
                    String tagName = classDescriptor.TagName;
                    polymorphClassDescriptors.Put(tagName, classDescriptor);
                    polymorphClasses.Put(tagName, classDescriptor.DescribedClass);
                }
            }
            return scope != null;
        }

        public Boolean ResolveUnresolvedClassesAnnotation()
        {
            if (unresolvedClassesAnnotation == null)
                return true;

            Boolean result = ResolveClassesAnnotation(unresolvedClassesAnnotation);
            if (result)
            {
                unresolvedClassesAnnotation = null;
            }
            return result;
        }

        /**
	     * Generate tag -> class mappings for a @serial_scope declaration.
	     * 
	     * @param scopeAnnotation
	     *          Name of the scope to lookup in the global space. Must be non-null.
	     * 
	     * @return true if the scope annotation is successfully resolved to a TranslationScope.
	     */
	    private Boolean ResolveClassesAnnotation(Type[] classesAnnotation)
	    {
		    InitPolymorphicClassDescriptorsList(classesAnnotation.Length);
		    foreach (Type thatClass in classesAnnotation)
		    {
			    ClassDescriptor classDescriptor = ClassDescriptor.GetClassDescriptor(thatClass);
			    RegisterPolymorphicDescriptor(classDescriptor);
			    polymorphClasses.Add(classDescriptor.TagName, classDescriptor.DescribedClass);
		    }
		    return true;
	    }

        private void InitTagClassDescriptorsArrayList(int initialSize)
        {
            if (polymorphClassDescriptors == null)
            {
                polymorphClassDescriptors = new DictionaryList<string, ClassDescriptor>(initialSize);
            }
            if (polymorphClasses == null)
            {
                polymorphClasses = new Dictionary<String, Type>(initialSize);
            }
        }

        public DictionaryList<String, ClassDescriptor> PolymorphClassDescriptors
        {
            get { return polymorphClassDescriptors; }
        }

        public Boolean IsCdata
        {
            get { return isCDATA; }
        }

        public String ElementStart
        {
            get { return IsCollection ? _collectionOrMapTagName : IsComposite ? compositeTagName : _tagName; }
        }

        /// <summary>
        /// True if this type is a CompositeElement
        /// </summary>
        public Boolean IsComposite
        {
            get { return type == FieldTypes.CompositeElement; }
        }

        public FieldDescriptor WrappedFd
        {
            get { return wrappedFD; }
            set { wrappedFD = value;
                value.Wrapper = this;
            }
        }

        public FieldDescriptor Wrapper
        {
            get { return wrapper; }
            set { wrapper = value; }
        }

        public ScalarType ScalarType
        {
            get { return scalarType; }
            set { scalarType = value; }
        }

        public Regex FilterRegex
        {
            get { return filterRegex; }
            set { filterRegex = value; }
        }

        public string FilterReplace
        {
            get { return filterReplace; }
            set { filterReplace = value; }
        }

        public object BibtexTagName
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool IsBibtexKey
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public int TlvId
        {
            get { return ElementStart.GetTlvId(); }
        }

        public int WrappedTLVId
        {
            get
            {
                int tempTlvId = 0;

                if (_tagName != null)
                    tempTlvId = _tagName.GetTlvId();

                return tempTlvId; ;
            }
        }


        public void AppendValue(TextWriter textWriter, object obj, TranslationContext translationContext, Format format)
        {
            ScalarType.AppendValue(textWriter, this, obj, format, translationContext);
        }

        public bool IsDefaultValueFromContext(object context)
        {
            if (context != null)
            {
                return ScalarType.IsDefaultValue(field, context);
            }

            return false;
        }

        public bool IsDefaultValue(String value)
        {
            return ScalarType.IsDefaultValue(value);
        }

        public void AppendCollectionScalarValue(TextWriter textWriter, object obj, TranslationContext translationContext,
                                                Format format)
        {
            ScalarType.AppendValue(obj, textWriter, !IsCdata, format);
        }

        /// <summary>
        /// Obtains the value decribed by this FieldDescriptor, with the given object used as Context
        /// </summary>
        /// <param name="contextObject">The context object to obtain a field value for.</param>
        /// <returns>The object value, or null if there is an exception</returns>
        public object GetObject(object contextObject)
        {
            try
            {
                return field.GetValue(contextObject);
            }
            catch (Exception e)
            {
                // TODO: CONSIDER AN EXCEPTION HERE INSTEAD OF A NULL.
                Debug.WriteLine(String.Format("cannot get value for field {0} due to exception: {1}",  field.Name, e.Message));
                return null;
            }
            
        }

        public object GetNested(object context)
        {
            return ReflectionTools.GetFieldValue(context, Field);
        }

        public ICollection GetCollection(Object context)
	    {
		    return (ICollection) ReflectionTools.GetFieldValue(context, field);
	    }

        public string GetValueString(object context)
        {
            string result = Null;

            if (context != null && IsScalar)
            {
                result = scalarType.ToString(field, context);

            }
            else
            {
                //Apparently returning "Null" was deemed appropriate if it is null. That is not appropriate for Maps / Lists that cannot be returned as ValueStrings. x`
                if (context != null)
                {
                    throw new ArgumentException(string.Format("Cannot GetValueString for field descriptor describing field \"{0}\" which is of type: {1}", this.Name, this.fieldType ?? "OH DEAR THAT VALUE IS NULL."));
                }
            }

		    return result;
        }

        public void SetFieldToScalar(object root, string value, TranslationContext translationContext)
        {
            if (ScalarType != null && !ScalarType.IsMarshallOnly)
            {
                ScalarType.SetField(root, field, value, dataFormat, translationContext);
            }
        }

        public void SetFieldToComposite(object root, object subRoot)
        {
            field.SetValue(root, subRoot);
        }

        public ClassDescriptor ChildClassDescriptor(string currentTagName)
        {
            if (!IsPolymorphic)
                return _elementClassDescriptor;

            if (polymorphClassDescriptors == null)
            {
                ResolveUnresolvedClassesAnnotation();
                ResolveUnresolvedScopeAnnotation();
            }

            if(polymorphClassDescriptors == null)
            {
                Debug.WriteLine("The " + this.Name + " field is declared polymorphic, but its polymorphic ClassDescriptor don't exist! Check annotation and is simplTypesScopes defined?");
                return null;
            }

            if (polymorphClassDescriptors.ContainsKey(currentTagName))
            {
                ClassDescriptor result = polymorphClassDescriptors[currentTagName];
                return polymorphClassDescriptors[currentTagName];
            }

            return null;
        }

        public void AddLeafNodeToCollection(object root, string value, TranslationContext translationContext)
        {
            if(String.IsNullOrEmpty(value))
            {
                return;
            }

            if(ScalarType != null)
            {
                Object typeConvertedValue = ScalarType.GetInstance(value, dataFormat, translationContext);

                if(typeConvertedValue != null)
                {
                    IList collection = (IList) AutomaticLazyGetCollectionOrMap(root);
                    collection.Add(typeConvertedValue);
                }
            }
        }

        public Object AutomaticLazyGetCollectionOrMap(object root)
        {
            Object
            collection = null;

            collection = field.GetValue(root);
            if(collection == null)
            {                  
                collection = Activator.CreateInstance(Field.FieldType); //TODO: use collectionType.Instance but first have generic type parameter specification system in S.IM.PL
                field.SetValue(root, collection);
            }

            return collection;
        }

        public bool IsCollectionTag(string currentTag)
        {
            return IsPolymorphic
                       ? polymorphClassDescriptors.ContainsKey(currentTag)
                       : _collectionOrMapTagName.Equals(currentTag);
        }

        public ClassDescriptor GetChildClassDescriptor(int tlvType)
        {
            throw new NotImplementedException();
        }

        public FieldDescriptor DescriptorClonedFrom
        {
            get { return _clonedFrom; }
            set { _clonedFrom = value; }
        }

        public ClassDescriptor ElementClassDescriptor
        {
            get { return _elementClassDescriptor; }
            set { _elementClassDescriptor = value; }
        }

        public Type ElementClass
        {
            get { return _elementClass; }
            private set { _elementClass = value; }
        }

       //generics
        public ClassDescriptor GenericTypeVarsContextCD
        {
            get { return genericTypeVarsContextCD; }
            set { genericTypeVarsContextCD = value; }
        }

        ///<sumary>
        ///lazy-evaluation method.
        ///</sumary>
        public List<GenericTypeVar> GetGenericTypeVars()
	    {
		    if (genericTypeVars == null)
		    {
				DeriveGenericTypeVariables();
		    }

		    return genericTypeVars;
	    }

        // added a setter to enable environment specific implementation -Fei
        public void SetGenericTypeVars(List<GenericTypeVar> derivedGenericTypeVariables)
	    {
		    genericTypeVars = derivedGenericTypeVariables;
	    }

        public List<GenericTypeVar> GetGenericTypeVarsContext()
        {
            return genericTypeVarsContextCD.GetGenericTypeVars();
        }

        private void DeriveGenericTypeVariables()
        {
            //FieldInfo field = fieldDescriptor.Field;
            Type genericType = Field.FieldType;
            List<GenericTypeVar> derivedGenericTypeVars = new List<GenericTypeVar>();

            if (genericType.IsGenericParameter)
            {
                GenericTypeVar g = GenericTypeVar.GetGenericTypeVarRef(genericType, GetGenericTypeVarsContext());
                derivedGenericTypeVars.Add(g);
            }
            else
            {
                TypeInfo typeInfo = genericType.GetTypeInfo();
                if (typeInfo.IsGenericType)
                {
                    Type[] types = typeInfo.GenericTypeArguments;

                    if (types == null | types.Length <= 0)
                        return;

                    foreach (Type t in types)
                    {
                        GenericTypeVar g = GenericTypeVar.GetGenericTypeVarRef(t, GetGenericTypeVarsContext());
                        derivedGenericTypeVars.Add(g);
                    }
                }
            }

            SetGenericTypeVars(derivedGenericTypeVars);
        }

        
        /// <summary>
        /// make a SHALLOW copy of this descriptor.
        /// </summary>
        /// <returns></returns>
        public FieldDescriptor Clone()
	    {
		    FieldDescriptor cloned = null;

            cloned = (FieldDescriptor) this.MemberwiseClone();
			cloned.clonedFrom = this;

		    return cloned;
	    }

        public Object GetMapKeyFieldValue(Object mapElement)
        {
            if (this.mapKeyFieldName != null)
            {
                ClassDescriptor cd = ClassDescriptor.GetClassDescriptor(mapElement);
                if (cd != null)
                {
                    FieldDescriptor fd = cd.GetFieldDescriptorByFieldName(mapKeyFieldName);
                    return fd.field.GetValue(mapElement);
                }
            }
            return null;
        }

        public void SetElementClassDescriptor(ClassDescriptor elementClassDescriptor)
        {
            ElementClassDescriptor = elementClassDescriptor;
            Type newElementClass = elementClassDescriptor.DescribedClass;
            if (newElementClass != null)
            {
                ElementClass = newElementClass;
            }
        }

        public Object GetValue(Object context)
        {
            Object resultObject = null;
            FieldInfo childField = this.Field;
            try
            {
                resultObject = childField.GetValue(context);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Can't access " + childField.Name);
            }
            return resultObject;
        }

        #region Ignored FieldDescriptor

        public static FieldDescriptor MakeIgnoredFieldDescriptor(string currentTag)
        {
            return new FieldDescriptor(currentTag);
        }

        FieldDescriptor(String tag) : base(tag, null)
	    {
		    this._tagName = tag;
		    this.type = FieldTypes.IgnoredElement;
		    this.field = null;
		    this.declaringClassDescriptor = null;
	    }

	    public static readonly FieldDescriptor IGNORED_ELEMENT_FIELD_DESCRIPTOR;

	    static FieldDescriptor()
	    {
		    IGNORED_ELEMENT_FIELD_DESCRIPTOR = new FieldDescriptor("IGNORED");
        }

        #endregion Ignored FieldDescriptor

        #region Resolve base class generic issue
        public void SetFieldBaseClassGeneric(Type[] superClassGenericArguments)
        {
            if (superClassGenericArguments.Length > 0)
            {
                Type declaringType = Field.DeclaringType;
                if (declaringType != null)
                    Field = declaringType.MakeGenericType(superClassGenericArguments).GetTypeInfo().GetDeclaredField(Field.Name);
            }
        }

        #endregion Resolve base class generic issue




        public override string ToString()
        {
            String className = declaringClassDescriptor == null ? null : declaringClassDescriptor.Name;
            String fieldName = Field == null ? Name : Field.Name;
            return this.GetType().Name + "[" + fieldName + ", type=" + FdType + "]";
        }
       

        /// <summary>
        /// Performs a value equality check on the values in LHS and RHS described by this field. 
        /// </summary>
        /// <param name="leftContext">Left context object</param>
        /// <param name="rightContext">Right context object</param>
        /// <returns>True if the values described by each context are the same</returns>
        public bool ContextSimplEquals(object leftContext, object rightContext)
        {
            var leftSideDescribedValue = this.GetValue(leftContext);
            var rightSideDescribedValue = this.GetValue(rightContext);

            if(leftSideDescribedValue.GetType().Equals(rightSideDescribedValue.GetType()))
            {
                if(this.IsComposite)
                {
                    var compositetype = new CompositeType(leftSideDescribedValue.GetType());
                    return compositetype.SimplEquals(leftSideDescribedValue, rightSideDescribedValue);
                }
                else if (this.IsCollection)
                {
                    return CollectionType.SimplEquals(leftSideDescribedValue, rightSideDescribedValue);
                }
                else if (this.IsScalar)
                {
                    return this.ScalarType.SimplEquals(leftSideDescribedValue, rightSideDescribedValue);
                }
                else
                {
                    throw new Exception("Unexpected type found at ContextSimplEquals!");
                }
            }
            else
            {
                return false;
            }
        }

    }
}

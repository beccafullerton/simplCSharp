//
//  ContinuedHTTPGetRequest.cs
//  s.im.pl serialization
//
//  Generated by DotNetTranslator on 04/09/11.
//  Copyright 2011 Interface Ecology Lab. 
//

using System;
using Simpl.Serialization.Attributes;

namespace Simpl.OODSS.Messages 
{
	/// <summary>
	/// missing java doc comments or could not find the source file.
	/// </summary>
	[SimplInherit]
	public class ContinuedHTTPGetRequest : HttpRequest
	{
		/// <summary>
		/// missing java doc comments or could not find the source file.
		/// </summary>
		[SimplScalar]
		[SimplHints(new Hint[] { Hint.XmlLeafCdata })]
		private String _messageFragment;

		/// <summary>
		/// missing java doc comments or could not find the source file.
		/// </summary>
		[SimplScalar]
		private Boolean _isLast;

		public ContinuedHTTPGetRequest()
		{ }

		public String MessageFragment
		{
			get{return _messageFragment;}
			set{_messageFragment = value;}
		}

		public Boolean IsLast
		{
			get{return _isLast;}
			set{_isLast = value;}
		}
	}
}
